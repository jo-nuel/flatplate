namespace FlatPlate.Core.Contracts.Authentication;

/// <summary>
/// Describes the non-sensitive account details returned to the desktop app.
/// </summary>
public sealed record AuthenticatedUser(
    Guid Id,
    string DisplayName,
    string Email,
    bool IsEmailVerified);
