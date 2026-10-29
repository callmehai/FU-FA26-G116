using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// Records THAT a person voted, never what they chose.
///
/// Splitting the receipt from the <see cref="VoteChoice"/> is what makes a secret ballot actually
/// secret: one person one vote is enforced here, by the unique index on (session, voter), while
/// the choice table carries no voter at all in an anonymous session. Hiding the name in the UI
/// would not have been enough — the link would still be sitting in the database for anyone with
/// read access.
/// </summary>
public class VoteReceipt : Entity
{
    public Guid VotingSessionId { get; set; }
    public VotingSession VotingSession { get; set; } = null!;

    public Guid VoterId { get; set; }
    public User Voter { get; set; } = null!;

    public DateTimeOffset CastAt { get; set; } = DateTimeOffset.UtcNow;
}
