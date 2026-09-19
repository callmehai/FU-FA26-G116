using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// Someone raising their hand during the sitting, and how the chair handled it (SCRUM-27).
/// Kept after the meeting because the minutes report who spoke, on what, and for how long.
/// </summary>
public class SpeakingRequest : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    /// <summary>The item under discussion when they asked.</summary>
    public Guid? AgendaItemId { get; set; }
    public AgendaItem? AgendaItem { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Place in the queue. Defaults to request order, but the chair may reorder, which is why this
    /// is stored rather than derived from <see cref="RequestedAt"/>.
    /// </summary>
    public int QueuePosition { get; set; }

    public SpeakingRequestStatus Status { get; set; } = SpeakingRequestStatus.Queued;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }
}
