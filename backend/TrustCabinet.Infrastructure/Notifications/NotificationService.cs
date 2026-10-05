using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Notifications;

namespace TrustCabinet.Infrastructure.Notifications;

/// <summary>Owns notification visibility and read state; caller ids always come from validated auth.</summary>
public sealed class NotificationService(
    INotificationRepository repository,
    IMeetingRepository meetings,
    TimeProvider clock) : INotificationService
{
    public async Task<NotificationPage> ListAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await repository.FindByRecipientAsync(userId, page, pageSize, ct);
        return new NotificationPage(items,
            await repository.CountUnreadAsync(userId, ct),
            await repository.CountByRecipientAsync(userId, ct),
            page,
            pageSize);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
        => repository.CountUnreadAsync(userId, ct);

    public async Task<Notification?> MarkReadAsync(
        Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        var notification = await repository.FindByIdAndRecipientAsync(notificationId, userId, ct);
        if (notification is null)
        {
            return null;
        }

        await repository.MarkAsReadAsync(notification, clock.GetUtcNow(), ct);

        return notification;
    }

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        return await repository.MarkAllUnreadAsReadAsync(userId, clock.GetUtcNow(), ct);
    }

    public async Task<NotificationTarget?> GetTargetAsync(
        Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        var notification = await repository.FindByIdAndRecipientAsync(notificationId, userId, ct);
        if (notification is null || !await CanOpenTargetAsync(userId, notification, ct))
        {
            return null;
        }

        return new NotificationTarget(notification.TargetType!, notification.TargetId!.Value);
    }

    private async Task<bool> CanOpenTargetAsync(Guid userId, Notification notification,
        CancellationToken ct = default)
    {
        if (notification.TargetType != "Meeting"
            || notification.TargetId is not { } meetingId
            || notification.MeetingId != meetingId)
        {
            return false;
        }

        return await meetings.FindAccessibleAsync(meetingId, userId, ct) is not null;
    }

    /// <summary>Queues one reminder per planned session and active invited user before start.</summary>
    public async Task<int> QueueStartingSoonAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var cutoff = now.AddMinutes(15);
        var upcoming = await repository.FindUpcomingMeetingRecipientsAsync(now, cutoff, ct);

        if (upcoming.Count == 0)
        {
            return 0;
        }

        var meetingIds = upcoming.Select(item => item.MeetingId).Distinct().ToArray();
        var existing = (await repository.FindReminderKeysAsync(meetingIds, ct))
            .ToHashSet(StringComparer.Ordinal);

        var created = 0;
        foreach (var item in upcoming)
        {
            // The planned start is part of the key, so a session rescheduled by UC-27 is reminded again.
            var key = $"meeting-session-starting-soon:{item.SessionId:D}:{item.PlannedStartsAt.UtcTicks}:{item.UserId:D}";
            if (!existing.Add(key))
            {
                continue;
            }

            repository.Add(new Notification
            {
                UserId = item.UserId,
                MeetingId = item.MeetingId,
                TargetType = "Meeting",
                TargetId = item.MeetingId,
                Type = NotificationType.MeetingStartingSoon,
                Title = "Cuộc họp sắp bắt đầu",
                Body = $"Phiên \"{item.SessionTitle}\" của cuộc họp \"{item.MeetingTitle}\" sẽ bắt đầu trong vòng 15 phút.",
                DeduplicationKey = key,
            });
            created++;
        }

        if (created > 0)
        {
            await repository.SaveChangesAsync(ct);
        }

        return created;
    }

    public Task<List<Notification>> PendingPushAsync(int limit, CancellationToken ct = default)
        => repository.FindPendingPushAsync(limit, ct);

    public async Task MarkPushedAsync(Guid notificationId, CancellationToken ct = default)
    {
        await repository.MarkPushedAsync(notificationId, clock.GetUtcNow(), ct);
    }
}
