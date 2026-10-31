using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Infrastructure;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Attendance;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.GuestAccess;

namespace TrustCabinet.Web;

public sealed class AttendanceChangePublisher(IHubContext<MeetingHub> hub, IParticipantRepository participants,
    IParticipantGuestAccess guests, IAgendaGuestAccess agendaGuests, TrustCabinetDbContext db) : IAttendanceChangePublisher
{
    public async Task PublishAsync(AttendanceChange change, CancellationToken ct)
    {
        var s = await participants.LoadAsync(change.MeetingId, false, ct);
        if (s is null) return;
        var candidates = s.Meeting.Participants.Select(p => p.UserId).Append(s.Meeting.CreatedById)
            .Concat(s.Meeting.AssignedApproverId is { } approver ? [approver] : [])
            .Distinct().Where(id => MeetingReadPolicy.CanRead(s.Meeting, id)).ToArray();
        var readers = await db.Users.AsNoTracking().Where(u => u.IsActive && candidates.Contains(u.Id))
            .Select(u => u.Id).ToArrayAsync(ct);
        var groups = readers.Select(id => MeetingHub.Group(change.MeetingId, id)).ToList();
        if (GuestAccessRules.MeetingOpen(s.Meeting.Status))
        {
            var sessions = await guests.ListReadersAsync(change.MeetingId, ct);
            foreach (var session in sessions.Distinct())
            {
                if (!session.IsValid || session.MeetingId != change.MeetingId || !s.Guests.Any(g => g.Id == session.InvitationId)
                    || !await guests.CanAccessAsync(session, ct)) continue;
                var identity = session;
                if (await agendaGuests.CanReadAsync(identity, ct)) groups.Add(MeetingHub.GuestGroup(identity));
            }
        }
        if (groups.Count > 0) await hub.Clients.Groups(groups).SendAsync("attendanceChanged", change, ct);
    }
}
