using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Meetings;
using TrustCabinet.Infrastructure.Notifications;

namespace TrustCabinet.Web.Tests.Notifications;

public class NotificationServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 24, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reading_is_scoped_to_the_recipient_and_is_idempotent()
    {
        await using var db = NewDb();
        var mine = NewUser("mine@example.com");
        var other = NewUser("other@example.com");
        var item = NewNotification(mine.Id);
        db.Notifications.AddRange(item, NewNotification(other.Id));
        await db.SaveChangesAsync();
        var service = NewService(db);

        Assert.Single((await service.ListAsync(mine.Id, 1, 20)).Items);
        Assert.Null(await service.MarkReadAsync(other.Id, item.Id));
        Assert.Equal(1, await service.GetUnreadCountAsync(mine.Id));

        var read = await service.MarkReadAsync(mine.Id, item.Id);
        Assert.Equal(Now, read?.ReadAt);
        Assert.Equal(Now, (await service.MarkReadAsync(mine.Id, item.Id))?.ReadAt);
        Assert.Equal(0, await service.GetUnreadCountAsync(mine.Id));
        Assert.Equal(1, await service.GetUnreadCountAsync(other.Id));
    }

    [Fact]
    public async Task Mark_all_changes_only_the_callers_unread_rows()
    {
        await using var db = NewDb();
        var owner = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        db.Notifications.AddRange(NewNotification(owner), NewNotification(owner), NewNotification(other));
        await db.SaveChangesAsync();
        var service = NewService(db);

        Assert.Equal(2, await service.MarkAllReadAsync(owner));
        Assert.Equal(0, await service.MarkAllReadAsync(owner));
        Assert.Equal(0, await service.GetUnreadCountAsync(owner));
        Assert.Equal(1, await service.GetUnreadCountAsync(other));
    }

    [Fact]
    public async Task Reminder_is_queued_once_for_each_active_participant_in_the_15_minute_window()
    {
        await using var db = NewDb();
        var organizer = NewUser("organizer@example.com");
        var active = NewUser("active@example.com");
        var inactive = NewUser("inactive@example.com");
        inactive.IsActive = false;
        var meeting = NewMeeting(organizer);
        meeting.StartsAt = Now.AddMinutes(10);
        meeting.EndsAt = Now.AddHours(1);
        db.Users.AddRange(organizer, active, inactive);
        db.Meetings.Add(meeting);
        db.MeetingParticipants.AddRange(
            new MeetingParticipant { MeetingId = meeting.Id, UserId = active.Id },
            new MeetingParticipant { MeetingId = meeting.Id, UserId = inactive.Id });
        await db.SaveChangesAsync();
        var service = NewService(db);

        Assert.Equal(1, await service.QueueStartingSoonAsync());
        Assert.Equal(0, await service.QueueStartingSoonAsync());
        var reminder = Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal(active.Id, reminder.UserId);
        Assert.Equal(NotificationType.MeetingStartingSoon, reminder.Type);
        Assert.Equal(meeting.Id, reminder.MeetingId);
    }

    [Fact]
    public async Task Adding_participant_to_published_meeting_queues_one_invitation_and_target_is_protected()
    {
        await using var db = NewDb();
        var organizer = NewUser("organizer@example.com");
        var recipient = NewUser("recipient@example.com");
        var stranger = NewUser("stranger@example.com");
        var meeting = NewMeeting(organizer);
        var participant = new MeetingParticipant { MeetingId = meeting.Id, UserId = recipient.Id };
        db.Users.AddRange(organizer, recipient, stranger);
        db.Meetings.Add(meeting);
        db.MeetingParticipants.Add(participant);
        await db.SaveChangesAsync();
        var service = NewService(db);

        await service.QueueParticipantInvitedAsync(meeting, participant, recipient);
        await service.QueueParticipantInvitedAsync(meeting, participant, recipient);
        await db.SaveChangesAsync();
        var invitation = Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal(NotificationType.MeetingInvited, invitation.Type);
        Assert.Null(await service.GetTargetAsync(stranger.Id, invitation.Id));
        var target = await service.GetTargetAsync(recipient.Id, invitation.Id);
        Assert.Equal(meeting.Id, target?.TargetId);
    }

    [Fact]
    public async Task Target_uses_the_shared_meeting_access_rule_for_creator_and_approver()
    {
        await using var db = NewDb();
        var creator = NewUser("creator@example.com");
        var approver = NewUser("approver@example.com");
        var meeting = NewMeeting(creator);
        meeting.Status = MeetingStatus.Draft;
        meeting.AssignedApproverId = approver.Id;
        var creatorNotification = TargetedNotification(creator.Id, meeting.Id);
        var approverNotification = TargetedNotification(approver.Id, meeting.Id);
        db.Users.AddRange(creator, approver);
        db.Meetings.Add(meeting);
        db.Notifications.AddRange(creatorNotification, approverNotification);
        await db.SaveChangesAsync();
        var service = NewService(db);

        Assert.NotNull(await service.GetTargetAsync(creator.Id, creatorNotification.Id));
        Assert.Null(await service.GetTargetAsync(approver.Id, approverNotification.Id));

        meeting.Status = MeetingStatus.WaitingApproval;
        await db.SaveChangesAsync();

        Assert.NotNull(await service.GetTargetAsync(approver.Id, approverNotification.Id));
    }

    private static TrustCabinetDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TrustCabinetDbContext(options);
    }

    private static NotificationService NewService(TrustCabinetDbContext db)
        => new(new NotificationRepository(db), new MeetingRepository(db), new FrozenClock());

    private static User NewUser(string email) => new()
    {
        FullName = email,
        Email = email,
        PasswordHash = "hash",
    };

    private static Notification NewNotification(Guid userId) => new()
    {
        UserId = userId,
        Type = NotificationType.MeetingInvited,
        Title = "Bạn được mời họp",
    };

    private static Notification TargetedNotification(Guid userId, Guid meetingId) => new()
    {
        UserId = userId,
        MeetingId = meetingId,
        TargetType = "Meeting",
        TargetId = meetingId,
        Type = NotificationType.MeetingInvited,
        Title = "Bạn được mời họp",
    };

    private static Meeting NewMeeting(User organizer) => new()
    {
        Title = "Họp giao ban",
        Status = MeetingStatus.Published,
        Sensitivity = Sensitivity.Internal,
        Mode = MeetingMode.Online,
        StartsAt = Now.AddHours(1),
        EndsAt = Now.AddHours(2),
        CreatedById = organizer.Id,
    };

    private sealed class FrozenClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
