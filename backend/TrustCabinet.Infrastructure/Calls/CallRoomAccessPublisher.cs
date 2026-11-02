using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Calls;

/// <summary>
/// SCRUM-59: when a guest invitation is revoked or a participant removed, reconcile that meeting's room at once,
/// so whoever lost access is taken out now rather than at the next 30-second run. The room rules decide who goes,
/// not the event: it only says which meeting to look at. Called after commit; a failure is logged by the caller and
/// the janitor still catches it.
/// </summary>
internal sealed class CallRoomAccessPublisher(ICallRoomService rooms) : IMeetingParticipantAccessPublisher
{
    /// <summary>The secretary's request waits for this, so LiveKit gets 2 seconds (as the agenda publisher); then the janitor.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    public async Task PublishAsync(MeetingAccessChange change, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Budget);
        await rooms.EnforceRoomAccessAsync(change.MeetingId, timeout.Token);
    }
}
