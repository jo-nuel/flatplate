namespace FlatPlate.Core.Contracts.Authentication;

/// <summary>
/// Requests a replacement email-verification code for an account.
/// </summary>
public sealed record RequestEmailVerificationRequest(string Email);
