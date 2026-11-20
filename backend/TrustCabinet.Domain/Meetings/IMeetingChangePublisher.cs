namespace TrustCabinet.Domain.Meetings;

/// <summary>
/// Only says which meeting: a reader re-reads what it shows, and that read is reauthorized. EventId lets a client drop
/// a redelivered event, like the other meeting events.
/// </summary>
public sealed record MeetingRereadChange(Guid EventId, Guid MeetingId, DateTimeOffset OccurredAt);

/// <summary>
/// Tells the open screens of a meeting that another user changed it, on <c>/hubs/meetings</c>. Called after the change
/// committed; a failed or slow push is logged and never fails the change, so an implementation does not throw.
/// </summary>
public interface IMeetingChangePublisher
{
    /// <summary>
    /// <c>meetingChanged</c>: the meeting's own fields, status or history moved (a workflow transition, an edit, a
    /// reschedule, the system cancelling or ending it). Reaches every current reader, and anyone tied to the meeting
    /// who just lost access to it, so their page re-reads and shows that it is gone.
    /// </summary>
    Task MeetingChangedAsync(Guid meetingId, CancellationToken ct = default);

    /// <summary><c>minutesChanged</c>: the AI draft or the minutes changed (SCRUM-36, SCRUM-32, SCRUM-33).</summary>
    Task MinutesChangedAsync(Guid meetingId, CancellationToken ct = default);

    /// <summary><c>commentsChanged</c>: a comment was posted on one of the meeting's documents (SCRUM-23).</summary>
    Task CommentsChangedAsync(Guid meetingId, CancellationToken ct = default);
}
