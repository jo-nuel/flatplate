namespace FlatPlate.Server.Security;

/// <summary>
/// Generates one-time codes and protects their stored representation.
/// </summary>
public interface IOneTimeCodeService
{
    string GenerateCode();

    string Protect(
        string normalizedEmail,
        string code,
        OneTimeCodePurpose purpose);

    bool Verify(
        string normalizedEmail,
        string suppliedCode,
        OneTimeCodePurpose purpose,
        string expectedHash);
}
