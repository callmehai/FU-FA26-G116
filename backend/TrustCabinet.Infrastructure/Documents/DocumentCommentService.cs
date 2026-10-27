using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.Documents;

/// <summary>
/// UC-35 (SCRUM-23): comments on a meeting's copy of a document, so the same file in two meetings has two
/// separate discussions. Anyone who can view the meeting may read and write them (BR-249, BR-275) until it
/// ends (BR-282); a removed document's comments stay in the database but are no longer shown (BR-284).
/// </summary>
public sealed class DocumentCommentService(IMeetingRepository meetings, TrustCabinetDbContext db, TimeProvider clock,
    ILogger<DocumentCommentService> logger) : IDocumentCommentService
{
    private static bool PersistenceError(Exception ex) => StoredFileWriter.PersistenceError(ex);

    public async Task<CommentOutcome<DocumentCommentThreads>> ListAsync(Guid meetingId, Guid meetingDocumentId,
        Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var meeting = await meetings.FindWorkflowAsync(meetingId, ct: ct);
            if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId) || !await IsAttachedAsync(meetingId, meetingDocumentId, ct))
                return new(CommentFailure.NotFound);

            var rows = await db.DocumentComments.AsNoTracking().Where(c => c.MeetingDocumentId == meetingDocumentId)
                .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
                .Select(c => new
                {
                    c.Id, c.ParentCommentId, c.Body, c.PageNumber, c.CreatedAt, c.AuthorId, c.GuestInvitationId,
                    Name = c.Author != null ? c.Author.FullName : c.GuestInvitation!.InvitedName,
                }).ToListAsync(ct);
            DocumentCommentRow Row(Guid id, string body, int? page, Guid? author, Guid? guest, string name,
                DateTimeOffset at, IReadOnlyList<DocumentCommentRow> replies)
                => new(id, body, page, new(author, guest, name, guest is not null), at, replies);
            var threads = rows.Where(r => r.ParentCommentId is null).Select(top => Row(top.Id, top.Body, top.PageNumber,
                top.AuthorId, top.GuestInvitationId, top.Name, top.CreatedAt,
                rows.Where(r => r.ParentCommentId == top.Id).Select(r => Row(r.Id, r.Body, r.PageNumber, r.AuthorId,
                    r.GuestInvitationId, r.Name, r.CreatedAt, [])).ToList())).ToList();
            return new(Value: new(meetingDocumentId, meeting.Status != MeetingStatus.Ended, threads));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to load the comments of document {DocumentId}", meetingDocumentId);
            return new(CommentFailure.ReadFailed);
        }
    }

    public async Task<CommentOutcome<DocumentCommentRow>> AddAsync(Guid meetingId, Guid meetingDocumentId, Guid actorId,
        string? body, int? pageNumber, Guid? parentCommentId, CancellationToken ct = default)
    {
        var text = body?.Trim();
        if (string.IsNullOrEmpty(text)) return new(CommentFailure.Blank);
        if (text.Length > DocumentFileRules.MaxCommentLength) return new(CommentFailure.TooLong);

        try
        {
            return await meetings.InTransactionAsync<CommentOutcome<DocumentCommentRow>>(async token =>
            {
                // Under the meeting lock, so a comment sent as the meeting ends is refused rather than slipped in
                // after it (BR-282).
                var meeting = await meetings.FindWorkflowAsync(meetingId, true, token);
                if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId)) return new(CommentFailure.NotFound);
                var document = await db.MeetingDocuments.AsNoTracking()
                    .Where(d => d.Id == meetingDocumentId && d.MeetingId == meetingId && d.RemovedAt == null)
                    .Select(d => new { d.StoredFile.ContentType, d.StoredFile.PageCount }).SingleOrDefaultAsync(token);
                // Removed, or of another meeting: the same answer (BR-211).
                if (document is null) return new(CommentFailure.NotFound);
                if (meeting.Status == MeetingStatus.Ended) return new(CommentFailure.Ended);

                Guid? threadId = null;
                if (parentCommentId is { } parentId)
                {
                    // A reply to a reply joins the same thread: one level deep (BR-317).
                    var parent = await db.DocumentComments.AsNoTracking()
                        .Where(c => c.Id == parentId && c.MeetingDocumentId == meetingDocumentId)
                        .Select(c => new { c.Id, c.ParentCommentId }).SingleOrDefaultAsync(token);
                    if (parent is null) return new(CommentFailure.NotFound);
                    threadId = parent.ParentCommentId ?? parent.Id;
                }

                // A page belongs to a top-level comment on a PDF, within the pages it has (BR-248).
                var page = threadId is null && document.ContentType == DocumentFileRules.PdfContentType
                    && pageNumber >= 1 && (document.PageCount is null || pageNumber <= document.PageCount) ? pageNumber : null;
                var now = clock.GetUtcNow();
                var comment = new DocumentComment { MeetingDocumentId = meetingDocumentId, ParentCommentId = threadId,
                    AuthorId = actorId, Body = text, PageNumber = page, CreatedAt = now, UpdatedAt = now };
                db.DocumentComments.Add(comment);
                await meetings.SaveChangesAsync(token);
                var name = await meetings.FindUserDisplayNameAsync(actorId, token) ?? "";
                return new(Value: new(comment.Id, text, page, new(actorId, null, name, false), now, []));
            }, ct);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to save a comment on document {DocumentId}", meetingDocumentId);
            return new(CommentFailure.SaveFailed);
        }
    }

    private Task<bool> IsAttachedAsync(Guid meetingId, Guid meetingDocumentId, CancellationToken ct)
        => db.MeetingDocuments.AnyAsync(d => d.Id == meetingDocumentId && d.MeetingId == meetingId && d.RemovedAt == null, ct);
}
