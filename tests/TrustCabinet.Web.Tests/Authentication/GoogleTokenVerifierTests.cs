using Google.Apis.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Infrastructure.Authentication;

namespace TrustCabinet.Web.Tests.Authentication;

/// <summary>
/// The real verifier — previously untested, which is how a malformed token came to return a 500
/// with a stack trace and a token with no subject claim came to be accepted.
///
/// What cannot be tested here: the signature, issuer, audience and expiry checks, because
/// GoogleJsonWebSignature.ValidationSettings exposes only Audience, HostedDomain, Clock,
/// ForceGoogleCertRefresh and two clock tolerances — no way to substitute a signing key, so
/// reaching those paths would need Google's private key. Those need an integration test with a
/// real Google-issued token; the gap is recorded in docs/03-api/01-xac-thuc.md.
/// </summary>
public class GoogleTokenVerifierTests
{
    private const string ClientId = "123456.apps.googleusercontent.com";

    private static GoogleTokenVerifier NewVerifier() => new(
        Options.Create(new GoogleAuthenticationOptions { Enabled = true, ClientId = ClientId }),
        NullLogger<GoogleTokenVerifier>.Instance);

    // ------------------------------------------------- payload rules (the testable half)

    [Fact]
    public void A_payload_with_a_verified_email_and_a_subject_identifies_the_holder()
    {
        var identity = GoogleTokenVerifier.IdentityFrom(new GoogleJsonWebSignature.Payload
        {
            Subject = "115742000000000000000",
            Email = "thuky@trustcabinet.local",
            EmailVerified = true,
        });

        Assert.NotNull(identity);
        Assert.Equal("115742000000000000000", identity.Subject);
        Assert.Equal("thuky@trustcabinet.local", identity.Email);
    }

    [Fact]
    public void A_payload_whose_email_is_not_verified_is_refused()
    {
        var identity = GoogleTokenVerifier.IdentityFrom(new GoogleJsonWebSignature.Payload
        {
            Subject = "115742000000000000000",
            Email = "thuky@trustcabinet.local",
            EmailVerified = false,
        });

        // An unverified address proves only that somebody typed it. Accepting it would let someone
        // claim an address they do not control.
        Assert.Null(identity);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_payload_without_a_usable_subject_is_refused(string? subject)
    {
        var identity = GoogleTokenVerifier.IdentityFrom(new GoogleJsonWebSignature.Payload
        {
            Subject = subject,
            Email = "thuky@trustcabinet.local",
            EmailVerified = true,
        });

        // This is the one that matters most. A null subject reaching the repository becomes
        // `WHERE google_subject_id IS NULL` in SQL, which every password-only account satisfies —
        // so the caller would be signed in as whichever unlinked row PostgreSQL returned first.
        Assert.Null(identity);
    }

    // ------------------------------------------------------------- token-level behaviour

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_token_is_refused_without_calling_google(string idToken)
    {
        Assert.Null(await NewVerifier().VerifyAsync(idToken));
    }

    [Theory]
    // No dots at all, and two segments: both raise InvalidJwtException.
    [InlineData("notajwt")]
    [InlineData("a.b")]
    // Three segments that are not valid base64url. This one used to throw FormatException, which
    // escaped the catch and surfaced as an HTTP 500 with a stack trace to an anonymous caller.
    [InlineData("a.b.c")]
    [InlineData("!!!.???.###")]
    public async Task A_malformed_token_returns_null_instead_of_throwing(string idToken)
    {
        // The assertion is as much that this does not throw as that it returns null: every way a
        // token can be unusable has to collapse to one answer, or the endpoint's failure modes
        // become distinguishable from each other.
        Assert.Null(await NewVerifier().VerifyAsync(idToken));
    }

    [Fact]
    public async Task A_well_formed_but_forged_token_is_refused()
    {
        // Header and payload are valid base64url JSON carrying the right audience, a subject and
        // email_verified: true — everything except a signature Google could have produced.
        var forged = string.Join('.',
            Base64Url("""{"alg":"RS256","kid":"forged"}"""),
            Base64Url($$"""
                {"iss":"accounts.google.com","sub":"115742000000000000000",
                 "email":"attacker@gmail.com","email_verified":true,
                 "aud":"{{ClientId}}","exp":9999999999,"iat":1600000000}
                """),
            Base64Url("chu-ky-gia"));

        Assert.Null(await NewVerifier().VerifyAsync(forged));
    }

    private static string Base64Url(string value)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
