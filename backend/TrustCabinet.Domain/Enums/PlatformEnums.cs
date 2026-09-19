namespace TrustCabinet.Domain.Enums;

/// <summary>What an in-app notification is about (SCRUM-14).</summary>
public enum NotificationType
{
    MeetingInvited,
    MeetingPublished,
    MeetingStartingSoon,
    MeetingCancelled,
    VotingOpened,
    MinutesWaitingApproval,
    MinutesIssued,
    DocumentAdded,
}

/// <summary>Delivery state of a queued email (SCRUM-15).</summary>
public enum EmailStatus
{
    Queued,
    Sent,
    /// <summary>Every retry used up. Kept so a human can see what was never delivered.</summary>
    Failed,
}

/// <summary>Whether a call is person-to-person or the room attached to a meeting.</summary>
public enum CallKind
{
    Direct,
    MeetingRoom,
}

public enum CallStatus
{
    Ringing,
    Active,
    Ended,
    /// <summary>Nobody picked up, or the callee declined.</summary>
    Missed,
}
