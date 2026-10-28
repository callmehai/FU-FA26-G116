using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Recordings;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web;

/// <summary>
/// SCRUM-109 — tells the people of a meeting that its per-mic transcript or capture mode changed, so they re-read.
/// The same people who may listen to its recordings: the active roster, not an approver or creator outside it.
/// </summary>
public sealed class SpeechChangePublisher(IHubContext<MeetingHub> hub, IMeetingRepository meetings, TrustCabinetDbContext db)
    : ISpeechChangePublisher
{
    public async Task PublishAsync(SpeechChange change, CancellationToken ct)
    {
        var meeting = await meetings.FindWorkflowAsync(change.MeetingId, ct: ct);
        if (meeting is null) return;
        var candidates = meeting.Participants.Select(p => p.UserId).Distinct()
            .Where(id => RecordingRules.CanListen(meeting, id)).ToArray();
        var listeners = await db.Users.AsNoTracking().Where(u => u.IsActive && candidates.Contains(u.Id))
            .Select(u => u.Id).ToArrayAsync(ct);
        if (listeners.Length == 0) return;
        await hub.Clients.Groups(listeners.Select(id => MeetingHub.Group(change.MeetingId, id)).ToList())
            .SendAsync("speechChanged", change, ct);
    }
}
