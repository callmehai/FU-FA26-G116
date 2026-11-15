using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Notifications;

/// <summary>Owns notification visibility and read state; caller ids always come from validated auth.</summary>
public sealed class NotificationService(
    INotificationRepository repository,
    IMeetingRepository meetings,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : INotificationService
{
    public async Task QueueMeetingStartedAsync(Guid meetingId, string meetingTitle, Guid historyId,
        DateTimeOffset occurredAt, IReadOnlyList<Guid> recipientUserIds, CancellationToken ct = default)
    {
        foreach (var userId in recipientUserIds.Distinct())
        {
            repository.Add(new Notification
            {
                UserId = userId,
                MeetingId = meetingId,
                TargetType = "Meeting",
                TargetId = meetingId,
                Type = NotificationType.MeetingStarted,
                Title = "Cuộc họp đã bắt đầu",
                Body = $"Cuộc họp “{meetingTitle}” đã bắt đầu.",
                OriginHistoryId = historyId,
                DeduplicationKey = $"meeting-started:{historyId:D}:{userId:D}",
                CreatedAt = occurredAt,
                UpdatedAt = occurredAt,
            });
        }
        if (recipientUserIds.Count > 0) await unitOfWork.SaveChangesAsync(ct);
    }

    public Task<NotificationPage> ListAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
        => unitOfWork.ExecuteAsync(async token =>
        {
            var items = await repository.FindByRecipientAsync(userId, page, pageSize, token);
            return new NotificationPage(items,
                await repository.CountUnreadAsync(userId, token),
                await repository.CountByRecipientAsync(userId, token),
                page,
                pageSize);
        }, TransactionMode.None, ct);

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
        => unitOfWork.ExecuteAsync(token => repository.CountUnreadAsync(userId, token), TransactionMode.None, ct);

    public Task<Notification?> MarkReadAsync(
        Guid userId, Guid notificationId, CancellationToken ct = default)
        => unitOfWork.ExecuteAsync<Notification?>(async token =>
        {
            var notification = await repository.FindByIdAndRecipientAsync(notificationId, userId, token);
            if (notification is null)
            {
                return null;
            }

            await repository.MarkAsReadAsync(notification, clock.GetUtcNow(), token);

            return notification;
        }, TransactionMode.None, ct);

    public Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
        => unitOfWork.ExecuteAsync(
            token => repository.MarkAllUnreadAsReadAsync(userId, clock.GetUtcNow(), token), TransactionMode.None, ct);

    public Task<NotificationTarget?> GetTargetAsync(
        Guid userId, Guid notificationId, CancellationToken ct = default)
        => unitOfWork.ExecuteAsync<NotificationTarget?>(async token =>
        {
            var notification = await repository.FindByIdAndRecipientAsync(notificationId, userId, token);
            if (notification is null || !await CanOpenTargetAsync(userId, notification, token))
            {
                return null;
            }

            return new NotificationTarget(notification.TargetType!, notification.TargetId!.Value);
        }, TransactionMode.None, ct);

    private async Task<bool> CanOpenTargetAsync(Guid userId, Notification notification,
        CancellationToken ct = default)
    {
        // SCRUM-142: a calendar-share notice opens the share for its owner or viewer — re-checked, like a meeting.
        if (notification.TargetType == "CalendarShare")
        {
            return notification.TargetId is { } shareId
                && await repository.IsCalendarSharePartyAsync(shareId, userId, ct);
        }

        // ADR-0060: "a Secretary took your sample" opens it while it is still there; deleted, it is gone like any target.
        if (notification.TargetType == "VoiceSample")
        {
            return notification.TargetId is { } sampleId
                && await repository.IsOwnVoiceSampleAsync(sampleId, userId, ct);
        }

        if (notification.TargetType != "Meeting"
            || notification.TargetId is not { } meetingId
            || notification.MeetingId != meetingId)
        {
            return false;
        }

        return await meetings.FindAccessibleAsync(meetingId, userId, ct) is not null;
    }

    /// <summary>
    /// Queues one reminder per invited active user during the 15 minutes before start. The key holds
    /// the planned start, so a rescheduled meeting is reminded again for its new time.
    /// </summary>
    public Task<int> QueueStartingSoonAsync(CancellationToken ct = default)
        => unitOfWork.ExecuteAsync(async token =>
        {
            var now = clock.GetUtcNow();
            var cutoff = now.AddMinutes(15);
            var upcoming = await repository.FindUpcomingMeetingRecipientsAsync(now, cutoff, token);

            if (upcoming.Count == 0)
            {
                return 0;
            }

            var meetingIds = upcoming.Select(item => item.MeetingId).Distinct().ToArray();
            var existing = (await repository.FindReminderKeysAsync(meetingIds, token))
                .ToHashSet(StringComparer.Ordinal);

            var created = 0;
            foreach (var item in upcoming)
            {
                var key = $"meeting-starting-soon:{item.MeetingId:D}:{item.PlannedStartTime.ToUnixTimeSeconds()}:{item.UserId:D}";
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
                    Body = $"Cuộc họp \"{item.MeetingTitle}\" sẽ bắt đầu trong vòng 15 phút.",
                    DeduplicationKey = key,
                });
                created++;
            }

            if (created > 0)
            {
                await unitOfWork.SaveChangesAsync(token);
            }

            return created;
        }, TransactionMode.None, ct);

    public Task<List<Notification>> PendingPushAsync(int limit, CancellationToken ct = default)
        => unitOfWork.ExecuteAsync(token => repository.FindPendingPushAsync(limit, token), TransactionMode.None, ct);

    public Task MarkPushedAsync(Guid notificationId, CancellationToken ct = default)
        => unitOfWork.ExecuteAsync(
            token => repository.MarkPushedAsync(notificationId, clock.GetUtcNow(), token), TransactionMode.None, ct);
}
