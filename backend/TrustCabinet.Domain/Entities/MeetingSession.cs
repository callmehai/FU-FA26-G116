using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>A session of one meeting; BR-56 checks its schedule before Publish, UC-29/UC-30 run it.</summary>
public class MeetingSession : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public required string Title { get; set; }

    public DateTimeOffset PlannedStartsAt { get; set; }
    public DateTimeOffset PlannedEndsAt { get; set; }

    public MeetingSessionStatus Status { get; set; } = MeetingSessionStatus.Planned;

    /// <summary>Set by UC-29 Start Meeting Session.</summary>
    public DateTimeOffset? ActualStartedAt { get; set; }

    /// <summary>Set by UC-30 End Meeting Session.</summary>
    public DateTimeOffset? ActualEndedAt { get; set; }
}
