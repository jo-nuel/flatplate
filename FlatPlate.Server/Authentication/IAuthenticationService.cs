using FlatPlate.Core.Contracts.Authentication;

namespace FlatPlate.Server.Authentication;

/// <summary>
/// Coordinates account registration, verification, sign-in, and password recovery.
/// </summary>
public interface IAuthenticationService
{
    Task<AuthenticationResult<AccountMessageResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult<AccountMessageResponse>> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult<AuthenticationResponse>> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult<AccountMessageResponse>> RequestPasswordResetAsync(
        RequestPasswordResetRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult<AccountMessageResponse>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);
}
