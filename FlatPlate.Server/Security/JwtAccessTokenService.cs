using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FlatPlate.Server.Models;
using Microsoft.IdentityModel.Tokens;

namespace FlatPlate.Server.Security;

/// <summary>
/// Creates HMAC-signed JWTs containing only the claims required by the API.
/// </summary>
public sealed class JwtAccessTokenService : IAccessTokenService
{
    private readonly AccessTokenSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly SigningCredentials _credentials;

    public JwtAccessTokenService(
        AccessTokenSettings settings,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Encoding.UTF8.GetByteCount(settings.SigningKey) < 32)
        {
            throw new ArgumentException(
                "The JWT signing key must be at least 32 bytes.",
                nameof(settings));
        }

        if (settings.Lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "The access-token lifetime must be positive.");
        }

        _settings = settings;
        _timeProvider = timeProvider;
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessToken Create(UserAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.Add(_settings.Lifetime);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, account.Email),
            new Claim(JwtRegisteredClaimNames.Name, account.DisplayName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: _credentials);

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }
}
