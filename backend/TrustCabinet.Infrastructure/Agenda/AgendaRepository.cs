using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Agenda;

public sealed class AgendaRepository(TrustCabinetDbContext db) : IAgendaRepository
{
    public async Task<IReadOnlyList<AgendaItem>> ListAsync(Guid meetingId, bool tracked, CancellationToken ct)
    {
        IQueryable<AgendaItem> query = db.AgendaItems.Include(i => i.Presenter).Where(i => i.MeetingId == meetingId);
        if (!tracked) query = query.AsNoTracking();
        return await query.OrderBy(i => i.Position).ThenBy(i => i.Id).ToListAsync(ct);
    }
    public void Add(AgendaItem item) => db.AgendaItems.Add(item);
    public void Remove(AgendaItem item) => db.AgendaItems.Remove(item);
}

/// <summary>
/// Agenda's view of the meeting's active documents (ids are MeetingDocument ids). The link table
/// allows a document on several items (ADR-0020 §5), but agenda planning still keeps the earlier
/// one-item rule until UC-33b/UC-33d bring their own endpoints (SCRUM-58).
/// </summary>
public sealed class AgendaDocumentRepository(TrustCabinetDbContext db) : IAgendaDocumentRepository
{
    public async Task<IReadOnlyList<AgendaDocumentOption>> ListAsync(Guid meetingId, CancellationToken ct)
        => await db.MeetingDocuments.AsNoTracking().Where(d => d.MeetingId == meetingId && d.RemovedAt == null)
            .OrderBy(d => d.FileName).ThenBy(d => d.Id)
            .Select(d => new AgendaDocumentOption(d.Id, d.FileName,
                d.AgendaItemLinks.OrderBy(l => l.AgendaItemId).Select(l => (Guid?)l.AgendaItemId).FirstOrDefault()))
            .ToListAsync(ct);

    public async Task<bool> CanAssociateAsync(Guid meetingId, Guid? itemId, IReadOnlyList<Guid> documentIds, CancellationToken ct)
        => await db.MeetingDocuments.CountAsync(d => documentIds.Contains(d.Id) && d.MeetingId == meetingId
            && d.RemovedAt == null && d.AgendaItemLinks.All(l => l.AgendaItemId == itemId), ct) == documentIds.Count;

    public async Task AssociateAsync(Guid meetingId, Guid itemId, IReadOnlyList<Guid> documentIds, CancellationToken ct)
    {
        var links = await db.MeetingDocumentAgendaItems.Where(l => l.AgendaItemId == itemId).ToListAsync(ct);
        db.MeetingDocumentAgendaItems.RemoveRange(links.Where(l => !documentIds.Contains(l.MeetingDocumentId)));
        db.MeetingDocumentAgendaItems.AddRange(documentIds.Where(id => links.All(l => l.MeetingDocumentId != id))
            .Select(id => new MeetingDocumentAgendaItem { MeetingDocumentId = id, AgendaItemId = itemId }));
    }
}

public sealed class AgendaMeetingEnd(TrustCabinetDbContext db) : IAgendaMeetingEnd
{
    public async Task<AgendaChange?> PrepareAsync(Guid meetingId, DateTimeOffset endedAt, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Agenda completion requires the caller's meeting transaction and lock.");
        // Reacquiring an already held row lock is harmless and protects accidental callers as well.
        await db.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {meetingId} FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);
        var item = await db.AgendaItems.SingleOrDefaultAsync(i => i.MeetingId == meetingId
            && i.Status == AgendaItemStatus.InProgress, ct);
        if (item is null) return null;
        if (!AgendaExecution.TryComplete(item, endedAt))
            throw new InvalidOperationException("The current agenda item has no valid start time or the end precedes its start.");
        return new(Guid.CreateVersion7(), meetingId, "MeetingEnded", item.Id, item.Title, item.ActualEndedAt!.Value);
    }
}

public static class AgendaServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetAgenda(this IServiceCollection services)
    {
        services.AddScoped<IAgendaRepository, AgendaRepository>();
        services.AddScoped<IAgendaDocumentRepository, AgendaDocumentRepository>();
        services.AddScoped<IAgendaService, AgendaService>();
        services.AddScoped<IAgendaMeetingEnd, AgendaMeetingEnd>();
        services.TryAddScoped<IAgendaGuestAccess, UnavailableAgendaGuestAccess>();
        return services;
    }
}
