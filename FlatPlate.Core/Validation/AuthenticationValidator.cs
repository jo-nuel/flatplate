using FlatPlate.Core.Contracts.Authentication;
using System.Net.Mail;

namespace FlatPlate.Core.Validation;

/// <summary>
/// Applies the same account-input rules in the desktop client and API.
/// </summary>
public static class AuthenticationValidator
{
    public const int MinimumDisplayNameLength = 2;
    public const int MaximumDisplayNameLength = 50;
    public const int MaximumEmailLength = 254;
    public const int MinimumPasswordLength = 10;
    public const int MaximumPasswordLength = 128;

    /// <summary>
    /// Validates all fields required to register an account.
    /// </summary>
    public static IReadOnlyList<AuthenticationValidationError> Validate(
        RegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<AuthenticationValidationError>();
        ValidateDisplayName(request.DisplayName, errors);
        ValidateEmail(request.Email, errors);
        ValidateNewPassword(request.Password, errors, nameof(request.Password));

        if (!string.Equals(
                request.Password,
                request.ConfirmPassword,
                StringComparison.Ordinal))
        {
            errors.Add(new(
                nameof(request.ConfirmPassword),
                "The password confirmation does not match."));
        }

        return errors;
    }

    /// <summary>
    /// Validates credentials without revealing whether an account exists.
    /// </summary>
    public static IReadOnlyList<AuthenticationValidationError> Validate(
        SignInRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<AuthenticationValidationError>();
        ValidateEmail(request.Email, errors);

        if (string.IsNullOrEmpty(request.Password))
        {
            errors.Add(new(nameof(request.Password), "Enter your password."));
        }

        return errors;
    }

    /// <summary>
    /// Validates an email-verification request.
    /// </summary>
    public static IReadOnlyList<AuthenticationValidationError> Validate(
        VerifyEmailRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<AuthenticationValidationError>();
        ValidateEmail(request.Email, errors);
        ValidateToken(request.Token, errors);
        return errors;
    }

    /// <summary>
    /// Validates a request for a password-reset message.
    /// </summary>
    public static IReadOnlyList<AuthenticationValidationError> Validate(
        RequestPasswordResetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<AuthenticationValidationError>();
        ValidateEmail(request.Email, errors);
        return errors;
    }

    /// <summary>
    /// Validates a password-reset token and the proposed replacement password.
    /// </summary>
    public static IReadOnlyList<AuthenticationValidationError> Validate(
        ResetPasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<AuthenticationValidationError>();
        ValidateEmail(request.Email, errors);
        ValidateToken(request.Token, errors);
        ValidateNewPassword(
            request.NewPassword,
            errors,
            nameof(request.NewPassword));

        if (!string.Equals(
                request.NewPassword,
                request.ConfirmNewPassword,
                StringComparison.Ordinal))
        {
            errors.Add(new(
                nameof(request.ConfirmNewPassword),
                "The password confirmation does not match."));
        }

        return errors;
    }

    private static void ValidateDisplayName(
        string displayName,
        ICollection<AuthenticationValidationError> errors)
    {
        var length = displayName?.Trim().Length ?? 0;

        if (length < MinimumDisplayNameLength || length > MaximumDisplayNameLength)
        {
            errors.Add(new(
                nameof(RegisterRequest.DisplayName),
                $"Display name must be {MinimumDisplayNameLength} to " +
                $"{MaximumDisplayNameLength} characters."));
        }
    }

    private static void ValidateEmail(
        string email,
        ICollection<AuthenticationValidationError> errors)
    {
        var trimmedEmail = email?.Trim() ?? string.Empty;
        var isValid = trimmedEmail.Length <= MaximumEmailLength
            && MailAddress.TryCreate(trimmedEmail, out var parsedEmail)
            && string.Equals(
                parsedEmail.Address,
                trimmedEmail,
                StringComparison.OrdinalIgnoreCase);

        if (!isValid)
        {
            errors.Add(new(
                nameof(SignInRequest.Email),
                "Enter a valid email address."));
        }
    }

    private static void ValidateNewPassword(
        string password,
        ICollection<AuthenticationValidationError> errors,
        string field)
    {
        if (password is null
            || password.Length < MinimumPasswordLength
            || password.Length > MaximumPasswordLength)
        {
            errors.Add(new(
                field,
                $"Password must be {MinimumPasswordLength} to " +
                $"{MaximumPasswordLength} characters."));
            return;
        }

        if (!password.Any(char.IsUpper)
            || !password.Any(char.IsLower)
            || !password.Any(char.IsDigit))
        {
            errors.Add(new(
                field,
                "Password must include an uppercase letter, a lowercase letter, " +
                "and a number."));
        }
    }

    private static void ValidateToken(
        string token,
        ICollection<AuthenticationValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            errors.Add(new(nameof(VerifyEmailRequest.Token), "Enter the verification code."));
        }
    }
}
