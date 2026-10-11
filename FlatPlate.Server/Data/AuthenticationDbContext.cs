using FlatPlate.Server.Models;
using FlatPlate.Core.Validation;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.Server.Data;

/// <summary>
/// Maps private account data to the server-owned authentication database.
/// </summary>
public sealed class AuthenticationDbContext : DbContext
{
    /// <summary>
    /// Creates an authentication context with the configured database provider.
    /// </summary>
    public AuthenticationDbContext(
        DbContextOptions<AuthenticationDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    /// <summary>
    /// Configures account lengths, required values, and unique email lookup.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var account = modelBuilder.Entity<UserAccount>();
        account.HasKey(user => user.Id);
        account.Property(user => user.DisplayName)
            .HasMaxLength(AuthenticationValidator.MaximumDisplayNameLength)
            .IsRequired();
        account.Property(user => user.Email)
            .HasMaxLength(AuthenticationValidator.MaximumEmailLength)
            .IsRequired();
        account.Property(user => user.NormalizedEmail)
            .HasMaxLength(AuthenticationValidator.MaximumEmailLength)
            .IsRequired();
        account.HasIndex(user => user.NormalizedEmail).IsUnique();
        account.Property(user => user.PasswordHash)
            .HasMaxLength(512)
            .IsRequired();
        account.Property(user => user.EmailVerificationTokenHash)
            .HasMaxLength(64);
        account.Property(user => user.PasswordResetTokenHash)
            .HasMaxLength(64);
    }
}
