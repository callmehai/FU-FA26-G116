using Microsoft.AspNetCore.SignalR;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Notes;

namespace TrustCabinet.Web;

/// <summary>
/// Tells the Chair's and the Secretary's open note panels — on the meeting page, in the room, in a second tab — that a
/// note was saved (SCRUM-30, BR-399). Nobody else reads notes (BR-395), so nobody else hears of them. The event holds
/// no text; a panel refetches, which is reauthorized.
/// </summary>
public sealed class NotesChangePublisher(IHubContext<MeetingHub> hub, MeetingAudience audience)
    : INotesChangePublisher
{
    public async Task PublishAsync(NotesChange change, CancellationToken ct)
    {
        var groups = MeetingHub.GroupsOf(change.MeetingId, await audience.ManagersAsync(change.MeetingId, ct));
        if (groups.Count > 0) await hub.Clients.Groups(groups).SendAsync("notesChanged", change, ct);
    }
}
