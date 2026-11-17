using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Notes;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Agenda;

public sealed class AgendaRepository(TrustCabinetDbContext db) : IAgendaRepository
{
    public async Task<IReadOnlyList<AgendaItem>> ListAsync(Guid meetingId, bool tracked, CancellationToken ct)
    {
        IQueryable<AgendaItem> query = db.AgendaItems.Include(i => i.Presenter).Include(i => i.PresenterGuestInvitation).Include(i => i.ConclusionUpdatedBy).Where(i => i.MeetingId == meetingId);
        if (!tracked) query = query.AsNoTracking();
        return await query.OrderBy(i => i.Position).ThenBy(i => i.Id).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<MeetingGuestInvitation>> CurrentGuestsAsync(Guid meetingId, CancellationToken ct)
        => await db.MeetingGuestInvitations.AsNoTracking().Where(g => g.MeetingId == meetingId && g.RevokedAt == null).ToListAsync(ct);
    public Task<bool> HasMinutesAsync(Guid meetingId, CancellationToken ct)
        => db.MeetingMinutes.AnyAsync(m => m.MeetingId == meetingId, ct);
    public void Add(AgendaItem item) => db.AgendaItems.Add(item);
    public async Task RemoveAsync(AgendaItem item, DateTimeOffset now, CancellationToken ct)
    {
        // The FK would set AgendaItemId to null, which collides with the author's meeting-wide note on the unique index
        // and fails the delete. Fold each note into that one instead.
        var notes = await db.MeetingNotes.Where(n => n.MeetingId == item.MeetingId
            && (n.AgendaItemId == item.Id || n.AgendaItemId == null)).ToListAsync(ct);
        foreach (var note in notes.Where(n => n.AgendaItemId == item.Id).ToList())
        {
            if (!string.IsNullOrWhiteSpace(note.Body))
            {
                var general = notes.FirstOrDefault(n => n.AgendaItemId == null && n.AuthorId == note.AuthorId);
                if (general is null)
                {
                    general = new MeetingNote { MeetingId = item.MeetingId, AuthorId = note.AuthorId, Body = "",
                        CreatedAt = note.CreatedAt };
                    db.MeetingNotes.Add(general);
                    notes.Add(general);
                }
                general.Body = MeetingNoteRules.Fold(general.Body, item.Title, note.Body);
                general.UpdatedAt = now;
            }
            db.MeetingNotes.Remove(note);
        }
        db.AgendaItems.Remove(item);
    }

    public async Task<Meeting?> LockMeetingAsync(Guid meetingId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Locking the meeting requires the caller's meeting transaction.");
        return (await db.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {meetingId} FOR UPDATE")
            .AsNoTracking().ToListAsync(ct)).SingleOrDefault();
    }

    public Task<AgendaItem?> FindCurrentAsync(Guid meetingId, CancellationToken ct)
        => db.AgendaItems.SingleOrDefaultAsync(i => i.MeetingId == meetingId
            && i.Status == AgendaItemStatus.InProgress, ct);

    public Task<AgendaItem?> FindAsync(Guid meetingId, Guid itemId, CancellationToken ct)
        => db.AgendaItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == itemId
            && i.MeetingId == meetingId, ct);

    public async Task<IReadOnlyList<MeetingParticipant>> ListParticipationsAsync(Guid meetingId, Guid userId,
        CancellationToken ct)
        => await db.MeetingParticipants.Include(p => p.User)
            .Where(p => p.MeetingId == meetingId && p.UserId == userId).ToListAsync(ct);

    public Task<MeetingGuestInvitation?> FindCurrentGuestAsync(Guid meetingId, Guid invitationId, CancellationToken ct)
        => db.MeetingGuestInvitations.AsNoTracking().SingleOrDefaultAsync(g =>
            g.Id == invitationId && g.MeetingId == meetingId && g.RevokedAt == null, ct);

    public Task<string> GetUserFullNameAsync(Guid userId, CancellationToken ct)
        => db.Users.Where(u => u.Id == userId).Select(u => u.FullName).SingleAsync(ct);
}

/// <summary>
/// Agenda's view of the meeting's active documents (ids are MeetingDocument ids). The link table
/// allows a document on several items (ADR-0020 §5). Each mutation changes only its own links.
/// </summary>
public sealed class AgendaDocumentRepository(TrustCabinetDbContext db) : IAgendaDocumentRepository
{
    public async Task<IReadOnlyList<AgendaDocumentOption>> ListAsync(Guid meetingId, CancellationToken ct)
        => await db.MeetingDocuments.AsNoTracking().Where(d => d.MeetingId == meetingId && d.RemovedAt == null)
            .OrderBy(d => d.FileName).ThenBy(d => d.Id)
            .Select(d => new AgendaDocumentOption(d.Id, d.FileName,
                d.AgendaItemLinks.OrderBy(l => l.AgendaItemId).Select(l => l.AgendaItemId).ToArray()))
            .ToListAsync(ct);

    public async Task<bool> CanAssociateAsync(Guid meetingId, Guid? itemId, IReadOnlyList<Guid> documentIds, CancellationToken ct)
        => await db.MeetingDocuments.CountAsync(d => documentIds.Contains(d.Id) && d.MeetingId == meetingId
            && d.RemovedAt == null, ct) == documentIds.Count;

    public async Task AssociateAsync(Guid meetingId, Guid itemId, IReadOnlyList<Guid> documentIds, CancellationToken ct)
    {
        var links = await db.MeetingDocumentAgendaItems.Where(l => l.AgendaItemId == itemId).ToListAsync(ct);
        db.MeetingDocumentAgendaItems.RemoveRange(links.Where(l => !documentIds.Contains(l.MeetingDocumentId)));
        db.MeetingDocumentAgendaItems.AddRange(documentIds.Where(id => links.All(l => l.MeetingDocumentId != id))
            .Select(id => new MeetingDocumentAgendaItem { MeetingDocumentId = id, AgendaItemId = itemId }));
    }
}

public static class AgendaServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetAgenda(this IServiceCollection services)
    {
        services.AddScoped<IAgendaRepository, AgendaRepository>();
        services.AddScoped<IAgendaDocumentRepository, AgendaDocumentRepository>();
        return services;
    }
}
