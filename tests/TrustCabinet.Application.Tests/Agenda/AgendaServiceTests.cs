using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Agenda;

public sealed class AgendaServiceTests
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

    public AgendaServiceTests() => _effects = new(_unitOfWork);

    private AgendaService Service() => new(_meetings, _unitOfWork, _agenda, _documents, _effects,
        new FixedClock(Now), NullLogger<AgendaService>.Instance, _effects, _effects);

    private Meeting GivenMeeting(MeetingStatus status)
    {
        var meeting = new Meeting
        {
            Title = "Họp giao ban", Status = status, CreatedById = Guid.CreateVersion7(),
            PlannedStartTime = Now.AddHours(1), PlannedEndTime = Now.AddHours(2),
        };
        foreach (var (userId, role) in new[] { (_chair, MeetingRole.Chair), (_secretary, MeetingRole.Secretary), (_member, MeetingRole.Member) })
        {
            meeting.Participants.Add(new MeetingParticipant
            {
                MeetingId = meeting.Id, UserId = userId, Role = role, AssignmentStatus = AssignmentStatus.Current,
                User = new User { Id = userId, FullName = $"{role}", Email = $"{userId}@trustcabinet.local", IsActive = true },
            });
            _meetings.ActiveUsers.Add(userId);
        }
        _meetings.Meetings.Add(meeting);
        return meeting;
    }

    [Fact]
    public async Task Creating_an_item_locks_the_meeting_saves_once_and_publishes_after_the_commit()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);

        var outcome = await Service().CreateAsync(meeting.Id, _secretary, new("Báo cáo quý"));

        Assert.Equal(AgendaFailure.None, outcome.Failure);
        var item = Assert.Single(_agenda.Added);
        Assert.Equal((item.Id, "Báo cáo quý", 1), (outcome.ItemId!.Value, item.Title, item.Position));
        Assert.Equal([meeting.Id], _meetings.Locked);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
        Assert.Equal(["publish:Created:commits=1"], _effects.Calls);
    }

    [Fact]
    public async Task Assigning_a_presenter_of_a_published_meeting_notifies_after_the_commit()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);

        var outcome = await Service().CreateAsync(meeting.Id, _secretary, new("Báo cáo quý", PresenterId: _member));

        Assert.Equal(AgendaFailure.None, outcome.Failure);
        Assert.Equal(["publish:Created:commits=1", "notify:commits=1"], _effects.Calls);
        var assignment = Assert.Single(_effects.Assignments);
        Assert.Equal((meeting.Id, outcome.ItemId!.Value, _member), (assignment.MeetingId, assignment.AgendaItemId, assignment.PresenterId));
    }

    [Fact]
    public async Task A_member_may_not_plan_the_agenda()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);

        var outcome = await Service().CreateAsync(meeting.Id, _member, new("Báo cáo quý"));

        Assert.Equal(AgendaFailure.Forbidden, outcome.Failure);
        Assert.Empty(_agenda.Added);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_effects.Calls);
    }

    [Fact]
    public async Task A_store_failure_while_saving_answers_save_failed_and_publishes_nothing()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().CreateAsync(meeting.Id, _secretary, new("Báo cáo quý"));

        Assert.Equal(AgendaFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Empty(_effects.Calls);
    }

    [Fact]
    public async Task Reading_runs_without_a_transaction_and_shows_the_items()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        _agenda.Items.Add(new AgendaItem { MeetingId = meeting.Id, Title = "Mục 1", Position = 1 });

        var outcome = await Service().GetAsync(meeting.Id, _member);

        var snapshot = Assert.IsType<AgendaSnapshot>(outcome.Data);
        Assert.Equal("Mục 1", Assert.Single(snapshot.Items).Title);
        Assert.False(snapshot.Permissions.CanCreate);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        Assert.Empty(_meetings.Locked);
    }

    [Fact]
    public async Task A_store_failure_while_reading_answers_read_failed()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        _agenda.Fail = new PersistenceUnavailableException();

        Assert.Equal(AgendaFailure.ReadFailed, (await Service().GetAsync(meeting.Id, _member)).Failure);
    }

    [Fact]
    public async Task A_guest_whose_session_is_not_live_reads_nothing()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var allowed = new MeetingGuestSession(meeting.Id, Guid.CreateVersion7(), Guid.CreateVersion7());
        _effects.GuestSessionsAllowed.Add(allowed.SessionId);

        Assert.Equal(AgendaFailure.NotFound,
            (await Service().GetForGuestAsync(meeting.Id, allowed with { SessionId = Guid.CreateVersion7() })).Failure);
        Assert.IsType<AgendaSnapshot>((await Service().GetForGuestAsync(meeting.Id, allowed)).Data);
        Assert.Equal([TransactionMode.None, TransactionMode.None], _unitOfWork.Modes);
    }
}
