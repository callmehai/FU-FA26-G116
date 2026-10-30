using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Infrastructure.Authentication;

namespace TrustCabinet.Infrastructure.GuestAccess;

/// <summary>
/// The secrets behind guest links and codes. Each key is derived from the token signing key under its
/// own label, so a deployment needs no new secret and a link token can never double as a code hash.
/// </summary>
public sealed class GuestCredentials(IOptions<AuthenticationOptions> options)
{
    private readonly byte[] _linkKey = Derive(options.Value.SigningKey, "trustcabinet:guest-invitation-link");
    private readonly byte[] _codeKey = Derive(options.Value.SigningKey, "trustcabinet:guest-verification-code");

    /// <summary>
    /// Derived rather than stored: a retried invitation dispatch gets the same link, nothing is written,
    /// and revoking the invitation is what kills the link (re-inviting creates a new id).
    /// </summary>
    public string LinkToken(Guid invitationId)
        => Base64UrlEncoder.Encode(HMACSHA256.HashData(_linkKey, Encoding.UTF8.GetBytes(invitationId.ToString("D"))));

    public bool IsLinkToken(Guid invitationId, string? token)
        => token is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(LinkToken(invitationId)), Encoding.UTF8.GetBytes(token));

    /// <summary>Keyed by the challenge id, so the same six digits never hash alike twice.</summary>
    public string CodeHash(Guid challengeId, string code)
        => Convert.ToHexString(HMACSHA256.HashData(_codeKey, Encoding.UTF8.GetBytes($"{challengeId:D}:{code}")));

    private static byte[] Derive(string key, string label)
        => HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(label));
}
