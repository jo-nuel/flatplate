namespace FlatPlate.Core.Contracts.Authentication;

/// <summary>
/// Requests a password-reset message for an email address.
/// </summary>
public sealed record RequestPasswordResetRequest(string Email);
