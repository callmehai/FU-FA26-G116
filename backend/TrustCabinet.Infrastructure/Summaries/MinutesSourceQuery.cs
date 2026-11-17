using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Summaries;

public sealed class MinutesSourceQuery(TrustCabinetDbContext db) : IMinutesSourceQuery
{
    public async Task<IReadOnlyList<MinutesAttendee>> ListAttendeesAsync(Guid meetingId, CancellationToken ct)
        => await db.MeetingParticipants.AsNoTracking()
            .Where(p => p.MeetingId == meetingId)
            .Where(ParticipantRules.ActiveExpression)
            .OrderBy(p => p.Role).ThenBy(p => p.User.FullName)
            .Select(p => new MinutesAttendee(p.Role, p.Attendance, p.User.FullName))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MinutesGuest>> ListGuestsAsync(Guid meetingId, CancellationToken ct)
        => await db.MeetingGuestInvitations.AsNoTracking()
            .Where(g => g.MeetingId == meetingId && g.RevokedAt == null)
            .Select(g => new MinutesGuest(g.SelfDeclaredDisplayName ?? g.InvitedName, g.Attendance))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MinutesNote>> ListNotesAsync(Guid meetingId, IReadOnlyCollection<Guid> authorIds,
        CancellationToken ct)
        => await db.MeetingNotes.AsNoTracking()
            .Where(n => n.MeetingId == meetingId && authorIds.Contains(n.AuthorId) && n.Body != "")
            .OrderBy(n => n.CreatedAt)
            .Select(n => new MinutesNote(n.AgendaItemId, n.Body))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListActiveUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
        => await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id).ToListAsync(ct);

    public async Task<MinutesIssuedContacts?> LoadIssuedEmailContactsAsync(Guid[] readerIds, Guid chairId, Guid organizerId,
        CancellationToken ct)
    {
        var people = await db.Users.AsNoTracking().Where(u => readerIds.Contains(u.Id) && u.IsActive)
            .Select(u => new { u.Id, u.FullName, u.Email }).ToListAsync(ct);
        if (people.Count == 0) return null;
        var chair = await db.Users.AsNoTracking().Where(u => u.Id == chairId).Select(u => u.FullName).SingleAsync(ct);
        var organizer = await db.Users.AsNoTracking().Where(u => u.Id == organizerId)
            .Select(u => new { u.FullName, u.Email }).SingleAsync(ct);
        return new MinutesIssuedContacts(people.Select(p => new MinutesEmailRecipient(p.Id, p.FullName, p.Email)).ToList(),
            chair, organizer.FullName, organizer.Email);
    }
}
