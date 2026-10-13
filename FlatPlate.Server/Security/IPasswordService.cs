using FlatPlate.Server.Models;
using Microsoft.AspNetCore.Identity;

namespace FlatPlate.Server.Security;

/// <summary>
/// Hashes and verifies account passwords without exposing hashing details.
/// </summary>
public interface IPasswordService
{
    string HashPassword(UserAccount account, string password);

    PasswordVerificationResult VerifyPassword(
        UserAccount account,
        string suppliedPassword);
}
