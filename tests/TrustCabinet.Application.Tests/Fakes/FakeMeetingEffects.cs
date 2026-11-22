using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Calls;
using TrustCabinet.Application.Notifications;
using TrustCabinet.Application.Speaking;
using TrustCabinet.Application.Votes;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// Every module <see cref="Meetings.MeetingLifecycleService"/> calls when a meeting starts or ends, recording each
/// call by name in order, so a test can assert what ran inside the transaction and what ran after the commit.
/// Members the lifecycle service does not call throw.
/// </summary>
public sealed class FakeMeetingEffects : IAgendaMeetingEnd, ISpeakingMeetingEnd, IAgendaChangePublisher, IVoteMeetingEnd,
    IVoteChangePublisher, INotificationService, ICallRoomService
{
    public List<string> Calls { get; } = [];

    /// <summary>The recipients passed to <see cref="QueueMeetingStartedAsync"/>.</summary>
    public IReadOnlyList<Guid>? StartedRecipients { get; private set; }

    Task<AgendaChange?> IAgendaMeetingEnd.PrepareAsync(Guid meetingId, DateTimeOffset endedAt, CancellationToken ct)
    {
        Calls.Add("agenda.prepare");
        return Task.FromResult<AgendaChange?>(null);
    }

    Task ISpeakingMeetingEnd.PrepareAsync(Guid meetingId, DateTimeOffset endedAt, CancellationToken ct)
    {
        Calls.Add("speaking.prepare");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VoteChange>> DeleteDraftsAsync(Guid meetingId, Guid? endedById, DateTimeOffset endedAt,
        CancellationToken ct = default)
    {
        Calls.Add("votes.deleteDrafts");
        return Task.FromResult<IReadOnlyList<VoteChange>>([]);
    }

    public Task<IReadOnlyList<VoteChange>> DeleteDraftsOnCancelAsync(Guid meetingId, Guid? cancelledById, DateTimeOffset at,
        CancellationToken ct = default)
    {
        Calls.Add("votes.deleteDraftsOnCancel");
        return Task.FromResult<IReadOnlyList<VoteChange>>([]);
    }

    public Task PublishAsync(AgendaChange change, CancellationToken ct)
    {
        Calls.Add($"agenda.publish:{change.ChangeType}");
        return Task.CompletedTask;
    }

    public Task PublishAsync(VoteChange change, bool managersOnly, CancellationToken ct)
    {
        Calls.Add("votes.publish");
        return Task.CompletedTask;
    }

    public Task CloseMeetingRoomAsync(Guid meetingId, CancellationToken ct = default)
    {
        Calls.Add("room.close");
        return Task.CompletedTask;
    }

    public Task QueueMeetingStartedAsync(Guid meetingId, string meetingTitle, Guid historyId, DateTimeOffset occurredAt,
        IReadOnlyList<Guid> recipientUserIds, CancellationToken ct = default)
    {
        Calls.Add("notifications.started");
        StartedRecipients = recipientUserIds;
        return Task.CompletedTask;
    }

    public Task<NotificationPage> ListAsync(Guid userId, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Notification?> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<NotificationTarget?> GetTargetAsync(Guid userId, Guid notificationId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<int> QueueStartingSoonAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<Notification>> PendingPushAsync(int limit, CancellationToken ct = default) => throw new NotSupportedException();
    public Task MarkPushedAsync(Guid notificationId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CallResult<CallRoomView>> GetRoomAsync(Guid meetingId, CallCaller caller, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CallResult<CallJoinTicket>> JoinAsync(Guid meetingId, CallCaller caller, bool recordingNoticeAcknowledged = false, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CallOutcome> MuteAsync(Guid meetingId, Guid actorId, CallMember target, CancellationToken ct = default) => throw new NotSupportedException();
    public Task HandleWebhookAsync(CallWebhookEvent webhook, CancellationToken ct = default) => throw new NotSupportedException();
    public Task ReconcileAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnforceRoomAccessAsync(Guid meetingId, CancellationToken ct = default) => throw new NotSupportedException();
}
