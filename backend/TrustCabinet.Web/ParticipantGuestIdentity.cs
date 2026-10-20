using System.Security.Claims;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Web;

public interface IParticipantGuestIdentityResolver
{
    ParticipantGuestSession? Resolve(ClaimsPrincipal principal);
}
public sealed class UnavailableParticipantGuestIdentityResolver : IParticipantGuestIdentityResolver
{
    public ParticipantGuestSession? Resolve(ClaimsPrincipal principal) => null;
}
