namespace FlatPlate.Core.Contracts.Authentication;

/// <summary>
/// Identifies an account and the one-time token used to verify its email.
/// </summary>
public sealed record VerifyEmailRequest(string Email, string Token);
