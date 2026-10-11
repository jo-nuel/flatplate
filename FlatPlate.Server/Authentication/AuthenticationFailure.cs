namespace FlatPlate.Server.Authentication;

/// <summary>
/// Identifies failures that an API endpoint can safely disclose to a client.
/// </summary>
public enum AuthenticationFailure
{
    None,
    InvalidRequest,
    DuplicateEmail,
    InvalidCredentials,
    EmailNotVerified,
    InvalidOrExpiredToken
}
