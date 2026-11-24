using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 unit tests for FE-13 (Notifications/Email), wave 2: the 33 email/invite
// UTCs missing from wave 1 (EmailQueueProcessor 6, InviteDispatch.Process 6,
// ProcessNext 6, StageCancel 4, StagePublish 3, StageReschedule 2, QueueDue 6).
// Outer class differs from NotiUnitRound1Tests on purpose.
// Every test carries [Trait("UTC", ...)] + [Trait("Trait", "UTC")] and the
// "<Class.Method>/<UTCnnn>" string in its DisplayName, so the trx (which has
// no traits) is matched back to the UTC rows by name.
public sealed class NotiUnitRound1Wave2Tests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 3, 0, 0, TimeSpan.Zero);

    // ---- EmailQueueProcessor.ProcessNextAsync ----

    private static EmailQueueProcessor QueueProcessor(
        FakeEmailMessageRepository messages, FakeUnitOfWork unitOfWork, FakeEmailSender sender,
        int maxAttempts = 8, string[]? undeliverable = null) => new(
        messages, unitOfWork, sender,
        Options.Create(new EmailDeliveryOptions
        {
            MaxAttempts = maxAttempts,
            RetryDelaySeconds = 60,
            ProcessingLeaseSeconds = 120,
            UndeliverableDomains = undeliverable ?? [],
        }),
        new FixedClock(Now), NullLogger<EmailQueueProcessor>.Instance);

    private static EmailMessage QueueDue(
        FakeEmailMessageRepository messages, string to = "an@example.vn", int attempts = 0)
    {
        var message = new EmailMessage
        {
            DeduplicationKey = $"test:{Guid.CreateVersion7():N}",
            ToAddress = to,
            Subject = "Lời mời",
            AttemptCount = attempts,
            NextAttemptAt = Now.AddMinutes(-1),
        };
        messages.Rows.Add(message);
        messages.Next = message;
        return message;
    }

    [Fact(DisplayName = "EmailQueueProcessor.ProcessNextAsync/UTC001: empty queue answers no work")]
    [Trait("UTC", "EmailQueueProcessor.ProcessNextAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Queue_EmptyQueue_AnswersNoWork()
    {
        var messages = new FakeEmailMessageRepository();
        var unitOfWork = new FakeUnitOfWork();
        var sender = new FakeEmailSender(unitOfWork);

        var result = await QueueProcessor(messages, unitOfWork, sender).ProcessNextAsync();

        Assert.Equal(EmailProcessingOutcome.NoWork, result.Outcome);
        Assert.Equal(0, result.AttemptCount);
        Assert.Empty(sender.Sent);
    }

    [Fact(DisplayName = "EmailQueueProcessor.ProcessNextAsync/UTC002: due message is sent on first try")]
    [Trait("UTC", "EmailQueueProcessor.ProcessNextAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Queue_DueMessage_SendsAndStampsProviderId()
    {
        var messages = new FakeEmailMessageRepository();
        var unitOfWork = new FakeUnitOfWork();
        var sender = new FakeEmailSender(unitOfWork);
        var message = QueueDue(messages);

        var result = await QueueProcessor(messages, unitOfWork, sender).ProcessNextAsync();

        Assert.Equal(EmailProcessingOutcome.Sent, result.Outcome);
        Assert.Equal(1, result.AttemptCount);
        Assert.Equal("provider-1", message.ProviderMessageId);
    }

    [Fact(DisplayName = "EmailQueueProcessor.ProcessNextAsync/UTC003: timed-out send is retried in one minute")]
    [Trait("UTC", "EmailQueueProcessor.ProcessNextAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Queue_TimeoutFailure_RetriesAfterOneMinute()
    {
        var messages = new FakeEmailMessageRepository();
        var unitOfWork = new FakeUnitOfWork();
        var sender = new FakeEmailSender(unitOfWork) { Fail = new TimeoutException("SMTP timeout") };
        var message = QueueDue(messages);

        var result = await QueueProcessor(messages, unitOfWork, sender).ProcessNextAsync();

        Assert.Equal(EmailProcessingOutcome.RetryScheduled, result.Outcome);
        Assert.Equal(1, result.AttemptCount);
        Assert.Equal(Now.AddSeconds(60), message.NextAttemptAt);
    }

    [Fact(DisplayName = "EmailQueueProcessor.ProcessNextAsync/UTC004: seventh attempt waits sixty-four minutes")]
    [Trait("UTC", "EmailQueueProcessor.ProcessNextAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Queue_SeventhAttemptFailure_RetriesAfterSixtyFourMinutes()
    {
        var messages = new FakeEmailMessageRepository();
        var unitOfWork = new FakeUnitOfWork();
        var sender = new FakeEmailSender(unitOfWork) { Fail = new IOException("SMTP unavailable") };
        var message = QueueDue(messages, attempts: 6);

        var result = await QueueProcessor(messages, unitOfWork, sender).ProcessNextAsync();

        Assert.Equal(EmailProcessingOutcome.RetryScheduled, result.Outcome);
        Assert.Equal(7, result.AttemptCount);
        Assert.Equal(Now.AddSeconds(3840), message.NextAttemptAt);
    }

    // ---- MeetingInvitationDispatchProcessor.ProcessAsync ----

    private sealed record DispatchSetup(
        FakeMeetingInvitationDispatchRepository Dispatches,
        FakeEmailMessageRepository Emails,
        FakeUnitOfWork UnitOfWork,
        Wave2Links Links);

    private static DispatchSetup NewDispatchSetup() => new(new(), new(), new(), new());

    private static MeetingInvitationDispatchProcessor DispatchProcessor(DispatchSetup setup, bool enabled = true) => new(
        setup.Dispatches, setup.Emails, setup.UnitOfWork,
        Options.Create(new EmailDeliveryOptions
        {
            ApplicationBaseUrl = "https://tc.example.vn",
            MaxAttempts = 8,
            RetryDelaySeconds = 60,
            Smtp = new() { Enabled = enabled, FromAddress = "no-reply@tc.example.vn" },
        }),
        new FixedClock(Now), setup.Links, NullLogger<MeetingInvitationDispatchProcessor>.Instance);

    private static MeetingInvitationDispatch NewDispatch(
        DispatchSetup setup,
        InvitationEventKind kind = InvitationEventKind.Invitation,
        InvitationDispatchState state = InvitationDispatchState.Pending,
        DateTimeOffset? nextAttemptAt = null)
    {
        var dispatch = new MeetingInvitationDispatch
        {
            MeetingId = Guid.CreateVersion7(),
            ParticipantId = Guid.CreateVersion7(),
            EventKind = kind,
            State = state,
            NextAttemptAt = nextAttemptAt,
            DeduplicationKey = $"roster:{Guid.CreateVersion7():N}",
            PayloadJson = JsonSerializer.Serialize(new InvitationPayload("Nguyễn Văn A", "a@example.vn",
                "Họp giao ban", "Mô tả", Now.AddDays(1), Now.AddDays(1).AddHours(1), "Phòng 1", "InPerson", "Member")),
        };
        setup.Dispatches.Rows.Add(dispatch);
        setup.Dispatches.Locked = dispatch;
        return dispatch;
    }

    [Fact(DisplayName = "MeetingInvitationDispatchProcessor.ProcessAsync/UTC001: disabled delivery queues nothing")]
    [Trait("UTC", "MeetingInvitationDispatchProcessor.ProcessAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Dispatch_DisabledDelivery_QueuesNothing()
    {
        var setup = NewDispatchSetup();

        var result = await DispatchProcessor(setup, enabled: false).ProcessAsync(Guid.CreateVersion7(), default);

        Assert.Equal(0, result);
        Assert.Empty(setup.Emails.Added);
        Assert.Empty(setup.UnitOfWork.Modes);
    }

    [Fact(DisplayName = "MeetingInvitationDispatchProcessor.ProcessAsync/UTC002: unknown id queues nothing")]
    [Trait("UTC", "MeetingInvitationDispatchProcessor.ProcessAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Dispatch_UnknownId_QueuesNothing()
    {
        var setup = NewDispatchSetup();

        var result = await DispatchProcessor(setup).ProcessAsync(Guid.CreateVersion7(), default);

        Assert.Equal(0, result);
        Assert.Empty(setup.Emails.Added);
        Assert.Equal([TransactionMode.None], setup.UnitOfWork.Modes);
    }

    [Fact(DisplayName = "MeetingInvitationDispatchProcessor.ProcessAsync/UTC003: materialized dispatch is unchanged")]
    [Trait("UTC", "MeetingInvitationDispatchProcessor.ProcessAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Dispatch_AlreadyMaterialized_QueuesNothing()
    {
        var setup = NewDispatchSetup();
        var dispatch = NewDispatch(setup, state: InvitationDispatchState.Materialized);

        var result = await DispatchProcessor(setup).ProcessAsync(dispatch.Id, default);

        Assert.Equal(0, result);
        Assert.Empty(setup.Emails.Added);
        Assert.Equal(0, setup.UnitOfWork.Saves);
    }

    [Fact(DisplayName = "MeetingInvitationDispatchProcessor.ProcessAsync/UTC004: suppressed dispatch is unchanged")]
    [Trait("UTC", "MeetingInvitationDispatchProcessor.ProcessAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Dispatch_AlreadySuppressed_QueuesNothing()
    {
        var setup = NewDispatchSetup();
        var dispatch = NewDispatch(setup, state: InvitationDispatchState.Suppressed);

        var result = await DispatchProcessor(setup).ProcessAsync(dispatch.Id, default);

        Assert.Equal(0, result);
        Assert.Empty(setup.Emails.Added);
        Assert.Equal(0, setup.UnitOfWork.Saves);
    }

    // ---- MeetingInvitationDispatchProcessor.ProcessNextAsync ----

    private sealed record BatchSetup(
        Wave2Dispatches Dispatches,
        FakeEmailMessageRepository Emails,
        FakeUnitOfWork UnitOfWork,
        Wave2Links Links);

    private static BatchSetup NewBatch() => new(new(), new(), new(), new());

    private static MeetingInvitationDispatchProcessor BatchProcessor(BatchSetup setup, bool enabled = true) => new(
        setup.Dispatches, setup.Emails, setup.UnitOfWork,
        Options.Create(new EmailDeliveryOptions
        {
            ApplicationBaseUrl = "https://tc.example.vn",
            MaxAttempts = 8,
            RetryDelaySeconds = 60,
            Smtp = new() { Enabled = enabled, FromAddress = "no-reply@tc.example.vn" },
        }),
        new FixedClock(Now), setup.Links, NullLogger<MeetingInvitationDispatchProcessor>.Instance);

    private static MeetingInvitationDispatch QueueDueDispatch(
        BatchSetup setup,
        InvitationEventKind kind = InvitationEventKind.Invitation,
        InvitationDispatchState state = InvitationDispatchState.Pending,
        Guid? participantId = null)
    {
        var dispatch = new MeetingInvitationDispatch
        {
            MeetingId = Guid.CreateVersion7(),
            ParticipantId = participantId ?? Guid.CreateVersion7(),
            EventKind = kind,
            State = state,
            DeduplicationKey = $"roster:{Guid.CreateVersion7():N}",
            PayloadJson = JsonSerializer.Serialize(new InvitationPayload("Nguyễn Văn A", "a@example.vn",
                "Họp giao ban", "Mô tả", Now.AddDays(1), Now.AddDays(1).AddHours(1), "Phòng 1", "InPerson", "Member")),
        };
        setup.Dispatches.Rows[dispatch.Id] = dispatch;
        return dispatch;
    }

    [Fact(DisplayName = "MeetingInvitationDispatchProcessor.ProcessNextAsync/UTC001: disabled delivery reads nothing")]
    [Trait("UTC", "MeetingInvitationDispatchProcessor.ProcessNextAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Batch_DisabledDelivery_ReadsNothing()
    {
        var setup = NewBatch();

        var result = await BatchProcessor(setup, enabled: false).ProcessNextAsync(default);

        Assert.Equal(0, result);
        Assert.Empty(setup.Emails.Added);
        Assert.Empty(setup.UnitOfWork.Modes);
    }

    [Fact(DisplayName = "MeetingInvitationDispatchProcessor.ProcessNextAsync/UTC002: no due dispatch queues nothing")]
    [Trait("UTC", "MeetingInvitationDispatchProcessor.ProcessNextAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Batch_NoDueDispatch_QueuesNothing()
    {
        var setup = NewBatch();

        var result = await BatchProcessor(setup).ProcessNextAsync(default);

        Assert.Equal(0, result);
        Assert.Empty(setup.Emails.Added);
        Assert.Equal([TransactionMode.None], setup.UnitOfWork.Modes);
    }

    [Fact(DisplayName = "MeetingInvitationDispatchProcessor.ProcessNextAsync/UTC003: three due dispatches queue three")]
    [Trait("UTC", "MeetingInvitationDispatchProcessor.ProcessNextAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Batch_ThreeDueDispatches_QueuesThree()
    {
        var setup = NewBatch();
        QueueDueDispatch(setup);
        QueueDueDispatch(setup);
        QueueDueDispatch(setup);

        var result = await BatchProcessor(setup).ProcessNextAsync(default);

        Assert.Equal(3, result);
        Assert.Equal(3, setup.Emails.Added.Count);
        Assert.Equal(3, setup.UnitOfWork.Saves);
    }

    [Fact(DisplayName = "MeetingInvitationDispatchProcessor.ProcessNextAsync/UTC004: only the emailable dispatch counts")]
    [Trait("UTC", "MeetingInvitationDispatchProcessor.ProcessNextAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Batch_MixedDispatches_CountsOnlyTheEmailedOne()
    {
        var setup = NewBatch();
        QueueDueDispatch(setup);
        QueueDueDispatch(setup, state: InvitationDispatchState.Materialized);
        var offRoster = QueueDueDispatch(setup);
        setup.Dispatches.InactiveParticipants.Add(offRoster.ParticipantId!.Value);

        var result = await BatchProcessor(setup).ProcessNextAsync(default);

        Assert.Equal(1, result);
        Assert.Single(setup.Emails.Added);
        Assert.Equal(InvitationDispatchState.Suppressed, offRoster.State);
    }

    // ---- MeetingInvitationEvents.StageCancellationAsync ----

    private sealed record StageSetup(
        FakeMeetingInvitationDispatchRepository Dispatches, FakeNotificationRepository Notes);

    private static Meeting NewStageMeeting() => new()
    {
        Title = "Họp giao ban",
        Mode = MeetingMode.InPerson,
        Location = "Phòng 1",
        PlannedStartTime = Now.AddDays(1),
        PlannedEndTime = Now.AddDays(1).AddHours(1),
    };

    private static MeetingParticipant AddRoster(
        Meeting meeting, MeetingRole role, string name, bool active = true)
    {
        var user = new User { FullName = name, Email = $"{Guid.CreateVersion7():N}@example.vn", IsActive = active };
        var participant = new MeetingParticipant { MeetingId = meeting.Id, UserId = user.Id, User = user, Role = role };
        meeting.Participants.Add(participant);
        return participant;
    }

    private static MeetingGuestInvitation AddStageGuest(StageSetup setup, Meeting meeting)
    {
        var guest = new MeetingGuestInvitation
        {
            MeetingId = meeting.Id,
            InvitedName = "Khách",
            Email = $"{Guid.CreateVersion7():N}@guests.test",
        };
        setup.Dispatches.Guests.Add(guest);
        guest.InitialInvitationDeliveryKey = $"guest:{guest.Id:N}";
        return guest;
    }

    [Fact(DisplayName = "MeetingInvitationEvents.StageCancellationAsync/UTC001: everyone emailed, actor gets no bell")]
    [Trait("UTC", "MeetingInvitationEvents.StageCancellationAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Cancel_FullRoster_EmailsEveryoneRingsMemberOnly()
    {
        var setup = new StageSetup(new(), new());
        var meeting = NewStageMeeting();
        var actor = AddRoster(meeting, MeetingRole.Chair, "An");
        var member = AddRoster(meeting, MeetingRole.Member, "Bình");
        AddStageGuest(setup, meeting);
        var historyId = Guid.CreateVersion7();

        await new MeetingInvitationEvents(setup.Dispatches, setup.Notes)
            .StageCancellationAsync(meeting, historyId, actor.UserId, "Hoãn do lịch bận", Now);

        Assert.Equal(3, setup.Dispatches.Added.Count(d =>
            d.EventKind == InvitationEventKind.Cancelled && d.EventId == historyId
            && d.DeduplicationKey.StartsWith($"roster:{historyId}:Cancelled:", StringComparison.Ordinal)));
        Assert.Equal(member.UserId, setup.Notes.Added.Single(n => n.Title == "Cuộc họp đã bị huỷ").UserId);
        Assert.Equal("Hoãn do lịch bận", JsonSerializer.Deserialize<InvitationPayload>(
            setup.Dispatches.Added[0].PayloadJson)!.Reason);
    }

    [Fact(DisplayName = "MeetingInvitationEvents.StageCancellationAsync/UTC002: lone actor is emailed without a bell")]
    [Trait("UTC", "MeetingInvitationEvents.StageCancellationAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Cancel_LoneActor_EmailsActorWithoutBell()
    {
        var setup = new StageSetup(new(), new());
        var meeting = NewStageMeeting();
        var actor = AddRoster(meeting, MeetingRole.Chair, "An");

        await new MeetingInvitationEvents(setup.Dispatches, setup.Notes)
            .StageCancellationAsync(meeting, Guid.CreateVersion7(), actor.UserId, "Bận", Now);

        Assert.Single(setup.Dispatches.Added);
        Assert.Equal(actor.Id, setup.Dispatches.Added[0].ParticipantId);
        Assert.Empty(setup.Notes.Added);
    }

    [Fact(DisplayName = "MeetingInvitationEvents.StageCancellationAsync/UTC003: empty roster stages nothing")]
    [Trait("UTC", "MeetingInvitationEvents.StageCancellationAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Cancel_EmptyRoster_StagesNothing()
    {
        var setup = new StageSetup(new(), new());
        var meeting = NewStageMeeting();

        await new MeetingInvitationEvents(setup.Dispatches, setup.Notes)
            .StageCancellationAsync(meeting, Guid.CreateVersion7(), Guid.CreateVersion7(), "Bận", Now);

        Assert.Empty(setup.Dispatches.Added);
        Assert.Empty(setup.Notes.Added);
        Assert.Empty(meeting.Participants);
    }

    // ---- MeetingInvitationEvents.StagePublicationInvitationsAsync ----

    [Fact(DisplayName = "MeetingInvitationEvents.StagePublicationInvitationsAsync/UTC001: actives and guest are invited")]
    [Trait("UTC", "MeetingInvitationEvents.StagePublicationInvitationsAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Publish_FullRoster_InvitesActivesAndGuest()
    {
        var setup = new StageSetup(new(), new());
        var meeting = NewStageMeeting();
        AddRoster(meeting, MeetingRole.Chair, "An");
        var member = AddRoster(meeting, MeetingRole.Member, "Bình");
        AddRoster(meeting, MeetingRole.Member, "Nghỉ", active: false);
        AddStageGuest(setup, meeting);

        await new MeetingInvitationEvents(setup.Dispatches, setup.Notes)
            .StagePublicationInvitationsAsync(meeting, Now);

        Assert.Equal(3, setup.Dispatches.Added.Count);
        Assert.Equal(3, setup.Dispatches.Added.Count(d =>
            d.EventKind == InvitationEventKind.Invitation && d.DeduplicationKey.Length > 0));
        Assert.Empty(setup.Notes.Added);
    }

    [Fact(DisplayName = "MeetingInvitationEvents.StagePublicationInvitationsAsync/UTC002: duplicate and removed seat invite once")]
    [Trait("UTC", "MeetingInvitationEvents.StagePublicationInvitationsAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Publish_DuplicateAndRemoved_InvitesTheUserOnce()
    {
        var setup = new StageSetup(new(), new());
        var meeting = NewStageMeeting();
        var userId = Guid.CreateVersion7();
        var first = AddRoster(meeting, MeetingRole.Member, "Bình");
        var second = AddRoster(meeting, MeetingRole.Secretary, "Bình");
        second.UserId = userId;
        first.UserId = userId;
        var removed = AddRoster(meeting, MeetingRole.Member, "Đã rút");
        removed.AssignmentStatus = AssignmentStatus.Removed;

        await new MeetingInvitationEvents(setup.Dispatches, setup.Notes)
            .StagePublicationInvitationsAsync(meeting, Now);

        Assert.Single(setup.Dispatches.Added);
        Assert.Equal(first.Id, setup.Dispatches.Added[0].ParticipantId);
        Assert.DoesNotContain(setup.Dispatches.Added, d => d.ParticipantId == removed.Id);
    }

    // ---- MeetingInvitationEvents.StageRescheduleInvitationsAsync ----

    [Fact(DisplayName = "MeetingInvitationEvents.StageRescheduleInvitationsAsync/UTC001: everyone emailed with old time")]
    [Trait("UTC", "MeetingInvitationEvents.StageRescheduleInvitationsAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Reschedule_FullRoster_EmailsEveryoneWithOldTime()
    {
        var setup = new StageSetup(new(), new());
        var meeting = NewStageMeeting();
        AddRoster(meeting, MeetingRole.Chair, "An");
        AddRoster(meeting, MeetingRole.Member, "Bình");
        AddStageGuest(setup, meeting);
        var historyId = Guid.CreateVersion7();
        var previousStart = new DateTimeOffset(2026, 11, 19, 2, 0, 0, TimeSpan.Zero);
        var previousEnd = previousStart.AddHours(1.5);
        meeting.PlannedStartTime = previousStart.AddHours(5);
        meeting.PlannedEndTime = previousStart.AddHours(6.5);

        await new MeetingInvitationEvents(setup.Dispatches, setup.Notes)
            .StageRescheduleInvitationsAsync(meeting, historyId, (previousStart, previousEnd), Now);

        Assert.Equal(3, setup.Dispatches.Added.Count(d =>
            d.EventKind == InvitationEventKind.Rescheduled && d.EventId == historyId
            && d.DeduplicationKey.StartsWith($"roster:{historyId}:Rescheduled:", StringComparison.Ordinal)));
        var payload = JsonSerializer.Deserialize<InvitationPayload>(setup.Dispatches.Added[0].PayloadJson)!;
        Assert.Equal((previousStart, previousEnd), (payload.PreviousStartsAt, payload.PreviousEndsAt));
        Assert.Empty(setup.Notes.Added);
    }

    [Fact(DisplayName = "MeetingInvitationEvents.StageRescheduleInvitationsAsync/UTC002: empty roster stages nothing")]
    [Trait("UTC", "MeetingInvitationEvents.StageRescheduleInvitationsAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Reschedule_EmptyRoster_StagesNothing()
    {
        var setup = new StageSetup(new(), new());
        var meeting = NewStageMeeting();

        await new MeetingInvitationEvents(setup.Dispatches, setup.Notes)
            .StageRescheduleInvitationsAsync(
                meeting, Guid.CreateVersion7(), (Now.AddDays(1), Now.AddDays(1).AddHours(1)), Now);

        Assert.Empty(setup.Dispatches.Added);
        Assert.Empty(setup.Notes.Added);
        Assert.Empty(meeting.Participants);
    }

    // ---- MeetingReminderEmails.QueueDueAsync ----

    private sealed record ReminderSetup(
        Wave2ReminderQuery Query,
        FakeEmailMessageRepository Emails,
        FakeUnitOfWork UnitOfWork,
        Wave2Links Links);

    private static ReminderSetup NewReminder() => new(new(), new(), new(), new());

    private static MeetingReminderEmails ReminderService(ReminderSetup setup) => new(
        setup.UnitOfWork, setup.Query, setup.Emails, setup.Links,
        Options.Create(new EmailDeliveryOptions { ApplicationBaseUrl = "https://tc.example.vn" }),
        new FixedClock(Now));

    private static Meeting NewReminderMeeting(
        ReminderSetup setup, MeetingMode mode = MeetingMode.InPerson, string? onlineLink = null)
    {
        var meeting = new Meeting
        {
            Title = "Họp giao ban",
            Status = MeetingStatus.Published,
            Mode = mode,
            Location = "Phòng 1",
            OnlineLink = onlineLink,
            PlannedStartTime = Now.AddMinutes(10),
            PlannedEndTime = Now.AddMinutes(70),
            CreatedById = Guid.CreateVersion7(),
        };
        setup.Query.Meetings.Add(new ReminderMeeting(meeting, "An", "an@example.vn"));
        return meeting;
    }

    private static ReminderParticipant AddInvitee(
        ReminderSetup setup, Meeting meeting, MeetingRole role, string name)
    {
        var participant = new ReminderParticipant(
            Guid.CreateVersion7(), meeting.Id, role, name, $"{Guid.CreateVersion7():N}@example.vn");
        setup.Query.Participants.Add(participant);
        return participant;
    }

    [Fact(DisplayName = "MeetingReminderEmails.QueueDueAsync/UTC001: no starting meeting queues nothing")]
    [Trait("UTC", "MeetingReminderEmails.QueueDueAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Reminders_NoStartingMeeting_QueuesNothing()
    {
        var setup = NewReminder();

        var queued = await ReminderService(setup).QueueDueAsync();

        Assert.Equal(0, queued);
        Assert.Empty(setup.Emails.Added);
        Assert.Equal(0, setup.UnitOfWork.Saves);
    }

    [Fact(DisplayName = "MeetingReminderEmails.QueueDueAsync/UTC002: in-person meeting reminds both participants")]
    [Trait("UTC", "MeetingReminderEmails.QueueDueAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Reminders_InPersonMeeting_RemindsBothParticipants()
    {
        var setup = NewReminder();
        var meeting = NewReminderMeeting(setup);
        var chair = AddInvitee(setup, meeting, MeetingRole.Chair, "Bình");
        var member = AddInvitee(setup, meeting, MeetingRole.Member, "Cường");
        var prefix = $"{MeetingReminderEmails.KeyPrefix}{meeting.Id:D}:{meeting.PlannedStartTime.ToUnixTimeSeconds()}:";

        var queued = await ReminderService(setup).QueueDueAsync();

        Assert.Equal(2, queued);
        Assert.Equal(
            new[] { $"{prefix}{chair.Id:D}", $"{prefix}{member.Id:D}" }.OrderBy(k => k),
            setup.Emails.Added.Select(e => e.DeduplicationKey).OrderBy(k => k));
        Assert.Equal(1, setup.UnitOfWork.Saves);
    }

    [Fact(DisplayName = "MeetingReminderEmails.QueueDueAsync/UTC003: TrustCabinet room meeting links the room")]
    [Trait("UTC", "MeetingReminderEmails.QueueDueAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Reminders_OnlineRoomMeeting_LinksTheRoom()
    {
        var setup = NewReminder();
        var meeting = NewReminderMeeting(setup, MeetingMode.Online);
        AddInvitee(setup, meeting, MeetingRole.Member, "Bình");
        var roomUrl = $"https://tc.example.vn/mgmt/meetings/{meeting.Id:D}/room";

        var queued = await ReminderService(setup).QueueDueAsync();

        Assert.Equal(1, queued);
        Assert.Contains(roomUrl, setup.Emails.Added[0].Body, StringComparison.Ordinal);
        Assert.Contains("Vào phòng họp", setup.Emails.Added[0].HtmlBody, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "MeetingReminderEmails.QueueDueAsync/UTC004: external-link meeting links its page")]
    [Trait("UTC", "MeetingReminderEmails.QueueDueAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Reminders_ExternalLinkMeeting_LinksTheMeetingPage()
    {
        var setup = NewReminder();
        var meeting = NewReminderMeeting(setup, MeetingMode.Online, "https://meet.example.vn/abc");
        AddInvitee(setup, meeting, MeetingRole.Member, "Bình");
        var pageUrl = $"https://tc.example.vn/mgmt/meetings/{meeting.Id:D}";

        var queued = await ReminderService(setup).QueueDueAsync();

        Assert.Equal(1, queued);
        Assert.Contains(pageUrl, setup.Emails.Added[0].Body, StringComparison.Ordinal);
        Assert.Contains("Xem cuộc họp", setup.Emails.Added[0].HtmlBody, StringComparison.Ordinal);
    }

    // ---- Small private fakes owned by this file ----

    private sealed class Wave2Links : IGuestInvitationLinkProvider
    {
        public bool Available { get; set; } = true;

        public GuestLinkStatus Status { get; set; } = GuestLinkStatus.Ready;

        public string Url { get; set; } = "https://tc.example.vn/guest/verify?t=abc";

        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(Available);

        public Task<GuestInvitationLink> PrepareAsync(Guid invitationId, CancellationToken ct)
            => Task.FromResult(Status == GuestLinkStatus.Ready
                ? new GuestInvitationLink(Status, Url)
                : new GuestInvitationLink(Status));
    }

    private sealed class Wave2Dispatches : IMeetingInvitationDispatchRepository
    {
        public readonly Dictionary<Guid, MeetingInvitationDispatch> Rows = new();

        public MeetingStatus Status { get; set; } = MeetingStatus.Published;

        public HashSet<Guid> InactiveParticipants { get; } = [];

        public void Add(MeetingInvitationDispatch dispatch) => Rows[dispatch.Id] = dispatch;

        public Task<MeetingGuestInvitation[]> ListActiveGuestInvitationsAsync(
            Guid meetingId, CancellationToken ct = default)
            => Task.FromResult(Array.Empty<MeetingGuestInvitation>());

        public Task<Guid[]> ListDueIdsAsync(DateTimeOffset now, int take, CancellationToken ct = default)
            => Task.FromResult(Rows.Values.Select(d => d.Id).Take(take).ToArray());

        public Task<MeetingInvitationDispatch?> FindAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Rows.TryGetValue(id, out var dispatch) ? dispatch : null);

        public Task<MeetingInvitationDispatch?> LockAsync(Guid meetingId, Guid id, CancellationToken ct = default)
            => Task.FromResult(Rows.TryGetValue(id, out var dispatch) ? dispatch : null);

        public Task<bool> IsGuestInvitationActiveAsync(Guid? guestInvitationId, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> IsParticipantActiveAsync(Guid participantId, CancellationToken ct = default)
            => Task.FromResult(!InactiveParticipants.Contains(participantId));

        public Task<bool> IsMeetingClosedAsync(Guid? meetingId, CancellationToken ct = default)
            => Task.FromResult(Status is MeetingStatus.Ended or MeetingStatus.Cancelled);

        public Task<MeetingStatus> GetMeetingStatusAsync(Guid? meetingId, CancellationToken ct = default)
            => Task.FromResult(Status);

        public Task<string?> FindCurrentChairNameAsync(Guid? meetingId, CancellationToken ct = default)
            => Task.FromResult<string?>("Nguyễn Chủ Toạ");

        public Task<MeetingOrganizer> GetOrganizerAsync(Guid? meetingId, CancellationToken ct = default)
            => Task.FromResult(new MeetingOrganizer("Trần Tổ Chức", "organizer@example.vn"));

        public Task<string?> GetParticipantInitialKeyAsync(Guid participantId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public Task<string?> GetGuestInitialKeyAsync(Guid? guestInvitationId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public Task<bool> HasMaterializedInvitationAsync(
            Guid? meetingId, Guid? participantId, Guid? guestInvitationId, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<int> SuppressAsync(Guid id, DateTimeOffset at, CancellationToken ct = default)
        {
            Rows[id].State = InvitationDispatchState.Suppressed;
            return Task.FromResult(1);
        }

        public Task<int> BlockOnDependencyAsync(
            Guid id, DateTimeOffset nextAttemptAt, DateTimeOffset at, CancellationToken ct = default)
            => Task.FromResult(1);

        public Task<int> RecordFailedAttemptAsync(
            Guid id, int maxAttempts, DateTimeOffset retryAt, string error, DateTimeOffset at,
            CancellationToken ct = default)
            => Task.FromResult(1);
    }

    private sealed class Wave2ReminderQuery : IMeetingReminderQuery
    {
        public List<ReminderMeeting> Meetings { get; } = [];

        public List<ReminderParticipant> Participants { get; } = [];

        public List<ReminderGuest> Guests { get; } = [];

        public List<ReminderInvitation> Invitations { get; } = [];

        public List<string> Keys { get; } = [];

        public Task<IReadOnlyList<ReminderMeeting>> ListStartingAsync(
            DateTimeOffset now, DateTimeOffset cutoff, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ReminderMeeting>>(Meetings
                .Where(m => m.Meeting.PlannedStartTime > now && m.Meeting.PlannedStartTime <= cutoff).ToList());

        public Task<IReadOnlyList<ReminderParticipant>> ListParticipantsAsync(
            Guid[] meetingIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ReminderParticipant>>(
                Participants.Where(p => meetingIds.Contains(p.MeetingId)).ToList());

        public Task<IReadOnlyList<ReminderGuest>> ListGuestsAsync(
            Guid[] meetingIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ReminderGuest>>(
                Guests.Where(g => meetingIds.Contains(g.MeetingId)).ToList());

        public Task<IReadOnlyList<ReminderInvitation>> ListInvitationsAsync(
            Guid[] meetingIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ReminderInvitation>>(
                Invitations.Where(i => meetingIds.Contains(i.MeetingId)).ToList());

        public Task<IReadOnlyList<string>> ListReminderKeysAsync(
            Guid[] meetingIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(Keys.ToList());
    }
}
