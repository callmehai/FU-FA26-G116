using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Domain.Notifications;

public interface INotificationService
{
    Task QueueMeetingStartedAsync(Guid meetingId, string meetingTitle, Guid historyId,
        DateTimeOffset occurredAt, IReadOnlyList<Guid> recipientUserIds, CancellationToken ct = default);

    Task<NotificationPage> ListAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);

    Task<Notification?> MarkReadAsync(
        Guid userId, Guid notificationId, CancellationToken ct = default);

    Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default);

    Task<NotificationTarget?> GetTargetAsync(
        Guid userId, Guid notificationId, CancellationToken ct = default);

    Task<int> QueueStartingSoonAsync(CancellationToken ct = default);

    Task<List<Notification>> PendingPushAsync(int limit, CancellationToken ct = default);

    Task MarkPushedAsync(Guid notificationId, CancellationToken ct = default);
}
