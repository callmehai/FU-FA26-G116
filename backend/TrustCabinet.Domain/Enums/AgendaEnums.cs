namespace TrustCabinet.Domain.Enums;

/// <summary>Where an agenda item is in the sitting.</summary>
public enum AgendaItemStatus
{
    Pending,
    InProgress,
    Done,
    /// <summary>Reached but deliberately deferred to a later meeting.</summary>
    Postponed,
}

/// <summary>
/// Whether someone actually attended, as opposed to what they answered on the invitation.
/// Recorded during the sitting and used for the quorum count (SCRUM-26).
/// </summary>
public enum AttendanceStatus
{
    /// <summary>Not marked yet.</summary>
    Unknown,
    Present,
    Absent,
    /// <summary>Absent with a reason accepted by the chair.</summary>
    Excused,
    /// <summary>Did not attend; someone else attended in their place.</summary>
    Delegated,
}

/// <summary>A request to speak, as it moves through the chair's queue (SCRUM-27).</summary>
public enum SpeakingRequestStatus
{
    Queued,
    /// <summary>The chair has called on them but they have not started.</summary>
    Invited,
    Speaking,
    Finished,
    Cancelled,
}
