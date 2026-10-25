using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Notifications;

namespace TrustCabinet.Infrastructure.Notifications;

/// <summary>Persists notifications and owns the recipient-scoped queries from the class diagram.</summary>
public sealed class NotificationRepository(TrustCabinetDbContext db) : INotificationRepository
{
    public Task<List<Notification>> FindByRecipientAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var offset = ((long)page - 1) * pageSize;
        if (offset > int.MaxValue)
        {
            return Task.FromResult(new List<Notification>());
        }

        return db.Notifications.AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public Task<Notification?> FindByIdAndRecipientAsync(
        Guid notificationId, Guid userId, CancellationToken ct = default)
        => db.Notifications.SingleOrDefaultAsync(
            notification => notification.Id == notificationId && notification.UserId == userId, ct);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default)
        => db.Notifications.CountAsync(
            notification => notification.UserId == userId && notification.ReadAt == null, ct);

    public Task<int> CountByRecipientAsync(Guid userId, CancellationToken ct = default)
        => db.Notifications.CountAsync(notification => notification.UserId == userId, ct);

    public async Task MarkAsReadAsync(Notification notification, DateTimeOffset now,
        CancellationToken ct = default)
    {
        notification.MarkAsRead(ToDatabasePrecision(now));
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> MarkAllUnreadAsReadAsync(
        Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        var readAt = ToDatabasePrecision(now);
        if (db.Database.IsRelational())
        {
            return await db.Notifications
                .Where(notification => notification.UserId == userId
                    && notification.ReadAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(notification => notification.ReadAt, readAt)
                    .SetProperty(notification => notification.UpdatedAt, readAt), ct);
        }

        // EF's in-memory provider cannot execute bulk updates. Keep the lightweight service and
        // endpoint tests provider-agnostic while production PostgreSQL uses the single UPDATE above.
        var unread = await db.Notifications
            .Where(notification => notification.UserId == userId && notification.ReadAt == null)
            .ToListAsync(ct);
        foreach (var notification in unread)
        {
            notification.MarkAsRead(readAt);
        }

        if (unread.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return unread.Count;
    }

    public void Add(Notification notification) => db.Notifications.Add(notification);

    public Task<List<UpcomingMeetingRecipient>> FindUpcomingMeetingRecipientsAsync(
        DateTimeOffset now, DateTimeOffset cutoff, CancellationToken ct = default)
        => db.MeetingParticipants.AsNoTracking().Where(Domain.Participants.ParticipantRules.ActiveExpression)
            .Where(participant => participant.Meeting.Status == MeetingStatus.Published
                && participant.Meeting.PlannedStartTime > now
                && participant.Meeting.PlannedStartTime <= cutoff)
            .Select(participant => new UpcomingMeetingRecipient(
                participant.MeetingId,
                participant.Meeting.Title,
                participant.Meeting.PlannedStartTime,
                participant.UserId))
            .ToListAsync(ct);

    public Task<List<string?>> FindReminderKeysAsync(Guid[] meetingIds,
        CancellationToken ct = default)
        => db.Notifications.AsNoTracking()
            .Where(notification => notification.Type == NotificationType.MeetingStartingSoon
                && notification.MeetingId != null
                && meetingIds.Contains(notification.MeetingId.Value))
            .Select(notification => notification.DeduplicationKey)
            .ToListAsync(ct);

    public Task<List<Notification>> FindPendingPushAsync(int limit,
        CancellationToken ct = default)
        => db.Notifications.AsNoTracking()
            .Where(notification => notification.PushedAt == null)
            .OrderBy(notification => notification.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task MarkPushedAsync(Guid notificationId, DateTimeOffset now,
        CancellationToken ct = default)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(
            item => item.Id == notificationId && item.PushedAt == null, ct);
        if (notification is null)
        {
            return;
        }

        notification.PushedAt = now;
        await db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    private static DateTimeOffset ToDatabasePrecision(DateTimeOffset value)
        => value.AddTicks(-(value.Ticks % 10));
}
