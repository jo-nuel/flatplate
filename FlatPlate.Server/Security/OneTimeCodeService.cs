using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FlatPlate.Server.Security;

/// <summary>
/// Protects short codes with a server-side HMAC key before database storage.
/// </summary>
public sealed class OneTimeCodeService : IOneTimeCodeService
{
    private readonly byte[] _key;

    public OneTimeCodeService(string signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);

        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new ArgumentException(
                "The one-time code signing key must be at least 32 bytes.",
                nameof(signingKey));
        }

        _key = Encoding.UTF8.GetBytes(signingKey);
    }

    public string GenerateCode() => RandomNumberGenerator
        .GetInt32(0, 1_000_000)
        .ToString("D6", CultureInfo.InvariantCulture);

    public string Protect(
        string normalizedEmail,
        string code,
        OneTimeCodePurpose purpose)
    {
        var payload = Encoding.UTF8.GetBytes(
            $"{purpose}:{normalizedEmail}:{code}");
        return Convert.ToHexString(HMACSHA256.HashData(_key, payload));
    }

    public bool Verify(
        string normalizedEmail,
        string suppliedCode,
        OneTimeCodePurpose purpose,
        string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(suppliedCode)
            || string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        var actualHash = Protect(normalizedEmail, suppliedCode, purpose);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actualHash),
            Encoding.ASCII.GetBytes(expectedHash));
    }
}
