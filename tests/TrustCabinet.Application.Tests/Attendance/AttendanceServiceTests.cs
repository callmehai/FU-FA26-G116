using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Attendance;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Attendance;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Attendance;

public sealed class AttendanceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeAttendanceRepository _attendance = new();
    private readonly FakeParticipantRepository _participants = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeParticipantEffects _effects;
    private readonly FakeAuditLog _audit = new();
    private readonly Guid _secretary = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();

    public AttendanceServiceTests() => _effects = new(_unitOfWork);

    private AttendanceService Service() => new(_attendance, _participants, _effects, _effects,
        new FakeCallerContext(), new FixedClock(Now), NullLogger<AttendanceService>.Instance, _unitOfWork, _audit);

    private (Meeting Meeting, MeetingParticipant Secretary, MeetingParticipant Member, MeetingGuestInvitation Guest)
        GivenInProgress()
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
        return (meeting, secretary, member, guest);
    }

    private static ParticipantActor User(Guid userId) => new(UserId: userId);

    [Fact]
    public async Task Reading_returns_ordered_rows_and_totals_in_a_repeatable_read()
    {
        var (meeting, _, member, guest) = GivenInProgress();

        var outcome = await Service().ReadAsync(meeting.Id, User(_member), new AttendanceQuery(PageSize: 20), default);

        Assert.Equal(AttendanceFailure.None, outcome.Failure);
        var page = Assert.IsType<AttendancePage>(outcome.Page);
        Assert.Equal(3, page.Total);
        Assert.Equal((0, 0, 3, 0, 3), (page.Totals.Present, page.Totals.Absent, page.Totals.Unmarked, page.Totals.Legacy, page.Totals.Total));
        Assert.Equal("Khách mời", page.Items[0].DisplayName);
        Assert.True(page.Permissions.CanViewInvitationResponses);
        Assert.False(page.Permissions.CanRecordAttendance);
        Assert.Equal([TransactionMode.RepeatableRead], _unitOfWork.Modes);
        Assert.Equal((1, 0), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
        _ = guest;
    }

    [Fact]
    public async Task Reading_rejects_an_invalid_query()
    {
        var (meeting, _, _, _) = GivenInProgress();

        var outcome = await Service().ReadAsync(meeting.Id, User(_secretary), new AttendanceQuery(PageSize: 0), default);

        Assert.Equal(AttendanceFailure.InvalidQuery, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Saving_after_the_meeting_left_in_progress_is_locked()
    {
        var (meeting, _, member, _) = GivenInProgress();
        meeting.Status = MeetingStatus.Ended;

        var outcome = await Service().SaveAsync(meeting.Id, User(_secretary),
            new AttendanceInput([new AttendanceEntry(member.Id, "Internal", "Present",
                AttendanceRules.Token(meeting.Id, "Internal", member.Id, 0))]), default);

        Assert.Equal(AttendanceFailure.Locked, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_effects.Calls);
    }

    [Fact]
    public async Task Saving_records_attendance_saves_once_and_publishes_after_the_commit()
    {
        var (meeting, secretary, member, _) = GivenInProgress();

        var outcome = await Service().SaveAsync(meeting.Id, User(_secretary),
            new AttendanceInput([new AttendanceEntry(member.Id, "Internal", "Present",
                AttendanceRules.Token(meeting.Id, "Internal", member.Id, 0))]), default);

        Assert.Equal(AttendanceFailure.None, outcome.Failure);
        var saved = Assert.IsType<AttendanceSaved>(outcome.Saved);
        Assert.Equal(1, saved.ChangedCount);
        var entry = Assert.Single(saved.Entries);
        Assert.Equal((member.Id, "Present", AttendanceRules.Token(meeting.Id, "Internal", member.Id, 1)),
            (entry.Id, entry.Attendance, entry.RowVersion));
        Assert.Equal((AttendanceStatus.Present, 1, _secretary), (member.Attendance, member.AttendanceVersion, member.AttendanceRecordedById));
        Assert.Equal((AttendanceStatus.Unknown, 0), (secretary.Attendance, secretary.AttendanceVersion));
        Assert.Equal([meeting.Id], _attendance.LockedMeetings);
        Assert.Equal([_secretary], _participants.LockedUsers);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits));
        var audit = Assert.Single(_audit.Rows);
        Assert.Equal(AuditActions.MeetingAttendanceRecorded, audit.Action);
        Assert.Equal(["attendance.publish:commits=1"], _effects.Calls);
    }

    [Fact]
    public async Task Saving_with_a_stale_version_is_rejected_without_saving()
    {
        var (meeting, _, member, _) = GivenInProgress();

        var outcome = await Service().SaveAsync(meeting.Id, User(_secretary),
            new AttendanceInput([new AttendanceEntry(member.Id, "Internal", "Present", new string('0', 64))]), default);

        Assert.Equal(AttendanceFailure.Stale, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_effects.Calls);
    }

    [Fact]
    public async Task Saving_is_forbidden_for_a_non_secretary()
    {
        var (meeting, _, member, _) = GivenInProgress();

        var outcome = await Service().SaveAsync(meeting.Id, User(_member),
            new AttendanceInput([new AttendanceEntry(member.Id, "Internal", "Present",
                AttendanceRules.Token(meeting.Id, "Internal", member.Id, 0))]), default);

        Assert.Equal(AttendanceFailure.Forbidden, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_effects.Calls);
    }

    [Fact]
    public async Task A_store_failure_while_saving_answers_save_failed_and_publishes_nothing()
    {
        var (meeting, _, member, _) = GivenInProgress();
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().SaveAsync(meeting.Id, User(_secretary),
            new AttendanceInput([new AttendanceEntry(member.Id, "Internal", "Present",
                AttendanceRules.Token(meeting.Id, "Internal", member.Id, 0))]), default);

        Assert.Equal(AttendanceFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 0, 1), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Equal(1, _unitOfWork.Discards);
        Assert.Empty(_effects.Calls);
    }

    [Fact]
    public async Task A_store_failure_while_reading_answers_read_failed()
    {
        var (meeting, _, _, _) = GivenInProgress();
        _participants.Fail = new PersistenceUnavailableException();

        var outcome = await Service().ReadAsync(meeting.Id, User(_secretary), new AttendanceQuery(), default);

        Assert.Equal(AttendanceFailure.ReadFailed, outcome.Failure);
        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }
}
