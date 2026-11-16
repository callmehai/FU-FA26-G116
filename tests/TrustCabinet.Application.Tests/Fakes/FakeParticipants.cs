using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Attendance;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Speaking;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// The roster as the participant use cases see it: snapshots by meeting, plus the lookups the moved queries
/// ran against the store. What the services stage or lock is recorded; nothing here saves.
/// </summary>
public sealed class FakeParticipantRepository : IParticipantRepository
{
    public Dictionary<Guid, ParticipantSnapshot> Snapshots { get; } = [];

    /// <summary>Loads in order, as (meetingId, forUpdate).</summary>
    public List<(Guid MeetingId, bool ForUpdate)> Loads { get; } = [];

    /// <summary>Accounts locked by <see cref="LockUserAsync"/> and <see cref="LockUsersAsync"/>, in order.</summary>
    public List<Guid> LockedUsers { get; } = [];

    /// <summary>Meeting loads and account locks in the one order they ran, so a test can assert the lock order.</summary>
    public List<string> Steps { get; } = [];

    /// <summary>Per-account active state returned by <see cref="LockUsersAsync"/>; missing ids are active.</summary>
    public Dictionary<Guid, bool> LockedUsersActive { get; } = [];

    /// <summary>Invitation option reads in order.</summary>
    public List<(Guid? MeetingId, ParticipantQuery Query)> OptionReads { get; } = [];

    /// <summary>What <see cref="ListInvitationOptionsAsync"/> answers.</summary>
    public (int Total, IReadOnlyList<ParticipantOption> Items) InvitationOptions = (0, []);

    /// <summary>Accounts counted as active by <see cref="ListActiveReaderIdsAsync"/>.</summary>
    public HashSet<Guid> ActiveUsers { get; } = [];

    public List<AgendaItem> AgendaItems { get; } = [];
    public List<MeetingParticipant> AddedParticipants { get; } = [];
    public List<MeetingGuestInvitation> AddedGuests { get; } = [];

    public List<VotingSession> Votes { get; } = [];
    public List<VoteReceipt> Receipts { get; } = [];
    public List<VoteEligibleVoter> EligibleVoters { get; } = [];

    /// <summary>Thrown by every read in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    public Task<ParticipantSnapshot?> LoadAsync(Guid meetingId, bool forUpdate, CancellationToken ct)
    {
        Loads.Add((meetingId, forUpdate));
        Steps.Add(forUpdate ? "lock-meeting" : "load-meeting");
        return Read(() => Snapshots.GetValueOrDefault(meetingId));
    }

    public Task LockUserAsync(Guid userId, CancellationToken ct)
    {
        LockedUsers.Add(userId);
        Steps.Add("lock-user");
        return Fail is { } failure ? Task.FromException(failure) : Task.CompletedTask;
    }

    public Task<IReadOnlyList<User>> LockUsersAsync(Guid[] userIds, CancellationToken ct)
        => Read<IReadOnlyList<User>>(() =>
        {
            LockedUsers.AddRange(userIds);
            return userIds.Order().Select(id => new User
            {
                Id = id, FullName = "Thành viên", Email = $"{id}@trustcabinet.local",
                IsActive = !LockedUsersActive.TryGetValue(id, out var active) || active,
            }).ToArray();
        });

    public Task<(int Total, IReadOnlyList<ParticipantOption> Items)> ListInvitationOptionsAsync(
        ParticipantSnapshot? snapshot, ParticipantQuery query, CancellationToken ct)
    {
        OptionReads.Add((snapshot?.Meeting.Id, query));
        return Read(() => InvitationOptions);
    }

    public Task<IReadOnlyList<Guid>> ListActiveReaderIdsAsync(Guid[] candidateUserIds, CancellationToken ct)
        => Read<IReadOnlyList<Guid>>(() => candidateUserIds.Where(ActiveUsers.Contains).ToArray());

    public Task<IReadOnlyList<AgendaItem>> ListPresentedItemsAsync(
        Guid meetingId, Guid? presenterUserId, Guid? presenterGuestInvitationId, CancellationToken ct)
        => Read<IReadOnlyList<AgendaItem>>(() => AgendaItems
            .Where(i => i.MeetingId == meetingId
                && (presenterUserId != null ? i.PresenterId == presenterUserId : i.PresenterGuestInvitationId == presenterGuestInvitationId))
            .ToList());

    public Task<bool> ExistsActiveGuestWithEmailsAsync(Guid meetingId, string[] emails, CancellationToken ct)
        => Read(() => Snapshots.GetValueOrDefault(meetingId) is { } s
            && s.Guests.Any(g => g.RevokedAt is null && emails.Contains(g.Email)));

    public Task<bool> ExistsActiveGuestWithEmailAsync(Guid meetingId, string email, CancellationToken ct)
        => Read(() => Snapshots.GetValueOrDefault(meetingId) is { } s
            && s.Guests.Any(g => g.RevokedAt is null && g.Email == email));

    public Task<bool> ExistsCurrentParticipantWithEmailAsync(Guid meetingId, string email, CancellationToken ct)
        => Read(() => Snapshots.GetValueOrDefault(meetingId) is { } s
            && s.Meeting.Participants.Any(p => ParticipantRules.Active(p) && p.User.Email == email));

    public void Add(MeetingParticipant participant, User account)
    {
        participant.User = account;
        AddedParticipants.Add(participant);
    }

    public void AddMany(IReadOnlyList<(MeetingParticipant Participant, User Account)> items)
    {
        foreach (var (participant, account) in items)
            Add(participant, account);
    }

    public void Add(MeetingGuestInvitation guest) => AddedGuests.Add(guest);

    public Task<IReadOnlyList<MeetingGuestInvitation>> ListStagedGuestsAsync(Guid meetingId, CancellationToken ct)
        => Read<IReadOnlyList<MeetingGuestInvitation>>(() =>
            (Snapshots.GetValueOrDefault(meetingId)?.Guests ?? [])
            .Concat(AddedGuests.Where(g => g.MeetingId == meetingId)).ToList());

    public Task<VotingSession?> FindOpenVoteAsync(Guid meetingId, CancellationToken ct)
        => Read(() => Votes.SingleOrDefault(v => v.MeetingId == meetingId && v.Status == VotingStatus.Open));

    public Task<bool> ExistsVoteReceiptWithEmailAsync(Guid votingSessionId, string email, CancellationToken ct)
        => Read(() => Receipts.Any(r => r.VotingSessionId == votingSessionId && r.GuestInvitation!.Email == email));

    public Task<VoteEligibleVoter?> FindEligibleVoterByEmailAsync(Guid votingSessionId, string email, CancellationToken ct)
        => Read(() => EligibleVoters.FirstOrDefault(e => e.VotingSessionId == votingSessionId && e.GuestInvitation!.Email == email));

    private Task<T> Read<T>(Func<T> answer) => Fail is { } failure ? Task.FromException<T>(failure) : Task.FromResult(answer());
}

/// <summary>
/// What the participant and attendance use cases do outside their transaction (realtime events, guest checks),
/// recorded in one ordered list so a test can see each ran after the commit. The commit count at the call time
/// tells whether it ran inside the transaction or after it.
/// </summary>
public sealed class FakeParticipantEffects(FakeUnitOfWork unitOfWork) : IParticipantsChangePublisher,
    IAttendanceChangePublisher, IMeetingParticipantAccessPublisher, IAgendaChangePublisher, IParticipantGuestAccess
{
    public List<string> Calls { get; } = [];
    public List<ParticipantsChange> ParticipantChanges { get; } = [];
    public List<AttendanceChange> AttendanceChanges { get; } = [];
    public List<MeetingAccessChange> AccessChanges { get; } = [];
    public List<AgendaChange> AgendaChanges { get; } = [];

    public HashSet<Guid> GuestSessionsAllowed { get; } = [];
    public Dictionary<Guid, GuestDisplayProfile> Profiles { get; } = [];
    public List<MeetingGuestSession> Readers { get; } = [];

    /// <summary>Thrown by every publish in place of delivering: a transport that cannot be reached.</summary>
    public Exception? FailPublish { get; set; }

    public Task PublishAsync(ParticipantsChange change, IReadOnlyCollection<Guid> userIds,
        IReadOnlyCollection<MeetingGuestSession> guests, CancellationToken ct)
    {
        Publish("participants.publish");
        ParticipantChanges.Add(change);
        return Task.CompletedTask;
    }

    public Task PublishAsync(AttendanceChange change, CancellationToken ct)
    {
        Publish("attendance.publish");
        AttendanceChanges.Add(change);
        return Task.CompletedTask;
    }

    public Task PublishAsync(MeetingAccessChange change, CancellationToken ct)
    {
        Publish("access.publish");
        AccessChanges.Add(change);
        return Task.CompletedTask;
    }

    public Task PublishAsync(AgendaChange change, CancellationToken ct)
    {
        Publish("agenda.publish");
        AgendaChanges.Add(change);
        return Task.CompletedTask;
    }

    public Task<bool> CanAccessAsync(MeetingGuestSession session, CancellationToken ct)
        => Task.FromResult(GuestSessionsAllowed.Contains(session.SessionId));

    public Task<IReadOnlyDictionary<Guid, GuestDisplayProfile>> GetProfilesAsync(Guid meetingId,
        IReadOnlyCollection<Guid> invitationIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<Guid, GuestDisplayProfile>>(
            Profiles.Where(p => invitationIds.Contains(p.Key)).ToDictionary());

    public Task<IReadOnlyList<MeetingGuestSession>> ListReadersAsync(Guid meetingId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MeetingGuestSession>>(Readers.Where(r => r.MeetingId == meetingId).ToArray());

    private void Publish(string name)
    {
        if (FailPublish is { } failure) throw failure;
        Calls.Add($"{name}:commits={unitOfWork.Commits}");
    }
}

/// <summary>Staged roster notices in order, as (meeting, participant, guest, kind, notify). The meeting-wide stagings do nothing.</summary>
public sealed class FakeMeetingInvitationStager : IMeetingInvitationStager
{
    public List<(Guid MeetingId, Guid? ParticipantId, Guid? GuestId, InvitationEventKind Kind, bool Notify)> Staged { get; } = [];

    public void Stage(Meeting meeting, MeetingParticipant? participant, MeetingGuestInvitation? guest,
        InvitationEventKind kind, DateTimeOffset now, bool notify, Guid? sourceEventId = null, string? reason = null)
        => Staged.Add((meeting.Id, participant?.Id, guest?.Id, kind, notify));

    public Task StagePublicationInvitationsAsync(Meeting meeting, DateTimeOffset now, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task StageCancellationAsync(
        Meeting meeting, Guid historyId, Guid actorId, string reason, DateTimeOffset now, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task StageGuestRescheduleInvitationsAsync(Meeting meeting, Guid historyId, DateTimeOffset now,
        CancellationToken ct = default)
        => Task.CompletedTask;
}

/// <summary>Closed speaking requests in order; <see cref="Result"/> is the closed count every call answers.</summary>
public sealed class FakeSpeakingRequestCloser : ISpeakingRequestCloser
{
    public List<(Guid MeetingId, Guid? UserId, IReadOnlyCollection<Guid>? GuestIds)> Closes { get; } = [];
    public int Result { get; set; }

    public Task<int> CloseOpenAsync(Guid meetingId, Guid? userId, IReadOnlyCollection<Guid>? guestInvitationIds,
        DateTimeOffset endedAt, DateTimeOffset now, CancellationToken ct)
    {
        Closes.Add((meetingId, userId, guestInvitationIds));
        return Task.FromResult(Result);
    }
}
