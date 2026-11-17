using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Meetings;

/// <summary>Who hears of a meeting: the recipients of every realtime event and the meeting hub's join checks.</summary>
public sealed class MeetingAudienceTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeParticipantRepository _participants = new();
    private readonly FakeAudienceQuery _query = new();
    private readonly FakeGuests _guests = new();

    private MeetingAudience Audience() => new(_unitOfWork, _meetings, _query, _guests, _participants, _guests);

    [Fact]
    public async Task Readers_are_the_active_readers_and_the_live_guest_sessions_of_an_open_meeting()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var member = Participant(meeting, MeetingRole.Member);
        var removed = Participant(meeting, MeetingRole.Member, removed: true);
        var deactivated = Participant(meeting, MeetingRole.Member);
        _query.Active.UnionWith([meeting.CreatedById, member, removed]);
        var guest = Session(meeting.Id);
        _guests.Readers.AddRange([guest, guest, Session(Guid.CreateVersion7()), new MeetingGuestSession(meeting.Id, Guid.Empty, Guid.Empty)]);

        var readers = await Audience().ReadersAsync(meeting.Id, CancellationToken.None);

        Assert.Equal(new[] { meeting.CreatedById, member }.Order(), readers.UserIds.Order());
        // The read policy decides who is even asked about: a removed participant is no reader, active or not.
        Assert.DoesNotContain(removed, Assert.Single(_query.Asked));
        Assert.Contains(deactivated, _query.Asked[0]);
        Assert.Equal([guest], readers.Guests);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Theory]
    [InlineData(MeetingStatus.Ended)]
    [InlineData(MeetingStatus.Approved)]
    public async Task Readers_of_a_meeting_closed_to_guests_have_no_guest_session(MeetingStatus status)
    {
        var meeting = GivenMeeting(status);
        _query.Active.Add(meeting.CreatedById);
        _guests.Readers.Add(Session(meeting.Id));

        var readers = await Audience().ReadersAsync(meeting.Id, CancellationToken.None);

        Assert.Equal([meeting.CreatedById], readers.UserIds);
        Assert.Empty(readers.Guests);
        Assert.Equal(0, _guests.Listed);
    }

    [Fact]
    public async Task A_meeting_that_does_not_exist_has_no_audience()
    {
        var audience = Audience();
        var id = Guid.CreateVersion7();

        Assert.Same(MeetingAudienceMembers.None, await audience.ReadersAsync(id, CancellationToken.None));
        Assert.Same(MeetingAudienceMembers.None, await audience.AttendanceReadersAsync(id, CancellationToken.None));
        Assert.Empty(await audience.ManagersAsync(id, CancellationToken.None));
        Assert.Empty(await audience.ListenersAsync(id, CancellationToken.None));
        Assert.Empty(_query.Asked);
    }

    [Fact]
    public async Task Managers_are_the_current_chair_and_secretary_whose_accounts_are_active()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var chair = Participant(meeting, MeetingRole.Chair);
        var secretary = Participant(meeting, MeetingRole.Secretary);
        var formerSecretary = Participant(meeting, MeetingRole.Secretary, removed: true);
        var member = Participant(meeting, MeetingRole.Member);
        _query.Active.UnionWith([chair, secretary, formerSecretary, member, meeting.CreatedById]);

        var managers = await Audience().ManagersAsync(meeting.Id, CancellationToken.None);

        Assert.Equal(new[] { chair, secretary }.Order(), managers.Order());
    }

    [Fact]
    public async Task Listeners_are_the_active_roster_not_a_creator_outside_it()
    {
        var meeting = GivenMeeting(MeetingStatus.Ended);
        var member = Participant(meeting, MeetingRole.Member);
        var deactivated = Participant(meeting, MeetingRole.Member);
        var removed = Participant(meeting, MeetingRole.Member, removed: true);
        _query.Active.UnionWith([meeting.CreatedById, member, removed]);

        var listeners = await Audience().ListenersAsync(meeting.Id, CancellationToken.None);

        Assert.Equal([member], listeners);
        Assert.Contains(deactivated, Assert.Single(_query.Asked));
        // The workflow load keeps removed participants: the listen rule, not the query, leaves them out.
        Assert.DoesNotContain(removed, _query.Asked[0]);
    }

    [Fact]
    public async Task Attendance_reaches_only_guest_sessions_still_invited_and_live_for_both_checks()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        _query.Active.Add(meeting.CreatedById);
        var invitations = new[] { Invitation(meeting), Invitation(meeting), Invitation(meeting) };
        _participants.Snapshots[meeting.Id] = new ParticipantSnapshot(meeting, invitations);
        var allowed = Session(meeting.Id, invitations[0].Id);
        var rosterRefuses = Session(meeting.Id, invitations[1].Id);
        var agendaRefuses = Session(meeting.Id, invitations[2].Id);
        var notInvited = Session(meeting.Id);
        var ofAnotherMeeting = new MeetingGuestSession(Guid.CreateVersion7(), invitations[0].Id, Guid.CreateVersion7());
        var malformed = new MeetingGuestSession(meeting.Id, invitations[0].Id, Guid.Empty);
        _guests.Readers.AddRange([allowed, allowed, rosterRefuses, agendaRefuses, notInvited, ofAnotherMeeting, malformed]);
        _guests.CanAccess.UnionWith([allowed.SessionId, agendaRefuses.SessionId, notInvited.SessionId,
            ofAnotherMeeting.SessionId, malformed.SessionId]);
        _guests.CanRead.UnionWith([allowed.SessionId, rosterRefuses.SessionId, notInvited.SessionId,
            ofAnotherMeeting.SessionId, malformed.SessionId]);

        var readers = await Audience().AttendanceReadersAsync(meeting.Id, CancellationToken.None);

        Assert.Equal([meeting.CreatedById], readers.UserIds);
        Assert.Equal([allowed], readers.Guests);
        Assert.Equal([(meeting.Id, false)], _participants.Loads);
    }

    [Fact]
    public async Task Attendance_of_a_meeting_closed_to_guests_reaches_only_its_internal_readers()
    {
        var meeting = GivenMeeting(MeetingStatus.Ended);
        var approver = Guid.CreateVersion7();
        meeting.AssignedApproverId = approver;
        var invitation = Invitation(meeting);
        _participants.Snapshots[meeting.Id] = new ParticipantSnapshot(meeting, [invitation]);
        var session = Session(meeting.Id, invitation.Id);
        _guests.Readers.Add(session);
        _guests.CanAccess.Add(session.SessionId);
        _guests.CanRead.Add(session.SessionId);
        _query.Active.UnionWith([meeting.CreatedById, approver]);

        var readers = await Audience().AttendanceReadersAsync(meeting.Id, CancellationToken.None);

        Assert.Equal(new[] { meeting.CreatedById, approver }.Order(), readers.UserIds.Order());
        Assert.Empty(readers.Guests);
        Assert.Equal(0, _guests.Listed);
    }

    [Fact]
    public async Task Attendance_of_a_draft_does_not_reach_its_approver()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        var approver = Guid.CreateVersion7();
        meeting.AssignedApproverId = approver;
        _participants.Snapshots[meeting.Id] = new ParticipantSnapshot(meeting, []);
        _query.Active.UnionWith([meeting.CreatedById, approver]);

        var readers = await Audience().AttendanceReadersAsync(meeting.Id, CancellationToken.None);

        // The read policy hides a draft from its approver (only the creator reads it), so they are not even asked about.
        Assert.Equal([meeting.CreatedById], readers.UserIds);
        Assert.DoesNotContain(approver, Assert.Single(_query.Asked));
    }

    [Fact]
    public async Task A_user_joins_only_a_meeting_they_may_read_with_an_active_account()
    {
        var meetingId = Guid.CreateVersion7();
        var reader = Guid.CreateVersion7();
        var deactivated = Guid.CreateVersion7();
        _meetings.ActiveUsers.Add(reader);
        _meetings.Accessible.UnionWith([(meetingId, reader), (meetingId, deactivated)]);
        var audience = Audience();

        Assert.True(await audience.IsUserActiveAsync(reader, CancellationToken.None));
        Assert.False(await audience.IsUserActiveAsync(deactivated, CancellationToken.None));
        Assert.True(await audience.CanUserJoinAsync(meetingId, reader, CancellationToken.None));
        Assert.False(await audience.CanUserJoinAsync(meetingId, deactivated, CancellationToken.None));
        Assert.False(await audience.CanUserJoinAsync(Guid.CreateVersion7(), reader, CancellationToken.None));
    }

    [Fact]
    public async Task A_guest_joins_only_its_own_open_meeting_with_a_live_session()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        var ended = GivenMeeting(MeetingStatus.Ended);
        var live = Session(meeting.Id);
        var revoked = Session(meeting.Id);
        var ofEnded = Session(ended.Id);
        _guests.CanRead.UnionWith([live.SessionId, ofEnded.SessionId]);
        var audience = Audience();

        Assert.True(await audience.CanGuestJoinAsync(live, meeting.Id, CancellationToken.None));
        Assert.False(await audience.CanGuestJoinAsync(live, ended.Id, CancellationToken.None));
        Assert.False(await audience.CanGuestJoinAsync(revoked, meeting.Id, CancellationToken.None));
        Assert.False(await audience.CanGuestJoinAsync(ofEnded, ended.Id, CancellationToken.None));
        Assert.False(await audience.CanGuestJoinAsync(new MeetingGuestSession(meeting.Id, Guid.Empty, Guid.Empty),
            meeting.Id, CancellationToken.None));
    }

    [Fact]
    public async Task A_storage_failure_reaches_the_caller_instead_of_an_audience()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var failure = new PersistenceUnavailableException();
        _query.Fail = failure;

        Assert.Same(failure, await Assert.ThrowsAsync<PersistenceUnavailableException>(
            () => Audience().ReadersAsync(meeting.Id, CancellationToken.None)));
    }

    private Meeting GivenMeeting(MeetingStatus status)
    {
        var meeting = new Meeting { Title = "Họp giao ban", Status = status, CreatedById = Guid.CreateVersion7() };
        _meetings.Meetings.Add(meeting);
        return meeting;
    }

    private static Guid Participant(Meeting meeting, MeetingRole role, bool removed = false)
    {
        var user = new User { FullName = "Thành viên", Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "x", IsActive = true };
        meeting.Participants.Add(new MeetingParticipant
        {
            MeetingId = meeting.Id,
            UserId = user.Id,
            User = user,
            Role = role,
            AssignmentStatus = removed ? AssignmentStatus.Removed : AssignmentStatus.Current,
        });
        return user.Id;
    }

    private static MeetingGuestInvitation Invitation(Meeting meeting) => new() { MeetingId = meeting.Id };

    private static MeetingGuestSession Session(Guid meetingId, Guid? invitationId = null)
        => new(meetingId, invitationId ?? Guid.CreateVersion7(), Guid.CreateVersion7());

    private sealed class FakeAudienceQuery : IMeetingAudienceQuery
    {
        public HashSet<Guid> Active { get; } = [];
        public List<Guid[]> Asked { get; } = [];
        public Exception? Fail { get; set; }

        public Task<IReadOnlyList<Guid>> ListActiveUserIdsAsync(Guid[] candidateUserIds, CancellationToken ct = default)
        {
            if (Fail is not null) throw Fail;
            Asked.Add(candidateUserIds);
            return Task.FromResult<IReadOnlyList<Guid>>(candidateUserIds.Where(Active.Contains).ToList());
        }
    }

    /// <summary>Both guest checks; the readers come back unfiltered, as a store that lists more than it should.</summary>
    private sealed class FakeGuests : IAgendaGuestAccess, IParticipantGuestAccess
    {
        public List<MeetingGuestSession> Readers { get; } = [];
        public HashSet<Guid> CanRead { get; } = [];
        public HashSet<Guid> CanAccess { get; } = [];
        public int Listed { get; private set; }

        public Task<bool> CanReadAsync(MeetingGuestSession session, CancellationToken ct)
            => Task.FromResult(CanRead.Contains(session.SessionId));

        public Task<IReadOnlyList<MeetingGuestSession>> ListReadersAsync(Guid meetingId, CancellationToken ct)
        {
            Listed++;
            return Task.FromResult<IReadOnlyList<MeetingGuestSession>>(Readers.ToList());
        }

        public Task<bool> CanAccessAsync(MeetingGuestSession session, CancellationToken ct)
            => Task.FromResult(CanAccess.Contains(session.SessionId));

        public Task<IReadOnlyDictionary<Guid, GuestDisplayProfile>> GetProfilesAsync(Guid meetingId,
            IReadOnlyCollection<Guid> invitationIds, CancellationToken ct) => throw new NotSupportedException();
    }
}
