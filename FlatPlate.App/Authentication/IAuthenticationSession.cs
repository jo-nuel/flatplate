using FlatPlate.Core.Contracts.Authentication;

namespace FlatPlate.App.Authentication;

/// <summary>
/// Holds the signed-in user and short-lived token for the current app process.
/// </summary>
public interface IAuthenticationSession
{
    event EventHandler? SessionChanged;

    AuthenticatedUser? CurrentUser { get; }

    bool IsAuthenticated { get; }

    void Start(AuthenticationResponse response);

    bool TryGetAccessToken(out string? accessToken);

    void Clear();
}
