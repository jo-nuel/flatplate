using FlatPlate.Server.Data;
using FlatPlate.Server.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.Tests;

/// <summary>
/// Checks the authentication database constraints with an isolated SQLite database.
/// </summary>
public sealed class AuthenticationDbContextTests
{
    /// <summary>
    /// Confirms that normalized email addresses are unique regardless of casing.
    /// </summary>
    [Test]
    public void SaveChanges_DuplicateNormalizedEmail_ThrowsDatabaseException()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AuthenticationDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AuthenticationDbContext(options);
        context.Database.EnsureCreated();
        context.UserAccounts.Add(CreateAccount("Jonathan@example.com"));
        context.SaveChanges();
        context.UserAccounts.Add(CreateAccount("jonathan@EXAMPLE.com"));

        Assert.That(
            () => context.SaveChanges(),
            Throws.TypeOf<DbUpdateException>());
    }

    /// <summary>
    /// Confirms that two different normalized email addresses can be saved.
    /// </summary>
    [Test]
    public void SaveChanges_DifferentEmails_SavesBothAccounts()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AuthenticationDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AuthenticationDbContext(options);
        context.Database.EnsureCreated();
        context.UserAccounts.Add(CreateAccount("first@example.com"));
        context.UserAccounts.Add(CreateAccount("second@example.com"));

        context.SaveChanges();

        Assert.That(context.UserAccounts.Count(), Is.EqualTo(2));
    }

    private static UserAccount CreateAccount(string email)
    {
        var now = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        return new UserAccount
        {
            DisplayName = "Test User",
            Email = email,
            NormalizedEmail = email.Trim().ToUpperInvariant(),
            PasswordHash = "test-password-hash",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }
}
