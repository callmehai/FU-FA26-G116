using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// Verified guest access to one invited meeting (UC-04, BR-154/155). Created only by consuming a
/// challenge, so a row is itself the evidence that the invited email was verified. Its Id is the
/// SessionId carried by the guest token; revoking the invitation ends every session of it.
/// </summary>
public class GuestSession : Entity
{
    public Guid InvitationId { get; set; }
    public MeetingGuestInvitation Invitation { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
