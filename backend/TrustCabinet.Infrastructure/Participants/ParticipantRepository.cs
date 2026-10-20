using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Participants;

public sealed class ParticipantRepository(TrustCabinetDbContext db) : IParticipantRepository
{
    public async Task<ParticipantSnapshot?> LoadAsync(Guid meetingId, bool forUpdate, CancellationToken ct)
    {
        if (forUpdate)
        {
            var locked = await db.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {meetingId} FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);
            if (locked.Count == 0) return null;
        }
        var meetings = db.Meetings.AsSplitQuery()
            .Include(m => m.Participants.Where(p => p.AssignmentStatus == AssignmentStatus.Current)).ThenInclude(p => p.User).AsQueryable();
        var meeting = await (forUpdate ? meetings : meetings.AsNoTracking()).SingleOrDefaultAsync(m => m.Id == meetingId, ct);
        if (meeting is null) return null;
        var guestQuery = db.MeetingGuestInvitations.Where(g => g.MeetingId == meetingId && g.RevokedAt == null);
        var guests = await (forUpdate ? guestQuery : guestQuery.AsNoTracking()).ToListAsync(ct);
        var grantQuery = db.MeetingParticipantPermissions.Where(g =>
            (g.MeetingParticipant != null && g.MeetingParticipant.MeetingId == meetingId && g.MeetingParticipant.AssignmentStatus == AssignmentStatus.Current)
            || (g.GuestInvitation != null && g.GuestInvitation.MeetingId == meetingId && g.GuestInvitation.RevokedAt == null));
        var grants = await (forUpdate ? grantQuery : grantQuery.AsNoTracking()).ToListAsync(ct);
        return new(meeting, guests, grants);
    }
}
