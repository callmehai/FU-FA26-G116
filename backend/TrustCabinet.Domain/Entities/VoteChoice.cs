using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A vote actually cast, counted to produce the result.
/// </summary>
public class VoteChoice : Entity
{
    public Guid VotingSessionId { get; set; }
    public VotingSession VotingSession { get; set; } = null!;

    public Guid VotingOptionId { get; set; }
    public VotingOption VotingOption { get; set; } = null!;

    /// <summary>
    /// Who cast it — and **always null when the vote is secret** (BR-259). That is the whole point of
    /// this column being nullable, and it is the one invariant in this table worth writing a test
    /// around. In a public vote it is set, so the result can show who chose what. Either way a
    /// recorded ballot is never changed or withdrawn (BR-265).
    /// </summary>
    public Guid? VoterId { get; set; }
    public User? Voter { get; set; }

    /// <summary>
    /// The Guest who cast it in a public vote (ADR-0016); like <see cref="VoterId"/>, always null in
    /// a secret one. At most one of the two is set.
    /// </summary>
    public Guid? GuestInvitationId { get; set; }
    public MeetingGuestInvitation? GuestInvitation { get; set; }

    public DateTimeOffset CastAt { get; set; } = DateTimeOffset.UtcNow;
}
