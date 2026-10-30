using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One person's confirmation that they read a meeting document to its last page (UC-74, BR-290). It belongs to the
/// person and the document, not to a vote, so it counts for every vote that links the same document and is never
/// withdrawn (BR-291). It is stored apart from ballots and points at nothing a ballot points at, so it cannot reveal
/// what anyone chose in a secret vote (BR-293).
/// </summary>
public class DocumentReadConfirmation : Entity
{
    public Guid MeetingDocumentId { get; set; }
    public MeetingDocument MeetingDocument { get; set; } = null!;

    /// <summary>Exactly one of <see cref="UserId"/> and <see cref="GuestInvitationId"/> is set.</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Set when a Guest confirmed (ADR-0016).</summary>
    public Guid? GuestInvitationId { get; set; }
    public MeetingGuestInvitation? GuestInvitation { get; set; }

    public DateTimeOffset ConfirmedAt { get; set; }
}
