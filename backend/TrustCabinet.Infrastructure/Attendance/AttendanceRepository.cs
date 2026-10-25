using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Attendance;

public sealed class AttendanceRepository(TrustCabinetDbContext db)
{
    public async Task LockMeetingAsync(Guid meetingId, CancellationToken ct)
        => await db.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {meetingId} FOR UPDATE")
            .AsNoTracking().ToArrayAsync(ct);

    // Load tracked targets only after acquiring row locks. Both queries use the same stable order.
    public async Task<(MeetingParticipant[] Internal, MeetingGuestInvitation[] Guests)> LockTargetsAsync(
        Guid meetingId, Guid[] internalIds, Guid[] guestIds, CancellationToken ct)
    {
        var participants = internalIds.Length == 0 ? [] : await db.MeetingParticipants.FromSqlInterpolated($"""
            SELECT * FROM meeting_participants WHERE meeting_id = {meetingId} AND id = ANY({internalIds})
            ORDER BY id FOR UPDATE
            """).ToArrayAsync(ct);
        var guests = guestIds.Length == 0 ? [] : await db.MeetingGuestInvitations.FromSqlInterpolated($"""
            SELECT * FROM meeting_guest_invitations WHERE meeting_id = {meetingId} AND id = ANY({guestIds})
            ORDER BY id FOR UPDATE
            """).ToArrayAsync(ct);
        return (participants, guests);
    }
}
