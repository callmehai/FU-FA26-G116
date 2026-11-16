using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Speaking;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// Speaking requests as the queue use cases see them: a list of tracked rows, the active accounts, and who is in the
/// room. What is staged is in <see cref="Added"/>, what is closed in <see cref="Closed"/>; nothing here saves.
/// </summary>
public sealed class FakeSpeakingRequestRepository : ISpeakingRequestRepository
{
    public List<SpeakingRequest> Requests { get; } = [];
    public HashSet<Guid> ActiveUsers { get; } = [];

    /// <summary>Members (by account) and guests (by session) connected to the room.</summary>
    public HashSet<Guid> InRoom { get; } = [];

    public Guid? CurrentAgendaItemId { get; set; }
    public List<SpeakingRequest> Added { get; } = [];
    public List<(Guid MeetingId, Guid? UserId, IReadOnlyCollection<Guid>? GuestIds, DateTimeOffset EndedAt)> Closed { get; } = [];

    /// <summary>Thrown by every read in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    public Task<bool> IsActiveUserAsync(Guid userId, CancellationToken ct) => Read(() => ActiveUsers.Contains(userId));

    public Task<SpeakingRequest?> FindAsync(Guid meetingId, Guid requestId, CancellationToken ct)
        => Read(() => Requests.SingleOrDefault(r => r.Id == requestId && r.MeetingId == meetingId));

    public Task<SpeakingRequest?> FindOpenAsync(Guid meetingId, SpeakingViewer viewer, CancellationToken ct)
        => Read(() => Requests.SingleOrDefault(r => r.MeetingId == meetingId && SpeakingRules.IsOpen(r.Status)
            && SpeakingRules.IsMine(r, viewer)));

    public Task<int> LastQueuePositionAsync(Guid meetingId, CancellationToken ct)
        => Read(() => Requests.Where(r => r.MeetingId == meetingId).Select(r => r.QueuePosition).DefaultIfEmpty().Max());

    public Task<Guid?> FindCurrentAgendaItemIdAsync(Guid meetingId, CancellationToken ct) => Read(() => CurrentAgendaItemId);

    public Task<bool> IsFloorHeldAsync(Guid meetingId, CancellationToken ct)
        => Read(() => Requests.Any(r => r.MeetingId == meetingId
            && r.Status is SpeakingRequestStatus.Invited or SpeakingRequestStatus.Speaking));

    public Task<SpeakingRequest?> FindQueuedNeighbourAsync(Guid meetingId, int queuePosition, bool up, CancellationToken ct)
    {
        var queued = Requests.Where(r => r.MeetingId == meetingId && r.Status == SpeakingRequestStatus.Queued);
        return Read(() => up
            ? queued.Where(r => r.QueuePosition < queuePosition).MaxBy(r => r.QueuePosition)
            : queued.Where(r => r.QueuePosition > queuePosition).MinBy(r => r.QueuePosition));
    }

    public Task<bool> IsInRoomAsync(Guid meetingId, ParticipantActor actor, CancellationToken ct)
        => Read(() => InRoom.Contains(actor.Guest?.SessionId ?? actor.UserId!.Value));

    public Task<IReadOnlyList<SpeakingRequestRow>> ListOpenAsync(Guid meetingId, CancellationToken ct)
        => Read<IReadOnlyList<SpeakingRequestRow>>(() => Requests.Where(r => r.MeetingId == meetingId && SpeakingRules.IsOpen(r.Status))
            .Select(r => new SpeakingRequestRow(r.Id, r.UserId, r.GuestInvitationId, r.Status, r.QueuePosition, r.RequestedAt,
                r.InvitedAt, r.StartedAt, r.AgendaItemId, null, r.UserId is null ? null : "Thành viên",
                r.UserId is null ? "Khách mời" : null))
            .ToArray());

    public Task<int> CloseOpenAsync(Guid meetingId, Guid? userId, IReadOnlyCollection<Guid>? guestInvitationIds,
        DateTimeOffset endedAt, DateTimeOffset now, CancellationToken ct)
        => Read(() =>
        {
            Closed.Add((meetingId, userId, guestInvitationIds, endedAt));
            return Requests.Where(r => r.MeetingId == meetingId).Count(r => SpeakingRules.Close(r, endedAt, now));
        });

    public Task CloseAllOpenAsync(Guid meetingId, DateTimeOffset endedAt, CancellationToken ct)
        => CloseOpenAsync(meetingId, null, null, endedAt, endedAt, ct);

    public void Add(SpeakingRequest request)
    {
        Added.Add(request);
        Requests.Add(request);
    }

    private Task<T> Read<T>(Func<T> answer) => Fail is { } failure ? Task.FromException<T>(failure) : Task.FromResult(answer());
}

/// <summary>Speaking events in order, with the commit count at the time, so a test can see each went out after the commit.</summary>
public sealed class FakeSpeakingChangePublisher(FakeUnitOfWork unitOfWork) : ISpeakingChangePublisher
{
    public List<(SpeakingChange Change, SpeakingViewer? Invited, int Commits)> Published { get; } = [];

    public Task PublishAsync(SpeakingChange change, SpeakingViewer? invited, CancellationToken ct)
    {
        Published.Add((change, invited, unitOfWork.Commits));
        return Task.CompletedTask;
    }
}
