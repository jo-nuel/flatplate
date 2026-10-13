namespace FlatPlate.Core.Contracts.Authentication;

/// <summary>
/// Returns the signed-in user and their short-lived API access token.
/// </summary>
public sealed record AuthenticationResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    AuthenticatedUser User);
