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

            // Google sets this when the address is verified. An unverified one proves only that
            // somebody typed it, so it must not be shown as though the system had confirmed it.
            if (!payload.EmailVerified)
            {
                logger.LogWarning("Google sign-in rejected: the token's email address is not verified.");
                return null;
            }

            return new GoogleIdentity(payload.Subject, payload.Email);
        }
        catch (InvalidJwtException e)
        {
            // The expected failure: expired, malformed, wrong audience, bad signature. Logged
            // without the token itself, which is a credential while it is still valid.
            logger.LogInformation("Google sign-in rejected: ID token did not verify ({Reason}).", e.Message);
            return null;
        }
    }
}
