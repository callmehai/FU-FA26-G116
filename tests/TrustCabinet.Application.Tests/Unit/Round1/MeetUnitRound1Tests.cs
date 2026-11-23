using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Application.Votes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Unit.Round1;

public sealed class MeetUnitRound1Tests
{

    // Round 1 Report 5.1 probes for FE-04 (Meeting Management), tester Phạm Gia Khánh.
    // One xUnit test per remaining UTC in docs/08-reports/test-reports/cases/FE-04/unit/*.json.
    // Each test carries [Trait("UTC", "<Class.Method>/<UTCnnn>")] for detail,
    // [Trait("Trait", "UTC")] so --filter "Trait=UTC" selects it, and a DisplayName
    // containing "<Class.Method>/<UTCnnn>" so the TRX (which carries no traits) joins
    // back to the JSON by name.

    #region Small helpers local to this file

    /// <summary>Captures what a service logs so a test can assert the Log Message line.</summary>
    private sealed class Round1Logger<T> : ILogger<T>
    {
        public sealed record Entry(LogLevel Level, string Message, Exception? Exception);

        public List<Entry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add(new Entry(logLevel, formatter(state, exception), exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    /// <summary>
    /// An <see cref="IMeetingRepository"/> that delegates to a <see cref="FakeMeetingRepository"/> but
    /// records reads and fills the seams the fake leaves open (owned reads, approver eligibility,
    /// history pages, read failures).
    /// </summary>
    private sealed class Round1Meetings(FakeMeetingRepository inner) : IMeetingRepository
    {
        public FakeMeetingRepository Inner { get; } = inner;

        public List<string> Reads { get; } = [];

        public Exception? FailRead { get; set; }

        public HashSet<Guid> EligibleApprovers { get; } = [];

        public Func<Guid, int, int, MeetingHistoryPage?>? HistoryPage { get; set; }

        public Task<Meeting?> FindAccessibleAsync(Guid id, Guid actorId, CancellationToken ct = default)
        {
            Reads.Add($"accessible:{id}:{actorId}");
            return FailRead is { } failure
                ? Task.FromException<Meeting?>(failure)
                : Inner.FindAccessibleAsync(id, actorId, ct);
        }

        public Task<MeetingListPage> FindAuthorizedPageAsync(Guid actorId, MeetingListQuery query, CancellationToken ct = default)
            => Inner.FindAuthorizedPageAsync(actorId, query, ct);

        public Task<Meeting?> LockOwnedAsync(Guid id, Guid creatorId, CancellationToken ct = default)
        {
            Reads.Add($"locked:{id}:{creatorId}");
            Inner.Locked.Add(id);
            return FailRead is { } failure
                ? Task.FromException<Meeting?>(failure)
                : Task.FromResult(Inner.Meetings.SingleOrDefault(m => m.Id == id && m.CreatedById == creatorId));
        }

        public Task<Meeting?> FindOwnedAsync(Guid id, Guid creatorId, CancellationToken ct = default)
        {
            Reads.Add($"owned:{id}:{creatorId}");
            return FailRead is { } failure
                ? Task.FromException<Meeting?>(failure)
                : Task.FromResult(Inner.Meetings.SingleOrDefault(m => m.Id == id && m.CreatedById == creatorId));
        }

        public Task<IReadOnlyList<MeetingApproverOption>> ListEligibleApproversAsync(CancellationToken ct = default)
            => Inner.ListEligibleApproversAsync(ct);

        public Task<bool> IsEligibleApproverAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(EligibleApprovers.Contains(id));

        public Task<Meeting?> FindWorkflowAsync(Guid id, bool forUpdate = false, CancellationToken ct = default)
        {
            Reads.Add($"workflow:{id}:{forUpdate}");
            return FailRead is { } failure
                ? Task.FromException<Meeting?>(failure)
                : Inner.FindWorkflowAsync(id, forUpdate, ct);
        }

        public Task<IReadOnlyList<Guid>> ListOverdueAsync(DateTimeOffset cutoff, int take, CancellationToken ct = default)
            => Inner.ListOverdueAsync(cutoff, take, ct);

        public Task<IReadOnlyList<Guid>> ListOverrunAsync(DateTimeOffset cutoff, int take, CancellationToken ct = default)
            => Inner.ListOverrunAsync(cutoff, take, ct);

        public Task<MeetingHistoryPage?> FindHistoryPageAsync(Guid meetingId, int page, int pageSize, CancellationToken ct = default)
        {
            Reads.Add($"history:{meetingId}:{page}:{pageSize}");
            return FailRead is { } failure
                ? Task.FromException<MeetingHistoryPage?>(failure)
                : Task.FromResult(HistoryPage?.Invoke(meetingId, page, pageSize));
        }

        public Task<bool> IsActiveUserWithCapabilityAsync(Guid userId, Capability capability, CancellationToken ct = default)
            => Inner.IsActiveUserWithCapabilityAsync(userId, capability, ct);

        public Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct = default)
            => Inner.IsUserActiveAsync(userId, ct);

        public Task<string?> FindUserDisplayNameAsync(Guid userId, CancellationToken ct = default)
            => Inner.FindUserDisplayNameAsync(userId, ct);

        public Task AddNotificationsAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct = default)
            => Inner.AddNotificationsAsync(notifications, ct);

        public Task AddHistoryAsync(MeetingStatusHistory history, CancellationToken ct = default)
            => Inner.AddHistoryAsync(history, ct);

        public Task<IReadOnlyList<User>> LockParticipantUsersAsync(Guid[] userIds, CancellationToken ct = default)
            => Inner.LockParticipantUsersAsync(userIds, ct);

        public Task AddAsync(Meeting meeting, CancellationToken ct = default)
            => Inner.AddAsync(meeting, ct);
    }

    /// <summary>Who of the candidates is active, recording who was asked about.</summary>
    private sealed class Round1AudienceQuery : IMeetingAudienceQuery
    {
        public HashSet<Guid> Active { get; } = [];

        public List<Guid[]> Asked { get; } = [];

        public Task<IReadOnlyList<Guid>> ListActiveUserIdsAsync(Guid[] candidateUserIds, CancellationToken ct = default)
        {
            Asked.Add(candidateUserIds);
            return Task.FromResult<IReadOnlyList<Guid>>(candidateUserIds.Where(Active.Contains).ToArray());
        }
    }

    /// <summary>Both guest checks of the audience, answered from sets the test fills.</summary>
    private sealed class Round1Guests : IAgendaGuestAccess, IParticipantGuestAccess
    {
        public HashSet<Guid> ReadableSessions { get; } = [];

        public HashSet<Guid> AccessibleSessions { get; } = [];

        public List<MeetingGuestSession> Readers { get; } = [];

        public Task<bool> CanReadAsync(MeetingGuestSession session, CancellationToken ct)
            => Task.FromResult(ReadableSessions.Contains(session.SessionId));

        public Task<IReadOnlyList<MeetingGuestSession>> ListReadersAsync(Guid meetingId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MeetingGuestSession>>(Readers.Where(r => r.MeetingId == meetingId).ToArray());

        public Task<bool> CanAccessAsync(MeetingGuestSession session, CancellationToken ct)
            => Task.FromResult(AccessibleSessions.Contains(session.SessionId));

        public Task<IReadOnlyDictionary<Guid, GuestDisplayProfile>> GetProfilesAsync(Guid meetingId,
            IReadOnlyCollection<Guid> invitationIds, CancellationToken ct)
            => throw new NotSupportedException();
    }

    /// <summary>Lifecycle stub for the overdue sweep: answers end-overrun per meeting, recording calls.</summary>
    private sealed class Round1Lifecycle : IMeetingLifecycleService
    {
        public List<(Guid MeetingId, DateTimeOffset Cutoff, string Reason)> EndCalls { get; } = [];

        public Func<Guid, bool>? EndAnswer { get; set; }

        public Task<MeetingWorkflowResult> TransitionAsync(Guid meetingId, MeetingTransitionCommand command, Guid actorId,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<bool> EndOverrunAsync(Guid meetingId, DateTimeOffset cutoff, string reason, CancellationToken ct = default)
        {
            EndCalls.Add((meetingId, cutoff, reason));
            return Task.FromResult(EndAnswer?.Invoke(meetingId) ?? true);
        }
    }

    /// <summary>Invitation staging that records every call, including publication and cancellation.</summary>
    private sealed class Round1Stager : IMeetingInvitationStager
    {
        public List<string> Calls { get; } = [];

        public void Stage(Meeting meeting, MeetingParticipant? participant, MeetingGuestInvitation? guest,
            InvitationEventKind kind, DateTimeOffset now, bool notify, Guid? sourceEventId = null, string? reason = null,
            (DateTimeOffset Start, DateTimeOffset End)? previous = null)
            => Calls.Add($"stage:{kind}:{notify}");

        public Task StagePublicationInvitationsAsync(Meeting meeting, DateTimeOffset now, CancellationToken ct = default)
        {
            Calls.Add("publication");
            return Task.CompletedTask;
        }

        public Task StageCancellationAsync(Meeting meeting, Guid historyId, Guid actorId, string reason,
            DateTimeOffset now, CancellationToken ct = default)
        {
            Calls.Add($"cancellation:{actorId}:{reason}");
            return Task.CompletedTask;
        }

        public Task StageRescheduleInvitationsAsync(Meeting meeting, Guid historyId,
            (DateTimeOffset Start, DateTimeOffset End) previous, DateTimeOffset now, CancellationToken ct = default)
        {
            Calls.Add($"reschedule:{previous.Start:O}");
            return Task.CompletedTask;
        }
    }

    private static class Round1Data
    {
        public static readonly DateTimeOffset Now = new(2026, 11, 21, 7, 0, 0, TimeSpan.Zero);

        public static string Stamp(DateTimeOffset value) => value.ToString("yyyy-MM-ddTHH:mm:sszzz");

        public static User NewUser(string name, bool active = true) => new()
        {
            FullName = name, Email = $"{Guid.CreateVersion7():N}@trustcabinet.local", IsActive = active,
        };

        public static Meeting NewMeeting(MeetingStatus status, Guid creator, DateTimeOffset? start = null,
            DateTimeOffset? end = null, MeetingMode mode = MeetingMode.InPerson) => new()
            {
                Title = "Họp xét duyệt kế hoạch quý",
                Description = "Cuộc họp xét duyệt theo lịch công tác.",
                Status = status,
                CreatedById = creator,
                PlannedStartTime = start ?? Now.AddDays(1),
                PlannedEndTime = end ?? Now.AddDays(1).AddHours(1),
                Mode = mode,
                Location = "Phòng 301",
                Sensitivity = Sensitivity.Normal,
            };

        public static MeetingParticipant AddParticipant(Meeting meeting, Guid userId, string name, MeetingRole role,
            bool active = true, AssignmentStatus assignment = AssignmentStatus.Current,
            InvitationResponse response = InvitationResponse.Pending)
        {
            var participant = new MeetingParticipant
            {
                MeetingId = meeting.Id,
                Meeting = meeting,
                UserId = userId,
                User = NewUser(name, active),
                Role = role,
                AssignmentStatus = assignment,
                Response = response,
            };
            meeting.Participants.Add(participant);
            return participant;
        }

        public static MeetingInput InputOf(Meeting meeting, string? title = null) => new(
            title ?? meeting.Title,
            Stamp(meeting.PlannedStartTime),
            Stamp(meeting.PlannedEndTime),
            meeting.Mode.ToString(),
            meeting.Location,
            meeting.OnlineLink,
            meeting.Sensitivity.ToString(),
            meeting.Description);
    }

    #endregion

    public sealed class MeetCreateRound1Tests
    {
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeMeetingRepository _meetings = new();
        private readonly FakeMeetingInvitationStager _invitations = new();
        private readonly Round1Logger<MeetingService> _logger = new();
        private readonly FixedClock _clock = new(Round1Data.Now);

        private MeetingService Service(IMeetingRepository? meetings = null)
            => new(meetings ?? _meetings, _invitations, _unitOfWork, _logger, _clock);

        private static MeetingInput ValidInput() => new(
            "Họp giao ban tháng 11",
            Round1Data.Stamp(Round1Data.Now.AddDays(1)),
            Round1Data.Stamp(Round1Data.Now.AddDays(1).AddHours(1)),
            "InPerson",
            "Phòng 301",
            null,
            "Normal",
            "Giao ban định kỳ.");

        [Fact(DisplayName = "MeetingService.CreateAsync/UTC001 creates a draft meeting with chair and secretary")]
        [Trait("UTC", "MeetingService.CreateAsync/UTC001")]
        [Trait("Trait", "UTC")]
        public async Task Create_draft_with_chair_and_secretary()
        {
            var actor = Guid.CreateVersion7();
            var chair = Guid.CreateVersion7();
            var secretary = Guid.CreateVersion7();
            var seeds = new List<ParticipantSeed>
            {
                new(chair, nameof(MeetingRole.Chair)), new(secretary, nameof(MeetingRole.Secretary)),
            };

            var result = await Service().CreateAsync(ValidInput(), actor, seeds);

            Assert.Equal(MeetingOutcome.Success, result.Outcome);
            Assert.Null(result.Errors);
            var meeting = Assert.Single(_meetings.Added);
            Assert.Equal(MeetingStatus.Draft, meeting.Status);
            Assert.Equal(actor, meeting.CreatedById);
            Assert.Null(meeting.AssignedApproverId);
            Assert.Equal(2, meeting.Participants.Count);
            Assert.All(meeting.Participants, p => Assert.Equal(InvitationResponse.Pending, p.Response));
            Assert.Contains(meeting.Participants, p => p.UserId == chair && p.Role == MeetingRole.Chair);
            Assert.Contains(meeting.Participants, p => p.UserId == secretary && p.Role == MeetingRole.Secretary);
            var history = Assert.Single(meeting.StatusHistory);
            Assert.Equal(MeetingHistoryAction.Create, history.Action);
            Assert.Equal(MeetingStatus.Draft, history.ToStatus);
            Assert.Equal(actor, history.ActorId);
            Assert.Equal(1, _unitOfWork.Saves);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.CreateAsync/UTC002 rejects an empty title")]
        [Trait("UTC", "MeetingService.CreateAsync/UTC002")]
        [Trait("Trait", "UTC")]
        public async Task Create_rejects_empty_title()
        {
            var actor = Guid.CreateVersion7();
            var input = ValidInput() with { Title = "" };
            var seeds = new List<ParticipantSeed> { new(Guid.CreateVersion7(), nameof(MeetingRole.Chair)) };

            var result = await Service().CreateAsync(input, actor, seeds);

            Assert.Equal(MeetingOutcome.InvalidInformation, result.Outcome);
            Assert.True(result.Errors!.ContainsKey("title"));
            Assert.Empty(_meetings.Added);
            Assert.Empty(_meetings.History);
            Assert.Equal(0, _unitOfWork.Saves);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.CreateAsync/UTC003 rejects a start that is not after now")]
        [Trait("UTC", "MeetingService.CreateAsync/UTC003")]
        [Trait("Trait", "UTC")]
        public async Task Create_rejects_start_at_now()
        {
            var actor = Guid.CreateVersion7();
            var input = ValidInput() with { PlannedStartTime = Round1Data.Stamp(Round1Data.Now) };
            var seeds = new List<ParticipantSeed> { new(Guid.CreateVersion7(), nameof(MeetingRole.Chair)) };

            var result = await Service().CreateAsync(input, actor, seeds);

            Assert.Equal(MeetingOutcome.InvalidInformation, result.Outcome);
            Assert.Equal("Thời gian bắt đầu phải sau thời điểm hiện tại.", result.Errors!["plannedStartTime"]);
            Assert.Empty(_meetings.Added);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.CreateAsync/UTC004 rejects a roster of 101 participants")]
        [Trait("UTC", "MeetingService.CreateAsync/UTC004")]
        [Trait("Trait", "UTC")]
        public async Task Create_rejects_101_participants()
        {
            var actor = Guid.CreateVersion7();
            var seeds = new List<ParticipantSeed> { new(Guid.CreateVersion7(), nameof(MeetingRole.Chair)) };
            for (var i = 0; i < 100; i++)
                seeds.Add(new ParticipantSeed(Guid.CreateVersion7(), nameof(MeetingRole.Member)));

            var result = await Service().CreateAsync(ValidInput(), actor, seeds);

            Assert.Equal(MeetingOutcome.InvalidParticipants, result.Outcome);
            Assert.True(result.Errors!.ContainsKey("participants"));
            Assert.Empty(_meetings.Added);
            Assert.Equal(0, _unitOfWork.Saves);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.CreateAsync/UTC005 rejects a roster without a chair")]
        [Trait("UTC", "MeetingService.CreateAsync/UTC005")]
        [Trait("Trait", "UTC")]
        public async Task Create_rejects_roster_without_chair()
        {
            var actor = Guid.CreateVersion7();
            var seeds = new List<ParticipantSeed>
            {
                new(Guid.CreateVersion7(), nameof(MeetingRole.Member)),
                new(Guid.CreateVersion7(), nameof(MeetingRole.Secretary)),
            };

            var result = await Service().CreateAsync(ValidInput(), actor, seeds);

            Assert.Equal(MeetingOutcome.InvalidParticipants, result.Outcome);
            Assert.Equal("Cuộc họp phải có đúng một Chủ toạ.", result.Errors!["participants"]);
            Assert.Empty(_meetings.Added);
            Assert.Empty(_logger.Entries);
        }
    }

    public sealed class MeetCheckUpdateRound1Tests
    {
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeMeetingRepository _inner = new();
        private readonly Round1Meetings _meetings;
        private readonly Round1Logger<MeetingService> _logger = new();

        public MeetCheckUpdateRound1Tests() => _meetings = new Round1Meetings(_inner);

        private MeetingService Service() => new(_meetings, new FakeMeetingInvitationStager(), _unitOfWork, _logger);

        [Fact(DisplayName = "MeetingService.CheckUpdateAsync/UTC001 allows editing a draft owned by the actor")]
        [Trait("UTC", "MeetingService.CheckUpdateAsync/UTC001")]
        [Trait("Trait", "UTC")]
        public async Task CheckUpdate_draft_owned_by_actor_succeeds()
        {
            var actor = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Draft, actor);
            _inner.Meetings.Add(meeting);

            var result = await Service().CheckUpdateAsync(meeting.Id, actor);

            Assert.Equal(MeetingOutcome.Success, result.Outcome);
            Assert.Equal(meeting.Id, result.Meeting!.Id);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.CheckUpdateAsync/UTC002 refuses a meeting waiting for approval")]
        [Trait("UTC", "MeetingService.CheckUpdateAsync/UTC002")]
        [Trait("Trait", "UTC")]
        public async Task CheckUpdate_waiting_approval_is_not_editable()
        {
            var actor = Guid.CreateVersion7();
            _inner.Meetings.Add(Round1Data.NewMeeting(MeetingStatus.WaitingApproval, actor));

            var result = await Service().CheckUpdateAsync(_inner.Meetings[0].Id, actor);

            Assert.Equal(MeetingOutcome.NotEditable, result.Outcome);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.CheckUpdateAsync/UTC003 answers NotFound for a foreign meeting")]
        [Trait("UTC", "MeetingService.CheckUpdateAsync/UTC003")]
        [Trait("Trait", "UTC")]
        public async Task CheckUpdate_unknown_meeting_is_not_found()
        {
            var result = await Service().CheckUpdateAsync(Guid.CreateVersion7(), Guid.CreateVersion7());

            Assert.Equal(MeetingOutcome.NotFound, result.Outcome);
            Assert.Empty(_logger.Entries);
        }
    }

    public sealed class MeetUpdateRound1Tests
    {
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeMeetingRepository _inner = new();
        private readonly Round1Meetings _meetings;
        private readonly Round1Stager _stager = new();
        private readonly Round1Logger<MeetingService> _logger = new();
        private readonly FixedClock _clock = new(Round1Data.Now);

        public MeetUpdateRound1Tests() => _meetings = new Round1Meetings(_inner);

        private MeetingService Service() => new(_meetings, _stager, _unitOfWork, _logger, _clock);

        [Fact(DisplayName = "MeetingService.UpdateAsync/UTC001 updates the title of a draft")]
        [Trait("UTC", "MeetingService.UpdateAsync/UTC001")]
        [Trait("Trait", "UTC")]
        public async Task Update_draft_title()
        {
            var actor = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Draft, actor);
            _inner.Meetings.Add(meeting);
            var input = Round1Data.InputOf(meeting, title: "Họp mới");

            var result = await Service().UpdateAsync(meeting.Id, input, actor);

            Assert.Equal(MeetingOutcome.Success, result.Outcome);
            Assert.Equal("Họp mới", result.Meeting!.Title);
            Assert.Equal(MeetingStatus.Draft, result.Meeting.Status);
            Assert.False(result.Rescheduled);
            Assert.Equal(Round1Data.Now, meeting.UpdatedAt);
            Assert.Empty(_inner.History);
            Assert.Equal(1, _unitOfWork.Saves);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.UpdateAsync/UTC002 refuses a meeting waiting for approval")]
        [Trait("UTC", "MeetingService.UpdateAsync/UTC002")]
        [Trait("Trait", "UTC")]
        public async Task Update_waiting_approval_is_not_editable()
        {
            var actor = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.WaitingApproval, actor);
            _inner.Meetings.Add(meeting);
            var called = false;
            Task<MeetingInput> Factory(CancellationToken ct)
            {
                called = true;
                return Task.FromResult(Round1Data.InputOf(meeting));
            }

            var result = await Service().UpdateAsync(meeting.Id, Factory, actor);

            Assert.Equal(MeetingOutcome.NotEditable, result.Outcome);
            Assert.False(called);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.UpdateAsync/UTC003 rejects a blank title")]
        [Trait("UTC", "MeetingService.UpdateAsync/UTC003")]
        [Trait("Trait", "UTC")]
        public async Task Update_rejects_blank_title()
        {
            var actor = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Draft, actor);
            _inner.Meetings.Add(meeting);
            var input = Round1Data.InputOf(meeting, title: "");

            var result = await Service().UpdateAsync(meeting.Id, input, actor);

            Assert.Equal(MeetingOutcome.InvalidInformation, result.Outcome);
            Assert.True(result.Errors!.ContainsKey("title"));
            Assert.Equal(0, _unitOfWork.Saves);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.UpdateAsync/UTC004 reschedules a published meeting")]
        [Trait("UTC", "MeetingService.UpdateAsync/UTC004")]
        [Trait("Trait", "UTC")]
        public async Task Update_published_reschedule_notifies_invitees()
        {
            var actor = Guid.CreateVersion7();
            var start = Round1Data.Now.AddDays(2);
            var meeting = Round1Data.NewMeeting(MeetingStatus.Published, actor, start, start.AddHours(1));
            var chair = Guid.CreateVersion7();
            var secretary = Guid.CreateVersion7();
            var member = Guid.CreateVersion7();
            Round1Data.AddParticipant(meeting, chair, "Trần Chủ toạ", MeetingRole.Chair);
            Round1Data.AddParticipant(meeting, secretary, "Lê Thư ký", MeetingRole.Secretary);
            Round1Data.AddParticipant(meeting, member, "Phạm Thành viên", MeetingRole.Member);
            _inner.Meetings.Add(meeting);
            var previous = (meeting.PlannedStartTime, meeting.PlannedEndTime);
            var input = Round1Data.InputOf(meeting) with
            {
                PlannedStartTime = Round1Data.Stamp(previous.Item1.AddDays(1)),
                PlannedEndTime = Round1Data.Stamp(previous.Item2.AddDays(1)),
            };

            var result = await Service().UpdateAsync(meeting.Id, input, actor);

            Assert.Equal(MeetingOutcome.Success, result.Outcome);
            Assert.True(result.Rescheduled);
            Assert.Equal(MeetingStatus.Published, result.Meeting!.Status);
            Assert.Equal(previous.Item1.AddDays(1), meeting.PlannedStartTime);
            var history = Assert.Single(_inner.History);
            Assert.Equal(MeetingHistoryAction.Reschedule, history.Action);
            Assert.Equal(MeetingStatus.Published, history.FromStatus);
            Assert.Equal(MeetingStatus.Published, history.ToStatus);
            Assert.Contains(_stager.Calls, c => c.StartsWith("reschedule:", StringComparison.Ordinal));
            var notified = _inner.Notifications.Select(n => n.UserId).ToArray();
            Assert.Equal(3, notified.Length);
            Assert.DoesNotContain(actor, notified);
            Assert.Contains(chair, notified);
            Assert.All(_inner.Notifications, n => Assert.Equal("Cuộc họp đã dời lịch", n.Title));
            Assert.Equal(1, _unitOfWork.Saves);
            Assert.Empty(_logger.Entries);
        }
    }

    public sealed class MeetReadinessRound1Tests
    {
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeMeetingRepository _inner = new();
        private readonly Round1Meetings _meetings;
        private readonly Round1Logger<MeetingService> _logger = new();

        public MeetReadinessRound1Tests() => _meetings = new Round1Meetings(_inner);

        private MeetingService Service() => new(_meetings, new FakeMeetingInvitationStager(), _unitOfWork, _logger);

        [Fact(DisplayName = "MeetingService.GetSubmissionReadinessAsync/UTC001 reports a complete draft ready")]
        [Trait("UTC", "MeetingService.GetSubmissionReadinessAsync/UTC001")]
        [Trait("Trait", "UTC")]
        public async Task Readiness_complete_draft_is_ready()
        {
            var actor = Guid.CreateVersion7();
            _inner.Meetings.Add(Round1Data.NewMeeting(MeetingStatus.Draft, actor));
            _inner.Capabilities.Add((actor, Capability.MeetingCreator));

            var result = await Service().GetSubmissionReadinessAsync(_inner.Meetings[0].Id, actor);

            Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
            Assert.Equal(MeetingStatus.Draft, result.Status);
            Assert.Empty(result.Errors);
            Assert.True(result.Ready);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.GetSubmissionReadinessAsync/UTC002 reports title errors on a draft")]
        [Trait("UTC", "MeetingService.GetSubmissionReadinessAsync/UTC002")]
        [Trait("Trait", "UTC")]
        public async Task Readiness_empty_title_is_not_ready()
        {
            var actor = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Draft, actor);
            meeting.Title = "";
            _inner.Meetings.Add(meeting);
            _inner.Capabilities.Add((actor, Capability.MeetingCreator));

            var result = await Service().GetSubmissionReadinessAsync(meeting.Id, actor);

            Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
            Assert.True(result.Errors.ContainsKey("title"));
            Assert.False(result.Ready);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.GetSubmissionReadinessAsync/UTC003 refuses a chair who did not create it")]
        [Trait("UTC", "MeetingService.GetSubmissionReadinessAsync/UTC003")]
        [Trait("Trait", "UTC")]
        public async Task Readiness_chair_without_ownership_is_forbidden()
        {
            var creator = Guid.CreateVersion7();
            var chair = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Draft, creator);
            Round1Data.AddParticipant(meeting, chair, "Trần Chủ toạ", MeetingRole.Chair);
            _inner.Meetings.Add(meeting);

            var result = await Service().GetSubmissionReadinessAsync(meeting.Id, chair);

            Assert.Equal(MeetingWorkflowOutcome.Forbidden, result.Outcome);
            Assert.Equal(MeetingStatus.Draft, result.Status);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.GetSubmissionReadinessAsync/UTC004 reports conflict while waiting for approval")]
        [Trait("UTC", "MeetingService.GetSubmissionReadinessAsync/UTC004")]
        [Trait("Trait", "UTC")]
        public async Task Readiness_waiting_approval_conflicts()
        {
            var actor = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.WaitingApproval, actor);
            _inner.Meetings.Add(meeting);
            _inner.Capabilities.Add((actor, Capability.MeetingCreator));

            var result = await Service().GetSubmissionReadinessAsync(meeting.Id, actor);

            Assert.Equal(MeetingWorkflowOutcome.StatusConflict, result.Outcome);
            Assert.Equal(MeetingStatus.WaitingApproval, result.Status);
            Assert.Empty(_logger.Entries);
        }
    }

    public sealed class MeetHistoryRound1Tests
    {
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeMeetingRepository _inner = new();
        private readonly Round1Meetings _meetings;
        private readonly Round1Logger<MeetingService> _logger = new();

        public MeetHistoryRound1Tests() => _meetings = new Round1Meetings(_inner);

        private MeetingService Service() => new(_meetings, new FakeMeetingInvitationStager(), _unitOfWork, _logger);

        private static MeetingHistoryRecord Row(Guid meetingId, MeetingHistoryAction action, Guid actor, string? reason,
            Guid? previousOrganizer = null, Guid? nextOrganizer = null) => new(
            Guid.CreateVersion7(), meetingId, action, MeetingStatus.Draft, MeetingStatus.WaitingApproval,
            actor, "Người thực hiện", Round1Data.Now, reason, null, previousOrganizer, nextOrganizer, null);

        [Fact(DisplayName = "MeetingService.GetHistoryAsync/UTC001 shows the full history to the creator")]
        [Trait("UTC", "MeetingService.GetHistoryAsync/UTC001")]
        [Trait("Trait", "UTC")]
        public async Task History_creator_sees_every_field()
        {
            var actor = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.WaitingApproval, actor);
            var approver = Guid.CreateVersion7();
            meeting.AssignedApproverId = approver;
            _inner.Meetings.Add(meeting);
            var oldOrganizer = Guid.CreateVersion7();
            var newOrganizer = Guid.CreateVersion7();
            var items = new[]
            {
                Row(meeting.Id, MeetingHistoryAction.SubmitForApproval, actor, null),
                Row(meeting.Id, MeetingHistoryAction.Reject, approver, "Thiếu tài liệu"),
                Row(meeting.Id, MeetingHistoryAction.TransferOrganizer, actor, null, oldOrganizer, newOrganizer),
            };
            _meetings.HistoryPage = (id, page, size) => new MeetingHistoryPage(items, page, size, 3);

            var result = await Service().GetHistoryAsync(meeting.Id, actor, 1, 20);

            Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
            Assert.Equal(3, result.Page!.Items.Count);
            Assert.Equal(1, result.Page.Page);
            Assert.Equal(20, result.Page.PageSize);
            Assert.Equal(3, result.Page.TotalCount);
            Assert.Equal("Thiếu tài liệu", result.Page.Items[1].RejectionReason);
            Assert.Equal(oldOrganizer, result.Page.Items[2].PreviousOrganizerId);
            Assert.Equal(newOrganizer, result.Page.Items[2].NewOrganizerId);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.GetHistoryAsync/UTC002 rejects a page size of 101 without reading")]
        [Trait("UTC", "MeetingService.GetHistoryAsync/UTC002")]
        [Trait("Trait", "UTC")]
        public async Task History_page_size_101_is_invalid()
        {
            var result = await Service().GetHistoryAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), 1, 101);

            Assert.Equal(MeetingWorkflowOutcome.InvalidRequest, result.Outcome);
            Assert.Empty(_meetings.Reads);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.GetHistoryAsync/UTC003 answers NotFound for an unknown meeting")]
        [Trait("UTC", "MeetingService.GetHistoryAsync/UTC003")]
        [Trait("Trait", "UTC")]
        public async Task History_unknown_meeting_is_not_found()
        {
            var result = await Service().GetHistoryAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), 1, 20);

            Assert.Equal(MeetingWorkflowOutcome.NotFound, result.Outcome);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.GetHistoryAsync/UTC004 returns an empty page beyond the rows")]
        [Trait("UTC", "MeetingService.GetHistoryAsync/UTC004")]
        [Trait("Trait", "UTC")]
        public async Task History_page_beyond_rows_is_empty()
        {
            var actor = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Ended, actor);
            _inner.Meetings.Add(meeting);
            _meetings.HistoryPage = (id, page, size) => new MeetingHistoryPage([], page, size, 3);

            var result = await Service().GetHistoryAsync(meeting.Id, actor, 50, 20);

            Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
            Assert.Empty(result.Page!.Items);
            Assert.Equal(50, result.Page.Page);
            Assert.Equal(3, result.Page.TotalCount);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.GetHistoryAsync/UTC005 hides reasons and organizers from a member")]
        [Trait("UTC", "MeetingService.GetHistoryAsync/UTC005")]
        [Trait("Trait", "UTC")]
        public async Task History_member_view_is_masked()
        {
            var creator = Guid.CreateVersion7();
            var member = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Published, creator);
            Round1Data.AddParticipant(meeting, member, "Phạm Thành viên", MeetingRole.Member);
            _inner.Meetings.Add(meeting);
            var items = new[] { Row(meeting.Id, MeetingHistoryAction.Reject, creator, "Lý do", creator, member) };
            _meetings.HistoryPage = (id, page, size) => new MeetingHistoryPage(items, page, size, 1);

            var result = await Service().GetHistoryAsync(meeting.Id, member, 1, 20);

            Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
            var row = Assert.Single(result.Page!.Items);
            Assert.Null(row.RejectionReason);
            Assert.Null(row.PreviousOrganizerId);
            Assert.Null(row.NewOrganizerId);
            Assert.Empty(_logger.Entries);
        }
    }

    public sealed class MeetTransitionRound1Tests
    {
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeMeetingRepository _inner = new();
        private readonly Round1Meetings _meetings;
        private readonly Round1Stager _stager = new();
        private readonly FakeMeetingEffects _effects = new();
        private readonly Round1Logger<MeetingService> _logger = new();
        private readonly FixedClock _clock = new(Round1Data.Now);

        public MeetTransitionRound1Tests() => _meetings = new Round1Meetings(_inner);

        private MeetingService Service() => new(_meetings, _stager, _unitOfWork, _logger, _clock, _effects);

        [Fact(DisplayName = "MeetingService.TransitionAsync/UTC001 submits a draft for approval")]
        [Trait("UTC", "MeetingService.TransitionAsync/UTC001")]
        [Trait("Trait", "UTC")]
        public async Task Transition_submit_draft_sets_approver_and_notifies()
        {
            var organizer = Guid.CreateVersion7();
            var approver = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Draft, organizer);
            _inner.Meetings.Add(meeting);
            _inner.Capabilities.Add((organizer, Capability.MeetingCreator));
            _meetings.EligibleApprovers.Add(approver);
            var command = new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, approver);

            var result = await Service().TransitionAsync(meeting.Id, command, organizer);

            Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
            Assert.Equal(MeetingStatus.WaitingApproval, meeting.Status);
            Assert.Equal(approver, meeting.AssignedApproverId);
            var history = Assert.Single(_inner.History);
            Assert.Equal(MeetingHistoryAction.SubmitForApproval, history.Action);
            Assert.Equal(approver, history.AssignedApproverId);
            var notice = Assert.Single(_inner.Notifications);
            Assert.Equal(approver, notice.UserId);
            Assert.Equal("Cuộc họp cần được duyệt", notice.Title);
            Assert.Equal(1, _unitOfWork.Saves);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.TransitionAsync/UTC002 refuses to submit a draft with an empty title")]
        [Trait("UTC", "MeetingService.TransitionAsync/UTC002")]
        [Trait("Trait", "UTC")]
        public async Task Transition_submit_with_empty_title_is_not_ready()
        {
            var organizer = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Draft, organizer);
            meeting.Title = "";
            _inner.Meetings.Add(meeting);
            _inner.Capabilities.Add((organizer, Capability.MeetingCreator));
            var command = new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, Guid.CreateVersion7());

            var result = await Service().TransitionAsync(meeting.Id, command, organizer);

            Assert.Equal(MeetingWorkflowOutcome.SubmissionNotReady, result.Outcome);
            Assert.True(result.Errors!.ContainsKey("title"));
            Assert.Equal(MeetingStatus.Draft, meeting.Status);
            Assert.Empty(_inner.History);
            Assert.Equal(0, _unitOfWork.Saves);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.TransitionAsync/UTC003 approves a waiting meeting")]
        [Trait("UTC", "MeetingService.TransitionAsync/UTC003")]
        [Trait("Trait", "UTC")]
        public async Task Transition_approve_by_assigned_approver()
        {
            var organizer = Guid.CreateVersion7();
            var approver = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.WaitingApproval, organizer);
            meeting.AssignedApproverId = approver;
            _inner.Meetings.Add(meeting);
            _inner.Capabilities.Add((approver, Capability.MeetingApprover));
            var command = new MeetingTransitionCommand(MeetingTransitionAction.Approve);

            var result = await Service().TransitionAsync(meeting.Id, command, approver);

            Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
            Assert.Equal(MeetingStatus.Approved, meeting.Status);
            Assert.Equal(MeetingHistoryAction.Approve, Assert.Single(_inner.History).Action);
            var notice = Assert.Single(_inner.Notifications);
            Assert.Equal(organizer, notice.UserId);
            Assert.Equal("Cuộc họp đã được duyệt", notice.Title);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.TransitionAsync/UTC004 publishes an approved meeting to its roster")]
        [Trait("UTC", "MeetingService.TransitionAsync/UTC004")]
        [Trait("Trait", "UTC")]
        public async Task Transition_publish_notifies_roster_but_not_organizer()
        {
            var organizer = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Approved, organizer);
            var chair = Guid.CreateVersion7();
            var secretary = Guid.CreateVersion7();
            var first = Guid.CreateVersion7();
            var second = Guid.CreateVersion7();
            Round1Data.AddParticipant(meeting, chair, "Trần Chủ toạ", MeetingRole.Chair);
            Round1Data.AddParticipant(meeting, secretary, "Lê Thư ký", MeetingRole.Secretary);
            Round1Data.AddParticipant(meeting, first, "Phạm Thành viên", MeetingRole.Member);
            Round1Data.AddParticipant(meeting, second, "Ngô Thành viên", MeetingRole.Member);
            _inner.Meetings.Add(meeting);
            _inner.Capabilities.Add((organizer, Capability.MeetingCreator));
            var command = new MeetingTransitionCommand(MeetingTransitionAction.Publish);

            var result = await Service().TransitionAsync(meeting.Id, command, organizer);

            Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
            Assert.Equal(MeetingStatus.Published, meeting.Status);
            Assert.Equal(4, _inner.Notifications.Count);
            Assert.All(_inner.Notifications, n => Assert.Equal("Cuộc họp đã được công bố", n.Title));
            Assert.DoesNotContain(_inner.Notifications, n => n.UserId == organizer);
            Assert.Contains(_stager.Calls, c => c == "publication");
            Assert.Equal(MeetingHistoryAction.Publish, Assert.Single(_inner.History).Action);
            Assert.Empty(_logger.Entries);
        }

        [Fact(DisplayName = "MeetingService.TransitionAsync/UTC005 cancels a published meeting with a trimmed reason")]
        [Trait("UTC", "MeetingService.TransitionAsync/UTC005")]
        [Trait("Trait", "UTC")]
        public async Task Transition_cancel_published_trims_reason_and_clears_drafts()
        {
            var organizer = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Published, organizer);
            _inner.Meetings.Add(meeting);
            _inner.Capabilities.Add((organizer, Capability.MeetingCreator));
            var command = new MeetingTransitionCommand(MeetingTransitionAction.Cancel, Reason: "  Hoãn do bão  ");

            var result = await Service().TransitionAsync(meeting.Id, command, organizer);

            Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
            Assert.Equal(MeetingStatus.Cancelled, meeting.Status);
            Assert.Equal("Hoãn do bão", meeting.CancellationReason);
            Assert.Equal(MeetingStatus.Published, meeting.CancelledFromStatus);
            Assert.Equal(organizer, meeting.CancelledById);
            var history = Assert.Single(_inner.History);
            Assert.Equal(MeetingHistoryAction.Cancel, history.Action);
            Assert.Equal("Hoãn do bão", history.Reason);
            Assert.Contains(_effects.Calls, c => c == "votes.deleteDraftsOnCancel");
            Assert.Contains(_stager.Calls, c => c == $"cancellation:{organizer}:Hoãn do bão");
            Assert.Empty(_logger.Entries);
        }
    }

    public sealed class MeetLifecycleRound1Tests
    {
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeMeetingRepository _inner = new();
        private readonly Round1Meetings _meetings;
        private readonly FakeMeetingEffects _effects = new();
        private readonly Round1Logger<MeetingLifecycleService> _logger = new();
        private readonly FixedClock _clock = new(Round1Data.Now);

        public MeetLifecycleRound1Tests() => _meetings = new Round1Meetings(_inner);

        private MeetingLifecycleService Service() => new(
            _meetings, _unitOfWork, _effects, _effects, _effects, _effects, _effects, _effects, _effects,
            _clock, _logger);

        private static MeetingTransitionCommand Start() => new(MeetingTransitionAction.Start);

        private static MeetingTransitionCommand End() => new(MeetingTransitionAction.End);

        [Fact(DisplayName = "MeetingLifecycleService.TransitionAsync/UTC001 starts a published meeting by its chair")]
        [Trait("UTC", "MeetingLifecycleService.TransitionAsync/UTC001")]
        [Trait("Trait", "UTC")]
        public async Task Lifecycle_chair_starts_published_meeting()
        {
            var chair = Guid.CreateVersion7();
            var secretary = Guid.CreateVersion7();
            var first = Guid.CreateVersion7();
            var second = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Published, Guid.CreateVersion7());
            Round1Data.AddParticipant(meeting, chair, "Trần Chủ toạ", MeetingRole.Chair);
            Round1Data.AddParticipant(meeting, secretary, "Lê Thư ký", MeetingRole.Secretary);
            Round1Data.AddParticipant(meeting, first, "Phạm Thành viên", MeetingRole.Member);
            Round1Data.AddParticipant(meeting, second, "Ngô Thành viên", MeetingRole.Member);
            _inner.Meetings.Add(meeting);

            var result = await Service().TransitionAsync(meeting.Id, Start(), chair);

            Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
            Assert.Equal(MeetingStatus.InProgress, meeting.Status);
            Assert.Equal(Round1Data.Now, meeting.ActualStartedAt);
            Assert.Equal(
                new[] { secretary, first, second }.Order().ToArray(),
                _effects.StartedRecipients!.Order().ToArray());
            var history = Assert.Single(_inner.History);
            Assert.Equal(MeetingHistoryAction.Start, history.Action);
            Assert.Equal(chair, history.ActorId);
            Assert.Contains(_effects.Calls, c => c == "notifications.started");
            Assert.DoesNotContain(_logger.Entries, e => e.Level >= LogLevel.Error);
        }

        [Fact(DisplayName = "MeetingLifecycleService.TransitionAsync/UTC002 ends an online meeting by its chair")]
        [Trait("UTC", "MeetingLifecycleService.TransitionAsync/UTC002")]
        [Trait("Trait", "UTC")]
        public async Task Lifecycle_chair_ends_online_meeting()
        {
            var chair = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.InProgress, Guid.CreateVersion7(), mode: MeetingMode.Online);
            Round1Data.AddParticipant(meeting, chair, "Trần Chủ toạ", MeetingRole.Chair);
            _inner.Meetings.Add(meeting);

            var result = await Service().TransitionAsync(meeting.Id, End(), chair);

            Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
            Assert.Equal(MeetingStatus.Ended, meeting.Status);
            Assert.Equal(Round1Data.Now, meeting.ActualEndedAt);
            Assert.Equal(
                new[] { "agenda.prepare", "speaking.prepare", "votes.deleteDrafts", "agenda.publish:MeetingEnded", "room.close" },
                _effects.Calls);
            Assert.DoesNotContain(_effects.Calls, c => c.StartsWith("votes.publish", StringComparison.Ordinal));
            var history = Assert.Single(_inner.History);
            Assert.Equal(MeetingHistoryAction.End, history.Action);
            Assert.DoesNotContain(_logger.Entries, e => e.Level >= LogLevel.Error);
        }

        [Fact(DisplayName = "MeetingLifecycleService.TransitionAsync/UTC003 ends an in-person meeting without closing a room")]
        [Trait("UTC", "MeetingLifecycleService.TransitionAsync/UTC003")]
        [Trait("Trait", "UTC")]
        public async Task Lifecycle_chair_ends_inperson_meeting_without_room_closure()
        {
            var chair = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.InProgress, Guid.CreateVersion7(), mode: MeetingMode.InPerson);
            Round1Data.AddParticipant(meeting, chair, "Trần Chủ toạ", MeetingRole.Chair);
            _inner.Meetings.Add(meeting);

            var result = await Service().TransitionAsync(meeting.Id, End(), chair);

            Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
            Assert.Equal(MeetingStatus.Ended, meeting.Status);
            Assert.DoesNotContain(_effects.Calls, c => c == "room.close");
            Assert.Contains(_effects.Calls, c => c == "agenda.publish:MeetingEnded");
            Assert.DoesNotContain(_logger.Entries, e => e.Level >= LogLevel.Error);
        }

        [Fact(DisplayName = "MeetingLifecycleService.TransitionAsync/UTC004 answers NotFound for an unknown meeting")]
        [Trait("UTC", "MeetingLifecycleService.TransitionAsync/UTC004")]
        [Trait("Trait", "UTC")]
        public async Task Lifecycle_unknown_meeting_is_not_found()
        {
            var result = await Service().TransitionAsync(Guid.CreateVersion7(), Start(), Guid.CreateVersion7());

            Assert.Equal(MeetingWorkflowOutcome.NotFound, result.Outcome);
            Assert.DoesNotContain(_logger.Entries, e => e.Level >= LogLevel.Error);
        }

        [Fact(DisplayName = "MeetingLifecycleService.TransitionAsync/UTC005 starts a published meeting by its secretary")]
        [Trait("UTC", "MeetingLifecycleService.TransitionAsync/UTC005")]
        [Trait("Trait", "UTC")]
        public async Task Lifecycle_secretary_starts_published_meeting()
        {
            var chair = Guid.CreateVersion7();
            var secretary = Guid.CreateVersion7();
            var member = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Published, Guid.CreateVersion7());
            Round1Data.AddParticipant(meeting, chair, "Trần Chủ toạ", MeetingRole.Chair);
            Round1Data.AddParticipant(meeting, secretary, "Lê Thư ký", MeetingRole.Secretary);
            Round1Data.AddParticipant(meeting, member, "Phạm Thành viên", MeetingRole.Member);
            _inner.Meetings.Add(meeting);

            var result = await Service().TransitionAsync(meeting.Id, Start(), secretary);

            Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
            Assert.Equal(MeetingStatus.InProgress, meeting.Status);
            Assert.Equal(Round1Data.Now, meeting.ActualStartedAt);
            Assert.Equal(
                new[] { chair, member }.Order().ToArray(),
                _effects.StartedRecipients!.Order().ToArray());
            var history = Assert.Single(_inner.History);
            Assert.Equal(MeetingHistoryAction.Start, history.Action);
            Assert.Equal(secretary, history.ActorId);
            Assert.DoesNotContain(_logger.Entries, e => e.Level >= LogLevel.Error);
        }

        [Fact(DisplayName = "MeetingLifecycleService.TransitionAsync/UTC006 refuses an end from a deactivated chair")]
        [Trait("UTC", "MeetingLifecycleService.TransitionAsync/UTC006")]
        [Trait("Trait", "UTC")]
        public async Task Lifecycle_deactivated_chair_cannot_end()
        {
            var chair = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.InProgress, Guid.CreateVersion7());
            Round1Data.AddParticipant(meeting, chair, "Trần Chủ toạ", MeetingRole.Chair, active: false);
            _inner.Meetings.Add(meeting);

            var result = await Service().TransitionAsync(meeting.Id, End(), chair);

            Assert.Equal(MeetingWorkflowOutcome.Forbidden, result.Outcome);
            Assert.Equal(MeetingStatus.InProgress, meeting.Status);
            Assert.Empty(_inner.History);
            Assert.DoesNotContain(_logger.Entries, e => e.Level >= LogLevel.Error);
        }
    }

    public sealed class MeetCancelOverdueRound1Tests
    {
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeMeetingRepository _inner = new();
        private readonly Round1Meetings _meetings;
        private readonly Round1Lifecycle _lifecycle = new();
        private readonly FakeMeetingEffects _effects = new();
        private readonly Round1Logger<MeetingOverdueService> _logger = new();
        private readonly FixedClock _clock = new(Round1Data.Now);

        public MeetCancelOverdueRound1Tests() => _meetings = new Round1Meetings(_inner);

        private MeetingOverdueService Service(IVoteMeetingEnd? voteEnd = null) => new(
            _meetings, _unitOfWork, _lifecycle,
            Options.Create(new MeetingOverdueOptions()), _clock, _logger, null, voteEnd);

        private Meeting GivenOverdue(MeetingStatus status, DateTimeOffset start)
        {
            var meeting = Round1Data.NewMeeting(status, Guid.CreateVersion7(), start, start.AddHours(1));
            _inner.Meetings.Add(meeting);
            return meeting;
        }

        [Fact(DisplayName = "MeetingOverdueService.CancelOverdueAsync/UTC001 cancels a draft overdue by 30 hours")]
        [Trait("UTC", "MeetingOverdueService.CancelOverdueAsync/UTC001")]
        [Trait("Trait", "UTC")]
        public async Task CancelOverdue_draft_30h_is_cancelled_with_drafts_cleared()
        {
            var meeting = GivenOverdue(MeetingStatus.Draft, Round1Data.Now.AddHours(-30));

            var cancelled = await Service(_effects).CancelOverdueAsync();

            Assert.Equal(1, cancelled);
            Assert.Equal(MeetingStatus.Cancelled, meeting.Status);
            Assert.Null(meeting.CancelledById);
            Assert.Equal(Round1Data.Now, meeting.CancelledAt);
            Assert.Equal(MeetingStatus.Draft, meeting.CancelledFromStatus);
            Assert.Equal("Không diễn ra theo lịch: quá 24 giờ sau giờ bắt đầu dự kiến mà chưa bắt đầu.",
                meeting.CancellationReason);
            var history = Assert.Single(_inner.History);
            Assert.Equal(MeetingHistoryAction.AutoCancel, history.Action);
            Assert.Null(history.ActorId);
            var notice = Assert.Single(_inner.Notifications);
            Assert.Equal(meeting.CreatedById, notice.UserId);
            Assert.Equal("Cuộc họp đã tự huỷ", notice.Title);
            Assert.Contains(_effects.Calls, c => c == "votes.deleteDraftsOnCancel");
            Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Information
                && e.Message.Contains(meeting.Id.ToString()));
        }

        [Fact(DisplayName = "MeetingOverdueService.CancelOverdueAsync/UTC002 cancels a meeting starting exactly 24 hours ago")]
        [Trait("UTC", "MeetingOverdueService.CancelOverdueAsync/UTC002")]
        [Trait("Trait", "UTC")]
        public async Task CancelOverdue_exactly_24h_is_cancelled()
        {
            var meeting = GivenOverdue(MeetingStatus.Published, Round1Data.Now.AddHours(-24));

            var cancelled = await Service().CancelOverdueAsync();

            Assert.Equal(1, cancelled);
            Assert.Equal(MeetingStatus.Cancelled, meeting.Status);
            Assert.Equal(MeetingStatus.Published, meeting.CancelledFromStatus);
        }

        [Fact(DisplayName = "MeetingOverdueService.CancelOverdueAsync/UTC003 leaves a meeting 23h59m overdue untouched")]
        [Trait("UTC", "MeetingOverdueService.CancelOverdueAsync/UTC003")]
        [Trait("Trait", "UTC")]
        public async Task CancelOverdue_23h59m_is_not_touched()
        {
            var meeting = GivenOverdue(MeetingStatus.Draft, Round1Data.Now.AddHours(-23).AddMinutes(-59));

            var cancelled = await Service().CancelOverdueAsync();

            Assert.Equal(0, cancelled);
            Assert.Equal(MeetingStatus.Draft, meeting.Status);
            Assert.Empty(_inner.History);
            Assert.Empty(_inner.Notifications);
        }

        [Fact(DisplayName = "MeetingOverdueService.CancelOverdueAsync/UTC004 skips a meeting started before the re-check")]
        [Trait("UTC", "MeetingOverdueService.CancelOverdueAsync/UTC004")]
        [Trait("Trait", "UTC")]
        public async Task CancelOverdue_inprogress_on_recheck_is_skipped()
        {
            var meeting = GivenOverdue(MeetingStatus.InProgress, Round1Data.Now.AddHours(-30));
            _inner.Listed = [meeting.Id];

            var cancelled = await Service().CancelOverdueAsync();

            Assert.Equal(0, cancelled);
            Assert.Equal(MeetingStatus.InProgress, meeting.Status);
            Assert.Empty(_inner.History);
        }

        [Fact(DisplayName = "MeetingOverdueService.CancelOverdueAsync/UTC005 cancels at most 50 meetings per sweep")]
        [Trait("UTC", "MeetingOverdueService.CancelOverdueAsync/UTC005")]
        [Trait("Trait", "UTC")]
        public async Task CancelOverdue_caps_at_50_per_sweep()
        {
            for (var i = 0; i < 60; i++)
                GivenOverdue(MeetingStatus.Draft, Round1Data.Now.AddHours(-30).AddMinutes(-i));

            var cancelled = await Service().CancelOverdueAsync();

            Assert.Equal(50, cancelled);
        }
    }

    public sealed class MeetEndOverrunRound1Tests
    {
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeMeetingRepository _inner = new();
        private readonly Round1Meetings _meetings;
        private readonly Round1Lifecycle _lifecycle = new();
        private readonly Round1Logger<MeetingOverdueService> _logger = new();
        private readonly FixedClock _clock = new(Round1Data.Now);

        public MeetEndOverrunRound1Tests() => _meetings = new Round1Meetings(_inner);

        private MeetingOverdueService Service(int autoEndHours = 24) => new(
            _meetings, _unitOfWork, _lifecycle,
            Options.Create(new MeetingOverdueOptions { AutoEndAfterHours = autoEndHours }),
            _clock, _logger);

        private Meeting GivenOverrun(DateTimeOffset? end = null, DateTimeOffset? started = null)
        {
            var plannedEnd = end ?? Round1Data.Now.AddHours(-25);
            var meeting = Round1Data.NewMeeting(MeetingStatus.InProgress, Guid.CreateVersion7(),
                plannedEnd.AddHours(-1), plannedEnd);
            meeting.ActualStartedAt = started ?? Round1Data.Now.AddHours(-26);
            _inner.Meetings.Add(meeting);
            return meeting;
        }

        [Fact(DisplayName = "MeetingOverdueService.EndOverrunAsync/UTC001 returns zero when nothing is overrun")]
        [Trait("UTC", "MeetingOverdueService.EndOverrunAsync/UTC001")]
        [Trait("Trait", "UTC")]
        public async Task EndOverrun_empty_returns_zero_without_calling_lifecycle()
        {
            var ended = await Service().EndOverrunAsync();

            Assert.Equal(0, ended);
            Assert.Empty(_lifecycle.EndCalls);
        }

        [Fact(DisplayName = "MeetingOverdueService.EndOverrunAsync/UTC002 ends three overrun meetings")]
        [Trait("UTC", "MeetingOverdueService.EndOverrunAsync/UTC002")]
        [Trait("Trait", "UTC")]
        public async Task EndOverrun_three_meetings_all_end()
        {
            GivenOverrun();
            GivenOverrun();
            GivenOverrun();
            _lifecycle.EndAnswer = _ => true;

            var ended = await Service().EndOverrunAsync();

            Assert.Equal(3, ended);
            Assert.Equal(3, _lifecycle.EndCalls.Count);
        }

        [Fact(DisplayName = "MeetingOverdueService.EndOverrunAsync/UTC003 skips a meeting the chair already ended")]
        [Trait("UTC", "MeetingOverdueService.EndOverrunAsync/UTC003")]
        [Trait("Trait", "UTC")]
        public async Task EndOverrun_already_ended_meeting_is_not_counted()
        {
            GivenOverrun();
            var endedByChair = GivenOverrun();
            GivenOverrun();
            _lifecycle.EndAnswer = id => id != endedByChair.Id;

            var ended = await Service().EndOverrunAsync();

            Assert.Equal(2, ended);
            Assert.Equal(3, _lifecycle.EndCalls.Count);
        }

        [Fact(DisplayName = "MeetingOverdueService.EndOverrunAsync/UTC004 uses a 48-hour cutoff and reason")]
        [Trait("UTC", "MeetingOverdueService.EndOverrunAsync/UTC004")]
        [Trait("Trait", "UTC")]
        public async Task EndOverrun_48h_cutoff_and_reason()
        {
            var meeting = GivenOverrun(Round1Data.Now.AddHours(-49), Round1Data.Now.AddHours(-50));

            var ended = await Service(48).EndOverrunAsync();

            Assert.Equal(1, ended);
            var call = Assert.Single(_lifecycle.EndCalls);
            Assert.Equal(meeting.Id, call.MeetingId);
            Assert.Equal(Round1Data.Now.AddHours(-48), call.Cutoff);
            Assert.Equal("Đang diễn ra quá 48 giờ sau giờ kết thúc dự kiến mà chưa ai kết thúc.", call.Reason);
        }
    }

    public sealed class MeetAudienceRound1Tests
    {
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeMeetingRepository _inner = new();
        private readonly Round1Meetings _meetings;
        private readonly Round1AudienceQuery _query = new();
        private readonly Round1Guests _guests = new();
        private readonly FakeParticipantRepository _participants = new();

        public MeetAudienceRound1Tests() => _meetings = new Round1Meetings(_inner);

        private MeetingAudience Audience() => new(_unitOfWork, _meetings, _query, _guests, _participants, _guests);

        private static MeetingGuestSession Session(Guid meetingId)
            => new(meetingId, Guid.CreateVersion7(), Guid.CreateVersion7());

        [Fact(DisplayName = "MeetingAudience.CanGuestJoinAsync/UTC001 lets a live session join its published meeting")]
        [Trait("UTC", "MeetingAudience.CanGuestJoinAsync/UTC001")]
        [Trait("Trait", "UTC")]
        public async Task GuestJoin_live_session_joins_published_meeting()
        {
            var meeting = Round1Data.NewMeeting(MeetingStatus.Published, Guid.CreateVersion7());
            _inner.Meetings.Add(meeting);
            var session = Session(meeting.Id);
            _guests.ReadableSessions.Add(session.SessionId);

            var joined = await Audience().CanGuestJoinAsync(session, meeting.Id, CancellationToken.None);

            Assert.True(joined);
        }

        [Fact(DisplayName = "MeetingAudience.CanGuestJoinAsync/UTC002 refuses a session with an empty id")]
        [Trait("UTC", "MeetingAudience.CanGuestJoinAsync/UTC002")]
        [Trait("Trait", "UTC")]
        public async Task GuestJoin_empty_session_id_loads_nothing()
        {
            var meeting = Round1Data.NewMeeting(MeetingStatus.Published, Guid.CreateVersion7());
            _inner.Meetings.Add(meeting);
            var session = new MeetingGuestSession(meeting.Id, Guid.CreateVersion7(), Guid.Empty);

            var joined = await Audience().CanGuestJoinAsync(session, meeting.Id, CancellationToken.None);

            Assert.False(joined);
            Assert.Empty(_meetings.Reads);
        }

        [Fact(DisplayName = "MeetingAudience.CanGuestJoinAsync/UTC003 refuses a session after the meeting ended")]
        [Trait("UTC", "MeetingAudience.CanGuestJoinAsync/UTC003")]
        [Trait("Trait", "UTC")]
        public async Task GuestJoin_ended_meeting_is_closed_to_guests()
        {
            var meeting = Round1Data.NewMeeting(MeetingStatus.Ended, Guid.CreateVersion7());
            _inner.Meetings.Add(meeting);
            var session = Session(meeting.Id);
            _guests.ReadableSessions.Add(session.SessionId);

            var joined = await Audience().CanGuestJoinAsync(session, meeting.Id, CancellationToken.None);

            Assert.False(joined);
        }

        [Fact(DisplayName = "MeetingAudience.CanUserJoinAsync/UTC001 lets an active member join a published meeting")]
        [Trait("UTC", "MeetingAudience.CanUserJoinAsync/UTC001")]
        [Trait("Trait", "UTC")]
        public async Task UserJoin_active_member_joins_published_meeting()
        {
            var member = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Published, Guid.CreateVersion7());
            _inner.Meetings.Add(meeting);
            _inner.ActiveUsers.Add(member);
            _inner.Accessible.Add((meeting.Id, member));

            var joined = await Audience().CanUserJoinAsync(meeting.Id, member, CancellationToken.None);

            Assert.True(joined);
        }

        [Fact(DisplayName = "MeetingAudience.CanUserJoinAsync/UTC002 refuses a deactivated chair without a read lookup")]
        [Trait("UTC", "MeetingAudience.CanUserJoinAsync/UTC002")]
        [Trait("Trait", "UTC")]
        public async Task UserJoin_deactivated_chair_skips_readability_lookup()
        {
            var chair = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Published, Guid.CreateVersion7());
            _inner.Meetings.Add(meeting);
            _inner.Accessible.Add((meeting.Id, chair));

            var joined = await Audience().CanUserJoinAsync(meeting.Id, chair, CancellationToken.None);

            Assert.False(joined);
            Assert.DoesNotContain(_meetings.Reads, r => r.StartsWith("accessible:", StringComparison.Ordinal));
        }

        [Fact(DisplayName = "MeetingAudience.CanUserJoinAsync/UTC003 refuses a member who may not read a draft")]
        [Trait("UTC", "MeetingAudience.CanUserJoinAsync/UTC003")]
        [Trait("Trait", "UTC")]
        public async Task UserJoin_member_cannot_join_draft()
        {
            var member = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Draft, Guid.CreateVersion7());
            _inner.Meetings.Add(meeting);
            _inner.ActiveUsers.Add(member);

            var joined = await Audience().CanUserJoinAsync(meeting.Id, member, CancellationToken.None);

            Assert.False(joined);
        }

        [Fact(DisplayName = "MeetingAudience.CanUserJoinAsync/UTC004 lets an invitee open a cancelled meeting")]
        [Trait("UTC", "MeetingAudience.CanUserJoinAsync/UTC004")]
        [Trait("Trait", "UTC")]
        public async Task UserJoin_member_joins_cancelled_from_published()
        {
            var member = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Cancelled, Guid.CreateVersion7());
            meeting.CancelledFromStatus = MeetingStatus.Published;
            _inner.Meetings.Add(meeting);
            _inner.ActiveUsers.Add(member);
            _inner.Accessible.Add((meeting.Id, member));

            var joined = await Audience().CanUserJoinAsync(meeting.Id, member, CancellationToken.None);

            Assert.True(joined);
        }
    }

    public sealed class MeetDetailsRound1Tests
    {
        [Fact(DisplayName = "MeetingDetails.From/UTC001 hides the approver of a draft")]
        [Trait("UTC", "MeetingDetails.From/UTC001")]
        [Trait("Trait", "UTC")]
        public void Details_draft_hides_assigned_approver()
        {
            var creator = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Draft, creator);
            meeting.AssignedApproverId = Guid.CreateVersion7();

            var details = MeetingDetails.From(meeting, creator);

            Assert.Null(details.AssignedApproverId);
        }

        [Fact(DisplayName = "MeetingDetails.From/UTC002 orders secretaries and shows a pending member view")]
        [Trait("UTC", "MeetingDetails.From/UTC002")]
        [Trait("Trait", "UTC")]
        public void Details_member_view_orders_secretaries()
        {
            var member = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Published, Guid.CreateVersion7());
            var chair = Round1Data.AddParticipant(meeting, Guid.CreateVersion7(), "Trần Chủ toạ", MeetingRole.Chair);
            Round1Data.AddParticipant(meeting, Guid.CreateVersion7(), "Bình", MeetingRole.Secretary);
            Round1Data.AddParticipant(meeting, Guid.CreateVersion7(), "An", MeetingRole.Secretary);
            Round1Data.AddParticipant(meeting, member, "Phạm Thành viên", MeetingRole.Member);

            var details = MeetingDetails.From(meeting, member);

            Assert.Equal(chair.UserId, details.Chair!.UserId);
            Assert.Equal(["An", "Bình"], details.Secretaries!.Select(s => s.FullName).ToArray());
            Assert.Equal("Member", details.MyMeetingRole);
            Assert.Null(details.MyResponse);
            Assert.True(details.CanRespond);
        }

        [Fact(DisplayName = "MeetingDetails.From/UTC003 shows the cancellation of a cancelled meeting")]
        [Trait("UTC", "MeetingDetails.From/UTC003")]
        [Trait("Trait", "UTC")]
        public void Details_cancelled_shows_cancellation()
        {
            var canceller = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Cancelled, Guid.CreateVersion7());
            meeting.CancelledAt = Round1Data.Now;
            meeting.CancelledById = canceller;
            meeting.CancelledBy = Round1Data.NewUser("Lan");
            meeting.CancellationReason = "Hoãn";

            var details = MeetingDetails.From(meeting, meeting.CreatedById);

            Assert.NotNull(details.Cancellation);
            Assert.Equal(Round1Data.Now, details.Cancellation.CancelledAt);
            Assert.Equal(canceller, details.Cancellation.CancelledById);
            Assert.Equal("Lan", details.Cancellation.CancelledByName);
            Assert.Equal("Hoãn", details.Cancellation.Reason);
        }

        [Fact(DisplayName = "MeetingDetails.From/UTC004 shows only the current chair")]
        [Trait("UTC", "MeetingDetails.From/UTC004")]
        [Trait("Trait", "UTC")]
        public void Details_chair_is_the_current_one()
        {
            var current = Guid.CreateVersion7();
            var former = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Published, Guid.CreateVersion7());
            Round1Data.AddParticipant(meeting, current, "Trần Chủ toạ", MeetingRole.Chair);
            Round1Data.AddParticipant(meeting, former, "Người tiền nhiệm", MeetingRole.Chair,
                assignment: AssignmentStatus.Removed);

            var details = MeetingDetails.From(meeting, meeting.CreatedById);

            Assert.Equal(current, details.Chair!.UserId);
        }
    }

    public sealed class MeetReadPolicyRound1Tests
    {
        private static Meeting GivenMeeting(MeetingStatus status, Guid actor, MeetingRole role,
            MeetingStatus? cancelledFrom = null)
        {
            var meeting = Round1Data.NewMeeting(status, Guid.CreateVersion7());
            meeting.CancelledFromStatus = cancelledFrom;
            var participant = Round1Data.AddParticipant(meeting, actor, "Người dùng", role);
            participant.MeetingId = meeting.Id;
            return meeting;
        }

        [Fact(DisplayName = "MeetingReadPolicy.CanRead/UTC001 lets the creator read a draft")]
        [Trait("UTC", "MeetingReadPolicy.CanRead/UTC001")]
        [Trait("Trait", "UTC")]
        public void Policy_creator_reads_draft()
        {
            var creator = Guid.CreateVersion7();
            var meeting = Round1Data.NewMeeting(MeetingStatus.Draft, creator);

            Assert.True(MeetingReadPolicy.CanRead(meeting, creator));
        }

        [Fact(DisplayName = "MeetingReadPolicy.CanRead/UTC002 lets a chair read at any status")]
        [Trait("UTC", "MeetingReadPolicy.CanRead/UTC002")]
        [Trait("Trait", "UTC")]
        public void Policy_chair_reads_draft()
        {
            var chair = Guid.CreateVersion7();

            Assert.True(MeetingReadPolicy.CanRead(GivenMeeting(MeetingStatus.Draft, chair, MeetingRole.Chair), chair));
        }

        [Fact(DisplayName = "MeetingReadPolicy.CanRead/UTC003 lets a member read a published meeting")]
        [Trait("UTC", "MeetingReadPolicy.CanRead/UTC003")]
        [Trait("Trait", "UTC")]
        public void Policy_member_reads_published()
        {
            var member = Guid.CreateVersion7();

            Assert.True(MeetingReadPolicy.CanRead(GivenMeeting(MeetingStatus.Published, member, MeetingRole.Member), member));
        }

        [Fact(DisplayName = "MeetingReadPolicy.CanRead/UTC004 refuses a member before publishing")]
        [Trait("UTC", "MeetingReadPolicy.CanRead/UTC004")]
        [Trait("Trait", "UTC")]
        public void Policy_member_cannot_read_approved()
        {
            var member = Guid.CreateVersion7();

            Assert.False(MeetingReadPolicy.CanRead(GivenMeeting(MeetingStatus.Approved, member, MeetingRole.Member), member));
        }

        [Fact(DisplayName = "MeetingReadPolicy.CanRead/UTC005 lets a member read a meeting cancelled after publishing")]
        [Trait("UTC", "MeetingReadPolicy.CanRead/UTC005")]
        [Trait("Trait", "UTC")]
        public void Policy_member_reads_cancelled_from_published()
        {
            var member = Guid.CreateVersion7();

            Assert.True(MeetingReadPolicy.CanRead(
                GivenMeeting(MeetingStatus.Cancelled, member, MeetingRole.Member, MeetingStatus.Published), member));
        }
    }

    // __APPEND__



}