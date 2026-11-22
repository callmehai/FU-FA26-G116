using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Notifications;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 unit tests for FE-13 (Notifications/Email): NotiUnitRound1.
// Every test carries [Trait("UTC", ...)] + [Trait("Trait", "UTC")] and the
// "<Class.Method>/<UTCnnn>" string in its DisplayName, so the trx (which has
// no traits) is matched back to the UTC rows by name.
public sealed class NotiUnitRound1Tests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 3, 0, 0, TimeSpan.Zero);

    private readonly FakeNotificationRepository _repository = new();
    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private NotificationService Service() => new(_repository, _meetings, _unitOfWork, new FixedClock(Now));

    // ---- NotificationService.MarkReadAsync ----

    [Fact(DisplayName = "NotificationService.MarkReadAsync/UTC001: own notification is stamped")]
    [Trait("UTC", "NotificationService.MarkReadAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task MarkRead_OwnNotification_StampsReadAt()
    {
        var user = Guid.CreateVersion7();
        var notification = new Notification { UserId = user, Type = NotificationType.MeetingInvited, Title = "Moi hop" };
        _repository.Rows.Add(notification);

        var result = await Service().MarkReadAsync(user, notification.Id);

        Assert.NotNull(result);
        Assert.Equal(Now, result.ReadAt);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact(DisplayName = "NotificationService.MarkReadAsync/UTC002: another user notification is invisible")]
    [Trait("UTC", "NotificationService.MarkReadAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task MarkRead_AnotherUserNotification_ReturnsNull()
    {
        var notification = new Notification { UserId = Guid.CreateVersion7(), Type = NotificationType.MeetingInvited, Title = "Moi hop" };
        _repository.Rows.Add(notification);

        var result = await Service().MarkReadAsync(Guid.CreateVersion7(), notification.Id);

        Assert.Null(result);
        Assert.Null(notification.ReadAt);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact(DisplayName = "NotificationService.MarkReadAsync/UTC003: unknown id returns null")]
    [Trait("UTC", "NotificationService.MarkReadAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task MarkRead_UnknownId_ReturnsNull()
    {
        var result = await Service().MarkReadAsync(Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.Null(result);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact(DisplayName = "NotificationService.MarkReadAsync/UTC004: already-read keeps its first ReadAt")]
    [Trait("UTC", "NotificationService.MarkReadAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task MarkRead_AlreadyRead_KeepsFirstReadAt()
    {
        var user = Guid.CreateVersion7();
        var first = Now.AddHours(-1);
        var notification = new Notification
        {
            UserId = user, Type = NotificationType.MeetingInvited, Title = "Moi hop",
            ReadAt = first, UpdatedAt = first,
        };
        _repository.Rows.Add(notification);

        var result = await Service().MarkReadAsync(user, notification.Id);

        Assert.NotNull(result);
        Assert.Equal(first, result.ReadAt);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    // ---- NotificationService.MarkAllReadAsync ----

    [Fact(DisplayName = "NotificationService.MarkAllReadAsync/UTC001: all unread are stamped at once")]
    [Trait("UTC", "NotificationService.MarkAllReadAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task MarkAllRead_ThreeUnread_StampsAll()
    {
        var user = Guid.CreateVersion7();
        for (var i = 0; i < 3; i++)
            _repository.Rows.Add(new Notification { UserId = user, Type = NotificationType.MeetingInvited, Title = "Moi hop" });

        var count = await Service().MarkAllReadAsync(user);

        Assert.Equal(3, count);
        Assert.All(_repository.Rows, n => Assert.Equal(Now, n.ReadAt));
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact(DisplayName = "NotificationService.MarkAllReadAsync/UTC002: nobody unread returns zero")]
    [Trait("UTC", "NotificationService.MarkAllReadAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task MarkAllRead_NothingUnread_ReturnsZero()
    {
        var user = Guid.CreateVersion7();
        var read = new Notification
        {
            UserId = user, Type = NotificationType.MeetingInvited, Title = "Moi hop",
            ReadAt = Now.AddHours(-1), UpdatedAt = Now.AddHours(-1),
        };
        _repository.Rows.Add(read);

        var count = await Service().MarkAllReadAsync(user);

        Assert.Equal(0, count);
        Assert.Equal(read.ReadAt, _repository.Rows.Single().ReadAt);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact(DisplayName = "NotificationService.MarkAllReadAsync/UTC003: unknown user returns zero")]
    [Trait("UTC", "NotificationService.MarkAllReadAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task MarkAllRead_UnknownUser_ReturnsZero()
    {
        var count = await Service().MarkAllReadAsync(Guid.CreateVersion7());

        Assert.Equal(0, count);
        Assert.Empty(_repository.Rows);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    // ---- NotificationService.MarkPushedAsync ----

    [Fact(DisplayName = "NotificationService.MarkPushedAsync/UTC001: pending row is stamped pushed")]
    [Trait("UTC", "NotificationService.MarkPushedAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task MarkPushed_PendingRow_StampsPushedAt()
    {
        var notification = new Notification { UserId = Guid.CreateVersion7(), Type = NotificationType.MeetingInvited, Title = "Moi hop" };
        _repository.Rows.Add(notification);

        await Service().MarkPushedAsync(notification.Id);

        Assert.Equal(Now, notification.PushedAt);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        Assert.Single(_repository.Rows);
    }

    [Fact(DisplayName = "NotificationService.MarkPushedAsync/UTC002: already-pushed keeps its PushedAt")]
    [Trait("UTC", "NotificationService.MarkPushedAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task MarkPushed_AlreadyPushed_KeepsPushedAt()
    {
        var pushed = Now.AddHours(-1);
        var notification = new Notification
        {
            UserId = Guid.CreateVersion7(), Type = NotificationType.MeetingInvited, Title = "Moi hop",
            PushedAt = pushed,
        };
        _repository.Rows.Add(notification);

        await Service().MarkPushedAsync(notification.Id);

        Assert.Equal(pushed, notification.PushedAt);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        Assert.Single(_repository.Rows);
    }

    [Fact(DisplayName = "NotificationService.MarkPushedAsync/UTC003: unknown id is a silent no-op")]
    [Trait("UTC", "NotificationService.MarkPushedAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task MarkPushed_UnknownId_DoesNothing()
    {
        await Service().MarkPushedAsync(Guid.CreateVersion7());

        Assert.Empty(_repository.Rows);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    // ---- NotificationService.GetTargetAsync ----

    [Fact(DisplayName = "NotificationService.GetTargetAsync/UTC001: meeting target opens for a reader")]
    [Trait("UTC", "NotificationService.GetTargetAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task GetTarget_MeetingReader_ReturnsMeetingTarget()
    {
        var (meetingId, user) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        _repository.Rows.Add(new Notification
        {
            UserId = user, MeetingId = meetingId, TargetType = "Meeting", TargetId = meetingId,
            Type = NotificationType.MeetingInvited, Title = "Moi hop",
        });
        _meetings.Accessible.Add((meetingId, user));

        var target = await Service().GetTargetAsync(user, _repository.Rows.Single().Id);

        Assert.Equal(new NotificationTarget("Meeting", meetingId), target);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        Assert.Single(_repository.Rows);
    }

    [Fact(DisplayName = "NotificationService.GetTargetAsync/UTC002: another user target stays hidden")]
    [Trait("UTC", "NotificationService.GetTargetAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task GetTarget_AnotherUserNotification_ReturnsNull()
    {
        var notification = new Notification
        {
            UserId = Guid.CreateVersion7(), MeetingId = Guid.CreateVersion7(),
            TargetType = "Meeting", TargetId = Guid.CreateVersion7(),
            Type = NotificationType.MeetingInvited, Title = "Moi hop",
        };
        _repository.Rows.Add(notification);

        var target = await Service().GetTargetAsync(Guid.CreateVersion7(), notification.Id);

        Assert.Null(target);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        Assert.NotNull(_repository.Rows.Single().TargetId);
    }

    [Fact(DisplayName = "NotificationService.GetTargetAsync/UTC003: tampered target is not opened")]
    [Trait("UTC", "NotificationService.GetTargetAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task GetTarget_MismatchedTargetId_ReturnsNull()
    {
        var (meetingId, user) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        _repository.Rows.Add(new Notification
        {
            UserId = user, MeetingId = meetingId, TargetType = "Meeting", TargetId = Guid.CreateVersion7(),
            Type = NotificationType.MeetingInvited, Title = "Moi hop",
        });
        _meetings.Accessible.Add((meetingId, user));

        var target = await Service().GetTargetAsync(user, _repository.Rows.Single().Id);

        Assert.Null(target);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        Assert.Single(_repository.Rows);
    }

    [Fact(DisplayName = "NotificationService.GetTargetAsync/UTC004: calendar share opens for a party")]
    [Trait("UTC", "NotificationService.GetTargetAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task GetTarget_CalendarShareParty_ReturnsShareTarget()
    {
        var (shareId, user) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        _repository.Rows.Add(new Notification
        {
            UserId = user, TargetType = "CalendarShare", TargetId = shareId,
            Type = NotificationType.MeetingInvited, Title = "Chia se lich",
        });
        _repository.CalendarShareParties.Add((shareId, user));

        var target = await Service().GetTargetAsync(user, _repository.Rows.Single().Id);

        Assert.Equal(new NotificationTarget("CalendarShare", shareId), target);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        Assert.Single(_repository.Rows);
    }

    [Fact(DisplayName = "NotificationService.GetTargetAsync/UTC005: own voice sample opens")]
    [Trait("UTC", "NotificationService.GetTargetAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task GetTarget_OwnVoiceSample_ReturnsSampleTarget()
    {
        var (sampleId, user) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        _repository.Rows.Add(new Notification
        {
            UserId = user, TargetType = "VoiceSample", TargetId = sampleId,
            Type = NotificationType.MeetingInvited, Title = "Mau giong",
        });
        _repository.OwnVoiceSamples.Add((sampleId, user));

        var target = await Service().GetTargetAsync(user, _repository.Rows.Single().Id);

        Assert.Equal(new NotificationTarget("VoiceSample", sampleId), target);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        Assert.Single(_repository.Rows);
    }

    [Fact(DisplayName = "NotificationService.GetTargetAsync/UTC006: unsupported type returns null")]
    [Trait("UTC", "NotificationService.GetTargetAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task GetTarget_UnsupportedType_ReturnsNull()
    {
        var user = Guid.CreateVersion7();
        _repository.Rows.Add(new Notification
        {
            UserId = user, TargetType = "Document", TargetId = Guid.CreateVersion7(),
            Type = NotificationType.MeetingInvited, Title = "Tai lieu",
        });

        var target = await Service().GetTargetAsync(user, _repository.Rows.Single().Id);

        Assert.Null(target);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        Assert.Single(_repository.Rows);
    }

    // ---- NotificationService.PendingPushAsync ----

    [Fact(DisplayName = "NotificationService.PendingPushAsync/UTC001: oldest unpushed come first")]
    [Trait("UTC", "NotificationService.PendingPushAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task PendingPush_FiveUnpushed_ReturnsThreeOldest()
    {
        var rows = Enumerable.Range(0, 5).Select(_ => new Notification
        {
            UserId = Guid.CreateVersion7(), Type = NotificationType.MeetingInvited, Title = "Moi hop",
        }).ToList();
        _repository.Rows.AddRange(rows);

        var pending = await Service().PendingPushAsync(3);

        Assert.Equal(3, pending.Count);
        Assert.Equal(rows.Take(3).Select(r => r.Id), pending.Select(r => r.Id));
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact(DisplayName = "NotificationService.PendingPushAsync/UTC002: all pushed returns empty")]
    [Trait("UTC", "NotificationService.PendingPushAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task PendingPush_AllPushed_ReturnsEmpty()
    {
        for (var i = 0; i < 2; i++)
            _repository.Rows.Add(new Notification
            {
                UserId = Guid.CreateVersion7(), Type = NotificationType.MeetingInvited, Title = "Moi hop",
                PushedAt = Now.AddMinutes(-i),
            });

        var pending = await Service().PendingPushAsync(10);

        Assert.Empty(pending);
        Assert.Equal(2, _repository.Rows.Count);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact(DisplayName = "NotificationService.PendingPushAsync/UTC003: limit equal to available returns all")]
    [Trait("UTC", "NotificationService.PendingPushAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task PendingPush_LimitEqualsAvailable_ReturnsAll()
    {
        for (var i = 0; i < 3; i++)
            _repository.Rows.Add(new Notification
            {
                UserId = Guid.CreateVersion7(), Type = NotificationType.MeetingInvited, Title = "Moi hop",
            });

        var pending = await Service().PendingPushAsync(3);

        Assert.Equal(3, pending.Count);
        Assert.Equal(3, _repository.Rows.Count);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    // ---- NotificationService.QueueMeetingStartedAsync ----

    [Fact(DisplayName = "NotificationService.QueueMeetingStartedAsync/UTC001: one notice per recipient")]
    [Trait("UTC", "NotificationService.QueueMeetingStartedAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task MeetingStarted_ThreeRecipients_QueuesThreeInOneCommit()
    {
        var (meetingId, historyId) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var users = new[] { Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7() };

        await Service().QueueMeetingStartedAsync(meetingId, "Hop giao ban", historyId, Now, users);

        Assert.Equal(3, _repository.Added.Count);
        Assert.All(_repository.Added, n => Assert.Equal($"meeting-started:{historyId:D}:{n.UserId:D}", n.DeduplicationKey));
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "NotificationService.QueueMeetingStartedAsync/UTC002: duplicate recipient queued once")]
    [Trait("UTC", "NotificationService.QueueMeetingStartedAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task MeetingStarted_DuplicateRecipient_QueuesDistinctOnly()
    {
        var (meetingId, historyId) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var (first, second) = (Guid.CreateVersion7(), Guid.CreateVersion7());

        await Service().QueueMeetingStartedAsync(meetingId, "Hop giao ban", historyId, Now, [first, first, second]);

        Assert.Equal(2, _repository.Added.Count);
        Assert.Contains(first, _repository.Added.Select(n => n.UserId));
        Assert.Contains(second, _repository.Added.Select(n => n.UserId));
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "NotificationService.QueueMeetingStartedAsync/UTC003: empty recipients write nothing")]
    [Trait("UTC", "NotificationService.QueueMeetingStartedAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task MeetingStarted_NoRecipients_WritesNothing()
    {
        await Service().QueueMeetingStartedAsync(Guid.CreateVersion7(), "Hop giao ban", Guid.CreateVersion7(), Now, []);

        Assert.Empty(_repository.Added);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact(DisplayName = "NotificationService.QueueMeetingStartedAsync/UTC004: single recipient queues one")]
    [Trait("UTC", "NotificationService.QueueMeetingStartedAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task MeetingStarted_SingleRecipient_QueuesOne()
    {
        var (meetingId, historyId, user) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        await Service().QueueMeetingStartedAsync(meetingId, "Hop giao ban", historyId, Now, [user]);

        var notice = Assert.Single(_repository.Added);
        Assert.Equal($"meeting-started:{historyId:D}:{user:D}", notice.DeduplicationKey);
        Assert.Equal(1, _unitOfWork.Saves);
    }

    // ---- NotificationService.QueueStartingSoonAsync ----

    [Fact(DisplayName = "NotificationService.QueueStartingSoonAsync/UTC001: two participants are reminded")]
    [Trait("UTC", "NotificationService.QueueStartingSoonAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task StartingSoon_TwoParticipants_QueuesTwo()
    {
        var meetingId = Guid.CreateVersion7();
        var (first, second) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var start = Now.AddMinutes(10);
        _repository.Upcoming.AddRange(
        [
            new(meetingId, "Giao ban", start, first),
            new(meetingId, "Giao ban", start, second),
        ]);

        var created = await Service().QueueStartingSoonAsync();

        Assert.Equal(2, created);
        Assert.All(_repository.Added, n => Assert.Equal("Cuộc họp sắp bắt đầu", n.Title));
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "NotificationService.QueueStartingSoonAsync/UTC002: start at the cutoff is inside")]
    [Trait("UTC", "NotificationService.QueueStartingSoonAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task StartingSoon_StartAtCutoff_IsInsideWindow()
    {
        var meetingId = Guid.CreateVersion7();
        var (first, second) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        _repository.Upcoming.AddRange(
        [
            new(meetingId, "Giao ban", Now.AddMinutes(15), first),
            new(meetingId, "Giao ban", Now.AddMinutes(15), second),
        ]);

        var created = await Service().QueueStartingSoonAsync();

        Assert.Equal(2, created);
        Assert.Equal(2, _repository.Added.Count);
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "NotificationService.QueueStartingSoonAsync/UTC003: one second past cutoff is out")]
    [Trait("UTC", "NotificationService.QueueStartingSoonAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task StartingSoon_StartPastCutoff_QueuesNothing()
    {
        var meetingId = Guid.CreateVersion7();
        _repository.Upcoming.Add(new(meetingId, "Giao ban", Now.AddMinutes(15).AddSeconds(1), Guid.CreateVersion7()));

        var created = await Service().QueueStartingSoonAsync();

        Assert.Equal(0, created);
        Assert.Empty(_repository.Added);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "NotificationService.QueueStartingSoonAsync/UTC004: reminded user is skipped")]
    [Trait("UTC", "NotificationService.QueueStartingSoonAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task StartingSoon_OneAlreadyReminded_QueuesOnlyTheOther()
    {
        var meetingId = Guid.CreateVersion7();
        var (first, second) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var start = Now.AddMinutes(10);
        _repository.Upcoming.AddRange(
        [
            new(meetingId, "Giao ban", start, first),
            new(meetingId, "Giao ban", start, second),
        ]);
        _repository.Rows.Add(new Notification
        {
            UserId = first, MeetingId = meetingId, Type = NotificationType.MeetingStartingSoon, Title = "x",
            DeduplicationKey = $"meeting-starting-soon:{meetingId:D}:{start.ToUnixTimeSeconds()}:{first:D}",
        });

        var created = await Service().QueueStartingSoonAsync();

        Assert.Equal(1, created);
        Assert.Equal(second, Assert.Single(_repository.Added).UserId);
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "NotificationService.QueueStartingSoonAsync/UTC005: duplicated upcoming row queued once")]
    [Trait("UTC", "NotificationService.QueueStartingSoonAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task StartingSoon_DuplicatedRow_QueuesOnce()
    {
        var meetingId = Guid.CreateVersion7();
        var user = Guid.CreateVersion7();
        var start = Now.AddMinutes(10);
        _repository.Upcoming.AddRange(
        [
            new(meetingId, "Giao ban", start, user),
            new(meetingId, "Giao ban", start, user),
        ]);

        var created = await Service().QueueStartingSoonAsync();

        Assert.Equal(1, created);
        Assert.Single(_repository.Added);
        Assert.Equal(1, _unitOfWork.Saves);
    }
}

internal sealed class NotiCapturingLogger : ILogger
{
    public List<string> Messages { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NotiNullScope();

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));

    private sealed class NotiNullScope : IDisposable
    {
        public void Dispose() { }
    }
}
