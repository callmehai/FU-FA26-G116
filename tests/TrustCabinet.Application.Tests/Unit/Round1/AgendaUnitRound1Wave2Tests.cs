using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 unit tests, wave 2 (đợt 2) for FE-05 (Agenda): AgendaUnitRound1Wave2.
// Tester: Phạm Gia Khánh. Covers the 13 UTC keys missing after đợt 1:
// Delete UTC003/UTC004, Reorder UTC003/UTC004, Start UTC003/UTC004,
// Complete UTC002/UTC005, RecordConclusion UTC003/UTC005/UTC006,
// Notify UTC002/UTC003.
// Every test carries [Trait("UTC", ...)] + [Trait("Trait", "UTC")] and the
// "<Class.Method>/<UTCnnn>" string in its DisplayName, so the trx (which has
// no traits) is matched back to the UTC rows by name.
// Note: the JSON rows for UTC004 of Delete/Reorder carry file-validation
// inputs from another sheet; the tests below follow each row's note
// (Delete UTC004 note cites MSG67 = planning denied → Forbidden;
// Reorder UTC003 note cites MSG67 with the Draft-deviation → NotFound).
public sealed class AgendaUnitRound1Wave2Tests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAgendaRepository _agenda = new();
    private readonly FakeAgendaDocumentRepository _documents = new();
    private readonly FakeAgendaEffects _effects;
    private readonly Guid _chair = Guid.CreateVersion7();
    private readonly Guid _secretary = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();
    private readonly Guid _organizer = Guid.CreateVersion7();

    public AgendaUnitRound1Wave2Tests() => _effects = new(_unitOfWork);

    private AgendaService Service() => new(_meetings, _unitOfWork,
        _agenda, _documents, _effects, new FixedClock(Now),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<AgendaService>.Instance, _effects, _effects);

    private Meeting GivenMeeting(MeetingStatus status)
    {
        var meeting = new Meeting
        {
            Title = "Họp giao ban", Status = status, CreatedById = _organizer,
            PlannedStartTime = Now.AddHours(1), PlannedEndTime = Now.AddHours(2),
        };
        foreach (var (userId, role) in new List<(Guid, MeetingRole)> { (_chair, MeetingRole.Chair), (_secretary, MeetingRole.Secretary), (_member, MeetingRole.Member) })
        {
            meeting.Participants.Add(new MeetingParticipant
            {
                MeetingId = meeting.Id, UserId = userId, Role = role, AssignmentStatus = AssignmentStatus.Current,
                Response = InvitationResponse.Accepted,
                User = new User { Id = userId, FullName = $"{role}", Email = $"{userId}@trustcabinet.local", IsActive = true },
            });
            _meetings.ActiveUsers.Add(userId);
        }
        _meetings.ActiveUsers.Add(_organizer);
        _meetings.Meetings.Add(meeting);
        return meeting;
    }

    private static MeetingParticipant Seat(Guid meetingId, Guid userId, MeetingRole role) => new()
    {
        MeetingId = meetingId, UserId = userId, Role = role, AssignmentStatus = AssignmentStatus.Current,
        Response = InvitationResponse.Accepted,
        User = new User { Id = userId, FullName = $"{role}", Email = $"{userId}@trustcabinet.local", IsActive = true },
    };

    private static AgendaItem Item(Guid meetingId, string title, int position,
        AgendaItemStatus status = AgendaItemStatus.Pending) =>
        new() { MeetingId = meetingId, Title = title, Position = position, Status = status };

    // ---- AgendaService.DeleteAsync ----

    [Fact(DisplayName = "AgendaService.DeleteAsync/UTC003: unknown item is not found")]
    [Trait("UTC", "AgendaService.DeleteAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Delete_UnknownItemIsNotFound()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);

        var outcome = await Service().DeleteAsync(meeting.Id, Guid.CreateVersion7(), _secretary);

        Assert.Equal(AgendaFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.ItemId);
        Assert.Empty(_effects.Calls);
    }

    [Fact(DisplayName = "AgendaService.DeleteAsync/UTC004: member without a planner seat is forbidden")]
    [Trait("UTC", "AgendaService.DeleteAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Delete_MemberWithoutPlannerSeatIsForbidden()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        var item = Item(meeting.Id, "Mục 1", 1);
        _agenda.Items.Add(item);

        var outcome = await Service().DeleteAsync(meeting.Id, item.Id, _member);

        Assert.Equal(AgendaFailure.Forbidden, outcome.Failure);
        Assert.Empty(_agenda.Removed);
        Assert.Empty(_effects.Calls);
    }

    // ---- AgendaService.ReorderAsync ----

    [Fact(DisplayName = "AgendaService.ReorderAsync/UTC003: member cannot reorder an unreadable draft")]
    [Trait("UTC", "AgendaService.ReorderAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Reorder_MemberCannotReorderAnUnreadableDraft()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        var p1 = Item(meeting.Id, "P1", 1); var p2 = Item(meeting.Id, "P2", 2); var p3 = Item(meeting.Id, "P3", 3);
        _agenda.Items.AddRange([p1, p2, p3]);
        var version = AgendaRules.OrderVersion(_agenda.Items);

        var outcome = await Service().ReorderAsync(meeting.Id, _member,
            new AgendaInput(OrderVersion: version, OrderedItemIds: [p3.Id, p1.Id, p2.Id]));

        Assert.Equal(AgendaFailure.NotFound, outcome.Failure);
        Assert.Equal((1, 2, 3), (p1.Position, p2.Position, p3.Position));
        Assert.Empty(_effects.Calls);
    }

    [Fact(DisplayName = "AgendaService.ReorderAsync/UTC004: store failure answers save failed")]
    [Trait("UTC", "AgendaService.ReorderAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Reorder_StoreFailureAnswersSaveFailed()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        var p1 = Item(meeting.Id, "P1", 1); var p2 = Item(meeting.Id, "P2", 2); var p3 = Item(meeting.Id, "P3", 3);
        _agenda.Items.AddRange([p1, p2, p3]);
        var version = AgendaRules.OrderVersion(_agenda.Items);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().ReorderAsync(meeting.Id, _secretary,
            new AgendaInput(OrderVersion: version, OrderedItemIds: [p3.Id, p1.Id, p2.Id]));

        Assert.Equal(AgendaFailure.SaveFailed, outcome.Failure);
        Assert.Equal(1, _unitOfWork.Rollbacks);
        Assert.Empty(_effects.Calls);
    }

    // ---- AgendaService.StartAsync ----

    [Fact(DisplayName = "AgendaService.StartAsync/UTC003: running item cannot start again")]
    [Trait("UTC", "AgendaService.StartAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Start_RunningItemCannotStartAgain()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var item = Item(meeting.Id, "Mục 1", 1, AgendaItemStatus.InProgress);
        _agenda.Items.Add(item);

        var outcome = await Service().StartAsync(meeting.Id, item.Id, _chair);

        Assert.Equal(AgendaFailure.CannotStart, outcome.Failure);
        Assert.Equal(AgendaItemStatus.InProgress, item.Status);
        Assert.Empty(_effects.Calls);
    }

    [Fact(DisplayName = "AgendaService.StartAsync/UTC004: unknown item is not found")]
    [Trait("UTC", "AgendaService.StartAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Start_UnknownItemIsNotFound()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);

        var outcome = await Service().StartAsync(meeting.Id, Guid.CreateVersion7(), _chair);

        Assert.Equal(AgendaFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.ItemId);
        Assert.Empty(_effects.Calls);
    }

    // ---- AgendaService.CompleteAsync ----

    [Fact(DisplayName = "AgendaService.CompleteAsync/UTC002: started this instant completes at its start")]
    [Trait("UTC", "AgendaService.CompleteAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Complete_StartedThisInstantCompletesAtItsStart()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var item = Item(meeting.Id, "Mục 1", 1, AgendaItemStatus.InProgress);
        item.ActualStartedAt = Now;
        _agenda.Items.Add(item);

        var outcome = await Service().CompleteAsync(meeting.Id, item.Id, _chair);

        Assert.Equal(AgendaFailure.None, outcome.Failure);
        Assert.Equal(item.ActualStartedAt, item.ActualEndedAt);
        Assert.Equal(["publish:Completed:commits=1"], _effects.Calls);
    }

    [Fact(DisplayName = "AgendaService.CompleteAsync/UTC005: unknown item is not found")]
    [Trait("UTC", "AgendaService.CompleteAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Complete_UnknownItemIsNotFound()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);

        var outcome = await Service().CompleteAsync(meeting.Id, Guid.CreateVersion7(), _chair);

        Assert.Equal(AgendaFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.ItemId);
        Assert.Empty(_effects.Calls);
    }

    // ---- AgendaService.RecordConclusionAsync ----

    [Fact(DisplayName = "AgendaService.RecordConclusionAsync/UTC003: minutes lock the conclusion")]
    [Trait("UTC", "AgendaService.RecordConclusionAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task RecordConclusion_MinutesLockTheConclusion()
    {
        var meeting = GivenMeeting(MeetingStatus.Ended);
        var item = Item(meeting.Id, "Mục 1", 1, AgendaItemStatus.Done);
        _agenda.Items.Add(item);
        _agenda.HasMinutes = true;

        var outcome = await Service().RecordConclusionAsync(meeting.Id, item.Id, _chair,
            new AgendaInput(Conclusion: "x"));

        Assert.Equal(AgendaFailure.Locked, outcome.Failure);
        Assert.Null(item.Conclusion);
        Assert.Empty(_effects.Calls);
    }

    [Fact(DisplayName = "AgendaService.RecordConclusionAsync/UTC005: non-JSON body answers unsupported media")]
    [Trait("UTC", "AgendaService.RecordConclusionAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task RecordConclusion_NonJsonBodyAnswersUnsupportedMedia()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var item = Item(meeting.Id, "Mục 1", 1, AgendaItemStatus.InProgress);
        _agenda.Items.Add(item);

        var outcome = await Service().RecordConclusionAsync(meeting.Id, item.Id, _chair,
            new AgendaInput(ParseFailure: AgendaFailure.UnsupportedMedia));

        Assert.Equal(AgendaFailure.UnsupportedMedia, outcome.Failure);
        Assert.True(outcome.Errors!.ContainsKey("body"));
        Assert.Null(item.Conclusion);
    }

    [Fact(DisplayName = "AgendaService.RecordConclusionAsync/UTC006: store failure answers save failed")]
    [Trait("UTC", "AgendaService.RecordConclusionAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task RecordConclusion_StoreFailureAnswersSaveFailed()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var item = Item(meeting.Id, "Mục 1", 1, AgendaItemStatus.InProgress);
        _agenda.Items.Add(item);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().RecordConclusionAsync(meeting.Id, item.Id, _chair,
            new AgendaInput(Conclusion: "Kết luận."));

        Assert.Equal(AgendaFailure.SaveFailed, outcome.Failure);
        Assert.Equal(1, _unitOfWork.Rollbacks);
        Assert.Empty(_effects.Calls);
    }

    // ---- AgendaPresenterNotifier.NotifyAsync ----

    private FakeNotificationRepository NotifyNotifications(FakeEmailMessageRepository emails)
    {
        var notifications = new FakeNotificationRepository();
        var services = new List<object> { _unitOfWork, _agenda, notifications, emails, new ReadyGuestLinks() };
        Scopes = new FakeServiceScopeFactory(services.ToArray());
        return notifications;
    }

    private FakeServiceScopeFactory Scopes { get; set; } = null!;

    private sealed class ReadyGuestLinks : IGuestInvitationLinkProvider
    {
        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);
        public Task<GuestInvitationLink> PrepareAsync(Guid invitationId, CancellationToken ct)
            => Task.FromResult(new GuestInvitationLink(GuestLinkStatus.Ready, $"https://hop.example.vn/guest/{invitationId:D}#token=t"));
    }

    private (Meeting Meeting, AgendaItem Item, Guid Presenter) GivenAssignment(string title = "Q3 review")
    {
        var meeting = new Meeting
        {
            Title = "Budget", Status = MeetingStatus.Published, CreatedById = _organizer,
            PlannedStartTime = new(2026, 12, 1, 2, 0, 0, TimeSpan.Zero),
            PlannedEndTime = new(2026, 12, 1, 3, 0, 0, TimeSpan.Zero),
        };
        _agenda.Meetings.Add(meeting);
        var presenter = Guid.CreateVersion7();
        var item = new AgendaItem { MeetingId = meeting.Id, Title = title, Position = 1, PresenterId = presenter };
        _agenda.Items.Add(item);
        return (meeting, item, presenter);
    }

    [Fact(DisplayName = "AgendaPresenterNotifier.NotifyAsync/UTC002: duplicate delivery adds nothing")]
    [Trait("UTC", "AgendaPresenterNotifier.NotifyAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Notify_DuplicateDeliveryAddsNothing()
    {
        var (meeting, item, presenter) = GivenAssignment();
        meeting.Participants.Add(Seat(meeting.Id, presenter, MeetingRole.Member));
        _agenda.Participations.Add(Seat(meeting.Id, presenter, MeetingRole.Member));
        var emails = new FakeEmailMessageRepository();
        var notifications = NotifyNotifications(emails);
        var assignment = new AgendaPresenterAssignment(Guid.CreateVersion7(), meeting.Id, item.Id, presenter, null, Now);
        notifications.Rows.Add(new Notification
        {
            Title = "Phân công trình bày mục nghị sự",
            DeduplicationKey = $"agenda-presenter:{assignment.EventId:D}:{presenter:D}",
        });

        await new AgendaPresenterNotifier(Scopes).NotifyAsync(assignment, default);

        Assert.Empty(notifications.Added);
        Assert.Empty(emails.Added);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
    }

    [Fact(DisplayName = "AgendaPresenterNotifier.NotifyAsync/UTC003: removed presenter gets nothing")]
    [Trait("UTC", "AgendaPresenterNotifier.NotifyAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Notify_RemovedPresenterGetsNothing()
    {
        var (meeting, item, presenter) = GivenAssignment();
        var emails = new FakeEmailMessageRepository();
        var notifications = NotifyNotifications(emails);
        var assignment = new AgendaPresenterAssignment(Guid.CreateVersion7(), meeting.Id, item.Id, presenter, null, Now);

        await new AgendaPresenterNotifier(Scopes).NotifyAsync(assignment, default);

        Assert.Empty(notifications.Added);
        Assert.Empty(emails.Added);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
    }
}
