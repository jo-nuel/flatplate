namespace FlatPlate.Core.Contracts.Authentication;

/// <summary>
/// Returns a safe status message for account operations that do not sign a user in.
/// </summary>
public sealed record AccountMessageResponse(string Message);
