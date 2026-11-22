using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Attendance;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Attendance;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 unit tests for FE-06 (Participants + Attendance), tester Pham Gia Khanh.
// Each test covers one UTC row: Return (result), Exception (N/A = completes) and
// Log Message (CaptureLogger / fakes: audit rows, staged notices, published events).
// New UTC001... numbering after the Round 1 curation (old ids in the report).

public sealed class PartUnitRound1Tests
{
    // Helper owned by this file (observes formatted log messages, which the
    // shared fakes cannot). Nothing under Fakes/ is changed.

    /// <summary>Captures ILogger output so a test can assert the "Log Message" line of a UTC.</summary>
    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        IDisposable ILogger.BeginScope<TState>(TState state) => Scope.Instance;

        public bool IsEnabled(LogLevel level) => true;

        public void Log<TState>(
            LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((level, formatter(state, exception), exception));

        private sealed class Scope : IDisposable
        {
            public static readonly Scope Instance = new();
            public void Dispose() { }
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeParticipantRepository _participants = new();
    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeParticipantEffects _effects;
    private readonly FakeMeetingInvitationStager _stager = new();
    private readonly FakeSpeakingRequestCloser _speaking = new();
    private readonly FakeAuditLog _audit = new();
    private readonly FakeAttendanceRepository _attendance = new();
    private readonly CaptureLogger<ParticipantService> _participantLogger = new();
    private readonly CaptureLogger<AttendanceService> _attendanceLogger = new();
    private readonly Guid _chair = Guid.CreateVersion7();
    private readonly Guid _secretary = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();

    public PartUnitRound1Tests() => _effects = new(_unitOfWork);

    private ParticipantService ParticipantService() => new(
        _participants, _meetings, _effects, _stager, _speaking, _audit, _unitOfWork,
        _effects, new FakeCallerContext(), new FixedClock(Now), _participantLogger,
        _effects, _effects, new NoCallerAccounts());

    private AttendanceService AttendanceService() => new(
        _attendance, _participants, _effects, _effects, new FakeCallerContext(),
        new FixedClock(Now), _attendanceLogger, _unitOfWork, _audit);

    private static ParticipantActor User(Guid userId) => new(UserId: userId);

    private Meeting GivenRoster(MeetingStatus status = MeetingStatus.Published, long revision = 5)
    {
        var meeting = new Meeting
        {
            Title = "Họp giao ban",
            Status = status,
            CreatedById = Guid.CreateVersion7(),
            PlannedStartTime = Now.AddHours(1),
            PlannedEndTime = Now.AddHours(2),
            ParticipantsRevision = revision,
        };
        foreach (var (userId, role) in new[] { (_chair, MeetingRole.Chair), (_secretary, MeetingRole.Secretary), (_member, MeetingRole.Member) })
        {
            meeting.Participants.Add(new MeetingParticipant
            {
                MeetingId = meeting.Id, UserId = userId, Role = role, AssignmentStatus = AssignmentStatus.Current,
                Response = InvitationResponse.Pending,
                User = new User { Id = userId, FullName = $"{role}", Email = $"{userId}@trustcabinet.local", IsActive = true },
            });
            _meetings.ActiveUsers.Add(userId);
        }
        _participants.Snapshots[meeting.Id] = new(meeting, []);
        return meeting;
    }

    // ---- ParticipantService.MutateAsync ----

    [Fact(DisplayName = "ParticipantService.MutateAsync/UTC001 adding members publishes after commit")]
    [Trait("UTC", "ParticipantService.MutateAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task ParticipantService_MutateAsync_UTC001_adding_members_publishes_after_commit()
    {
        var meeting = GivenRoster();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        var result = await ParticipantService().MutateAsync(meeting.Id, null, User(_secretary),
            ParticipantOperation.AddInternal,
            new ParticipantInput(UserIds: [first, second], Role: nameof(MeetingRole.Member), Revision: 5),
            default);

        Assert.Equal(ParticipantFailure.None, result.Failure); // Return
        Assert.Equal(ParticipantSuccess.Created, result.Success);
        Assert.Equal(6, result.Revision);
        Assert.Equal(2, ((dynamic)result.Data!).createdIds.Count);
        Assert.Equal(2, _participants.AddedParticipants.Count);
        Assert.All(_participants.AddedParticipants, p =>
            Assert.Equal((meeting.Id, MeetingRole.Member), (p.MeetingId, p.Role)));
        var audit = Assert.Single(_audit.Rows);
        Assert.Equal((AuditActions.MeetingParticipants(ParticipantOperation.AddInternal), meeting.Id, _secretary),
            (audit.Action, audit.ObjectId, audit.ActorId));
        Assert.Equal(["participants.publish:commits=1"], _effects.Calls);
        Assert.Equal(2, _stager.Staged.Count); // Log Message (no invitation staged only in Draft)
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits)); // Exception: N/A
        Assert.Empty(_participantLogger.Entries);
    }

    [Fact(DisplayName = "ParticipantService.MutateAsync/UTC002 a plain member cannot add")]
    [Trait("UTC", "ParticipantService.MutateAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task ParticipantService_MutateAsync_UTC002_a_plain_member_cannot_add()
    {
        var meeting = GivenRoster();

        var result = await ParticipantService().MutateAsync(meeting.Id, null, User(_member),
            ParticipantOperation.AddInternal,
            new ParticipantInput(UserIds: [Guid.CreateVersion7()], Role: nameof(MeetingRole.Member), Revision: 5),
            default);

        Assert.Equal(ParticipantFailure.Forbidden, result.Failure); // Return
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_effects.Calls);
        Assert.Empty(_audit.Rows);
        Assert.Empty(_participantLogger.Entries); // Log Message: N/A + Exception: N/A
    }

    [Fact(DisplayName = "ParticipantService.MutateAsync/UTC003 adding after the meeting ended is locked")]
    [Trait("UTC", "ParticipantService.MutateAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task ParticipantService_MutateAsync_UTC003_adding_after_the_meeting_ended_is_locked()
    {
        var meeting = GivenRoster(MeetingStatus.Ended);

        var result = await ParticipantService().MutateAsync(meeting.Id, null, User(_secretary),
            ParticipantOperation.AddInternal,
            new ParticipantInput(UserIds: [Guid.CreateVersion7()], Role: nameof(MeetingRole.Member), Revision: 5),
            default);

        Assert.Equal(ParticipantFailure.Locked, result.Failure); // Return
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_effects.Calls);
        Assert.Empty(_participantLogger.Entries); // Log Message: N/A + Exception: N/A
    }

    [Fact(DisplayName = "ParticipantService.MutateAsync/UTC004 a stale revision is refused")]
    [Trait("UTC", "ParticipantService.MutateAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task ParticipantService_MutateAsync_UTC004_a_stale_revision_is_refused()
    {
        var meeting = GivenRoster();

        var result = await ParticipantService().MutateAsync(meeting.Id, null, User(_secretary),
            ParticipantOperation.AddInternal,
            new ParticipantInput(UserIds: [Guid.CreateVersion7()], Role: nameof(MeetingRole.Member), Revision: 4),
            default);

        Assert.Equal(ParticipantFailure.Stale, result.Failure); // Return
        Assert.Null(result.Revision); // the stale branch sets no revision (see Deviation in the UTC)
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_participantLogger.Entries); // Log Message: N/A + Exception: N/A
    }

    [Fact(DisplayName = "ParticipantService.MutateAsync/UTC005 adding 100 members at once works")]
    [Trait("UTC", "ParticipantService.MutateAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task ParticipantService_MutateAsync_UTC005_adding_100_members_at_once_works()
    {
        var meeting = GivenRoster();
        var ids = Enumerable.Range(0, 100).Select(_ => Guid.CreateVersion7()).ToArray();

        var result = await ParticipantService().MutateAsync(meeting.Id, null, User(_secretary),
            ParticipantOperation.AddInternal,
            new ParticipantInput(UserIds: ids, Role: nameof(MeetingRole.Member), Revision: 5),
            default);

        Assert.Equal(ParticipantFailure.None, result.Failure); // Return
        Assert.Equal(ParticipantSuccess.Created, result.Success);
        Assert.Equal(100, _participants.AddedParticipants.Count);
        Assert.Equal(6, result.Revision);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits)); // Exception: N/A
        Assert.Empty(_participantLogger.Entries); // Log Message: N/A
    }

    [Fact(DisplayName = "ParticipantService.MutateAsync/UTC006 adding 101 members is refused")]
    [Trait("UTC", "ParticipantService.MutateAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task ParticipantService_MutateAsync_UTC006_adding_101_members_is_refused()
    {
        var meeting = GivenRoster();
        var ids = Enumerable.Range(0, 101).Select(_ => Guid.CreateVersion7()).ToArray();

        var result = await ParticipantService().MutateAsync(meeting.Id, null, User(_secretary),
            ParticipantOperation.AddInternal,
            new ParticipantInput(UserIds: ids, Role: nameof(MeetingRole.Member), Revision: 5),
            default);

        Assert.Equal(ParticipantFailure.Invalid, result.Failure); // Return
        Assert.Equal(["Chọn từ 1 đến 100 người dùng hợp lệ, không trùng nhau."], result.Errors!["userIds"]);
        Assert.Empty(_participants.AddedParticipants);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_participantLogger.Entries); // Log Message: N/A + Exception: N/A
    }

    // ---- AttendanceService.SaveAsync ----

    private (Meeting Meeting, MeetingParticipant Member) GivenInProgress()
    {
        var meeting = new Meeting
        {
            Title = "Họp giao ban",
            Status = MeetingStatus.InProgress,
            CreatedById = Guid.CreateVersion7(),
            PlannedStartTime = Now.AddHours(-1),
            PlannedEndTime = Now.AddHours(1),
        };
        var secretary = new MeetingParticipant
        {
            MeetingId = meeting.Id, UserId = _secretary, Role = MeetingRole.Secretary,
            AssignmentStatus = AssignmentStatus.Current, Response = InvitationResponse.Accepted,
            Attendance = AttendanceStatus.Unknown, AttendanceVersion = 0,
            User = new User { Id = _secretary, FullName = "Thư ký", Email = $"{_secretary}@trustcabinet.local", IsActive = true },
        };
        var member = new MeetingParticipant
        {
            MeetingId = meeting.Id, UserId = _member, Role = MeetingRole.Member,
            AssignmentStatus = AssignmentStatus.Current, Response = InvitationResponse.Accepted,
            Attendance = AttendanceStatus.Unknown, AttendanceVersion = 0,
            User = new User { Id = _member, FullName = "Thành viên", Email = $"{_member}@trustcabinet.local", IsActive = true },
        };
        meeting.Participants.Add(secretary);
        meeting.Participants.Add(member);
        var guest = new MeetingGuestInvitation
        {
            MeetingId = meeting.Id, InvitedName = "Khách mời", Email = "khach@trustcabinet.local",
            Response = InvitationResponse.Accepted, Attendance = AttendanceStatus.Unknown, AttendanceVersion = 0,
        };
        _participants.Snapshots[meeting.Id] = new(meeting, [guest]);
        _attendance.ActiveUsers.Add(_secretary);
        _attendance.ActiveUsers.Add(_member);
        _attendance.InternalTargets.Add(secretary);
        _attendance.InternalTargets.Add(member);
        _attendance.GuestTargets.Add(guest);
        _attendance.UserNames[_secretary] = "Thư ký";
        _effects.Profiles[guest.Id] = new("Khách mời", null);
        return (meeting, member);
    }

    [Fact(DisplayName = "AttendanceService.SaveAsync/UTC001 recording attendance saves audits and publishes")]
    [Trait("UTC", "AttendanceService.SaveAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task AttendanceService_SaveAsync_UTC001_recording_attendance_saves_audits_and_publishes()
    {
        var (meeting, member) = GivenInProgress();

        var result = await AttendanceService().SaveAsync(meeting.Id, User(_secretary),
            new AttendanceInput([new AttendanceEntry(member.Id, "Internal", "Present",
                AttendanceRules.Token(meeting.Id, "Internal", member.Id, 0))]), default);

        Assert.Equal(AttendanceFailure.None, result.Failure); // Return
        var saved = Assert.IsType<AttendanceSaved>(result.Saved);
        Assert.Equal(1, saved.ChangedCount);
        var entry = Assert.Single(saved.Entries);
        Assert.Equal((member.Id, "Present", AttendanceRules.Token(meeting.Id, "Internal", member.Id, 1)),
            (entry.Id, entry.Attendance, entry.RowVersion));
        Assert.Equal((AttendanceStatus.Present, 1, _secretary),
            (member.Attendance, member.AttendanceVersion, member.AttendanceRecordedById));
        Assert.Equal(new AttendanceRecorder(_secretary, "Thư ký"), entry.RecordedBy);
        var audit = Assert.Single(_audit.Rows);
        Assert.Equal((AuditActions.MeetingAttendanceRecorded, meeting.Id, _secretary),
            (audit.Action, audit.ObjectId, audit.ActorId));
        Assert.Equal(["attendance.publish:commits=1"], _effects.Calls);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits)); // Exception: N/A
        Assert.Empty(_attendanceLogger.Entries); // Log Message (audit row + event, no logger call)
    }

    [Fact(DisplayName = "AttendanceService.SaveAsync/UTC002 a chair cannot record attendance")]
    [Trait("UTC", "AttendanceService.SaveAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task AttendanceService_SaveAsync_UTC002_a_chair_cannot_record_attendance()
    {
        var (meeting, member) = GivenInProgress();
        meeting.Participants.Add(new MeetingParticipant
        {
            MeetingId = meeting.Id, UserId = _chair, Role = MeetingRole.Chair,
            AssignmentStatus = AssignmentStatus.Current, Response = InvitationResponse.Accepted,
            Attendance = AttendanceStatus.Unknown, AttendanceVersion = 0,
            User = new User { Id = _chair, FullName = "Chủ tọa", Email = $"{_chair}@trustcabinet.local", IsActive = true },
        });
        _attendance.ActiveUsers.Add(_chair);

        var result = await AttendanceService().SaveAsync(meeting.Id, User(_chair),
            new AttendanceInput([new AttendanceEntry(member.Id, "Internal", "Present",
                AttendanceRules.Token(meeting.Id, "Internal", member.Id, 0))]), default);

        Assert.Equal(AttendanceFailure.Forbidden, result.Failure); // Return
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_effects.Calls);
        Assert.Empty(_attendanceLogger.Entries); // Log Message: N/A + Exception: N/A
    }

    [Fact(DisplayName = "AttendanceService.SaveAsync/UTC003 recording before the meeting starts is locked")]
    [Trait("UTC", "AttendanceService.SaveAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task AttendanceService_SaveAsync_UTC003_recording_before_the_meeting_starts_is_locked()
    {
        var (meeting, member) = GivenInProgress();
        meeting.Status = MeetingStatus.Published;

        var result = await AttendanceService().SaveAsync(meeting.Id, User(_secretary),
            new AttendanceInput([new AttendanceEntry(member.Id, "Internal", "Present",
                AttendanceRules.Token(meeting.Id, "Internal", member.Id, 0))]), default);

        Assert.Equal(AttendanceFailure.Locked, result.Failure); // Return
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_effects.Calls);
        Assert.Empty(_attendanceLogger.Entries); // Log Message: N/A + Exception: N/A
    }

    [Fact(DisplayName = "AttendanceService.SaveAsync/UTC004 a moved version token is stale")]
    [Trait("UTC", "AttendanceService.SaveAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task AttendanceService_SaveAsync_UTC004_a_moved_version_token_is_stale()
    {
        var (meeting, member) = GivenInProgress();
        member.AttendanceVersion = 1;

        var result = await AttendanceService().SaveAsync(meeting.Id, User(_secretary),
            new AttendanceInput([new AttendanceEntry(member.Id, "Internal", "Present",
                AttendanceRules.Token(meeting.Id, "Internal", member.Id, 0))]), default);

        Assert.Equal(AttendanceFailure.Stale, result.Failure); // Return
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_effects.Calls);
        Assert.Empty(_attendanceLogger.Entries); // Log Message: N/A + Exception: N/A
    }

    [Fact(DisplayName = "AttendanceService.SaveAsync/UTC005 a version overflow fails the save")]
    [Trait("UTC", "AttendanceService.SaveAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task AttendanceService_SaveAsync_UTC005_a_version_overflow_fails_the_save()
    {
        var (meeting, member) = GivenInProgress();
        member.AttendanceVersion = long.MaxValue;

        var result = await AttendanceService().SaveAsync(meeting.Id, User(_secretary),
            new AttendanceInput([new AttendanceEntry(member.Id, "Internal", "Present",
                AttendanceRules.Token(meeting.Id, "Internal", member.Id, long.MaxValue))]), default);

        Assert.Equal(AttendanceFailure.SaveFailed, result.Failure); // Return
        Assert.Equal(1, _unitOfWork.Discards);
        Assert.Empty(_audit.Rows);
        Assert.Empty(_effects.Calls);
        var log = Assert.Single(_attendanceLogger.Entries); // Log Message + Exception: N/A (caught inside)
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.Contains("Attendance save failed", log.Message);
    }

    [Fact(DisplayName = "AttendanceService.SaveAsync/UTC006 an oversized body is refused")]
    [Trait("UTC", "AttendanceService.SaveAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task AttendanceService_SaveAsync_UTC006_an_oversized_body_is_refused()
    {
        var (meeting, _) = GivenInProgress();

        var result = await AttendanceService().SaveAsync(meeting.Id, User(_secretary),
            new AttendanceInput(ParseFailure: AttendanceFailure.TooLarge), default);

        Assert.Equal(AttendanceFailure.TooLarge, result.Failure); // Return
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_effects.Calls);
        Assert.Empty(_attendanceLogger.Entries); // Log Message: N/A + Exception: N/A
    }
}
