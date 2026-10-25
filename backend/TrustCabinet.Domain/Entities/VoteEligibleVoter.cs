using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One person entitled to vote, fixed when the vote opens from its eligible roles and their Meeting
/// access at that moment (BR-190). Attendance plays no part. Kept as rows rather than recomputed,
/// because roles and participants can change while the vote is open and eligibility must not.
/// </summary>
public class VoteEligibleVoter : Entity
{
    public Guid VotingSessionId { get; set; }
    public VotingSession VotingSession { get; set; } = null!;

    /// <summary>Exactly one of <see cref="UserId"/> and <see cref="GuestInvitationId"/> is set.</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Set for a Guest, who never has a User row (ADR-0016).</summary>
    public Guid? GuestInvitationId { get; set; }
    public MeetingGuestInvitation? GuestInvitation { get; set; }
}
