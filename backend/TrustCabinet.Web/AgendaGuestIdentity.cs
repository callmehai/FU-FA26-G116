using System.Security.Claims;
using TrustCabinet.Domain.Agenda;

namespace TrustCabinet.Web;

/// <summary>
/// UC-04 maps an already authenticated principal to its meeting/invitation/session identity.
/// Guest tokens must use a non-Guid sub, carry no internal capabilities and never impersonate a User.
/// This resolves identity only; IAgendaGuestAccess must still check live authorization.
/// Return null only for non-guest principals; malformed guest identities must remain guest identities
/// with empty IDs so the caller denies them instead of falling back to internal User access.
/// </summary>
public interface IAgendaGuestIdentityResolver
{
    AgendaGuestSession? Resolve(ClaimsPrincipal principal);
}

/// <summary>No guest authentication is installed by SCRUM-25.</summary>
public sealed class UnavailableAgendaGuestIdentityResolver : IAgendaGuestIdentityResolver
{
    public AgendaGuestSession? Resolve(ClaimsPrincipal principal) => null;
}
