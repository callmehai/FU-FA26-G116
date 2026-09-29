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
}
