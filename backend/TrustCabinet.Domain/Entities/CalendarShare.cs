using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One owner letting one viewer see their busy hours (SCRUM-142, UC-84a → UC-84e, ADR-0058). The direction counts: A
/// viewing B's calendar is a different row from B viewing A's (BR-423). Only busy/free ever leaves this table's
/// owner — never a meeting's title, place or attendees (BR-427).
/// </summary>
public class CalendarShare : Entity
{
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    /// <summary>A Meeting Organizer (holds MeetingCreator) while the share is open (BR-424, BR-430).</summary>
    public Guid ViewerId { get; set; }
    public User Viewer { get; set; } = null!;

    /// <summary>Who sent it. The other side answers it (UC-84c) and only the sender withdraws it (UC-84e).</summary>
    public CalendarShareInitiator InitiatedBy { get; set; }

    /// <summary>The level in force, or asked for while <see cref="Status"/> is Pending.</summary>
    public CalendarShareLevel Level { get; set; }

    /// <summary>First day shown, counted in Vietnam's calendar.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Last day shown, inclusive. After it the share has ended (BR-425): left out of every read and refusing every action,
    /// with nothing written to the row (PO 09/11/2026, Q-B).
    /// </summary>
    public DateOnly EndDate { get; set; }

    public CalendarShareStatus Status { get; set; }

    /// <summary>The level the viewer asked to raise to (UC-84d); null when no change is pending.</summary>
    public CalendarShareLevel? PendingLevel { get; set; }

    /// <summary>The end date the viewer asked to extend to (UC-84d); null when no change is pending.</summary>
    public DateOnly? PendingEndDate { get; set; }

    /// <summary>When the viewer asked for the change. Not null exactly when a change is pending.</summary>
    public DateTimeOffset? PendingRequestedAt { get; set; }

    /// <summary>When the asked side accepted or declined.</summary>
    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>When it reached a final state.</summary>
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>Who closed it; null when the system did (a share past its end date, closed when a new one took its place).</summary>
    public Guid? ClosedById { get; set; }
    public User? ClosedBy { get; set; }

    public bool HasPendingChange => PendingRequestedAt is not null;

    public void ClearPendingChange()
    {
        PendingLevel = null;
        PendingEndDate = null;
        PendingRequestedAt = null;
    }
}
