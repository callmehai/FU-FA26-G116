using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Authentication;

/// <summary>
/// Issues the token pair: a signed JWT to authenticate calls, and an opaque random string to get
/// the next one.
/// </summary>
public sealed class JwtTokenIssuer(IOptions<AuthenticationOptions> options) : ITokenIssuer
{
    /// <summary>Claim carrying one <see cref="Capability"/>. Repeated once per capability.</summary>
    public const string CapabilityClaim = "cap";

    private readonly AuthenticationOptions _options = options.Value;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(_options.AccessTokenMinutes);

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public string CreateAccessToken(User user, IReadOnlyCollection<Capability> capabilities)
    {
        var now = DateTimeOffset.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            // A token stays valid until it expires, so each one needs its own id for an audit
            // entry to point at a specific token rather than "some token of this user's".
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString()),
        };

        // One claim per capability rather than a comma-joined string, so [Authorize] policies can
        // match a single value without parsing.
        claims.AddRange(capabilities.Select(c => new Claim(CapabilityClaim, c.ToString())));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.Add(AccessTokenLifetime).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken()
        // 256 bits from a cryptographic RNG: the token carries no information, so its only defence
        // is being impossible to guess. Base64Url because it travels in JSON and, later, a cookie.
        => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
}

/// <summary>
/// The registered claim names this code uses. Spelled out rather than taken from
/// JwtRegisteredClaimNames so the short JWT names ("sub", "jti") survive ASP.NET Core's default
/// claim-mapping, which otherwise rewrites them into long URIs.
/// </summary>
internal static class JwtRegisteredClaimNames
{
    public const string Sub = "sub";
    public const string Jti = "jti";
    public const string Email = "email";
}
