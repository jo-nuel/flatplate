using FlatPlate.Server.Models;

namespace FlatPlate.Server.Security;

/// <summary>
/// Creates short-lived signed tokens for authenticated API requests.
/// </summary>
public interface IAccessTokenService
{
    AccessToken Create(UserAccount account);
}
