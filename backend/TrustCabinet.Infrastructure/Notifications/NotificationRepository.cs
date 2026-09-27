using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Notifications;

public sealed record UpcomingMeetingRecipient(Guid MeetingId, string MeetingTitle, Guid UserId);

/// <summary>Persists notifications and owns the recipient-scoped queries from the class diagram.</summary>
public sealed class NotificationRepository(TrustCabinetDbContext db)
{
    public Task<List<Notification>> FindByRecipientAsync(
        Guid userId, int limit, int offset, CancellationToken ct = default)
        => db.Notifications.AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

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
        if (notification.IsRead)
        {
            return;
        }

        notification.MarkAsRead(ToDatabasePrecision(now));
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> MarkAllUnreadAsReadAsync(
        Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        var unread = await db.Notifications
            .Where(notification => notification.UserId == userId && notification.ReadAt == null)
            .ToListAsync(ct);
        foreach (var notification in unread)
        {
            notification.MarkAsRead(ToDatabasePrecision(now));
        }

        if (unread.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return unread.Count;
    }

    public bool IsQueued(string key)
        => db.ChangeTracker.Entries<Notification>().Any(entry =>
            entry.State == EntityState.Added && entry.Entity.DeduplicationKey == key);

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => db.Notifications.AnyAsync(notification => notification.DeduplicationKey == key, ct);

    public void Add(Notification notification) => db.Notifications.Add(notification);

    public Task<List<UpcomingMeetingRecipient>> FindUpcomingMeetingRecipientsAsync(
        DateTimeOffset now, DateTimeOffset cutoff, CancellationToken ct = default)
        => db.MeetingParticipants.AsNoTracking()
            .Where(participant => participant.Meeting.Status == MeetingStatus.Published
                && participant.Meeting.StartsAt > now
                && participant.Meeting.StartsAt <= cutoff
                && participant.User.IsActive)
            .Select(participant => new UpcomingMeetingRecipient(
                participant.MeetingId, participant.Meeting.Title, participant.UserId))
            .ToListAsync(ct);

    public Task<List<string?>> FindReminderKeysAsync(Guid[] meetingIds,
        CancellationToken ct = default)
        => db.Notifications.AsNoTracking()
            .Where(notification => notification.Type == NotificationType.MeetingStartingSoon
                && notification.MeetingId != null
                && meetingIds.Contains(notification.MeetingId.Value))
            .Select(notification => notification.DeduplicationKey)
            .ToListAsync(ct);

    public Task<bool> CanOpenMeetingAsync(Guid userId, Guid meetingId,
        CancellationToken ct = default)
        => db.Meetings.AnyAsync(meeting => meeting.Id == meetingId
            && (meeting.CreatedById == userId
                || meeting.Participants.Any(participant => participant.UserId == userId))
            && (meeting.Status == MeetingStatus.Published
                || meeting.Status == MeetingStatus.InProgress
                || meeting.Status == MeetingStatus.Ended
                || meeting.Status == MeetingStatus.MinutesIssued
                || meeting.Status == MeetingStatus.Archived), ct);

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
