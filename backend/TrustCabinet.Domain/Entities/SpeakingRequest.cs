using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// Someone raising their hand during the sitting, and how the chair handled it (SCRUM-27, UC-79).
/// Kept after the meeting because the minutes report who spoke, on what, and for how long (BR-339).
/// The person is a member (<see cref="UserId"/>) or a guest invitation (<see cref="GuestInvitationId"/>), never both.
/// </summary>
public class SpeakingRequest : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    /// <summary>The item under discussion when they asked, taken by the server.</summary>
    public Guid? AgendaItemId { get; set; }
    public AgendaItem? AgendaItem { get; set; }

    /// <summary>Null when a guest raised the hand.</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Who raised the hand, when a guest did: the invitation, as for a guest's sentence or ballot.</summary>
    public Guid? GuestInvitationId { get; set; }
    public MeetingGuestInvitation? GuestInvitation { get; set; }

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Place in the queue. Defaults to request order, but the chair may reorder, which is why this
    /// is stored rather than derived from <see cref="RequestedAt"/>.
    /// </summary>
    public int QueuePosition { get; set; }

    public SpeakingRequestStatus Status { get; set; } = SpeakingRequestStatus.Queued;

    /// <summary>When the Chair invited the person (UC-79 step 5).</summary>
    public DateTimeOffset? InvitedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>When the turn finished or the request was cancelled.</summary>
    public DateTimeOffset? EndedAt { get; set; }
}
