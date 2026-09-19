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

    public ICollection<VoteChoice> Choices { get; set; } = [];
}
