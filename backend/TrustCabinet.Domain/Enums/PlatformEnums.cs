namespace TrustCabinet.Domain.Enums;

/// <summary>What an in-app notification is about (SCRUM-14).</summary>
public enum NotificationType
{
    MeetingWaitingApproval,
    MeetingInvited,
    MeetingPublished,
    MeetingStartingSoon,
    MeetingRescheduled,
    MeetingCancelled,
    VotingOpened,
    MinutesWaitingApproval,
    MinutesIssued,
    DocumentAdded,
    MeetingRoleChanged,
    MeetingPermissionsChanged,
    MeetingParticipantRemoved,
}

/// <summary>Delivery state of a queued email (SCRUM-15).</summary>
public enum EmailStatus
{
    Queued,
    /// <summary>Claimed by a worker until its lease expires.</summary>
    Processing,
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
