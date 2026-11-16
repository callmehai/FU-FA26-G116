using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Documents;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Documents;

public sealed class MeetingDocumentRepository(TrustCabinetDbContext db) : IMeetingDocumentRepository
{
    public Task<Folder?> FindUploadFolderAsync(Guid ownerId, Guid meetingId, CancellationToken ct)
        => db.Folders.SingleOrDefaultAsync(f => f.OwnerId == ownerId && f.MeetingId == meetingId, ct);

    public void AddFolder(Folder folder) => db.Folders.Add(folder);

    public void AddStoredFiles(IEnumerable<StoredFile> files) => db.StoredFiles.AddRange(files);

    public void AddUpload(Document document, MeetingDocument meetingDocument)
    {
        db.Documents.Add(document);
        db.MeetingDocuments.Add(meetingDocument);
    }

    public void AddMeetingDocument(MeetingDocument meetingDocument)
        => db.MeetingDocuments.Add(meetingDocument);

    public async Task<MeetingDocumentList> ListAsync(Guid meetingId, CancellationToken ct)
    {
        var items = await db.AgendaItems.AsNoTracking().Where(i => i.MeetingId == meetingId)
            .OrderBy(i => i.Position).ThenBy(i => i.Id).Select(i => new { i.Id, i.Position, i.Title, i.Status }).ToListAsync(ct);
        var rows = await db.MeetingDocuments.AsNoTracking()
            .Where(d => d.MeetingId == meetingId && d.RemovedAt == null)
            .OrderBy(d => d.AddedAt).ThenBy(d => d.Id)
            .Select(d => new
            {
                Row = new MeetingDocumentRow(d.Id, d.FileName, d.StoredFile.ContentType, d.StoredFile.ByteSize,
                    d.StoredFile.PageCount, d.Comments.Count, new MeetingPerson(d.AddedById, d.AddedBy.FullName), d.AddedAt),
                ItemIds = d.AgendaItemLinks.Select(l => l.AgendaItemId).ToList(),
                OnVote = d.VoteLinks.Any(),
            }).ToListAsync(ct);

        // A document linked to several items shows under each (BR-277). General means linked to no item and no
        // vote (BR-280, option B as in AI-eCabinet): a vote's own document shows only under its vote.
        var groups = items.Select(i => new MeetingDocumentAgendaGroup(i.Id, i.Position, i.Title, i.Status.ToString(),
            rows.Where(r => r.ItemIds.Contains(i.Id)).Select(r => r.Row).ToList())).ToList();
        var general = rows.Where(r => r.ItemIds.Count == 0 && !r.OnVote).Select(r => r.Row).ToList();
        var current = items.FirstOrDefault(i => i.Status == AgendaItemStatus.InProgress)?.Id;
        return new(meetingId, current, groups, general, []);
    }

    public async Task<IReadOnlyList<MeetingDocumentVoteGroup>> ListVoteGroupsAsync(Guid meetingId, bool includeDrafts,
        Guid? userId, Guid? guestInvitationId, CancellationToken ct)
    {
        var links = await db.VoteDocuments.AsNoTracking()
            .Where(l => l.VotingSession.MeetingId == meetingId && l.MeetingDocument.MeetingId == meetingId
                && l.MeetingDocument.RemovedAt == null && (includeDrafts || l.VotingSession.Status != VotingStatus.Draft))
            .OrderBy(l => l.VotingSession.CreatedAt).ThenBy(l => l.VotingSessionId)
            .ThenBy(l => l.MeetingDocument.AddedAt).ThenBy(l => l.MeetingDocumentId)
            .Select(l => new
            {
                l.VotingSessionId, l.VotingSession.Title, l.VotingSession.Status,
                IsVoter = l.VotingSession.EligibleVoters.Any(e => (userId != null && e.UserId == userId)
                    || (guestInvitationId != null && e.GuestInvitationId == guestInvitationId)),
                Row = new MeetingVoteDocumentRow(l.MeetingDocumentId, l.MeetingDocument.FileName,
                    l.MeetingDocument.StoredFile.ContentType, l.MeetingDocument.StoredFile.ByteSize,
                    l.MeetingDocument.StoredFile.PageCount, l.MeetingDocument.Comments.Count,
                    new MeetingPerson(l.MeetingDocument.AddedById, l.MeetingDocument.AddedBy.FullName),
                    l.MeetingDocument.AddedAt,
                    // One confirmation per person and document counts for every vote that links it (BR-291).
                    l.MeetingDocument.ReadConfirmations
                        .Where(c => (userId != null && c.UserId == userId)
                            || (guestInvitationId != null && c.GuestInvitationId == guestInvitationId))
                        .Select(c => (DateTimeOffset?)c.ConfirmedAt).FirstOrDefault()),
            }).ToListAsync(ct);
        // Ordered above, so grouping keeps the votes' order and each vote's documents oldest first.
        return links.GroupBy(l => (l.VotingSessionId, l.Title, l.Status, l.IsVoter))
            .Select(g => new MeetingDocumentVoteGroup(g.Key.VotingSessionId, g.Key.Title, g.Key.Status.ToString(),
                g.Key.IsVoter && g.Key.Status == VotingStatus.Open, g.Select(l => l.Row).ToList())).ToList();
    }

    public Task<AttachedDocumentFile?> FindAttachedFileAsync(Guid meetingId, Guid meetingDocumentId, bool includeDrafts,
        CancellationToken ct)
        => db.MeetingDocuments.AsNoTracking()
            .Where(d => d.Id == meetingDocumentId && d.MeetingId == meetingId && d.RemovedAt == null)
            .VisibleTo(includeDrafts)
            .Select(d => new AttachedDocumentFile(d.FileName, d.StoredFile.StorageKey, d.StoredFile.ContentType,
                d.StoredFile.ByteSize))
            .SingleOrDefaultAsync(ct);

    public Task<MeetingDocument?> FindForRemoveAsync(Guid meetingId, Guid meetingDocumentId, CancellationToken ct)
        => db.MeetingDocuments.Include(d => d.AgendaItemLinks)
            .SingleOrDefaultAsync(d => d.Id == meetingDocumentId && d.MeetingId == meetingId && d.RemovedAt == null, ct);

    public void RemoveAgendaItemLinks(IEnumerable<MeetingDocumentAgendaItem> links)
        => db.MeetingDocumentAgendaItems.RemoveRange(links);

    public Task<bool> ExistsAsync(Guid meetingId, Guid meetingDocumentId, CancellationToken ct)
        => db.MeetingDocuments.AnyAsync(d => d.Id == meetingDocumentId && d.MeetingId == meetingId
            && d.RemovedAt == null, ct);

    public Task<bool> AgendaItemExistsAsync(Guid meetingId, Guid agendaItemId, CancellationToken ct)
        => db.AgendaItems.AnyAsync(i => i.Id == agendaItemId && i.MeetingId == meetingId, ct);

    public async Task<IReadOnlyList<MeetingDocumentLinkState>> FindLinkStatesAsync(Guid meetingId, Guid agendaItemId,
        Guid[] documentIds, CancellationToken ct)
        => await db.MeetingDocuments
            .Where(d => documentIds.Contains(d.Id) && d.MeetingId == meetingId && d.RemovedAt == null)
            .Select(d => new MeetingDocumentLinkState(d.Id,
                d.AgendaItemLinks.Any(l => l.AgendaItemId == agendaItemId)))
            .ToListAsync(ct);

    public void AddAgendaItemLinks(IEnumerable<MeetingDocumentAgendaItem> links)
        => db.MeetingDocumentAgendaItems.AddRange(links);

    public Task<MeetingDocumentAgendaItem?> FindAgendaItemLinkAsync(Guid meetingDocumentId, Guid agendaItemId,
        CancellationToken ct)
        => db.MeetingDocumentAgendaItems.SingleOrDefaultAsync(l => l.MeetingDocumentId == meetingDocumentId
            && l.AgendaItemId == agendaItemId, ct);

    public void RemoveAgendaItemLink(MeetingDocumentAgendaItem link) => db.MeetingDocumentAgendaItems.Remove(link);

    public async Task<IReadOnlyList<Document>> FindLibraryDocumentsAsync(Guid ownerId, Guid[] documentIds,
        CancellationToken ct)
        => await db.Documents.Include(d => d.StoredFile)
            .Where(d => documentIds.Contains(d.Id) && d.OwnerId == ownerId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MeetingDocumentFileState>> FindMeetingFilesAsync(Guid meetingId,
        Guid[] storedFileIds, Guid? agendaItemId, Guid? voteId, CancellationToken ct)
        => await db.MeetingDocuments
            .Where(d => d.MeetingId == meetingId && d.RemovedAt == null && storedFileIds.Contains(d.StoredFileId))
            .Select(d => new MeetingDocumentFileState(d.Id, d.StoredFileId,
                d.AgendaItemLinks.Any(l => l.AgendaItemId == agendaItemId)
                    || d.VoteLinks.Any(l => l.VotingSessionId == voteId)))
            .ToListAsync(ct);

    public void AddVoteDocuments(IEnumerable<VoteDocument> links) => db.VoteDocuments.AddRange(links);
}
