using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Participants;

public sealed class ParticipantServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeParticipantRepository _participants = new();
    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeParticipantEffects _effects;
    private readonly FakeMeetingInvitationStager _stager = new();
    private readonly FakeSpeakingRequestCloser _speaking = new();
    private readonly FakeAuditLog _audit = new();
    private readonly Guid _chair = Guid.CreateVersion7();
    private readonly Guid _secretary = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();
    private readonly Guid _outsider = Guid.CreateVersion7();

    public ParticipantServiceTests() => _effects = new(_unitOfWork);

    private ParticipantService Service() => new(_participants, _meetings, _effects, _stager, _speaking,
        _audit, _unitOfWork, _effects, new FakeCallerContext(), new FixedClock(Now),
        NullLogger<ParticipantService>.Instance, _effects, _effects);

    private Meeting GivenPublished()
    {
        var meeting = new Meeting
        {
            Title = "Họp giao ban",
            Status = MeetingStatus.Published,
            CreatedById = Guid.CreateVersion7(),
            PlannedStartTime = Now.AddHours(1),
            PlannedEndTime = Now.AddHours(2),
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
            _participants.ActiveUsers.Add(userId);
        }
        _participants.Snapshots[meeting.Id] = new(meeting, []);
        return meeting;
    }

    private static ParticipantActor User(Guid userId) => new(UserId: userId);

    [Fact]
    public async Task Reading_the_roster_returns_paged_rows_in_a_repeatable_read()
    {
        var meeting = GivenPublished();

        var outcome = await Service().ReadAsync(meeting.Id, User(_secretary), ParticipantOperation.Roster,
            new ParticipantQuery(PageSize: 20), default);

        Assert.Equal(ParticipantFailure.None, outcome.Failure);
        var page = Assert.IsType<ParticipantPage>(outcome.Data);
        Assert.Equal(3, page.Total);
        Assert.Equal("0", page.Revision);
        Assert.Equal([(meeting.Id, false)], _participants.Loads);
        Assert.Equal([TransactionMode.RepeatableRead], _unitOfWork.Modes);
        Assert.Equal((1, 0), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task A_guest_reader_sees_only_their_own_email()
    {
        var meeting = GivenPublished();
        var guest = new MeetingGuestInvitation
        {
            MeetingId = meeting.Id, InvitedName = "Khách mời", Email = "khach@trustcabinet.local",
            Response = InvitationResponse.Pending,
        };
        _participants.Snapshots[meeting.Id] = new(meeting, [guest]);
        var session = new MeetingGuestSession(meeting.Id, guest.Id, Guid.CreateVersion7());
        _effects.GuestSessionsAllowed.Add(session.SessionId);
        _effects.Profiles[guest.Id] = new("Khách mời", null);

        var outcome = await Service().ReadAsync(meeting.Id, new ParticipantActor(Guest: session),
            ParticipantOperation.Roster, new ParticipantQuery(PageSize: 20), default);

        Assert.Equal(ParticipantFailure.None, outcome.Failure);
        var page = Assert.IsType<ParticipantPage>(outcome.Data);
        Assert.Equal(4, page.Total);
        Assert.All(page.Items.Where(r => r.Kind == "Internal"), r => Assert.Null(r.Email));
        Assert.Equal("khach@trustcabinet.local", Assert.Single(page.Items, r => r.Kind == "Guest").Email);
    }

    [Fact]
    public async Task Options_are_denied_for_a_non_manager()
    {
        var meeting = GivenPublished();

        var outcome = await Service().ReadAsync(meeting.Id, User(_member), ParticipantOperation.Options,
            new ParticipantQuery(), default);

        Assert.Equal(ParticipantFailure.Forbidden, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_effects.Calls);
    }

    [Fact]
    public async Task Adding_members_locks_accounts_saves_once_and_publishes_after_the_commit()
    {
        var meeting = GivenPublished();
        _meetings.ActiveUsers.Add(_outsider);

        var outcome = await Service().MutateAsync(meeting.Id, null, User(_secretary), ParticipantOperation.AddInternal,
            new ParticipantInput(UserIds: [_outsider], Role: nameof(MeetingRole.Member), Revision: meeting.ParticipantsRevision),
            default);

        Assert.Equal(ParticipantFailure.None, outcome.Failure);
        Assert.Equal(ParticipantSuccess.Created, outcome.Success);
        var created = Assert.Single(_participants.AddedParticipants);
        Assert.Equal((meeting.Id, _outsider, MeetingRole.Member), (created.MeetingId, created.UserId, created.Role));
        Assert.Equal([_secretary, _outsider], _participants.LockedUsers);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits));
        var staged = Assert.Single(_stager.Staged);
        Assert.Equal((meeting.Id, created.Id, null, InvitationEventKind.Invitation, true), (staged.MeetingId, staged.ParticipantId, staged.GuestId, staged.Kind, staged.Notify));
        Assert.Single(_audit.Rows);
        Assert.Equal(["participants.publish:commits=1"], _effects.Calls);
        Assert.Equal(1, outcome.Revision);
    }

    [Fact]
    public async Task Adding_an_existing_member_is_rejected_without_saving()
    {
        var meeting = GivenPublished();

        var outcome = await Service().MutateAsync(meeting.Id, null, User(_secretary), ParticipantOperation.AddInternal,
            new ParticipantInput(UserIds: [_member], Role: nameof(MeetingRole.Member), Revision: meeting.ParticipantsRevision),
            default);

        Assert.Equal(ParticipantFailure.Invalid, outcome.Failure);
        Assert.Equal("userIds", Assert.Single(outcome.Errors!).Key);
        Assert.Empty(_participants.AddedParticipants);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_effects.Calls);
    }

    [Fact]
    public async Task Changing_with_a_deactivated_actor_is_rejected_without_saving()
    {
        var meeting = GivenPublished();
        _participants.LockedUsersActive[_secretary] = false;
        var target = meeting.Participants.Single(p => p.UserId == _member).Id;

        var outcome = await Service().MutateAsync(meeting.Id, null, User(_secretary),
            ParticipantOperation.ChangeAssignments,
            new ParticipantInput(
                Changes: [new ParticipantChange(target, nameof(MeetingRole.Member))],
                Revision: meeting.ParticipantsRevision),
            default);

        Assert.Equal(ParticipantFailure.Inactive, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_effects.Calls);
    }

    [Fact]
    public async Task Handing_over_the_chair_saves_twice_and_reports_the_handover()
    {
        var meeting = GivenPublished();
        var memberAssignment = meeting.Participants.Single(p => p.UserId == _member).Id;

        var outcome = await Service().MutateAsync(meeting.Id, null, User(_chair), ParticipantOperation.HandOverChair,
            new ParticipantInput(NewChairParticipantId: memberAssignment, Confirmed: true, Revision: meeting.ParticipantsRevision),
            default);

        Assert.Equal(ParticipantFailure.None, outcome.Failure);
        Assert.Equal(ParticipantSuccess.ChairHandedOver, outcome.Success);
        Assert.Equal(MeetingRole.Member, meeting.Participants.Single(p => p.UserId == _chair).Role);
        Assert.Equal(MeetingRole.Chair, meeting.Participants.Single(p => p.UserId == _member).Role);
        Assert.Equal((2, 1, 0), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Equal(["participants.publish:commits=1"], _effects.Calls);
    }

    [Fact]
    public async Task Removing_a_member_clears_presentations_closes_speaking_and_notifies_access()
    {
        var meeting = GivenPublished();
        var assignment = meeting.Participants.Single(p => p.UserId == _member).Id;
        _participants.AgendaItems.Add(new AgendaItem { MeetingId = meeting.Id, Title = "Mục 1", PresenterId = _member });
        _speaking.Result = 1;

        var outcome = await Service().MutateAsync(meeting.Id, assignment, User(_secretary),
            ParticipantOperation.RemoveInternal, new ParticipantInput(Revision: meeting.ParticipantsRevision),
            default);

        Assert.Equal(ParticipantFailure.None, outcome.Failure);
        Assert.Equal(ParticipantSuccess.Removed, outcome.Success);
        Assert.Null(Assert.Single(_participants.AgendaItems).PresenterId);
        var close = Assert.Single(_speaking.Closes);
        Assert.Equal((meeting.Id, _member, null), (close.MeetingId, close.UserId, close.GuestIds));
        Assert.Equal(["agenda.publish:commits=1", "participants.publish:commits=1", "access.publish:commits=1"], _effects.Calls);
    }

    [Fact]
    public async Task Responding_records_the_answer_without_bumping_the_revision()
    {
        var meeting = GivenPublished();

        var outcome = await Service().MutateAsync(meeting.Id, null, User(_member), ParticipantOperation.Respond,
            new ParticipantInput(Response: "Accepted"), default);

        Assert.Equal(ParticipantFailure.None, outcome.Failure);
        Assert.Equal(ParticipantSuccess.Responded, outcome.Success);
        Assert.Equal(InvitationResponse.Accepted, meeting.Participants.Single(p => p.UserId == _member).Response);
        var view = Assert.IsType<InvitationResponseView>(outcome.Data);
        Assert.Equal("Accepted", view.Response);
        Assert.Null(outcome.Revision);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits));
    }

    [Fact]
    public async Task A_store_failure_while_saving_answers_save_failed_and_publishes_nothing()
    {
        var meeting = GivenPublished();
        _meetings.ActiveUsers.Add(_outsider);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().MutateAsync(meeting.Id, null, User(_secretary), ParticipantOperation.AddInternal,
            new ParticipantInput(UserIds: [_outsider], Role: nameof(MeetingRole.Member), Revision: meeting.ParticipantsRevision),
            default);

        Assert.Equal(ParticipantFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 0, 1), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Equal(1, _unitOfWork.Discards);
        Assert.Empty(_effects.Calls);
    }

    [Fact]
    public async Task A_store_failure_while_reading_answers_read_failed()
    {
        var meeting = GivenPublished();
        _participants.Fail = new PersistenceUnavailableException();

        var outcome = await Service().ReadAsync(meeting.Id, User(_secretary), ParticipantOperation.Roster,
            new ParticipantQuery(), default);

        Assert.Equal(ParticipantFailure.ReadFailed, outcome.Failure);
        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }
}
