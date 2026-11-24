namespace TrustCabinet.Domain.Enums;

/// <summary>
/// How much of an owner's calendar a share shows (SCRUM-142, BR-140, BR-141, ADR-0058). Level 1 covers Normal meetings, Level 2
/// Normal and Internal ones. There is no level for Secret meetings: they are never shared, not even as busy.
/// </summary>
public enum CalendarShareLevel
{
    Level1,
    Level2,
}

/// <summary>
/// Where a calendar share is. Pending and Effective are the open states (BR-142, BR-143: at most one per owner → viewer pair);
/// every other state is final and never comes back (BR-142, BR-143).
/// </summary>
public enum CalendarShareStatus
{
    /// <summary>Sent, waiting for the asked side (UC-80c).</summary>
    Pending,
    Effective,
    Declined,
    /// <summary>The sender withdrew it before it was answered (UC-80e).</summary>
    Withdrawn,
    /// <summary>Either side ended an Effective share (UC-80e AF-1), or a new share replaced one past its end date.</summary>
    Ended,
    /// <summary>The viewer lost the Meeting Organizer right or an account was deactivated (BR-142, BR-143).</summary>
    Stopped,
}

/// <summary>Who sent a share: the viewer asking (UC-80a) or the owner offering (UC-80b). The other side answers it.</summary>
public enum CalendarShareInitiator
{
    Viewer,
    Owner,
}

/// <summary>Why a share was stopped (BR-142, BR-143), kept in the audit row.</summary>
public enum CalendarShareStopReason
{
    ViewerLostMeetingCreator,
    AccountDeactivated,
}
