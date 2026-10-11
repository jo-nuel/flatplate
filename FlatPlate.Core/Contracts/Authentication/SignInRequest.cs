namespace FlatPlate.Core.Contracts.Authentication;

/// <summary>
/// Contains the credentials submitted when a user signs in.
/// </summary>
public sealed record SignInRequest(string Email, string Password);
