namespace FlatPlate.Server.Security;

/// <summary>
/// Separates codes so a reset code cannot be used for email verification.
/// </summary>
public enum OneTimeCodePurpose
{
    EmailVerification,
    PasswordReset
}
