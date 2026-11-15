using TrustCabinet.Application.Notifications;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// Notifications in a list. <see cref="Added"/> holds what was staged; nothing here saves. It does not order rows
/// the way the real repository does, so a test here asserts membership, not order.
/// </summary>
public sealed class FakeNotificationRepository : INotificationRepository
{
    public List<Notification> Rows { get; } = [];
    public List<Notification> Added { get; } = [];
    public List<UpcomingMeetingRecipient> Upcoming { get; } = [];
    public HashSet<(Guid ShareId, Guid UserId)> CalendarShareParties { get; } = [];
    public HashSet<(Guid SampleId, Guid UserId)> OwnVoiceSamples { get; } = [];

    /// <summary>Thrown by every call but <see cref="Add"/>, in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    public Task<List<Notification>> FindByRecipientAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
        => Read(() => Rows.Where(n => n.UserId == userId).Skip((page - 1) * pageSize).Take(pageSize).ToList());

    public Task<Notification?> FindByIdAndRecipientAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
        => Read(() => Rows.SingleOrDefault(n => n.Id == notificationId && n.UserId == userId));

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default)
        => Read(() => Rows.Count(n => n.UserId == userId && n.ReadAt == null));

    public Task<int> CountByRecipientAsync(Guid userId, CancellationToken ct = default)
        => Read(() => Rows.Count(n => n.UserId == userId));

    public Task MarkAsReadAsync(Notification notification, DateTimeOffset now, CancellationToken ct = default)
        => Read(() =>
        {
            notification.MarkAsRead(now);
            return true;
        });

    public Task<int> MarkAllUnreadAsReadAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default)
        => Read(() =>
        {
            var unread = Rows.Where(n => n.UserId == userId && n.ReadAt == null).ToList();
            unread.ForEach(n => n.MarkAsRead(now));
            return unread.Count;
        });

    public void Add(Notification notification) => Added.Add(notification);

    public Task<List<UpcomingMeetingRecipient>> FindUpcomingMeetingRecipientsAsync(
        DateTimeOffset now, DateTimeOffset cutoff, CancellationToken ct = default)
        => Read(() => Upcoming.Where(u => u.PlannedStartTime > now && u.PlannedStartTime <= cutoff).ToList());

    public Task<List<string?>> FindReminderKeysAsync(Guid[] meetingIds, CancellationToken ct = default)
        => Read(() => Rows.Where(n => n.Type == NotificationType.MeetingStartingSoon
                && n.MeetingId is { } id && meetingIds.Contains(id))
            .Select(n => n.DeduplicationKey).ToList());

    public Task<List<Notification>> FindPendingPushAsync(int limit, CancellationToken ct = default)
        => Read(() => Rows.Where(n => n.PushedAt == null).Take(limit).ToList());

    public Task MarkPushedAsync(Guid notificationId, DateTimeOffset now, CancellationToken ct = default)
        => Read(() =>
        {
            if (Rows.SingleOrDefault(n => n.Id == notificationId && n.PushedAt == null) is { } row) row.PushedAt = now;
            return true;
        });

    public Task<bool> IsCalendarSharePartyAsync(Guid shareId, Guid userId, CancellationToken ct = default)
        => Read(() => CalendarShareParties.Contains((shareId, userId)));

    public Task<bool> IsOwnVoiceSampleAsync(Guid sampleId, Guid userId, CancellationToken ct = default)
        => Read(() => OwnVoiceSamples.Contains((sampleId, userId)));

    private Task<T> Read<T>(Func<T> answer) => Fail is { } failure ? Task.FromException<T>(failure) : Task.FromResult(answer());
}
