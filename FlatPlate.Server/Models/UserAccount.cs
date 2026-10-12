namespace FlatPlate.Server.Models;

/// <summary>
/// Stores the private server-side state for a FlatPlate user account.
/// </summary>
public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsEmailVerified { get; set; }

    public string? EmailVerificationTokenHash { get; set; }

    public DateTimeOffset? EmailVerificationTokenExpiresAtUtc { get; set; }

    public DateTimeOffset? EmailVerificationSentAtUtc { get; set; }

    public string? PasswordResetTokenHash { get; set; }

    public DateTimeOffset? PasswordResetTokenExpiresAtUtc { get; set; }

    public DateTimeOffset? PasswordResetSentAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? PasswordChangedAtUtc { get; set; }
}
