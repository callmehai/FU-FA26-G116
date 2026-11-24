using Microsoft.AspNetCore.SignalR;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Votes;

namespace TrustCabinet.Web;

/// <summary>
/// Tells the meeting screen and the room's voting panel that a vote changed (SCRUM-28). A draft change reaches only
/// the current Chair and Secretary, who alone see drafts (BR-086, BR-087); everything else reaches every current reader.
/// </summary>
public sealed class VoteChangePublisher(IHubContext<MeetingHub> hub, MeetingAudience audience) : IVoteChangePublisher
{
    public async Task PublishAsync(VoteChange change, bool managersOnly, CancellationToken ct)
    {
        var groups = managersOnly
            ? MeetingHub.GroupsOf(change.MeetingId, await audience.ManagersAsync(change.MeetingId, ct))
            : MeetingHub.GroupsOf(change.MeetingId, await audience.ReadersAsync(change.MeetingId, ct));
        if (groups.Count > 0) await hub.Clients.Groups(groups).SendAsync("voteChanged", change, ct);
    }
}
