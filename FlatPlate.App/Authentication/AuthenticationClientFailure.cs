namespace FlatPlate.App.Authentication;

/// <summary>
/// Classifies authentication client failures so view models can show useful feedback.
/// </summary>
public enum AuthenticationClientFailure
{
    None,
    InvalidRequest,
    InvalidCredentials,
    EmailNotVerified,
    DuplicateEmail,
    InvalidOrExpiredCode,
    Network,
    Server,
    InvalidResponse
}
