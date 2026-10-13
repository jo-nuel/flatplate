namespace FlatPlate.Core.Contracts.Authentication;

/// <summary>
/// Contains the details required to create a FlatPlate account.
/// </summary>
public sealed record RegisterRequest(
    string DisplayName,
    string Email,
    string Password,
    string ConfirmPassword);
