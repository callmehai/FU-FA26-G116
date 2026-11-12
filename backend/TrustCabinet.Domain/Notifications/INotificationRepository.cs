using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Domain.Notifications;

public interface INotificationRepository
{
    Task<List<Notification>> FindByRecipientAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default);

    Task<Notification?> FindByIdAndRecipientAsync(
        Guid notificationId, Guid userId, CancellationToken ct = default);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);

    Task<int> CountByRecipientAsync(Guid userId, CancellationToken ct = default);

    Task MarkAsReadAsync(
        Notification notification, DateTimeOffset now, CancellationToken ct = default);

    Task<int> MarkAllUnreadAsReadAsync(
        Guid userId, DateTimeOffset now, CancellationToken ct = default);

    void Add(Notification notification);

    Task<List<UpcomingMeetingRecipient>> FindUpcomingMeetingRecipientsAsync(
        DateTimeOffset now, DateTimeOffset cutoff, CancellationToken ct = default);

    Task<List<string?>> FindReminderKeysAsync(
        Guid[] meetingIds, CancellationToken ct = default);

    Task<List<Notification>> FindPendingPushAsync(int limit, CancellationToken ct = default);

    Task MarkPushedAsync(
        Guid notificationId, DateTimeOffset now, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>SCRUM-142: whether the user is the owner or the viewer of the calendar share a notification points to.</summary>
    Task<bool> IsCalendarSharePartyAsync(Guid shareId, Guid userId, CancellationToken ct = default);

    /// <summary>ADR-0060: the sample still exists and is this person's own.</summary>
    Task<bool> IsOwnVoiceSampleAsync(Guid sampleId, Guid userId, CancellationToken ct = default);
}
