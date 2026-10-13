using FlatPlate.Server.Models;
using FlatPlate.Server.Security;
using Microsoft.AspNetCore.Identity;

namespace FlatPlate.Tests;

/// <summary>
/// Checks that account passwords are salted, hashed, and verified securely.
/// </summary>
public sealed class PasswordServiceTests
{
    private PasswordService _passwordService = null!;

    /// <summary>
    /// Creates the same ASP.NET Core password hasher used by the API.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _passwordService = new PasswordService(new PasswordHasher<UserAccount>());
    }

    /// <summary>
    /// Confirms that the stored value does not contain the submitted password.
    /// </summary>
    [Test]
    public void HashPassword_ValidPassword_DoesNotStorePlainText()
    {
        var account = CreateAccount();

        var result = _passwordService.HashPassword(account, "FlatPlate9A");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.EqualTo("FlatPlate9A"));
            Assert.That(result, Does.Not.Contain("FlatPlate9A"));
        });
    }

    /// <summary>
    /// Confirms that the random salt produces different hashes for the same password.
    /// </summary>
    [Test]
    public void HashPassword_SamePasswordTwice_ProducesDifferentHashes()
    {
        var account = CreateAccount();

        var first = _passwordService.HashPassword(account, "FlatPlate9A");
        var second = _passwordService.HashPassword(account, "FlatPlate9A");

        Assert.That(second, Is.Not.EqualTo(first));
    }

    /// <summary>
    /// Confirms that the correct password matches its stored hash.
    /// </summary>
    [Test]
    public void VerifyPassword_CorrectPassword_ReturnsSuccess()
    {
        var account = CreateAccount();
        account.PasswordHash = _passwordService.HashPassword(account, "FlatPlate9A");

        var result = _passwordService.VerifyPassword(account, "FlatPlate9A");

        Assert.That(result, Is.Not.EqualTo(PasswordVerificationResult.Failed));
    }

    /// <summary>
    /// Confirms that an incorrect password does not match the stored hash.
    /// </summary>
    [Test]
    public void VerifyPassword_IncorrectPassword_ReturnsFailed()
    {
        var account = CreateAccount();
        account.PasswordHash = _passwordService.HashPassword(account, "FlatPlate9A");

        var result = _passwordService.VerifyPassword(account, "WrongPassword9");

        Assert.That(result, Is.EqualTo(PasswordVerificationResult.Failed));
    }

    /// <summary>
    /// Confirms that an account without a stored password hash cannot authenticate.
    /// </summary>
    [Test]
    public void VerifyPassword_MissingHash_ReturnsFailed()
    {
        var result = _passwordService.VerifyPassword(CreateAccount(), "FlatPlate9A");

        Assert.That(result, Is.EqualTo(PasswordVerificationResult.Failed));
    }

    private static UserAccount CreateAccount()
    {
        return new UserAccount
        {
            DisplayName = "Jonathan",
            Email = "jonathan@example.com",
            NormalizedEmail = "JONATHAN@EXAMPLE.COM",
        };
    }
}
