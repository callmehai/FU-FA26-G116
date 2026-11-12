namespace TrustCabinet.Domain.Enums;

/// <summary>What an in-app notification is about (SCRUM-14).</summary>
public enum NotificationType
{
    MeetingWaitingApproval,
    MeetingApproved,
    MeetingRejected,
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
    MeetingParticipantRemoved,
    AgendaPresenterAssigned,
    MeetingStarted,
    /// <summary>The Chair sent the minutes back to the Secretary with a reason (SCRUM-33, UC-65).</summary>
    MinutesReturned,

    // SCRUM-142 — calendar sharing (BR-426). Target "CalendarShare", never a meeting.
    CalendarShareRequested,
    CalendarShareReceived,
    CalendarShareAccepted,
    CalendarShareDeclined,
    CalendarShareChangeRequested,
    CalendarShareUpdated,
    CalendarShareChangeDeclined,
    CalendarShareWithdrawn,
    CalendarShareEnded,
    CalendarShareStopped,

    /// <summary>ADR-0060: a meeting's Secretary recorded a Voice ID sample for the person. Target "VoiceSample".</summary>
    VoiceSampleCollected,
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

/// <summary>
/// What the email provider later reported about a <see cref="EmailStatus.Sent"/> message (ADR-0031). "Sent" only
/// means the provider accepted it; whether the recipient's server took it arrives afterwards through a webhook.
/// </summary>
public enum EmailDeliveryState
{
    Delivered,
    /// <summary>The recipient's server deferred it; the provider is still trying.</summary>
    Delayed,
    Bounced,
    /// <summary>The recipient marked it as spam.</summary>
    Complained,
    /// <summary>Not attempted: the address is on the provider's suppression list after an earlier bounce or complaint.</summary>
    Suppressed,
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
