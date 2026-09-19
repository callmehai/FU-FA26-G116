using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One recorded move through the meeting state machine: who changed it, when, and why.
///
/// This is evidence, not a technical log. Rows are append-only — the application never updates or
/// deletes one, and the detail screen's timeline is rendered straight from them.
/// </summary>
public class MeetingStatusHistory : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    /// <summary>Null on the row that records the meeting's creation, which has no previous state.</summary>
    public MeetingStatus? FromStatus { get; set; }

    public MeetingStatus ToStatus { get; set; }

    public Guid ActorId { get; set; }
    public User Actor { get; set; } = null!;

    /// <summary>Required when a meeting is rejected, so the secretary sees what to fix (SCRUM-19).</summary>
    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }
}
