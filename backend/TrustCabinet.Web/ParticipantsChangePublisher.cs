using TrustCabinet.Domain.Meetings;
using Microsoft.AspNetCore.SignalR;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Web;

/// <summary>Invalidates the roster for the union of readers before and after a committed change.</summary>
public sealed class ParticipantsChangePublisher(IHubContext<MeetingHub> hub) : IParticipantsChangePublisher
{
    public Task PublishAsync(ParticipantsChange change, IReadOnlyCollection<Guid> userIds,
        IReadOnlyCollection<MeetingGuestSession> guests, CancellationToken ct)
    {
        // Removed readers receive only an invalidation, never roster data. Their reload is reauthorized.
        var groups = userIds.Select(id => MeetingHub.Group(change.MeetingId, id))
            .Concat(guests.Where(g => g.IsValid && g.MeetingId == change.MeetingId)
                .Select(MeetingHub.GuestGroup))
            .Distinct().ToArray();
        return groups.Length == 0 ? Task.CompletedTask
            : hub.Clients.Groups(groups).SendAsync("participantsChanged", change, ct);
    }
}
