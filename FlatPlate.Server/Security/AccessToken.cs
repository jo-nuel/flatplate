namespace FlatPlate.Server.Security;

/// <summary>
/// Contains a serialized access token and its expiry.
/// </summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);
