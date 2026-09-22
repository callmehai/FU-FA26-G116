using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Authentication;

namespace TrustCabinet.Infrastructure.Authentication;

/// <summary>
/// Verifies a Google ID token with Google's own library.
///
/// Not hand-rolled: verifying an ID token properly means fetching Google's current signing
/// certificates, honouring their rotation, checking the signature, the issuer, the audience and
/// the expiry. <see cref="GoogleJsonWebSignature"/> does all of it and caches the certificates;
/// a bespoke version is where a project ends up accepting an unsigned token because it only
/// decoded the payload.
/// </summary>
public sealed class GoogleTokenVerifier(
    IOptions<GoogleAuthenticationOptions> options,
    ILogger<GoogleTokenVerifier> logger) : IGoogleTokenVerifier
{
    private readonly GoogleAuthenticationOptions _options = options.Value;

    public async Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                // Without this the token only has to be *a* valid Google token: one issued to any
                // other application would be accepted, and anyone running such an app could sign
                // in here as one of their own users.
                Audience = [_options.ClientId],
            });

            var identity = IdentityFrom(payload);

            if (identity is null)
            {
                logger.LogWarning(
                    "Google sign-in rejected: the token verified but carries no usable subject or "
                    + "an unverified email address.");
            }

            return identity;
        }
        // Every way a token can be unusable collapses to one answer. Catching only
        // InvalidJwtException left FormatException — thrown when a segment is not valid
        // base64url, e.g. "a.b.c" — to escape as a 500 with a stack trace, to an anonymous
        // caller, and made a malformed token distinguishable from an invalid one.
        catch (Exception e) when (e is InvalidJwtException or FormatException)
        {
            // Logged without the token itself, which is a credential while it is still valid.
            logger.LogInformation("Google sign-in rejected: ID token did not verify ({Reason}).", e.Message);
            return null;
        }
        // Deliberately NOT caught: HttpRequestException from fetching Google's signing
        // certificates. That is an outage on our side, not a bad token, and answering "your
        // sign-in failed" would send the user off to re-check a password that was never the
        // problem. It surfaces as a 500 for now; a 503 would be better and is worth a ticket.
    }

    /// <summary>
    /// The checks that apply to a payload whose signature, issuer, audience and expiry Google's
    /// library has already accepted. Returns null when the payload cannot identify anybody.
    ///
    /// Internal and static so it can be tested directly. The signature check around it cannot be:
    /// <c>ValidationSettings</c> exposes only Audience, HostedDomain, Clock, ForceGoogleCertRefresh
    /// and the two clock tolerances — there is no way to substitute a signing key, so producing a
    /// token that reaches this point in a unit test would require Google's private key. Keeping
    /// these two rules out here means they are covered even though the wrapper is not.
    /// </summary>
    internal static GoogleIdentity? IdentityFrom(GoogleJsonWebSignature.Payload payload)
    {
        // Google sets this when the address is verified. An unverified one proves only that
        // somebody typed it, so it must not be treated as confirmed.
        if (!payload.EmailVerified)
        {
            return null;
        }

        // ValidationSettings has no option to require `sub`, and Payload.Subject is a plain
        // nullable string, so a token without one arrives here as null. It must be refused at this
        // boundary: downstream, `WHERE google_subject_id = NULL` becomes `IS NULL` in SQL and
        // would match an account that has no Google link at all.
        if (string.IsNullOrWhiteSpace(payload.Subject))
        {
            return null;
        }

        return new GoogleIdentity(payload.Subject, payload.Email);
    }
}
