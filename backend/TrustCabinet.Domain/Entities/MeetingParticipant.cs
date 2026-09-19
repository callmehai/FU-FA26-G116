using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One invited person on one meeting, plus how they answered the invitation.
/// </summary>
public class MeetingParticipant : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Their role in this meeting, which need not match their system role.</summary>
    public MeetingRole Role { get; set; } = MeetingRole.Member;

    public InvitationResponse Response { get; set; } = InvitationResponse.Pending;

    /// <summary>Set only when <see cref="Response"/> is Delegated: who attends in their place.</summary>
    public Guid? DelegatedToUserId { get; set; }
    public User? DelegatedToUser { get; set; }

    public DateTimeOffset? RespondedAt { get; set; }
}
