using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// Who chose what in a **public** vote, so the closed result can name each voter's choice (BR-273).
///
/// A secret vote writes no row here at all (BR-259). A row per secret ballot, even one naming nobody, would still
/// line up with the voter's <see cref="VoteReceipt"/>: a plain SELECT returns both tables in the order they were
/// written. A secret ballot only moves <see cref="VotingOption.BallotCount"/>. The tally of every vote is read from
/// that counter; these rows only say who. A recorded ballot is never changed or withdrawn (BR-265).
/// </summary>
public class VoteChoice : Entity
{
    public Guid VotingSessionId { get; set; }
    public VotingSession VotingSession { get; set; } = null!;

    public Guid VotingOptionId { get; set; }
    public VotingOption VotingOption { get; set; } = null!;

    /// <summary>The internal user who chose it. Exactly one of this and <see cref="GuestInvitationId"/> is set.</summary>
    public Guid? VoterId { get; set; }
    public User? Voter { get; set; }

    /// <summary>The Guest who chose it (ADR-0016).</summary>
    public Guid? GuestInvitationId { get; set; }
    public MeetingGuestInvitation? GuestInvitation { get; set; }
}
