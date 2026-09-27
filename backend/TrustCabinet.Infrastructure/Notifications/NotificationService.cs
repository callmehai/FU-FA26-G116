using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Notifications;

public sealed record NotificationPage(
    IReadOnlyList<Notification> Items, int UnreadCount, int TotalCount);

public sealed record NotificationTarget(string TargetType, Guid TargetId, string Url);

/// <summary>Owns notification visibility and read state; caller ids always come from validated auth.</summary>
public sealed class NotificationService(TrustCabinetDbContext db, TimeProvider clock)
{
    private readonly NotificationRepository repository = new(db);

    public async Task<NotificationPage> ListAsync(
        Guid userId, int limit, int offset, CancellationToken ct = default)
    {
        var items = await repository.FindByRecipientAsync(userId, limit, offset, ct);
        return new NotificationPage(items,
            await repository.CountUnreadAsync(userId, ct),
            await repository.CountByRecipientAsync(userId, ct));
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
        if (notification is null || !ValidateOwnership(userId, notification)
            || !await CanOpenTargetAsync(userId, notification, ct))
        {
            return null;
        }

        return new NotificationTarget(notification.TargetType!,
            notification.TargetId!.Value, notification.ActionUrl!);
    }

    public static bool ValidateOwnership(Guid userId, Notification notification)
        => notification.UserId == userId;

    public async Task<bool> CanOpenTargetAsync(Guid userId, Notification notification,
        CancellationToken ct = default)
    {
        if (!ValidateOwnership(userId, notification)
            || notification.TargetType != "Meeting"
            || notification.TargetId is not { } meetingId
            || notification.MeetingId != meetingId
            || notification.ActionUrl != $"/mgmt/meetings/{meetingId:D}")
        {
            return false;
        }

        return await repository.CanOpenMeetingAsync(userId, meetingId, ct);
    }

    /// <summary>
    /// Called by the future participant-add handler for a published meeting. It only adds a row to
    /// the current DbContext, so the participant and notification commit in the same SaveChanges.
    /// </summary>
    public async Task QueueParticipantInvitedAsync(
        Meeting meeting, MeetingParticipant participant, User recipient, CancellationToken ct = default)
    {
        if (meeting.Status != MeetingStatus.Published)
        {
            throw new InvalidOperationException("Invitation notification requires a published meeting.");
        }

        if (participant.MeetingId != meeting.Id || participant.UserId != recipient.Id)
        {
            throw new ArgumentException("Participant, recipient, and meeting must match.");
        }

        if (!recipient.IsActive)
        {
            return;
        }

        var key = $"meeting-invited:{meeting.Id:D}:{participant.Id:D}";
        if (repository.IsQueued(key) || await repository.ExistsAsync(key, ct))
        {
            return;
        }

        repository.Add(new Notification
        {
            UserId = recipient.Id,
            MeetingId = meeting.Id,
            TargetType = "Meeting",
            TargetId = meeting.Id,
            ActionUrl = $"/mgmt/meetings/{meeting.Id:D}",
            Type = NotificationType.MeetingInvited,
            Title = "Bạn được mời họp",
            Body = $"Bạn được mời tham dự cuộc họp \"{meeting.Title}\".",
            DeduplicationKey = key,
        });
    }

    /// <summary>Queues one reminder per invited active user during the 15 minutes before start.</summary>
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
            var key = $"meeting-starting-soon:{item.MeetingId:D}:{item.UserId:D}";
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
                ActionUrl = $"/mgmt/meetings/{item.MeetingId:D}",
                Type = NotificationType.MeetingStartingSoon,
                Title = "Cuộc họp sắp bắt đầu",
                Body = $"Cuộc họp \"{item.MeetingTitle}\" sẽ bắt đầu trong vòng 15 phút.",
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
