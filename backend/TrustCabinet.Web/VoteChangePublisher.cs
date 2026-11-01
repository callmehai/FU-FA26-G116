using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Votes;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web;

/// <summary>
/// Tells the meeting screen and the room's voting panel that a vote changed (SCRUM-28). A draft change reaches only
/// the current Chair and Secretary, who alone see drafts (BR-253); everything else reaches every current reader.
/// </summary>
public sealed class VoteChangePublisher(IHubContext<MeetingHub> hub, IMeetingRepository meetings,
    TrustCabinetDbContext db, IAgendaGuestAccess guests) : IVoteChangePublisher
{
    public async Task PublishAsync(VoteChange change, bool managersOnly, CancellationToken ct)
    {
        var groups = managersOnly
            ? await ManagerGroupsAsync(change.MeetingId, ct)
            : await AgendaChangePublisher.ReaderGroupsAsync(change.MeetingId, meetings, db, guests, ct);
        if (groups.Count > 0) await hub.Clients.Groups(groups).SendAsync("voteChanged", change, ct);
    }

    private async Task<List<string>> ManagerGroupsAsync(Guid meetingId, CancellationToken ct)
    {
        var meeting = await meetings.FindWorkflowAsync(meetingId, ct: ct);
        if (meeting is null) return [];
        var candidates = meeting.Participants.Select(p => p.UserId).Distinct()
            .Where(id => ParticipantRules.IsManager(meeting, id)).ToArray();
        var active = await db.Users.AsNoTracking().Where(u => candidates.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id).ToListAsync(ct);
        return active.Select(id => MeetingHub.Group(meetingId, id)).ToList();
    }
}
