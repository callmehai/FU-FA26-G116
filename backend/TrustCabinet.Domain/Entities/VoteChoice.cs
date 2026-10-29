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
    /// Who cast it — and **always null when the session is anonymous**. That is the whole point of
    /// this column being nullable, and it is the one invariant in this table worth writing a test
    /// around. In an open session it is set, which is what lets someone change their vote before
    /// the session closes.
    ///
    /// A consequence worth knowing: in an anonymous session a vote cannot be changed, because
    /// nothing identifies which row to replace. If the team needs that, it takes a per-voter token
    /// and should be decided deliberately rather than bolted on.
    /// </summary>
    public Guid? VoterId { get; set; }
    public User? Voter { get; set; }

    public DateTimeOffset CastAt { get; set; } = DateTimeOffset.UtcNow;
}
