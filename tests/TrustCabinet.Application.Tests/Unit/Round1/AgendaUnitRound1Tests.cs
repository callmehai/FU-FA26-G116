using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 unit tests for FE-05 (Agenda): AgendaUnitRound1.
// Every test carries [Trait("UTC", ...)] + [Trait("Trait", "UTC")] and the
// "<Class.Method>/<UTCnnn>" string in its DisplayName, so the trx (which has
// no traits) is matched back to the UTC rows by name.
public sealed class AgendaUnitRound1Tests
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

    public AgendaUnitRound1Tests() => _effects = new(_unitOfWork);

    private AgendaService Service(ILogger<AgendaService>? logger = null) => new(_meetings, _unitOfWork,
        _agenda, _documents, _effects, new FixedClock(Now),
        logger ?? NullAgendaLog.Instance, _effects, _effects);

    private sealed class NullAgendaLog : ILogger<AgendaService>
    {
        public static readonly NullAgendaLog Instance = new();
        IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel level) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) { }
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class CapturingAgendaLog : ILogger<AgendaService>
    {
        public List<string> Messages { get; } = [];
        IDisposable ILogger.BeginScope<TState>(TState state) => CapturingAgendaLog.NullScope.Instance;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private Meeting GivenMeeting(MeetingStatus status, bool organizerSeat = false)
    {
        var meeting = new Meeting
        {
            Title = "Họp giao ban", Status = status, CreatedById = _organizer,
            PlannedStartTime = Now.AddHours(1), PlannedEndTime = Now.AddHours(2),
        };
        var roster = new List<(Guid, MeetingRole)> { (_chair, MeetingRole.Chair), (_secretary, MeetingRole.Secretary), (_member, MeetingRole.Member) };
        if (organizerSeat) roster.Add((_organizer, MeetingRole.Member));
        foreach (var (userId, role) in roster)
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

    private static MeetingParticipant Seat(Guid meetingId, Guid userId, MeetingRole role,
        AssignmentStatus assignment = AssignmentStatus.Current, bool active = true) => new()
        {
            MeetingId = meetingId, UserId = userId, Role = role, AssignmentStatus = assignment,
            Response = InvitationResponse.Accepted,
            User = new User { Id = userId, FullName = $"{role}", Email = $"{userId}@trustcabinet.local", IsActive = active },
        };

    private static Meeting RulesMeeting(MeetingStatus status, Guid actor, MeetingRole role,
        AssignmentStatus assignment = AssignmentStatus.Current, bool active = true)
    {
        var meeting = new Meeting { Title = "Họp giao ban", Status = status, CreatedById = Guid.CreateVersion7() };
        meeting.Participants.Add(Seat(meeting.Id, actor, role, assignment, active));
        return meeting;
    }

    private static AgendaItem Item(Guid meetingId, string title, int position,
        AgendaItemStatus status = AgendaItemStatus.Pending) =>
        new() { MeetingId = meetingId, Title = title, Position = position, Status = status };

    // ---- AgendaRules.Actions ----

    [Fact(DisplayName = "AgendaRules.Actions/UTC001: chair plans a pending item while nothing runs")]
    [Trait("UTC", "AgendaRules.Actions/UTC001")]
    [Trait("Trait", "UTC")]
    public void Actions_ChairMayPlanAPendingItemWhileNothingRuns()
    {
        var meeting = RulesMeeting(MeetingStatus.InProgress, _chair, MeetingRole.Chair);
        var item = Item(meeting.Id, "Mục 1", 1);

        var actions = AgendaRules.Actions(meeting, _chair, item, [item, Item(meeting.Id, "Mục 2", 2)]);

        Assert.Equal((true, true, true, true, false, false, true),
            (actions.CanEdit, actions.CanEditPlanningFields, actions.CanDelete, actions.CanSetCurrent,
                actions.CanComplete, actions.CanRecordConclusion, actions.CanLinkDocuments));
        Assert.False(AgendaRules.Actions(meeting, _chair, item, [item], true).CanRecordConclusion);
        Assert.Equal(actions, AgendaRules.Actions(meeting, _chair, item, [item, Item(meeting.Id, "Mục 2", 2)]));
    }

    [Fact(DisplayName = "AgendaRules.Actions/UTC002: secretary plans but never runs an item")]
    [Trait("UTC", "AgendaRules.Actions/UTC002")]
    [Trait("Trait", "UTC")]
    public void Actions_SecretaryPlansButNeverRunsAnItem()
    {
        var meeting = RulesMeeting(MeetingStatus.InProgress, _secretary, MeetingRole.Secretary);
        var item = Item(meeting.Id, "Mục 1", 1);

        var actions = AgendaRules.Actions(meeting, _secretary, item, [item]);

        Assert.True(actions.CanEdit);
        Assert.True(actions.CanDelete);
        Assert.Equal((false, false, true), (actions.CanSetCurrent, actions.CanComplete, actions.CanLinkDocuments));
    }

    [Fact(DisplayName = "AgendaRules.Actions/UTC003: member sees no action")]
    [Trait("UTC", "AgendaRules.Actions/UTC003")]
    [Trait("Trait", "UTC")]
    public void Actions_MemberSeesNoAction()
    {
        var meeting = RulesMeeting(MeetingStatus.InProgress, _member, MeetingRole.Member);
        var item = Item(meeting.Id, "Mục 1", 1);

        var actions = AgendaRules.Actions(meeting, _member, item, [item]);

        Assert.Equal(new AgendaActions(false, false, false, false, false, false, false), actions);
        Assert.False(actions.CanEdit);
        Assert.False(actions.CanLinkDocuments);
    }

    [Fact(DisplayName = "AgendaRules.Actions/UTC004: issued minutes close conclusions")]
    [Trait("UTC", "AgendaRules.Actions/UTC004")]
    [Trait("Trait", "UTC")]
    public void Actions_IssuedMinutesCloseConclusions()
    {
        var meeting = RulesMeeting(MeetingStatus.Ended, _secretary, MeetingRole.Secretary);
        var item = Item(meeting.Id, "Mục 1", 1, AgendaItemStatus.Done);

        var actions = AgendaRules.Actions(meeting, _secretary, item, [item], true);

        Assert.False(actions.CanRecordConclusion);
        Assert.Equal((false, false, false), (actions.CanEdit, actions.CanDelete, actions.CanComplete));
        Assert.True(AgendaRules.Actions(meeting, _secretary, item, [item], false).CanRecordConclusion);
    }

    // ---- AgendaRules.CanRecordConclusion ----

    [Fact(DisplayName = "AgendaRules.CanRecordConclusion/UTC001: chair records on a running item")]
    [Trait("UTC", "AgendaRules.CanRecordConclusion/UTC001")]
    [Trait("Trait", "UTC")]
    public void CanRecordConclusion_ChairRecordsOnARunningItem()
    {
        var meeting = RulesMeeting(MeetingStatus.InProgress, _chair, MeetingRole.Chair);
        var item = Item(meeting.Id, "Mục 1", 1, AgendaItemStatus.InProgress);

        Assert.True(AgendaRules.CanRecordConclusion(meeting, _chair, item, false));
        Assert.False(AgendaRules.CanRecordConclusion(meeting, _member, item, false));
        Assert.True(AgendaRules.CanRecordConclusion(meeting, _chair,
            Item(meeting.Id, "Mục 2", 2, AgendaItemStatus.Done), false));
    }

    [Fact(DisplayName = "AgendaRules.CanRecordConclusion/UTC002: issued minutes refuse")]
    [Trait("UTC", "AgendaRules.CanRecordConclusion/UTC002")]
    [Trait("Trait", "UTC")]
    public void CanRecordConclusion_IssuedMinutesRefuse()
    {
        var meeting = RulesMeeting(MeetingStatus.Ended, _chair, MeetingRole.Chair);
        var item = Item(meeting.Id, "Mục 1", 1, AgendaItemStatus.Done);

        Assert.False(AgendaRules.CanRecordConclusion(meeting, _chair, item, true));
        Assert.True(AgendaRules.CanRecordConclusion(meeting, _chair, item, false));
        Assert.False(AgendaRules.CanRecordConclusion(meeting, _chair, Item(meeting.Id, "Mục 2", 2), false));
    }

    [Fact(DisplayName = "AgendaRules.CanRecordConclusion/UTC003: cancelled meeting refuses")]
    [Trait("UTC", "AgendaRules.CanRecordConclusion/UTC003")]
    [Trait("Trait", "UTC")]
    public void CanRecordConclusion_CancelledMeetingRefuses()
    {
        var meeting = RulesMeeting(MeetingStatus.Cancelled, _chair, MeetingRole.Chair);
        var item = Item(meeting.Id, "Mục 1", 1, AgendaItemStatus.Done);

        Assert.False(AgendaRules.CanRecordConclusion(meeting, _chair, item, false));
        Assert.False(AgendaRules.CanRecordConclusion(RulesMeeting(MeetingStatus.Draft, _chair, MeetingRole.Chair),
            _chair, item, false));
        Assert.False(AgendaRules.CanRecordConclusion(meeting, _chair, item, true));
    }

    // ---- AgendaRules.CurrentParticipant ----

    // ---- AgendaRules.HasRole ----

    // ---- AgendaRules.IsPlanner ----

    // ---- AgendaRules.Normalize ----

    // ---- AgendaRules.OrderVersion ----

    [Fact(DisplayName = "AgendaRules.OrderVersion/UTC001: empty agenda hashes the empty text")]
    [Trait("UTC", "AgendaRules.OrderVersion/UTC001")]
    [Trait("Trait", "UTC")]
    public void OrderVersion_EmptyAgendaHashesTheEmptyText()
    {
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            AgendaRules.OrderVersion([]));
        Assert.Equal(64, AgendaRules.OrderVersion([]).Length);
        Assert.Equal(AgendaRules.OrderVersion([]), AgendaRules.OrderVersion([]));
    }

    [Fact(DisplayName = "AgendaRules.OrderVersion/UTC002: one pending item hashes its line")]
    [Trait("UTC", "AgendaRules.OrderVersion/UTC002")]
    [Trait("Trait", "UTC")]
    public void OrderVersion_OnePendingItemHashesItsLine()
    {
        var items = new[] { new AgendaItem { Title = "M", Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), Position = 1, Status = AgendaItemStatus.Pending } };

        Assert.Equal("3fd739a99fdff6a5bd0d80ce84ce251c69aad806ded66b37ffb1d2bea9fde669",
            AgendaRules.OrderVersion(items));
        Assert.NotEqual(AgendaRules.OrderVersion([]), AgendaRules.OrderVersion(items));
        Assert.Equal(AgendaRules.OrderVersion(items), AgendaRules.OrderVersion(items));
    }

    [Fact(DisplayName = "AgendaRules.OrderVersion/UTC003: two pending items join lines in order")]
    [Trait("UTC", "AgendaRules.OrderVersion/UTC003")]
    [Trait("Trait", "UTC")]
    public void OrderVersion_TwoPendingItemsJoinLinesInOrder()
    {
        AgendaItem At(int n) => new() { Title = "M", Id = Guid.Parse($"aaaaaaaa-0000-0000-0000-00000000000{n}"), Position = n, Status = AgendaItemStatus.Pending };

        Assert.Equal("9d4314cd6201dff90158397739a4dd919b61d12d26772ff7ac2163ee7d7b849d",
            AgendaRules.OrderVersion([At(2), At(1)]));
        Assert.Equal(AgendaRules.OrderVersion([At(1), At(2)]), AgendaRules.OrderVersion([At(2), At(1)]));
        Assert.NotEqual(AgendaRules.OrderVersion([At(1)]), AgendaRules.OrderVersion([At(1), At(2)]));
    }

    [Fact(DisplayName = "AgendaRules.OrderVersion/UTC004: a status change moves the version")]
    [Trait("UTC", "AgendaRules.OrderVersion/UTC004")]
    [Trait("Trait", "UTC")]
    public void OrderVersion_AStatusChangeMovesTheVersion()
    {
        AgendaItem At(int n, AgendaItemStatus status) => new() { Title = "M", Id = Guid.Parse($"aaaaaaaa-0000-0000-0000-00000000000{n}"), Position = n, Status = status };

        Assert.Equal("16433c03efffa38a96ab256fcd1b605245e73b9fcd8a0b0ba9987f18b634fdf6",
            AgendaRules.OrderVersion([At(1, AgendaItemStatus.InProgress), At(2, AgendaItemStatus.Pending)]));
        Assert.NotEqual(AgendaRules.OrderVersion([At(1, AgendaItemStatus.Pending), At(2, AgendaItemStatus.Pending)]),
            AgendaRules.OrderVersion([At(1, AgendaItemStatus.InProgress), At(2, AgendaItemStatus.Pending)]));
        Assert.Equal(64, AgendaRules.OrderVersion([At(1, AgendaItemStatus.Done)]).Length);
    }

    [Fact(DisplayName = "AgendaRules.OrderVersion/UTC005: tied positions break by id")]
    [Trait("UTC", "AgendaRules.OrderVersion/UTC005")]
    [Trait("Trait", "UTC")]
    public void OrderVersion_TiedPositionsBreakById()
    {
        AgendaItem At(string tail) => new() { Title = "M", Id = Guid.Parse($"aaaaaaaa-0000-0000-0000-0000000000{tail}"), Position = 1, Status = AgendaItemStatus.Pending };

        Assert.Equal("7e2852961818f17cfe13b6ae7abc5f4ec6b0b8e666bf2128f14992358a1c4f0f",
            AgendaRules.OrderVersion([At("02"), At("01")]));
        Assert.Equal(AgendaRules.OrderVersion([At("01"), At("02")]), AgendaRules.OrderVersion([At("02"), At("01")]));
        Assert.NotEqual(AgendaRules.OrderVersion([At("01")]), AgendaRules.OrderVersion([At("01"), At("02")]));
    }

    // ---- AgendaRules.PlanningAllowed ----

    // ---- AgendaRules.Validate ----

    [Fact(DisplayName = "AgendaRules.Validate/UTC001: in-range input has no errors")]
    [Trait("UTC", "AgendaRules.Validate/UTC001")]
    [Trait("Trait", "UTC")]
    public void Validate_InRangeInputHasNoErrors()
    {
        var errors = AgendaRules.Validate(new AgendaInput("Budget review", "Q3"));

        Assert.Empty(errors);
        Assert.False(errors.ContainsKey("title"));
        Assert.False(errors.ContainsKey("description"));
    }

    [Fact(DisplayName = "AgendaRules.Validate/UTC002: empty title fails the title rule")]
    [Trait("UTC", "AgendaRules.Validate/UTC002")]
    [Trait("Trait", "UTC")]
    public void Validate_EmptyTitleFailsTheTitleRule()
    {
        var errors = AgendaRules.Validate(new AgendaInput(""));

        Assert.True(errors.ContainsKey("title"));
        Assert.Equal(["Tiêu đề phải có từ 1 đến 300 ký tự."], errors["title"]);
        Assert.Single(errors);
    }

    [Fact(DisplayName = "AgendaRules.Validate/UTC003: 301-character title fails the title rule")]
    [Trait("UTC", "AgendaRules.Validate/UTC003")]
    [Trait("Trait", "UTC")]
    public void Validate_OverlongTitleFailsTheTitleRule()
    {
        var errors = AgendaRules.Validate(new AgendaInput(new string('x', 301)));

        Assert.True(errors.ContainsKey("title"));
        Assert.Equal(["Tiêu đề phải có từ 1 đến 300 ký tự."], errors["title"]);
        Assert.Empty(AgendaRules.Validate(new AgendaInput(new string('x', 300))));
    }

    [Fact(DisplayName = "AgendaRules.Validate/UTC004: one-character title passes")]
    [Trait("UTC", "AgendaRules.Validate/UTC004")]
    [Trait("Trait", "UTC")]
    public void Validate_OneCharacterTitlePasses()
    {
        Assert.Empty(AgendaRules.Validate(new AgendaInput("A")));
        Assert.Empty(AgendaRules.Validate(new AgendaInput("A", new string('y', 4000))));
        Assert.True(AgendaRules.Validate(new AgendaInput("A", new string('y', 4001))).ContainsKey("description"));
    }

    // ---- AgendaExecution.TryComplete ----

    [Fact(DisplayName = "AgendaExecution.TryComplete/UTC001: running item completes at the end time")]
    [Trait("UTC", "AgendaExecution.TryComplete/UTC001")]
    [Trait("Trait", "UTC")]
    public void TryComplete_RunningItemCompletesAtTheEndTime()
    {
        var item = new AgendaItem { Title = "M", Status = AgendaItemStatus.InProgress, ActualStartedAt = new(2026, 12, 1, 9, 0, 0, TimeSpan.Zero) };
        var endedAt = new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero);

        Assert.True(AgendaExecution.TryComplete(item, endedAt));
        Assert.Equal(AgendaItemStatus.Done, item.Status);
        Assert.True(item.ActualEndedAt == endedAt);
        Assert.Equal(endedAt, item.UpdatedAt);
    }

    [Fact(DisplayName = "AgendaExecution.TryComplete/UTC002: ending exactly at the start is allowed")]
    [Trait("UTC", "AgendaExecution.TryComplete/UTC002")]
    [Trait("Trait", "UTC")]
    public void TryComplete_EndingExactlyAtTheStartIsAllowed()
    {
        var start = new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero);
        var item = new AgendaItem { Title = "M", Status = AgendaItemStatus.InProgress, ActualStartedAt = start };

        Assert.True(AgendaExecution.TryComplete(item, start));
        Assert.Equal(start, item.ActualEndedAt);
        Assert.Equal(AgendaItemStatus.Done, item.Status);
    }

    [Fact(DisplayName = "AgendaExecution.TryComplete/UTC003: end before the start refuses")]
    [Trait("UTC", "AgendaExecution.TryComplete/UTC003")]
    [Trait("Trait", "UTC")]
    public void TryComplete_EndBeforeTheStartRefuses()
    {
        var start = new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero);
        var item = new AgendaItem { Title = "M", Status = AgendaItemStatus.InProgress, ActualStartedAt = start.AddTicks(10) };
        var endedAt = start;

        Assert.False(AgendaExecution.TryComplete(item, endedAt));
        Assert.Equal(AgendaItemStatus.InProgress, item.Status);
        Assert.Null(item.ActualEndedAt);
    }

    [Fact(DisplayName = "AgendaExecution.TryComplete/UTC004: pending item cannot complete")]
    [Trait("UTC", "AgendaExecution.TryComplete/UTC004")]
    [Trait("Trait", "UTC")]
    public void TryComplete_PendingItemCannotComplete()
    {
        var item = new AgendaItem { Title = "M", Status = AgendaItemStatus.Pending };

        Assert.False(AgendaExecution.TryComplete(item, Now));
        Assert.Equal(AgendaItemStatus.Pending, item.Status);
        Assert.Null(item.ActualEndedAt);
    }

    // ---- AgendaExecution.UtcMicroseconds ----

    // ---- AgendaService.CreateAsync ----

    [Fact(DisplayName = "AgendaService.CreateAsync/UTC001: secretary creates the first item as pending")]
    [Trait("UTC", "AgendaService.CreateAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Create_SecretaryCreatesTheFirstItemAsPending()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);

        var outcome = await Service().CreateAsync(meeting.Id, _secretary, new("Opening"));

        Assert.Equal(AgendaFailure.None, outcome.Failure);
        var item = Assert.Single(_agenda.Added);
        Assert.Equal(("Opening", 1, AgendaItemStatus.Pending, ""), (item.Title, item.Position, item.Status, item.Description));
        Assert.Equal(["publish:Created:commits=1"], _effects.Calls);
    }

    [Fact(DisplayName = "AgendaService.CreateAsync/UTC002: 301-character title is invalid")]
    [Trait("UTC", "AgendaService.CreateAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Create_OverlongTitleIsInvalid()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);

        var outcome = await Service().CreateAsync(meeting.Id, _secretary, new(new string('x', 301)));

        Assert.Equal(AgendaFailure.Invalid, outcome.Failure);
        Assert.True(outcome.Errors!.ContainsKey("title"));
        Assert.Empty(_agenda.Added);
    }

    [Fact(DisplayName = "AgendaService.CreateAsync/UTC003: organizer without a seat is forbidden")]
    [Trait("UTC", "AgendaService.CreateAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Create_OrganizerWithoutASeatIsForbidden()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);

        var outcome = await Service().CreateAsync(meeting.Id, _organizer, new("A"));

        Assert.Equal(AgendaFailure.Forbidden, outcome.Failure);
        Assert.Empty(_agenda.Added);
        Assert.Empty(_effects.Calls);
    }

    [Fact(DisplayName = "AgendaService.CreateAsync/UTC004: unsupported media body is refused")]
    [Trait("UTC", "AgendaService.CreateAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Create_UnsupportedMediaBodyIsRefused()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);

        var outcome = await Service().CreateAsync(meeting.Id, _secretary,
            new("A", ParseFailure: AgendaFailure.UnsupportedMedia));

        Assert.Equal(AgendaFailure.UnsupportedMedia, outcome.Failure);
        Assert.True(outcome.Errors!.ContainsKey("body"));
        Assert.Empty(_agenda.Added);
    }

    [Fact(DisplayName = "AgendaService.CreateAsync/UTC005: oversized body is refused")]
    [Trait("UTC", "AgendaService.CreateAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Create_OversizedBodyIsRefused()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);

        var outcome = await Service().CreateAsync(meeting.Id, _secretary,
            new("A", ParseFailure: AgendaFailure.TooLarge));

        Assert.Equal(AgendaFailure.TooLarge, outcome.Failure);
        Assert.True(outcome.Errors!.ContainsKey("body"));
        Assert.Empty(_agenda.Added);
    }

    [Fact(DisplayName = "AgendaService.CreateAsync/UTC006: position overflow answers save failed")]
    [Trait("UTC", "AgendaService.CreateAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Create_PositionOverflowAnswersSaveFailed()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        _agenda.Items.Add(Item(meeting.Id, "Cũ", int.MaxValue));

        var outcome = await Service().CreateAsync(meeting.Id, _secretary, new("A"));

        Assert.Equal(AgendaFailure.SaveFailed, outcome.Failure);
        Assert.Empty(_agenda.Added);
        Assert.Empty(_effects.Calls);
    }

    // ---- AgendaService.UpdateAsync ----

    [Fact(DisplayName = "AgendaService.UpdateAsync/UTC001: secretary renames a draft item")]
    [Trait("UTC", "AgendaService.UpdateAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Update_SecretaryRenamesADraftItem()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        var item = Item(meeting.Id, "Old", 1);
        _agenda.Items.Add(item);

        var outcome = await Service().UpdateAsync(meeting.Id, item.Id, _secretary, new("New", ""));

        Assert.Equal(AgendaFailure.None, outcome.Failure);
        Assert.Equal("New", item.Title);
        Assert.Equal(["publish:Updated:commits=1"], _effects.Calls);
    }

    [Fact(DisplayName = "AgendaService.UpdateAsync/UTC002: unknown item is not found")]
    [Trait("UTC", "AgendaService.UpdateAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Update_UnknownItemIsNotFound()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);

        var outcome = await Service().UpdateAsync(meeting.Id, Guid.CreateVersion7(), _secretary, new("A"));

        Assert.Equal(AgendaFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.ItemId);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "AgendaService.UpdateAsync/UTC003: blank title is invalid")]
    [Trait("UTC", "AgendaService.UpdateAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Update_BlankTitleIsInvalid()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        var item = Item(meeting.Id, "Old", 1);
        _agenda.Items.Add(item);

        var outcome = await Service().UpdateAsync(meeting.Id, item.Id, _secretary, new(""));

        Assert.Equal(AgendaFailure.Invalid, outcome.Failure);
        Assert.True(outcome.Errors!.ContainsKey("title"));
        Assert.Equal("Old", item.Title);
    }

    [Fact(DisplayName = "AgendaService.UpdateAsync/UTC004: member cannot read a draft meeting")]
    [Trait("UTC", "AgendaService.UpdateAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Update_MemberCannotReadADraftMeeting()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        var item = Item(meeting.Id, "Old", 1);
        _agenda.Items.Add(item);

        var outcome = await Service().UpdateAsync(meeting.Id, item.Id, _member, new("A"));

        Assert.Equal(AgendaFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.ItemId);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "AgendaService.UpdateAsync/UTC005: store failure answers save failed")]
    [Trait("UTC", "AgendaService.UpdateAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Update_StoreFailureAnswersSaveFailed()
    {
        var log = new CapturingAgendaLog();
        var meeting = GivenMeeting(MeetingStatus.Draft);
        var item = Item(meeting.Id, "Old", 1);
        _agenda.Items.Add(item);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service(log).UpdateAsync(meeting.Id, item.Id, _secretary, new("New"));

        Assert.Equal(AgendaFailure.SaveFailed, outcome.Failure);
        Assert.Equal(1, _unitOfWork.Rollbacks);
        Assert.Equal(1, log.Messages.Count(m => m.Contains("Failed agenda Update")));
    }

    // ---- AgendaService.DeleteAsync ----

    [Fact(DisplayName = "AgendaService.DeleteAsync/UTC001: secretary deletes a pending item")]
    [Trait("UTC", "AgendaService.DeleteAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Delete_SecretaryDeletesAPendingItem()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        var item = Item(meeting.Id, "Mục 1", 1);
        _agenda.Items.Add(item);

        var outcome = await Service().DeleteAsync(meeting.Id, item.Id, _secretary);

        Assert.Equal(AgendaFailure.None, outcome.Failure);
        Assert.Equal([item], _agenda.Removed);
        Assert.Equal(["publish:Deleted:commits=1"], _effects.Calls);
    }

    [Fact(DisplayName = "AgendaService.DeleteAsync/UTC002: running item is delete locked")]
    [Trait("UTC", "AgendaService.DeleteAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Delete_RunningItemIsDeleteLocked()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var item = Item(meeting.Id, "Mục 1", 1, AgendaItemStatus.InProgress);
        _agenda.Items.Add(item);

        var outcome = await Service().DeleteAsync(meeting.Id, item.Id, _chair);

        Assert.Equal(AgendaFailure.DeleteLocked, outcome.Failure);
        Assert.Empty(_agenda.Removed);
        Assert.Empty(_effects.Calls);
    }

    [Fact(DisplayName = "AgendaService.DeleteAsync/UTC005: store failure answers save failed")]
    [Trait("UTC", "AgendaService.DeleteAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Delete_StoreFailureAnswersSaveFailed()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        var item = Item(meeting.Id, "Mục 1", 1);
        _agenda.Items.Add(item);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().DeleteAsync(meeting.Id, item.Id, _secretary);

        Assert.Equal(AgendaFailure.SaveFailed, outcome.Failure);
        Assert.Equal(1, _unitOfWork.Rollbacks);
        Assert.Empty(_effects.Calls);
    }

    // ---- AgendaService.ReorderAsync ----

    [Fact(DisplayName = "AgendaService.ReorderAsync/UTC001: secretary reorders three items")]
    [Trait("UTC", "AgendaService.ReorderAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Reorder_SecretaryReordersThreeItems()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        var p1 = Item(meeting.Id, "P1", 1); var p2 = Item(meeting.Id, "P2", 2); var p3 = Item(meeting.Id, "P3", 3);
        _agenda.Items.AddRange([p1, p2, p3]);
        var version = AgendaRules.OrderVersion(_agenda.Items);

        var outcome = await Service().ReorderAsync(meeting.Id, _secretary,
            new AgendaInput(OrderVersion: version, OrderedItemIds: [p3.Id, p1.Id, p2.Id]));

        Assert.Equal(AgendaFailure.None, outcome.Failure);
        Assert.Equal((1, 2, 3), (p3.Position, p1.Position, p2.Position));
        Assert.Equal(["publish:Reordered:commits=1"], _effects.Calls);
    }

    [Fact(DisplayName = "AgendaService.ReorderAsync/UTC002: stale version answers stale order")]
    [Trait("UTC", "AgendaService.ReorderAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Reorder_StaleVersionAnswersStaleOrder()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        var p1 = Item(meeting.Id, "P1", 1); var p2 = Item(meeting.Id, "P2", 2); var p3 = Item(meeting.Id, "P3", 3);
        _agenda.Items.AddRange([p1, p2, p3]);
        var version = AgendaRules.OrderVersion(_agenda.Items);
        p1.Status = AgendaItemStatus.InProgress;

        var outcome = await Service().ReorderAsync(meeting.Id, _secretary,
            new AgendaInput(OrderVersion: version, OrderedItemIds: [p3.Id, p1.Id, p2.Id]));

        Assert.Equal(AgendaFailure.StaleOrder, outcome.Failure);
        Assert.Equal((1, 2, 3), (p1.Position, p2.Position, p3.Position));
        Assert.Empty(_effects.Calls);
    }

    // ---- AgendaService.StartAsync ----

    [Fact(DisplayName = "AgendaService.StartAsync/UTC001: chair starts a pending item")]
    [Trait("UTC", "AgendaService.StartAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Start_ChairStartsAPendingItem()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var item = Item(meeting.Id, "Mục 1", 1);
        _agenda.Items.Add(item);

        var outcome = await Service().StartAsync(meeting.Id, item.Id, _chair);

        Assert.Equal(AgendaFailure.None, outcome.Failure);
        Assert.Equal(AgendaItemStatus.InProgress, item.Status);
        Assert.True(item.ActualStartedAt == Now);
        Assert.Equal(["publish:Started:commits=1"], _effects.Calls);
    }

    [Fact(DisplayName = "AgendaService.StartAsync/UTC002: second item answers already current")]
    [Trait("UTC", "AgendaService.StartAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Start_SecondItemAnswersAlreadyCurrent()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var running = Item(meeting.Id, "J", 1, AgendaItemStatus.InProgress);
        var pending = Item(meeting.Id, "I", 2);
        _agenda.Items.AddRange([running, pending]);

        var outcome = await Service().StartAsync(meeting.Id, pending.Id, _chair);

        Assert.Equal(AgendaFailure.AlreadyCurrent, outcome.Failure);
        Assert.Equal(AgendaItemStatus.Pending, pending.Status);
        Assert.Empty(_effects.Calls);
    }

    [Fact(DisplayName = "AgendaService.StartAsync/UTC005: store failure answers save failed")]
    [Trait("UTC", "AgendaService.StartAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Start_StoreFailureAnswersSaveFailed()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var item = Item(meeting.Id, "Mục 1", 1);
        _agenda.Items.Add(item);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().StartAsync(meeting.Id, item.Id, _chair);

        Assert.Equal(AgendaFailure.SaveFailed, outcome.Failure);
        Assert.Equal(1, _unitOfWork.Rollbacks);
        Assert.Empty(_effects.Calls);
    }

    // ---- AgendaService.CompleteAsync ----

    [Fact(DisplayName = "AgendaService.CompleteAsync/UTC001: chair completes the running item")]
    [Trait("UTC", "AgendaService.CompleteAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Complete_ChairCompletesTheRunningItem()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var item = Item(meeting.Id, "Mục 1", 1, AgendaItemStatus.InProgress);
        item.ActualStartedAt = Now.AddMinutes(-10);
        _agenda.Items.Add(item);

        var outcome = await Service().CompleteAsync(meeting.Id, item.Id, _chair);

        Assert.Equal(AgendaFailure.None, outcome.Failure);
        Assert.Equal(AgendaItemStatus.Done, item.Status);
        Assert.True(item.ActualEndedAt == Now);
        Assert.Equal(["publish:Completed:commits=1"], _effects.Calls);
    }

    [Fact(DisplayName = "AgendaService.CompleteAsync/UTC003: future start answers save failed with a log")]
    [Trait("UTC", "AgendaService.CompleteAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Complete_FutureStartAnswersSaveFailedWithALog()
    {
        var log = new CapturingAgendaLog();
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var item = Item(meeting.Id, "Mục 1", 1, AgendaItemStatus.InProgress);
        item.ActualStartedAt = Now.AddMinutes(1);
        _agenda.Items.Add(item);

        var outcome = await Service(log).CompleteAsync(meeting.Id, item.Id, _chair);

        Assert.Equal(AgendaFailure.SaveFailed, outcome.Failure);
        Assert.Equal(AgendaItemStatus.InProgress, item.Status);
        Assert.Equal(1, log.Messages.Count(m => m.Contains("Invalid actual timestamps")));
    }

    [Fact(DisplayName = "AgendaService.CompleteAsync/UTC004: pending item cannot complete")]
    [Trait("UTC", "AgendaService.CompleteAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Complete_PendingItemCannotComplete()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var item = Item(meeting.Id, "Mục 1", 1);
        _agenda.Items.Add(item);

        var outcome = await Service().CompleteAsync(meeting.Id, item.Id, _chair);

        Assert.Equal(AgendaFailure.CannotComplete, outcome.Failure);
        Assert.Equal(AgendaItemStatus.Pending, item.Status);
        Assert.Empty(_effects.Calls);
    }

    // ---- AgendaService.RecordConclusionAsync ----

    [Fact(DisplayName = "AgendaService.RecordConclusionAsync/UTC001: chair records a trimmed conclusion")]
    [Trait("UTC", "AgendaService.RecordConclusionAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task RecordConclusion_ChairRecordsATrimmedConclusion()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var item = Item(meeting.Id, "Mục 1", 1, AgendaItemStatus.InProgress);
        _agenda.Items.Add(item);

        var outcome = await Service().RecordConclusionAsync(meeting.Id, item.Id, _chair,
            new AgendaInput(Conclusion: "  Approved the budget.  "));

        Assert.Equal(AgendaFailure.None, outcome.Failure);
        Assert.Equal("Approved the budget.", item.Conclusion);
        Assert.Equal(["publish:ConclusionUpdated:commits=1"], _effects.Calls);
    }

    [Fact(DisplayName = "AgendaService.RecordConclusionAsync/UTC002: 2001-character conclusion is invalid")]
    [Trait("UTC", "AgendaService.RecordConclusionAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task RecordConclusion_OverlongConclusionIsInvalid()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var item = Item(meeting.Id, "Mục 1", 1, AgendaItemStatus.InProgress);
        _agenda.Items.Add(item);

        var outcome = await Service().RecordConclusionAsync(meeting.Id, item.Id, _chair,
            new AgendaInput(Conclusion: new string('x', 2001)));

        Assert.Equal(AgendaFailure.Invalid, outcome.Failure);
        Assert.True(outcome.Errors!.ContainsKey("conclusion"));
        Assert.Null(item.Conclusion);
    }

    [Fact(DisplayName = "AgendaService.RecordConclusionAsync/UTC004: unknown item is not found")]
    [Trait("UTC", "AgendaService.RecordConclusionAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task RecordConclusion_UnknownItemIsNotFound()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);

        var outcome = await Service().RecordConclusionAsync(meeting.Id, Guid.CreateVersion7(), _chair,
            new AgendaInput(Conclusion: "x"));

        Assert.Equal(AgendaFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.ItemId);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    // ---- AgendaMeetingEnd.PrepareAsync ----

    [Fact(DisplayName = "AgendaMeetingEnd.PrepareAsync/UTC001: running item completes with the meeting")]
    [Trait("UTC", "AgendaMeetingEnd.PrepareAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Prepare_RunningItemCompletesWithTheMeeting()
    {
        var meetingId = Guid.CreateVersion7();
        _agenda.Meetings.Add(new Meeting { Id = meetingId, Title = "Họp", Status = MeetingStatus.InProgress });
        var item = new AgendaItem
        {
            MeetingId = meetingId, Title = "Mục 1", Position = 1, Status = AgendaItemStatus.InProgress,
            ActualStartedAt = new(2026, 12, 1, 9, 0, 0, TimeSpan.Zero),
        };
        _agenda.Items.Add(item);
        var endedAt = new DateTimeOffset(2026, 12, 1, 10, 30, 0, TimeSpan.Zero);

        var change = await new AgendaMeetingEnd(_agenda, _unitOfWork).PrepareAsync(meetingId, endedAt);

        Assert.NotNull(change);
        Assert.Equal(("MeetingEnded", item.Id, "Mục 1", endedAt), (change!.ChangeType, change.AgendaItemId, change.Title, change.OccurredAt));
        Assert.Equal((AgendaItemStatus.Done, endedAt), (item.Status, item.ActualEndedAt));
    }

    [Fact(DisplayName = "AgendaMeetingEnd.PrepareAsync/UTC002: no running item changes nothing")]
    [Trait("UTC", "AgendaMeetingEnd.PrepareAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Prepare_NoRunningItemChangesNothing()
    {
        var meetingId = Guid.CreateVersion7();
        _agenda.Meetings.Add(new Meeting { Id = meetingId, Title = "Họp", Status = MeetingStatus.InProgress });
        _agenda.Items.Add(new AgendaItem { MeetingId = meetingId, Title = "Mục 1", Position = 1, Status = AgendaItemStatus.Pending });

        var change = await new AgendaMeetingEnd(_agenda, _unitOfWork).PrepareAsync(meetingId, Now);

        Assert.Null(change);
        Assert.Equal([meetingId], _agenda.Locked);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact(DisplayName = "AgendaMeetingEnd.PrepareAsync/UTC003: end before the start throws")]
    [Trait("UTC", "AgendaMeetingEnd.PrepareAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Prepare_EndBeforeTheStartThrows()
    {
        var meetingId = Guid.CreateVersion7();
        _agenda.Meetings.Add(new Meeting { Id = meetingId, Title = "Họp", Status = MeetingStatus.InProgress });
        var item = new AgendaItem
        {
            MeetingId = meetingId, Title = "Mục 1", Position = 1, Status = AgendaItemStatus.InProgress,
            ActualStartedAt = new(2026, 12, 1, 9, 0, 0, TimeSpan.Zero),
        };
        _agenda.Items.Add(item);

        await Assert.ThrowsAsync<InvalidAgendaExecutionException>(() => new AgendaMeetingEnd(_agenda, _unitOfWork)
            .PrepareAsync(meetingId, new(2026, 12, 1, 8, 0, 0, TimeSpan.Zero)));
        Assert.Equal(AgendaItemStatus.InProgress, item.Status);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    // ---- AgendaPresenterNotifier.NotifyAsync ----

    private FakeNotificationRepository NotifyNotifications(out FakeServiceScopeFactory scopes, bool withLinks = true)
    {
        var notifications = new FakeNotificationRepository();
        var emails = new FakeEmailMessageRepository();
        var services = new List<object> { _unitOfWork, _agenda, notifications, emails };
        if (withLinks) services.Add(new ReadyGuestLinks());
        scopes = new FakeServiceScopeFactory(services.ToArray());
        return notifications;
    }

    private sealed class ReadyGuestLinks : IGuestInvitationLinkProvider
    {
        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);
        public Task<GuestInvitationLink> PrepareAsync(Guid invitationId, CancellationToken ct)
            => Task.FromResult(new GuestInvitationLink(GuestLinkStatus.Ready, $"https://hop.example.vn/guest/{invitationId:D}#token=t"));
    }

    [Fact(DisplayName = "AgendaPresenterNotifier.NotifyAsync/UTC001: current presenter gets an in-app notice")]
    [Trait("UTC", "AgendaPresenterNotifier.NotifyAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Notify_CurrentPresenterGetsAnInAppNotice()
    {
        var meeting = new Meeting
        {
            Title = "Budget", Status = MeetingStatus.Published, CreatedById = _organizer,
            PlannedStartTime = new(2026, 12, 1, 2, 0, 0, TimeSpan.Zero),
            PlannedEndTime = new(2026, 12, 1, 3, 0, 0, TimeSpan.Zero),
        };
        _agenda.Meetings.Add(meeting);
        var presenter = Guid.CreateVersion7();
        meeting.Participants.Add(Seat(meeting.Id, presenter, MeetingRole.Member));
        _agenda.Participations.Add(Seat(meeting.Id, presenter, MeetingRole.Member));
        var item = new AgendaItem { MeetingId = meeting.Id, Title = "Q3 review", Position = 1, PresenterId = presenter };
        _agenda.Items.Add(item);
        var notifications = NotifyNotifications(out var scopes);
        var assignment = new AgendaPresenterAssignment(Guid.CreateVersion7(), meeting.Id, item.Id, presenter, null, Now);

        await new AgendaPresenterNotifier(scopes).NotifyAsync(assignment, default);

        var notice = Assert.Single(notifications.Added);
        Assert.Equal($"agenda-presenter:{assignment.EventId:D}:{presenter:D}", notice.DeduplicationKey);
        Assert.Contains("\"Q3 review\" trong cuộc họp \"Budget\" lúc 09:00 01/12/2026", notice.Body);
    }

    [Fact(DisplayName = "AgendaPresenterNotifier.NotifyAsync/UTC004: guest presenter gets one email")]
    [Trait("UTC", "AgendaPresenterNotifier.NotifyAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Notify_GuestPresenterGetsOneEmail()
    {
        var meeting = new Meeting
        {
            Title = "Budget", Status = MeetingStatus.Published, CreatedById = _organizer,
            PlannedStartTime = new(2026, 12, 1, 2, 0, 0, TimeSpan.Zero),
            PlannedEndTime = new(2026, 12, 1, 3, 0, 0, TimeSpan.Zero),
        };
        _agenda.Meetings.Add(meeting);
        var guest = new MeetingGuestInvitation { MeetingId = meeting.Id, InvitedName = "Tran Khach", Email = "khach@example.com" };
        _agenda.Guests.Add(guest);
        var item = new AgendaItem { MeetingId = meeting.Id, Title = "Q3 review", Position = 1, PresenterGuestInvitationId = guest.Id };
        _agenda.Items.Add(item);
        var notifications = NotifyNotifications(out var scopes);
        var assignment = new AgendaPresenterAssignment(Guid.CreateVersion7(), meeting.Id, item.Id, null, guest.Id, Now);
        var emails = (FakeEmailMessageRepository)scopes.CreateScope().ServiceProvider
            .GetService(typeof(IEmailMessageRepository))!;

        await new AgendaPresenterNotifier(scopes).NotifyAsync(assignment, default);

        var email = Assert.Single(emails.Added);
        Assert.Equal($"agenda-presenter:{assignment.EventId:D}:{guest.Id:D}", email.DeduplicationKey);
        Assert.Contains("Xác minh email để vào cuộc họp:", email.Body);
    }
}
