namespace FlatPlate.Core.Contracts.Authentication;

/// <summary>
/// Contains a one-time token and replacement password for an account.
/// </summary>
public sealed record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmNewPassword);
