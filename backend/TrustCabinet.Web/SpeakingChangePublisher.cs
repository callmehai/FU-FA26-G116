using Microsoft.AspNetCore.SignalR;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Speaking;

namespace TrustCabinet.Web;

/// <summary>
/// Tells the speaking panels that the queue changed (SCRUM-27): <c>speakingChanged</c> to every current reader, guests
/// included, and <c>speakingInvited</c> to the invited person alone, so the room can say it is their turn.
/// </summary>
public sealed class SpeakingChangePublisher(IHubContext<MeetingHub> hub, MeetingAudience audience,
    IAgendaGuestAccess guests) : ISpeakingChangePublisher
{
    public async Task PublishAsync(SpeakingChange change, SpeakingViewer? invited, CancellationToken ct)
    {
        var groups = MeetingHub.GroupsOf(change.MeetingId, await audience.ReadersAsync(change.MeetingId, ct));
        if (groups.Count == 0) return;
        await hub.Clients.Groups(groups).SendAsync("speakingChanged", change, ct);
        if (invited is null) return;

        // Only groups that are readers now: someone removed in between is not told it is their turn.
        var own = invited.UserId is { } userId
            ? groups.Where(g => g == MeetingHub.Group(change.MeetingId, userId)).ToList()
            : (await guests.ListReadersAsync(change.MeetingId, ct))
                .Where(g => g.IsValid && g.InvitationId == invited.GuestInvitationId)
                .Select(MeetingHub.GuestGroup).Where(groups.Contains).ToList();
        if (own.Count > 0) await hub.Clients.Groups(own).SendAsync("speakingInvited", change, ct);
    }
}
