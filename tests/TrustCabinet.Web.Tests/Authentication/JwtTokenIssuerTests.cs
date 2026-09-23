using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Authentication;

namespace TrustCabinet.Web.Tests.Authentication;

/// <summary>
/// What the access token carries, and that it only validates under the key that signed it.
/// </summary>
public class JwtTokenIssuerTests
{
    private const string SigningKey = "chi-dung-trong-test-khong-phai-khoa-that-32+";

    private static readonly AuthenticationOptions Options = new()
    {
        SigningKey = SigningKey,
        Issuer = "TrustCabinet",
        Audience = "TrustCabinet",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7,
    };

    private static readonly User Secretary = new()
    {
        FullName = "Nguyễn Thư Ký",
        Email = "thuky@trustcabinet.local",
        PasswordHash = "khong-lien-quan",
        RoleId = BuiltInRoles.Secretary,
    };

    private static JwtTokenIssuer NewIssuer(AuthenticationOptions? options = null)
        => new(Microsoft.Extensions.Options.Options.Create(options ?? Options));

    private static JwtSecurityToken Parse(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void The_access_token_identifies_the_user()
    {
        var token = Parse(NewIssuer().CreateAccessToken(Secretary, []));

        Assert.Equal(Secretary.Id.ToString(), token.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal(Secretary.Email, token.Claims.First(c => c.Type == "email").Value);
        // No role claim: authorization reads capabilities, and a role can be renamed while the
        // token is alive (ADR-0008).
        Assert.DoesNotContain(token.Claims, c => c.Type is ClaimTypes.Role or "role");
    }

    [Fact]
    public void The_access_token_carries_one_claim_per_capability()
    {
        var token = Parse(NewIssuer().CreateAccessToken(
            Secretary, [Capability.MeetingCreator, Capability.MinutesEditor]));

        var capabilities = token.Claims
            .Where(c => c.Type == JwtTokenIssuer.CapabilityClaim)
            .Select(c => c.Value)
            .ToArray();

        // Separate claims rather than one joined string, so an authorization policy can match a
        // single value without parsing.
        Assert.Equal(["MeetingCreator", "MinutesEditor"], capabilities);
    }

    [Fact]
    public void An_access_token_for_a_member_carries_no_capability_claim()
    {
        var token = Parse(NewIssuer().CreateAccessToken(Secretary, []));

        Assert.DoesNotContain(token.Claims, c => c.Type == JwtTokenIssuer.CapabilityClaim);
    }

    [Fact]
    public void The_access_token_never_carries_the_password_hash()
    {
        var raw = NewIssuer().CreateAccessToken(Secretary, []);

        // A JWT is signed, not encrypted: anyone holding it can read every claim inside.
        Assert.DoesNotContain("khong-lien-quan", raw);
    }

    [Fact]
    public void Every_access_token_has_its_own_id()
    {
        var issuer = NewIssuer();

        var first = Parse(issuer.CreateAccessToken(Secretary, [])).Claims.First(c => c.Type == "jti").Value;
        var second = Parse(issuer.CreateAccessToken(Secretary, [])).Claims.First(c => c.Type == "jti").Value;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void The_access_token_expires_after_the_configured_lifetime()
    {
        var issuer = NewIssuer(new AuthenticationOptions { SigningKey = SigningKey, AccessTokenMinutes = 5 });

        var token = Parse(issuer.CreateAccessToken(Secretary, []));

        Assert.Equal(TimeSpan.FromMinutes(5), issuer.AccessTokenLifetime);
        // A minute of slack: the assertion is about the configured lifetime, not about how long
        // the two statements above took.
        Assert.InRange(
            token.ValidTo,
            DateTime.UtcNow.AddMinutes(4),
            DateTime.UtcNow.AddMinutes(6));
    }

    [Fact]
    public void The_access_token_validates_under_the_key_that_signed_it()
    {
        var raw = NewIssuer().CreateAccessToken(Secretary, [Capability.MeetingCreator]);

        var principal = NewHandler().ValidateToken(raw, Parameters(SigningKey), out _);

        Assert.Equal(Secretary.Id.ToString(), principal.FindFirstValue("sub"));
    }

    [Fact]
    public void A_token_signed_with_another_key_is_rejected()
    {
        var raw = NewIssuer().CreateAccessToken(Secretary, []);

        // Otherwise anyone could mint their own token with any role in it.
        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() =>
            NewHandler().ValidateToken(
                raw, Parameters("mot-khoa-khac-hoan-toan-va-du-32-ky-tu"), out _));
    }

    [Fact]
    public void A_tampered_token_is_rejected()
    {
        var raw = NewIssuer().CreateAccessToken(Secretary, []);

        // Flip the last character of the signature.
        var tampered = raw[..^1] + (raw[^1] == 'A' ? 'B' : 'A');

        Assert.ThrowsAny<SecurityTokenException>(() =>
            NewHandler().ValidateToken(tampered, Parameters(SigningKey), out _));
    }

    [Fact]
    public void A_token_from_another_issuer_is_rejected()
    {
        var foreign = NewIssuer(new AuthenticationOptions
        {
            SigningKey = SigningKey,
            Issuer = "SomethingElse",
            Audience = "TrustCabinet",
        });

        var raw = foreign.CreateAccessToken(Secretary, []);

        Assert.Throws<SecurityTokenInvalidIssuerException>(() =>
            NewHandler().ValidateToken(raw, Parameters(SigningKey), out _));
    }

    [Fact]
    public void A_fresh_token_carries_no_notBefore()
    {
        // The token used to be issued with notBefore: now. Validation runs at ClockSkew = Zero —
        // deliberately, so an expired token dies at its expiry instead of five minutes later — and
        // that same zero tolerance applies to "nbf": a validator whose clock sat a second behind
        // the issuer's would reject a token that had only just been minted.
        //
        // "exp" still bounds the token, so dropping "nbf" widens nothing.
        var token = Parse(NewIssuer().CreateAccessToken(Secretary, []));

        Assert.DoesNotContain(token.Claims, c => c.Type == "nbf");
    }

    [Fact]
    public void A_token_validates_against_a_validator_whose_clock_is_behind()
    {
        // The failure the missing "nbf" prevents, played out: one machine issues, another whose
        // clock lags validates. With notBefore: now and ClockSkew = Zero this threw
        // SecurityTokenNotYetValidException on a token issued moments earlier.
        var raw = NewIssuer().CreateAccessToken(Secretary, [Capability.AdminUsers]);

        var laggingClock = Parameters(SigningKey);
        laggingClock.LifetimeValidator = (notBefore, expires, _, _) =>
        {
            var theirNow = DateTime.UtcNow.AddSeconds(-30);
            return (notBefore is null || notBefore <= theirNow)
                && (expires is null || expires > theirNow);
        };

        var principal = NewHandler().ValidateToken(raw, laggingClock, out _);

        Assert.Equal(Secretary.Id.ToString(), principal.FindFirst("sub")!.Value);
    }

    [Fact]
    public void Refresh_tokens_are_unique_and_opaque()
    {
        var issuer = NewIssuer();

        var tokens = Enumerable.Range(0, 100).Select(_ => issuer.CreateRefreshToken()).ToArray();

        Assert.Equal(100, tokens.Distinct().Count());

        foreach (var token in tokens)
        {
            // 256 bits, base64url — no dots, so it is visibly not a JWT and carries no claims.
            Assert.DoesNotContain('.', token);
            Assert.True(token.Length >= 40, $"Refresh token too short to be unguessable: {token.Length}");
        }
    }

    [Fact]
    public void The_refresh_token_lifetime_comes_from_configuration()
    {
        var issuer = NewIssuer(new AuthenticationOptions { SigningKey = SigningKey, RefreshTokenDays = 3 });

        Assert.Equal(TimeSpan.FromDays(3), issuer.RefreshTokenLifetime);
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
