using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Documents;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Documents;

public sealed class DocumentCommentRepository(TrustCabinetDbContext db) : IDocumentCommentRepository
{
    public Task LockMeetingSharedAsync(Guid meetingId, CancellationToken ct)
        => db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM meetings WHERE id = {meetingId} FOR SHARE", ct);

    public Task<bool> IsAttachedAsync(Guid meetingId, Guid meetingDocumentId, bool includeDrafts,
        CancellationToken ct)
        => db.MeetingDocuments.Where(d => d.Id == meetingDocumentId && d.MeetingId == meetingId && d.RemovedAt == null)
            .VisibleTo(includeDrafts).AnyAsync(ct);

    public async Task<IReadOnlyList<DocumentCommentFlatRow>> ListAsync(Guid meetingDocumentId,
        CancellationToken ct)
        => await db.DocumentComments.AsNoTracking().Where(c => c.MeetingDocumentId == meetingDocumentId)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Select(c => new DocumentCommentFlatRow(c.Id, c.ParentCommentId, c.Body, c.PageNumber, c.AuthorId,
                c.GuestInvitationId,
                // The guest as they named themselves (UC-04 step 5), else the name the Secretary invited.
                c.Author != null ? c.Author.FullName
                    : c.GuestInvitation!.SelfDeclaredDisplayName ?? c.GuestInvitation.InvitedName,
                c.CreatedAt)).ToListAsync(ct);

    public async Task<(Guid Id, Guid? ParentCommentId)?> FindParentAsync(Guid parentId, Guid meetingDocumentId,
        CancellationToken ct)
    {
        var parent = await db.DocumentComments.AsNoTracking()
            .Where(c => c.Id == parentId && c.MeetingDocumentId == meetingDocumentId)
            .Select(c => new { c.Id, c.ParentCommentId }).SingleOrDefaultAsync(ct);
        return parent is null ? null : (parent.Id, parent.ParentCommentId);
    }

    public Task<DocumentPageInfo?> FindPageInfoAsync(Guid meetingId, Guid meetingDocumentId, bool includeDrafts,
        CancellationToken ct)
        => db.MeetingDocuments.AsNoTracking()
            .Where(d => d.Id == meetingDocumentId && d.MeetingId == meetingId && d.RemovedAt == null)
            .VisibleTo(includeDrafts)
            .Select(d => new DocumentPageInfo(d.StoredFile.ContentType, d.StoredFile.PageCount))
            .SingleOrDefaultAsync(ct);

    public Task<string> FindGuestDisplayNameAsync(Guid invitationId, CancellationToken ct)
        => db.MeetingGuestInvitations.Where(g => g.Id == invitationId)
            .Select(g => g.SelfDeclaredDisplayName ?? g.InvitedName).SingleAsync(ct);

    public void Add(DocumentComment comment) => db.DocumentComments.Add(comment);
}
