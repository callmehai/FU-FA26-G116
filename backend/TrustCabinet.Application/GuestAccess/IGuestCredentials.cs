using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Application.GuestAccess;

/// <summary>
/// The secrets behind guest links, codes and tokens. The HMAC keys are derived from the token signing key, so a
/// deployment needs no new secret; an implementation that mints JWTs stays in Infrastructure with the signing key.
/// </summary>
public interface IGuestCredentials
{
    /// <summary>
    /// Derived rather than stored: a retried invitation dispatch gets the same link, nothing is written, and
    /// revoking the invitation is what kills the link (re-inviting creates a new id).
    /// </summary>
    bool IsLinkToken(Guid invitationId, string? token);

    /// <summary>Keyed by the challenge id, so the same six digits never hash alike twice.</summary>
    string CodeHash(Guid challengeId, string code);

    /// <summary>
    /// A JWT the existing bearer handler accepts, so Agenda, Participants and the hub need no new scheme. The
    /// subject is not a Guid, which is what keeps every internal endpoint closed to it; it carries no
    /// capability, email or name, and expires with the session.
    /// </summary>
    string AccessToken(MeetingGuestSession session, DateTimeOffset expiresAt);
}
