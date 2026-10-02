using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>A planned session belonging to one meeting; BR-56 checks its schedule before Publish.</summary>
public class MeetingSession : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public required string Title { get; set; }

    public DateTimeOffset PlannedStartsAt { get; set; }
    public DateTimeOffset PlannedEndsAt { get; set; }

    public MeetingSessionStatus Status { get; set; } = MeetingSessionStatus.Planned;
}
