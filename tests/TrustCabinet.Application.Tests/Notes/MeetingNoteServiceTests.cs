using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Notes;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Tests.Notes;

public sealed class MeetingNoteServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingNoteRepository _notes = new();
    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeNotesChangePublisher _publisher;
    private readonly Guid _secretary = Guid.CreateVersion7();
    private readonly Guid _chair = Guid.CreateVersion7();
    private readonly Guid _meeting = Guid.CreateVersion7();

    public MeetingNoteServiceTests()
    {
        _publisher = new FakeNotesChangePublisher(_unitOfWork);
    }

    private MeetingNoteService Service() => new(_notes, _meetings, _publisher, _unitOfWork,
        new FixedClock(Now), NullLogger<MeetingNoteService>.Instance);

    private void SeedMeeting(MeetingStatus status = MeetingStatus.InProgress)
    {
        MeetingParticipant Participant(Guid userId, MeetingRole role) => new()
        {
            MeetingId = _meeting,
            UserId = userId,
            Role = role,
            AssignmentStatus = AssignmentStatus.Current,
            Response = InvitationResponse.Accepted,
            User = new User { Id = userId, FullName = role.ToString(), Email = "u@trustcabinet.local", IsActive = true },
        };
        _meetings.Meetings.Add(new Meeting
        {
            Id = _meeting,
            Title = "Họp giao ban",
            Status = status,
            Sensitivity = Sensitivity.Normal,
            PlannedStartTime = Now.AddHours(-1),
            PlannedEndTime = Now.AddHours(1),
            CreatedAt = Now,
            UpdatedAt = Now,
            Participants = [Participant(_secretary, MeetingRole.Secretary), Participant(_chair, MeetingRole.Chair)],
        });
        _meetings.ActiveUsers.Add(_secretary);
        _meetings.ActiveUsers.Add(_chair);
    }

    [Fact]
    public async Task Reading_returns_the_secretaries_notes_with_write_access()
    {
        SeedMeeting();
        _notes.Notes.Add(new MeetingNote
        {
            MeetingId = _meeting,
            AuthorId = _secretary,
            Body = "Chuẩn bị tài liệu",
            CreatedAt = Now,
            UpdatedAt = Now,
            Author = new User { Id = _secretary, FullName = "Thư ký", Email = "Thư ký@trustcabinet.local", IsActive = true },
        });

        var outcome = await Service().GetAsync(_meeting, _secretary);

        Assert.Equal(NoteFailure.None, outcome.Failure);
        Assert.NotNull(outcome.State);
        Assert.Single(outcome.State.Notes);
        Assert.True(outcome.State.CanWrite);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task Reading_answers_NotFound_for_a_reader_who_is_neither_chair_nor_secretary()
    {
        SeedMeeting();
        var member = Guid.CreateVersion7();
        var meeting = _meetings.Meetings.Single(m => m.Id == _meeting);
        meeting.Participants.Add(new MeetingParticipant
        {
            MeetingId = _meeting,
            UserId = member,
            Role = MeetingRole.Member,
            AssignmentStatus = AssignmentStatus.Current,
            Response = InvitationResponse.Accepted,
            User = new User { Id = member, FullName = "Thành viên", Email = "Thành viên@trustcabinet.local", IsActive = true },
        });
        _meetings.ActiveUsers.Add(member);

        var outcome = await Service().GetAsync(_meeting, member);

        Assert.Equal(NoteFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.State);
    }

    [Fact]
    public async Task Reading_maps_a_database_failure_to_read_failed()
    {
        SeedMeeting();
        _notes.Fail = new PersistenceUnavailableException();

        var outcome = await Service().GetAsync(_meeting, _secretary);

        Assert.Equal(NoteFailure.ReadFailed, outcome.Failure);
    }

    [Fact]
    public async Task Saving_publishes_after_the_commit()
    {
        SeedMeeting();

        var outcome = await Service().SaveAsync(_meeting, _secretary, null, "Ghi chú mới", null);

        Assert.Equal(NoteFailure.None, outcome.Failure);
        Assert.NotNull(outcome.Note);
        Assert.Equal("Ghi chú mới", outcome.Note.Body);
        Assert.Equal(1, _unitOfWork.Saves);
        Assert.Equal(1, _unitOfWork.Commits);
        Assert.Single(_publisher.Published);
        Assert.Equal(["publish:commits=1"], _publisher.Calls);
    }

    [Fact]
    public async Task Saving_over_a_newer_window_is_stale_and_saves_nothing()
    {
        SeedMeeting();
        _notes.Notes.Add(new MeetingNote
        {
            MeetingId = _meeting,
            AuthorId = _secretary,
            Body = "Bản mới hơn",
            CreatedAt = Now.AddMinutes(-5),
            UpdatedAt = Now,
            Author = new User { Id = _secretary, FullName = "Thư ký", Email = "Thư ký@trustcabinet.local", IsActive = true },
        });

        var outcome = await Service().SaveAsync(_meeting, _secretary, null, "Bản cũ", Now.AddMinutes(-10));

        Assert.Equal(NoteFailure.Stale, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Saving_maps_a_database_failure_to_save_failed()
    {
        SeedMeeting();
        _notes.Fail = new PersistenceUnavailableException();

        var outcome = await Service().SaveAsync(_meeting, _secretary, null, "Ghi chú", null);

        Assert.Equal(NoteFailure.SaveFailed, outcome.Failure);
        Assert.Empty(_publisher.Published);
    }
}
