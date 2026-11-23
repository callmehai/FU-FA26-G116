using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Application.Authentication;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Authentication;

namespace TrustCabinet.Web.Tests.Authentication;

// Round 1 FE-01 unit tests for Password.Verify (PasswordHasher) and Jwt.AccessToken
// (JwtTokenIssuer.CreateAccessToken). Pure in-process tests, no database: the hasher is the
// real one and the issuer only needs a signing key configured in the test.
// Each test carries BOTH [Trait("UTC", "<Class.Method>/<UTCnnn>")] (what apply-trx matches on,
// since the trx carries no trait) AND [Trait("Trait", "UTC")], plus a DisplayName containing
// the same "<Class.Method>/<UTCnnn>" key.
// Tester: Phạm Gia Khánh.
public sealed class PasswordVerifyJwtRound1Tests
{
    private readonly PasswordHasher _hasher = new();

    [Fact(DisplayName = "PasswordHasher.Verify/UTC001 the same password verifies against its own hash")]
    [Trait("UTC", "PasswordHasher.Verify/UTC001")]
    [Trait("Trait", "UTC")]
    public void Verify_UTC001()
    {
        // Return: true. Exception: N/A (no throw). Log: N/A (no logger in the hasher).
        var hash = _hasher.Hash("MatKhau@2026");

        Assert.True(_hasher.Verify(hash, "MatKhau@2026"));
    }

    [Fact(DisplayName = "PasswordHasher.Verify/UTC002 a malformed stored hash fails verification instead of throwing")]
    [Trait("UTC", "PasswordHasher.Verify/UTC002")]
    [Trait("Trait", "UTC")]
    public void Verify_UTC002()
    {
        // Return: false for each malformed shape. Exception: N/A — none of these may throw
        // (the framework parser's FormatException is caught inside Verify). Log: N/A.
        foreach (var storedHash in new[] { "", "khong-phai-hash", "SET-BY-SCRUM-11" })
        {
            Assert.False(_hasher.Verify(storedHash, "bat-ky-mat-khau"));
        }
    }

    [Fact(DisplayName = "PasswordHasher.Hash/UTC003 the same password hashes differently every time")]
    [Trait("UTC", "PasswordHasher.Hash/UTC003")]
    [Trait("Trait", "UTC")]
    public void Hash_UTC003()
    {
        // Return: two different hash strings, both verifying against the password
        // (proof the salt is per-password and random). Exception: N/A. Log: N/A.
        var first = _hasher.Hash("MatKhau@2026");
        var second = _hasher.Hash("MatKhau@2026");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify(first, "MatKhau@2026"));
        Assert.True(_hasher.Verify(second, "MatKhau@2026"));
    }

    private const string SigningKey = "chi-dung-trong-test-khong-phai-khoa-that-32+";

    private static readonly User Secretary = new()
    {
        FullName = "Nguyễn Thư Ký",
        Email = "thuky@trustcabinet.local",
        PasswordHash = "khong-lien-quan",
        RoleId = BuiltInRoles.Member,
    };

    private static JwtTokenIssuer NewIssuer(AuthenticationOptions? options = null)
        => new(Microsoft.Extensions.Options.Options.Create(options ?? new AuthenticationOptions
        {
            SigningKey = SigningKey,
            Issuer = "TrustCabinet",
            Audience = "TrustCabinet",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
        }));

    private static JwtSecurityToken Parse(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact(DisplayName = "JwtTokenIssuer.CreateAccessToken/UTC001 the access token carries subject, name, email and one claim per capability")]
    [Trait("UTC", "JwtTokenIssuer.CreateAccessToken/UTC001")]
    [Trait("Trait", "UTC")]
    public void CreateAccessToken_UTC001()
    {
        // Return: signed HS256 JWT with sub, jti, email, ClaimTypes.Name and one "cap" claim
        // per capability (MeetingCreator, VoiceCollector); no role claim; iss/aud "TrustCabinet".
        // Exception: N/A. Log: N/A.
        var issuer = NewIssuer();
        var token = Parse(issuer.CreateAccessToken(Secretary, [Capability.MeetingCreator, Capability.VoiceCollector]));

        Assert.Equal(Secretary.Id.ToString(), token.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal(Secretary.Email, token.Claims.First(c => c.Type == "email").Value);
        Assert.Equal("Nguyễn Thư Ký", token.Claims.First(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal(
            ["MeetingCreator", "VoiceCollector"],
            token.Claims.Where(c => c.Type == ITokenIssuer.CapabilityClaim).Select(c => c.Value).ToArray());
        // No role claim: authorization reads capabilities, and a role can be renamed while the
        // token is alive (ADR-0008).
        Assert.DoesNotContain(token.Claims, c => c.Type is ClaimTypes.Role or "role");
        Assert.Equal("TrustCabinet", token.Issuer);
        Assert.Contains("TrustCabinet", token.Audiences);
        Assert.Equal(SecurityAlgorithms.HmacSha256, token.SignatureAlgorithm);

        // jti is unique per token.
        var other = Parse(issuer.CreateAccessToken(Secretary, [Capability.MeetingCreator, Capability.VoiceCollector]));
        Assert.NotEqual(
            token.Claims.First(c => c.Type == "jti").Value,
            other.Claims.First(c => c.Type == "jti").Value);

        // exp is about 15 minutes out, with no nbf.
        Assert.InRange(token.ValidTo, DateTime.UtcNow.AddMinutes(13), DateTime.UtcNow.AddMinutes(17));
        Assert.DoesNotContain(token.Claims, c => c.Type == "nbf");
    }

    [Fact(DisplayName = "JwtTokenIssuer.CreateAccessToken/UTC002 a token for an empty capability list carries no cap claim")]
    [Trait("UTC", "JwtTokenIssuer.CreateAccessToken/UTC002")]
    [Trait("Trait", "UTC")]
    public void CreateAccessToken_UTC002()
    {
        // Return: signed JWT with sub, jti, email and name claims and no "cap" claim.
        // Exception: N/A. Log: N/A.
        var token = Parse(NewIssuer().CreateAccessToken(Secretary, []));

        Assert.Equal(Secretary.Id.ToString(), token.Claims.First(c => c.Type == "sub").Value);
        Assert.NotEmpty(token.Claims.First(c => c.Type == "jti").Value);
        Assert.Equal(Secretary.Email, token.Claims.First(c => c.Type == "email").Value);
        Assert.Equal("Nguyễn Thư Ký", token.Claims.First(c => c.Type == ClaimTypes.Name).Value);
        Assert.DoesNotContain(token.Claims, c => c.Type == ITokenIssuer.CapabilityClaim);
    }

    [Fact(DisplayName = "JwtTokenIssuer.CreateAccessToken/UTC003 the access token expires about five minutes after creation")]
    [Trait("UTC", "JwtTokenIssuer.CreateAccessToken/UTC003")]
    [Trait("Trait", "UTC")]
    public void CreateAccessToken_UTC003()
    {
        // Return: exp about 5 minutes after creation; no nbf claim. Exception: N/A. Log: N/A.
        var issuer = NewIssuer(new AuthenticationOptions { SigningKey = SigningKey, AccessTokenMinutes = 5 });

        var token = Parse(issuer.CreateAccessToken(Secretary, []));

        Assert.Equal(TimeSpan.FromMinutes(5), issuer.AccessTokenLifetime);
        Assert.InRange(
            token.ValidTo,
            DateTime.UtcNow.AddMinutes(4),
            DateTime.UtcNow.AddMinutes(6));
        Assert.DoesNotContain(token.Claims, c => c.Type == "nbf");
    }

    [Fact(DisplayName = "JwtTokenIssuer.CreateAccessToken/UTC004 the token validates under its key and fails under any other key or after tampering")]
    [Trait("UTC", "JwtTokenIssuer.CreateAccessToken/UTC004")]
    [Trait("Trait", "UTC")]
    public void CreateAccessToken_UTC004()
    {
        // Return: the token validates under the configured signing key and fails validation
        // under any other key or after tampering. Exception: N/A (rejections surface as the
        // validator's SecurityTokenException, which is the asserted outcome). Log: N/A.
        var raw = NewIssuer().CreateAccessToken(Secretary, [Capability.MeetingCreator]);

        var principal = NewHandler().ValidateToken(raw, Parameters(SigningKey), out _);

        Assert.Equal(Secretary.Id.ToString(), principal.FindFirstValue("sub"));

        // Otherwise anyone could mint their own token with any capability in it.
        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() =>
            NewHandler().ValidateToken(
                raw, Parameters("mot-khoa-khac-hoan-toan-va-du-32-ky-tu"), out _));

        // Flip the last character of the signature.
        var tampered = raw[..^1] + (raw[^1] == 'A' ? 'B' : 'A');

        Assert.ThrowsAny<SecurityTokenException>(() =>
            NewHandler().ValidateToken(tampered, Parameters(SigningKey), out _));
    }

    /// <summary>
    /// Mirrors what Program.cs configures — including MapInboundClaims = false, without which the
    /// handler rewrites "sub" into a WS-Federation URI and FindFirstValue("sub") returns null.
    /// </summary>
    private static JwtSecurityTokenHandler NewHandler() => new() { MapInboundClaims = false };

    private static TokenValidationParameters Parameters(string key) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = "TrustCabinet",
        ValidateAudience = true,
        ValidAudience = "TrustCabinet",
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ClockSkew = TimeSpan.Zero,
    };
}
