using Microsoft.AspNetCore.SignalR;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Attendance;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Web;

public sealed class AttendanceChangePublisher(IHubContext<MeetingHub> hub, IParticipantRepository participants,
    IParticipantGuestAccess guests, IAgendaGuestAccess agendaGuests) : IAttendanceChangePublisher
{
    public async Task PublishAsync(AttendanceChange change, CancellationToken ct)
    {
        var s = await participants.LoadAsync(change.MeetingId, false, ct);
        if (s is null) return;
        var groups = s.Meeting.Participants.Where(ParticipantRules.Active)
            .Where(p => ParticipantReadPermissions.Evaluate(s, new(p.UserId)).CanViewParticipants)
            .Select(p => MeetingHub.Group(change.MeetingId, p.UserId)).Distinct().ToList();
        if (ParticipantRules.Published(s.Meeting.Status))
        {
            var sessions = await guests.ListSessionsAsync(change.MeetingId, ct);
            foreach (var session in sessions.Distinct())
            {
                if (!session.IsValid || session.MeetingId != change.MeetingId || !s.Guests.Any(g => g.Id == session.InvitationId)
                    || !ParticipantReadPermissions.Evaluate(s, new(Guest: session)).CanViewParticipants
                    || !await guests.CanAccessAsync(session, ct)) continue;
                var identity = new AgendaGuestSession(session.MeetingId, session.InvitationId, session.SessionId);
                if (await agendaGuests.CanReadAsync(identity, ct)) groups.Add(MeetingHub.GuestGroup(identity));
            }
        }
        if (groups.Count > 0) await hub.Clients.Groups(groups).SendAsync("attendanceChanged", change, ct);
    }
}
