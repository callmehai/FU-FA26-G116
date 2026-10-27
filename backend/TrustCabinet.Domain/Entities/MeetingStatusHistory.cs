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

    public required MeetingHistoryAction Action { get; set; }

    /// <summary>The approver selected for a submission; null for other actions and legacy rows.</summary>
    public Guid? AssignedApproverId { get; set; }
    public User? AssignedApprover { get; set; }

    /// <summary>The organizer before an administrative transfer; null for other lifecycle actions.</summary>
    public Guid? PreviousOrganizerId { get; set; }
    public User? PreviousOrganizer { get; set; }

    /// <summary>The organizer receiving an approved meeting during administrative recovery.</summary>
    public Guid? NewOrganizerId { get; set; }
    public User? NewOrganizer { get; set; }

    /// <summary>Null only when the system acted on its own, as when it cancels an overdue meeting (BR-301).</summary>
    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }

    /// <summary>
    /// Required when a meeting is rejected, so the secretary sees what to fix (SCRUM-19), and when it is cancelled
    /// (BR-298).
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }
}
