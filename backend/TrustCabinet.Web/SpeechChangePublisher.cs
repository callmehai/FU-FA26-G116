using Microsoft.AspNetCore.SignalR;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Recordings;

namespace TrustCabinet.Web;

/// <summary>
/// SCRUM-109 — tells the people of a meeting that its per-mic transcript or capture mode changed, so they re-read.
/// The same people who may listen to its recordings: the active roster, not an approver or creator outside it.
/// </summary>
public sealed class SpeechChangePublisher(IHubContext<MeetingHub> hub, MeetingAudience audience)
    : ISpeechChangePublisher
{
    public async Task PublishAsync(SpeechChange change, CancellationToken ct)
    {
        var listeners = await audience.ListenersAsync(change.MeetingId, ct);
        if (listeners.Count == 0) return;
        await hub.Clients.Groups(MeetingHub.GroupsOf(change.MeetingId, listeners))
            .SendAsync("speechChanged", change, ct);
    }
}
