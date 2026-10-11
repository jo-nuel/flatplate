namespace FlatPlate.Server.Security;

/// <summary>
/// Configures the issuer, audience, signing key, and lifetime of API access tokens.
/// </summary>
public sealed record AccessTokenSettings(
    string Issuer,
    string Audience,
    string SigningKey,
    TimeSpan Lifetime);
