using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Notifications;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Notifications;

public sealed class NotificationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeNotificationRepository _repository = new();
    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly NotificationService _service;

    public NotificationServiceTests() => _service = new(_repository, _meetings, _unitOfWork, new FixedClock(Now));

    [Fact]
    public async Task Reminder_is_queued_once_per_recipient_and_saved_once()
    {
        var meetingId = Guid.CreateVersion7();
        var (first, second) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var start = Now.AddMinutes(10);
        _repository.Upcoming.AddRange([
            new(meetingId, "Giao ban", start, first),
            new(meetingId, "Giao ban", start, second),
        ]);
        // The first recipient already has this reminder from an earlier tick.
        _repository.Rows.Add(new Notification
        {
            UserId = first, MeetingId = meetingId, Type = NotificationType.MeetingStartingSoon, Title = "x",
            DeduplicationKey = $"meeting-starting-soon:{meetingId:D}:{start.ToUnixTimeSeconds()}:{first:D}",
        });

        Assert.Equal(1, await _service.QueueStartingSoonAsync());

        var reminder = Assert.Single(_repository.Added);
        Assert.Equal((second, meetingId, "Meeting"), (reminder.UserId, reminder.TargetId!.Value, reminder.TargetType));
        Assert.Equal(1, _unitOfWork.Saves);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task Nothing_to_remind_saves_nothing()
    {
        Assert.Equal(0, await _service.QueueStartingSoonAsync());
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Meeting_started_notice_goes_once_to_each_recipient_with_its_history_key()
    {
        var (meetingId, historyId, user) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        await _service.QueueMeetingStartedAsync(meetingId, "Giao ban", historyId, Now, [user, user]);

        var notice = Assert.Single(_repository.Added);
        Assert.Equal($"meeting-started:{historyId:D}:{user:D}", notice.DeduplicationKey);
        Assert.Equal(1, _unitOfWork.Saves);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task A_meeting_target_opens_only_while_the_read_policy_lets_the_recipient_in()
    {
        var (meetingId, user) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var notification = new Notification
        {
            UserId = user, MeetingId = meetingId, TargetType = "Meeting", TargetId = meetingId,
            Type = NotificationType.MeetingInvited, Title = "Mời họp",
        };
        _repository.Rows.Add(notification);

        Assert.Null(await _service.GetTargetAsync(user, notification.Id));

        _meetings.Accessible.Add((meetingId, user));
        Assert.Equal(new NotificationTarget("Meeting", meetingId), await _service.GetTargetAsync(user, notification.Id));
        // Someone else's notification does not exist for this caller.
        Assert.Null(await _service.GetTargetAsync(Guid.CreateVersion7(), notification.Id));
        // Every read goes through the unit of work, which translates a storage failure (R8).
        Assert.Equal([TransactionMode.None, TransactionMode.None, TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task Marking_read_stamps_the_clock_and_another_users_notification_is_not_found()
    {
        var user = Guid.CreateVersion7();
        var notification = new Notification { UserId = user, Type = NotificationType.MeetingInvited, Title = "Mời họp" };
        _repository.Rows.Add(notification);

        Assert.Null(await _service.MarkReadAsync(Guid.CreateVersion7(), notification.Id));
        Assert.Equal(Now, (await _service.MarkReadAsync(user, notification.Id))?.ReadAt);
        Assert.Equal(0, await _service.GetUnreadCountAsync(user));
        Assert.Equal(0, await _service.MarkAllReadAsync(user));
        Assert.Equal(Enumerable.Repeat(TransactionMode.None, 4), _unitOfWork.Modes);
    }

    [Fact]
    public async Task Pushing_reads_pending_rows_and_stamps_them_through_the_unit_of_work()
    {
        var notification = new Notification { UserId = Guid.CreateVersion7(), Type = NotificationType.MeetingInvited, Title = "Mời họp" };
        _repository.Rows.Add(notification);

        Assert.Same(notification, Assert.Single(await _service.PendingPushAsync(100)));
        await _service.MarkPushedAsync(notification.Id);

        Assert.Equal(Now, notification.PushedAt);
        Assert.Empty(await _service.PendingPushAsync(100));
        Assert.Equal(Enumerable.Repeat(TransactionMode.None, 3), _unitOfWork.Modes);
    }

    [Fact]
    public async Task A_store_that_cannot_be_reached_surfaces_as_a_persistence_exception()
    {
        _repository.Fail = new PersistenceUnavailableException();

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => _service.ListAsync(Guid.CreateVersion7(), 1, 20));
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);

        _repository.Fail = null;
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();
        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => _service.QueueMeetingStartedAsync(
            Guid.CreateVersion7(), "Giao ban", Guid.CreateVersion7(), Now, [Guid.CreateVersion7()]));
    }
}
