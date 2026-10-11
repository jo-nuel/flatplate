using FlatPlate.Core.Contracts.Authentication;

namespace FlatPlate.App.Authentication;

/// <summary>
/// Sends account requests to the server without exposing transport details to the UI.
/// </summary>
public interface IAuthenticationApiClient
{
    Task<AuthenticationClientResult<AccountMessageResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthenticationClientResult<AccountMessageResponse>> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthenticationClientResult<AuthenticationResponse>> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthenticationClientResult<AccountMessageResponse>> RequestPasswordResetAsync(
        RequestPasswordResetRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthenticationClientResult<AccountMessageResponse>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);
}
