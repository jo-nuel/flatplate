using FlatPlate.Server.Models;
using Microsoft.AspNetCore.Identity;

namespace FlatPlate.Server.Security;

/// <summary>
/// Uses ASP.NET Core Identity's salted password hasher for account credentials.
/// </summary>
public sealed class PasswordService : IPasswordService
{
    private readonly IPasswordHasher<UserAccount> _passwordHasher;

    /// <summary>
    /// Creates the service with the server's configured password hasher.
    /// </summary>
    public PasswordService(IPasswordHasher<UserAccount> passwordHasher)
    {
        ArgumentNullException.ThrowIfNull(passwordHasher);
        _passwordHasher = passwordHasher;
    }

    /// <inheritdoc />
    public string HashPassword(UserAccount account, string password)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrEmpty(password);
        return _passwordHasher.HashPassword(account, password);
    }

    /// <inheritdoc />
    public PasswordVerificationResult VerifyPassword(
        UserAccount account,
        string suppliedPassword)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrEmpty(suppliedPassword);

        if (string.IsNullOrWhiteSpace(account.PasswordHash))
        {
            return PasswordVerificationResult.Failed;
        }

        return _passwordHasher.VerifyHashedPassword(
            account,
            account.PasswordHash,
            suppliedPassword);
    }
}
