using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One answer a voter can pick. Usually the three standard ones — tán thành, không tán thành,
/// không ý kiến — but a session may define its own.
/// </summary>
public class VotingOption : Entity
{
    public Guid VotingSessionId { get; set; }
    public VotingSession VotingSession { get; set; } = null!;

    /// <summary>Display order, starting at 1.</summary>
    public int Position { get; set; }

    public required string Label { get; set; }

    /// <summary>
    /// How many ballots chose this option — the tally of every vote, secret or public. A secret ballot leaves no
    /// other trace of its choice (BR-259), so this counter is the only place it is counted. Each ballot rewrites the
    /// counters of all the vote's options and never touches <c>UpdatedAt</c>, so the rows do not show which one moved.
    /// </summary>
    public int BallotCount { get; set; }

    public ICollection<VoteChoice> Choices { get; set; } = [];
}
