using TrustCabinet.Domain.Meetings;
using System.Security.Claims;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Infrastructure.GuestAccess;

namespace TrustCabinet.Web;

/// <summary>
/// UC-04 maps an already authenticated principal to its meeting/invitation/session identity.
/// Guest tokens must use a non-Guid sub, carry no internal capabilities and never impersonate a User.
/// This resolves identity only; IAgendaGuestAccess must still check live authorization.
/// Return null only for non-guest principals; malformed guest identities must remain guest identities
/// with empty IDs so the caller denies them instead of falling back to internal User access.
/// </summary>
public interface IMeetingGuestIdentityResolver
{
    MeetingGuestSession? Resolve(ClaimsPrincipal principal);
}

/// <summary>No guest authentication is installed by SCRUM-25.</summary>
public sealed class UnavailableMeetingGuestIdentityResolver : IMeetingGuestIdentityResolver
{
    public MeetingGuestSession? Resolve(ClaimsPrincipal principal) => null;
}

/// <summary>
/// UC-04: reads the guest claims GuestCredentials writes, from the principal the bearer handler already
/// validated. No database, header, body or query is consulted; the adapters recheck the session itself.
/// </summary>
public sealed class GuestTokenIdentityResolver : IMeetingGuestIdentityResolver
{
    private static readonly MeetingGuestSession Malformed = new(Guid.Empty, Guid.Empty, Guid.Empty);

    public MeetingGuestSession? Resolve(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return null;
        var subject = principal.FindFirstValue("sub");
        var guestSubject = subject?.StartsWith(GuestTokenClaims.SubjectPrefix, StringComparison.Ordinal) == true;
        if (!guestSubject && !principal.Claims.Any(c => c.Type is GuestTokenClaims.Meeting
                or GuestTokenClaims.Invitation or GuestTokenClaims.Session))
            return null;
        var identity = new MeetingGuestSession(Id(principal, GuestTokenClaims.Meeting),
            Id(principal, GuestTokenClaims.Invitation), Id(principal, GuestTokenClaims.Session));
        // A user subject carrying guest claims, or a guest subject naming another session, is denied as a guest.
        return guestSubject && identity.IsValid && subject == GuestTokenClaims.SubjectPrefix + identity.SessionId.ToString("D")
            ? identity : Malformed;
    }

    private static Guid Id(ClaimsPrincipal principal, string type)
        => principal.FindAll(type).ToArray() is [var only] && Guid.TryParse(only.Value, out var id) ? id : Guid.Empty;
}
