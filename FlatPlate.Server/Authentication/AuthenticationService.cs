using FlatPlate.Core.Contracts.Authentication;
using FlatPlate.Core.Validation;
using FlatPlate.Server.Data;
using FlatPlate.Server.Messaging;
using FlatPlate.Server.Models;
using FlatPlate.Server.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.Server.Authentication;

/// <summary>
/// Implements the server-owned account lifecycle and keeps credentials out of the client.
/// </summary>
public sealed class AuthenticationService : IAuthenticationService
{
    private static readonly TimeSpan VerificationCodeLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ResetCodeLifetime = TimeSpan.FromMinutes(15);
    private const string InvalidTokenMessage = "The code is invalid or has expired.";

    private readonly AuthenticationDbContext _dbContext;
    private readonly IPasswordService _passwordService;
    private readonly IOneTimeCodeService _codeService;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IAccountMessageSender _messageSender;
    private readonly TimeProvider _timeProvider;

    public AuthenticationService(
        AuthenticationDbContext dbContext,
        IPasswordService passwordService,
        IOneTimeCodeService codeService,
        IAccessTokenService accessTokenService,
        IAccountMessageSender messageSender,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _passwordService = passwordService
            ?? throw new ArgumentNullException(nameof(passwordService));
        _codeService = codeService
            ?? throw new ArgumentNullException(nameof(codeService));
        _accessTokenService = accessTokenService
            ?? throw new ArgumentNullException(nameof(accessTokenService));
        _messageSender = messageSender
            ?? throw new ArgumentNullException(nameof(messageSender));
        _timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<AuthenticationResult<AccountMessageResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = AuthenticationValidator.Validate(request);
        if (errors.Count > 0)
        {
            return AuthenticationResult<AccountMessageResponse>.Invalid(errors);
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        if (await _dbContext.UserAccounts.AnyAsync(
                account => account.NormalizedEmail == normalizedEmail,
                cancellationToken))
        {
            return AuthenticationResult<AccountMessageResponse>.Failed(
                AuthenticationFailure.DuplicateEmail,
                "An account already uses that email address.");
        }

        var now = _timeProvider.GetUtcNow();
        var account = new UserAccount
        {
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        account.PasswordHash = _passwordService.HashPassword(account, request.Password);

        var code = _codeService.GenerateCode();
        account.EmailVerificationTokenHash = _codeService.Protect(
            normalizedEmail,
            code,
            OneTimeCodePurpose.EmailVerification);
        account.EmailVerificationTokenExpiresAtUtc = now.Add(VerificationCodeLifetime);
        _dbContext.UserAccounts.Add(account);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueEmailViolation(exception))
        {
            return AuthenticationResult<AccountMessageResponse>.Failed(
                AuthenticationFailure.DuplicateEmail,
                "An account already uses that email address.");
        }

        await _messageSender.SendEmailVerificationAsync(
            account,
            code,
            cancellationToken);

        return AuthenticationResult<AccountMessageResponse>.Success(new(
            "Account created. Check your email for the verification code."));
    }

    public async Task<AuthenticationResult<AccountMessageResponse>> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = AuthenticationValidator.Validate(request);
        if (errors.Count > 0)
        {
            return AuthenticationResult<AccountMessageResponse>.Invalid(errors);
        }

        var account = await FindByEmailAsync(request.Email, cancellationToken);
        if (account is null
            || account.EmailVerificationTokenExpiresAtUtc <= _timeProvider.GetUtcNow()
            || !_codeService.Verify(
                account.NormalizedEmail,
                request.Token,
                OneTimeCodePurpose.EmailVerification,
                account.EmailVerificationTokenHash ?? string.Empty))
        {
            return AuthenticationResult<AccountMessageResponse>.Failed(
                AuthenticationFailure.InvalidOrExpiredToken,
                InvalidTokenMessage);
        }

        account.IsEmailVerified = true;
        account.EmailVerificationTokenHash = null;
        account.EmailVerificationTokenExpiresAtUtc = null;
        account.UpdatedAtUtc = _timeProvider.GetUtcNow();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthenticationResult<AccountMessageResponse>.Success(new(
            "Your email has been verified. You can now sign in."));
    }

    public async Task<AuthenticationResult<AuthenticationResponse>> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = AuthenticationValidator.Validate(request);
        if (errors.Count > 0)
        {
            return AuthenticationResult<AuthenticationResponse>.Invalid(errors);
        }

        var account = await FindByEmailAsync(request.Email, cancellationToken);
        var verification = account is null
            ? PasswordVerificationResult.Failed
            : _passwordService.VerifyPassword(account, request.Password);

        if (account is null || verification == PasswordVerificationResult.Failed)
        {
            return AuthenticationResult<AuthenticationResponse>.Failed(
                AuthenticationFailure.InvalidCredentials,
                "The email or password is incorrect.");
        }

        if (!account.IsEmailVerified)
        {
            return AuthenticationResult<AuthenticationResponse>.Failed(
                AuthenticationFailure.EmailNotVerified,
                "Verify your email before signing in.");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.PasswordHash = _passwordService.HashPassword(account, request.Password);
            account.UpdatedAtUtc = _timeProvider.GetUtcNow();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var token = _accessTokenService.Create(account);
        return AuthenticationResult<AuthenticationResponse>.Success(new(
            token.Value,
            token.ExpiresAtUtc,
            ToAuthenticatedUser(account)));
    }

    public async Task<AuthenticationResult<AccountMessageResponse>> RequestPasswordResetAsync(
        RequestPasswordResetRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = AuthenticationValidator.Validate(request);
        if (errors.Count > 0)
        {
            return AuthenticationResult<AccountMessageResponse>.Invalid(errors);
        }

        var account = await FindByEmailAsync(request.Email, cancellationToken);
        if (account is not null)
        {
            var now = _timeProvider.GetUtcNow();
            var code = _codeService.GenerateCode();
            account.PasswordResetTokenHash = _codeService.Protect(
                account.NormalizedEmail,
                code,
                OneTimeCodePurpose.PasswordReset);
            account.PasswordResetTokenExpiresAtUtc = now.Add(ResetCodeLifetime);
            account.UpdatedAtUtc = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _messageSender.SendPasswordResetAsync(
                account,
                code,
                cancellationToken);
        }

        return AuthenticationResult<AccountMessageResponse>.Success(new(
            "If an account uses that email, a password reset code has been sent."));
    }

    public async Task<AuthenticationResult<AccountMessageResponse>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = AuthenticationValidator.Validate(request);
        if (errors.Count > 0)
        {
            return AuthenticationResult<AccountMessageResponse>.Invalid(errors);
        }

        var account = await FindByEmailAsync(request.Email, cancellationToken);
        if (account is null
            || account.PasswordResetTokenExpiresAtUtc <= _timeProvider.GetUtcNow()
            || !_codeService.Verify(
                account.NormalizedEmail,
                request.Token,
                OneTimeCodePurpose.PasswordReset,
                account.PasswordResetTokenHash ?? string.Empty))
        {
            return AuthenticationResult<AccountMessageResponse>.Failed(
                AuthenticationFailure.InvalidOrExpiredToken,
                InvalidTokenMessage);
        }

        var now = _timeProvider.GetUtcNow();
        account.PasswordHash = _passwordService.HashPassword(account, request.NewPassword);
        account.PasswordResetTokenHash = null;
        account.PasswordResetTokenExpiresAtUtc = null;
        account.PasswordChangedAtUtc = now;
        account.UpdatedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthenticationResult<AccountMessageResponse>.Success(new(
            "Your password has been reset. You can now sign in."));
    }

    private Task<UserAccount?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken) =>
        _dbContext.UserAccounts.SingleOrDefaultAsync(
            account => account.NormalizedEmail == NormalizeEmail(email),
            cancellationToken);

    private static string NormalizeEmail(string email) =>
        email.Trim().ToUpperInvariant();

    private static AuthenticatedUser ToAuthenticatedUser(UserAccount account) =>
        new(account.Id, account.DisplayName, account.Email, account.IsEmailVerified);

    private static bool IsUniqueEmailViolation(DbUpdateException exception) =>
        exception.InnerException switch
        {
            SqliteException { SqliteErrorCode: 19 } => true,
            SqlException { Number: 2601 or 2627 } => true,
            _ => false
        };
}
