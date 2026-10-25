namespace TrustCabinet.Domain.Enums;

/// <summary>
/// The meeting state machine, in the order a meeting travels through it. It stops at Ended: the
/// minutes have their own status (ADR-0013). Documented in
/// docs/02-design/01-vong-doi-cuoc-hop.md, which is the authority — keep the two in step.
///
/// Stored as a string, not an int, so the database stays readable and inserting a new state
/// later never renumbers the existing rows.
/// </summary>
public enum MeetingStatus
{
    Draft,
    WaitingApproval,
    Rejected,
    Approved,
    Published,
    InProgress,
    Ended,
}

/// <summary>Server-owned lifecycle action written to immutable meeting history.</summary>
public enum MeetingHistoryAction
{
    Create,
    SubmitForApproval,
    Approve,
    Reject,
    ReturnToDraft,
    Publish,
    TransferOrganizer,
    /// <summary>The organizer moved a Published meeting's schedule, place or link (UC-15 AF-1).</summary>
    Reschedule,
    LegacyTransition,
    Start,
    End,
}

/// <summary>Actions accepted by the meeting transition API. LegacyTransition is never a command.</summary>
public enum MeetingTransitionAction
{
    SubmitForApproval,
    Approve,
    Reject,
    ReturnToDraft,
    Publish,
    TransferOrganizer,
    Start,
    End,
}

/// <summary>
/// How sensitive a meeting's contents are. Independent of <see cref="MeetingStatus"/>: it decides
/// who may read documents and recordings, not where the meeting is in its lifecycle.
/// </summary>
public enum Sensitivity
{
    Normal,
    Internal,
    Confidential,
    Secret,
}

/// <summary>Whether attendees gather in a room, join remotely, or both.</summary>
public enum MeetingMode
{
    InPerson,
    Online,
    Hybrid,
}

/// <summary>
/// A participant's role in one specific meeting. Deliberately separate from <see cref="Entities.Role"/>:
/// a user who is a plain Member in the system can still chair a single meeting.
/// </summary>
public enum MeetingRole
{
    Chair,
    Secretary,
    Member,
    Guest,
}

/// <summary>Whether an invited participant has answered, and how.</summary>
public enum InvitationResponse
{
    Pending,
    Accepted,
    Declined,
    /// <summary>Answered by nominating someone else to attend instead.</summary>
    Delegated,
}
