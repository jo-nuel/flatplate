using FlatPlate.App.Authentication;
using FlatPlate.App.ViewModels;
using FlatPlate.Core.Contracts.Authentication;
using FlatPlate.Core.Validation;

namespace FlatPlate.Tests;

/// <summary>
/// Checks account-screen transitions and the signed-in application gate.
/// </summary>
public sealed class AuthenticationViewModelTests
{
    [Test]
    public async Task SignInAsync_Success_StartsSessionAndUnlocksShell()
    {
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero));
        var session = new AuthenticationSession(clock);
        var response = CreateAuthenticationResponse(clock);
        var api = new StubAuthenticationApiClient
        {
            SignInResult = AuthenticationClientResult<AuthenticationResponse>
                .Success(response)
        };
        var authentication = new AuthenticationViewModel(api, session)
        {
            Email = "jonathan@example.com"
        };
        using var shell = new MainWindowViewModel(session);

        var signedIn = await authentication.SignInAsync("FlatPlate123");

        Assert.Multiple(() =>
        {
            Assert.That(signedIn, Is.True);
            Assert.That(shell.IsAuthenticated, Is.True);
            Assert.That(shell.IsSignedOut, Is.False);
            Assert.That(shell.DisplayName, Is.EqualTo("Jonathan"));
            Assert.That(shell.WelcomeMessage, Is.EqualTo("Welcome back, Jonathan"));
        });
    }

    [Test]
    public async Task RegisterAsync_Success_MovesToVerificationForm()
    {
        var session = new AuthenticationSession(TimeProvider.System);
        var api = new StubAuthenticationApiClient
        {
            RegisterResult = AuthenticationClientResult<AccountMessageResponse>
                .Success(new("Check your email for the verification code."))
        };
        var viewModel = new AuthenticationViewModel(api, session)
        {
            DisplayName = "Jonathan",
            Email = "jonathan@example.com"
        };

        var registered = await viewModel.RegisterAsync(
            "FlatPlate123",
            "FlatPlate123");

        Assert.Multiple(() =>
        {
            Assert.That(registered, Is.True);
            Assert.That(viewModel.IsVerifyEmailVisible, Is.True);
            Assert.That(viewModel.IsRegisterVisible, Is.False);
            Assert.That(viewModel.StatusMessage, Does.Contain("verification code"));
        });
    }

    [Test]
    public async Task RegisterAsync_ValidationFailure_ShowsAllMessages()
    {
        var session = new AuthenticationSession(TimeProvider.System);
        var errors = new[]
        {
            new AuthenticationValidationError("Email", "Enter a valid email address."),
            new AuthenticationValidationError("Password", "Password is too short.")
        };
        var api = new StubAuthenticationApiClient
        {
            RegisterResult = AuthenticationClientResult<AccountMessageResponse>.Failed(
                AuthenticationClientFailure.InvalidRequest,
                "Invalid request.",
                errors)
        };
        var viewModel = new AuthenticationViewModel(api, session);

        var registered = await viewModel.RegisterAsync("short", "different");

        Assert.Multiple(() =>
        {
            Assert.That(registered, Is.False);
            Assert.That(viewModel.StatusMessage, Does.Contain("valid email"));
            Assert.That(viewModel.StatusMessage, Does.Contain("too short"));
            Assert.That(viewModel.CanSubmit, Is.True);
        });
    }

    [Test]
    public void SignOutCommand_ActiveSession_LocksShell()
    {
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero));
        var session = new AuthenticationSession(clock);
        session.Start(CreateAuthenticationResponse(clock));
        using var shell = new MainWindowViewModel(session);

        shell.SignOutCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(shell.IsAuthenticated, Is.False);
            Assert.That(shell.IsSignedOut, Is.True);
            Assert.That(shell.DisplayName, Is.Empty);
        });
    }

    private static AuthenticationResponse CreateAuthenticationResponse(
        TimeProvider clock) => new(
            "signed-token",
            clock.GetUtcNow().AddMinutes(15),
            new AuthenticatedUser(
                Guid.NewGuid(),
                "Jonathan",
                "jonathan@example.com",
                true));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubAuthenticationApiClient : IAuthenticationApiClient
    {
        public AuthenticationClientResult<AccountMessageResponse> RegisterResult { get; init; } =
            AuthenticationClientResult<AccountMessageResponse>.Failed(
                AuthenticationClientFailure.Server,
                "Not configured.");

        public AuthenticationClientResult<AuthenticationResponse> SignInResult { get; init; } =
            AuthenticationClientResult<AuthenticationResponse>.Failed(
                AuthenticationClientFailure.Server,
                "Not configured.");

        public Task<AuthenticationClientResult<AccountMessageResponse>> RegisterAsync(
            RegisterRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RegisterResult);

        public Task<AuthenticationClientResult<AccountMessageResponse>> VerifyEmailAsync(
            VerifyEmailRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AuthenticationClientResult<AccountMessageResponse>.Success(
                new("Verified.")));

        public Task<AuthenticationClientResult<AuthenticationResponse>> SignInAsync(
            SignInRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SignInResult);

        public Task<AuthenticationClientResult<AccountMessageResponse>> RequestPasswordResetAsync(
            RequestPasswordResetRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AuthenticationClientResult<AccountMessageResponse>.Success(
                new("Reset requested.")));

        public Task<AuthenticationClientResult<AccountMessageResponse>> ResetPasswordAsync(
            ResetPasswordRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AuthenticationClientResult<AccountMessageResponse>.Success(
                new("Password reset.")));
    }
}
