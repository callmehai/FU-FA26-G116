using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Notes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Notes;

public sealed class MeetingNoteRepository(TrustCabinetDbContext db) : IMeetingNoteRepository
{
    public async Task<IReadOnlyList<MeetingNote>> ListAsync(Guid meetingId, HashSet<Guid> authorIds,
        CancellationToken ct)
        => await db.MeetingNotes.AsNoTracking().Include(n => n.Author)
            .Where(n => n.MeetingId == meetingId && authorIds.Contains(n.AuthorId))
            .OrderBy(n => n.AgendaItem == null ? 0 : n.AgendaItem.Position)
            .ThenBy(n => n.Author.FullName)
            .ToListAsync(ct);

    public Task<Guid?> FindCurrentItemAsync(Guid meetingId, CancellationToken ct)
        => db.AgendaItems.AsNoTracking()
            .Where(a => a.MeetingId == meetingId && a.Status == AgendaItemStatus.InProgress)
            .Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);

    public Task<bool> HasMinutesAsync(Guid meetingId, CancellationToken ct)
        => db.MeetingMinutes.AnyAsync(m => m.MeetingId == meetingId, ct);

    public Task<bool> AgendaItemExistsAsync(Guid meetingId, Guid agendaItemId, CancellationToken ct)
        => db.AgendaItems.AnyAsync(a => a.Id == agendaItemId && a.MeetingId == meetingId, ct);

    public async Task<Guid?> FindIdAsync(Guid meetingId, Guid? agendaItemId, Guid authorId,
        CancellationToken ct)
    {
        var existing = await db.MeetingNotes.AsNoTracking()
            .Where(n => n.MeetingId == meetingId && n.AgendaItemId == agendaItemId && n.AuthorId == authorId)
            .Select(n => new { n.Id }).FirstOrDefaultAsync(ct);
        return existing?.Id;
    }

    public void Add(MeetingNote note) => db.MeetingNotes.Add(note);

    public Task<int> TryUpdateAsync(Guid id, DateTimeOffset from, DateTimeOffset to, string body, DateTimeOffset now,
        CancellationToken ct)
        => db.MeetingNotes
            .Where(n => n.Id == id && n.UpdatedAt >= from && n.UpdatedAt <= to)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.Body, body).SetProperty(x => x.UpdatedAt, now), ct);

    public Task<MeetingNote> GetAsync(Guid meetingId, Guid? agendaItemId, Guid authorId, CancellationToken ct)
        => db.MeetingNotes.AsNoTracking().Include(n => n.Author)
            .FirstAsync(n => n.MeetingId == meetingId && n.AgendaItemId == agendaItemId && n.AuthorId == authorId, ct);
}
