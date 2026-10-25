using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// Records THAT a person voted, never what they chose.
///
/// Splitting the receipt from the <see cref="VoteChoice"/> is what makes a secret ballot actually
/// secret: one person one vote is enforced here, by the unique indexes on (session, voter), while
/// the choice table carries no voter at all in a secret vote. Hiding the name in the UI
/// would not have been enough — the link would still be sitting in the database for anyone with
/// read access.
/// </summary>
public class VoteReceipt : Entity
{
    public Guid VotingSessionId { get; set; }
    public VotingSession VotingSession { get; set; } = null!;

    /// <summary>Exactly one of <see cref="VoterId"/> and <see cref="GuestInvitationId"/> is set.</summary>
    public Guid? VoterId { get; set; }
    public User? Voter { get; set; }

    /// <summary>Set when a Guest voted (ADR-0016).</summary>
    public Guid? GuestInvitationId { get; set; }
    public MeetingGuestInvitation? GuestInvitation { get; set; }

    public DateTimeOffset CastAt { get; set; } = DateTimeOffset.UtcNow;
}
