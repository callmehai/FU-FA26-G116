using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.GuestAccess;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Documents;

/// <summary>
/// UC-35 (SCRUM-23): comments on a meeting's copy of a document, so the same file in two meetings has two
/// separate discussions. Anyone who can view the meeting may read and write them (BR-249, BR-275) until it
/// ends (BR-282) or is cancelled; a removed document's comments stay in the database but are no longer shown (BR-284).
/// A verified guest reads and writes as a Participant does (BR-181), under their invitation (ADR-0016), while their
/// UC-04 session is current, rechecked on every call: a revoked invitation stops the next comment.
/// </summary>
public sealed class DocumentCommentService(IMeetingRepository meetings, TrustCabinetDbContext db, IAgendaGuestAccess guests,
    TimeProvider clock, ILogger<DocumentCommentService> logger) : IDocumentCommentService
{
    private static bool PersistenceError(Exception ex) => StoredFileWriter.PersistenceError(ex);

    public async Task<CommentOutcome<DocumentCommentThreads>> ListAsync(Guid meetingId, Guid meetingDocumentId,
        ParticipantActor actor, CancellationToken ct = default)
    {
        try
        {
            var meeting = await FindReadableAsync(meetingId, actor, ct);
            if (meeting is null
                || !await IsAttachedAsync(meetingId, meetingDocumentId, MeetingDocumentService.SeesDrafts(meeting, actor), ct))
                return new(CommentFailure.NotFound);

            var rows = await db.DocumentComments.AsNoTracking().Where(c => c.MeetingDocumentId == meetingDocumentId)
                .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
                .Select(c => new
                {
                    c.Id, c.ParentCommentId, c.Body, c.PageNumber, c.CreatedAt, c.AuthorId, c.GuestInvitationId,
                    // The guest as they named themselves (UC-04 step 5), else the name the Secretary invited.
                    Name = c.Author != null ? c.Author.FullName
                        : c.GuestInvitation!.SelfDeclaredDisplayName ?? c.GuestInvitation.InvitedName,
                }).ToListAsync(ct);
            DocumentCommentRow Row(Guid id, string body, int? page, Guid? author, Guid? guest, string name,
                DateTimeOffset at, IReadOnlyList<DocumentCommentRow> replies)
                => new(id, body, page, new(author, guest, name, guest is not null), at, replies);
            var threads = rows.Where(r => r.ParentCommentId is null).Select(top => Row(top.Id, top.Body, top.PageNumber,
                top.AuthorId, top.GuestInvitationId, top.Name, top.CreatedAt,
                rows.Where(r => r.ParentCommentId == top.Id).Select(r => Row(r.Id, r.Body, r.PageNumber, r.AuthorId,
                    r.GuestInvitationId, r.Name, r.CreatedAt, [])).ToList())).ToList();
            return new(Value: new(meetingDocumentId, meeting.Status is not (MeetingStatus.Ended or MeetingStatus.Cancelled), threads));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to load the comments of document {DocumentId}", meetingDocumentId);
            return new(CommentFailure.ReadFailed);
        }
    }

    public async Task<CommentOutcome<DocumentCommentRow>> AddAsync(Guid meetingId, Guid meetingDocumentId,
        ParticipantActor actor, string? body, int? pageNumber, Guid? parentCommentId, CancellationToken ct = default)
    {
        var text = body?.Trim();
        if (string.IsNullOrEmpty(text)) return new(CommentFailure.Blank);
        if (text.Length > DocumentFileRules.MaxCommentLength) return new(CommentFailure.TooLong);

        try
        {
            return await meetings.InTransactionAsync<CommentOutcome<DocumentCommentRow>>(async token =>
            {
                // A share lock on the meeting row, so a comment sent as the meeting ends or is cancelled is refused
                // rather than slipped in after it (BR-282), while comments do not queue behind one another.
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM meetings WHERE id = {meetingId} FOR SHARE", token);
                // Likewise the guest's session row, so a comment sent as the guest signs out lands before it or not at all.
                if (actor.Guest is { } session)
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT 1 FROM guest_sessions WHERE id = {session.SessionId} FOR SHARE", token);
                // A guest whose meeting just ended has lost it altogether (BR-181), so MSG156 rather than MSG251.
                var meeting = await FindReadableAsync(meetingId, actor, token);
                if (meeting is null) return new(CommentFailure.NotFound);
                var document = await db.MeetingDocuments.AsNoTracking()
                    .Where(d => d.Id == meetingDocumentId && d.MeetingId == meetingId && d.RemovedAt == null)
                    .VisibleTo(MeetingDocumentService.SeesDrafts(meeting, actor))
                    .Select(d => new { d.StoredFile.ContentType, d.StoredFile.PageCount }).SingleOrDefaultAsync(token);
                // Removed, of another meeting, or only on a draft vote the actor may not see: the same answer (BR-211, BR-253).
                if (document is null) return new(CommentFailure.NotFound);
                if (meeting.Status == MeetingStatus.Ended) return new(CommentFailure.Ended);
                if (meeting.Status == MeetingStatus.Cancelled) return new(CommentFailure.Cancelled);

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
                var guestId = actor.Guest?.InvitationId;
                var comment = new DocumentComment { MeetingDocumentId = meetingDocumentId, ParentCommentId = threadId,
                    AuthorId = actor.UserId, GuestInvitationId = guestId, Body = text, PageNumber = page, CreatedAt = now,
                    UpdatedAt = now };
                db.DocumentComments.Add(comment);
                await meetings.SaveChangesAsync(token);
                var name = guestId is { } invitation
                    ? await db.MeetingGuestInvitations.Where(g => g.Id == invitation)
                        .Select(g => g.SelfDeclaredDisplayName ?? g.InvitedName).SingleAsync(token)
                    : await meetings.FindUserDisplayNameAsync(actor.UserId!.Value, token) ?? "";
                return new(Value: new(comment.Id, text, page, new(actor.UserId, guestId, name, guestId is not null), now, []));
            }, ct);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to save a comment on document {DocumentId}", meetingDocumentId);
            return new(CommentFailure.SaveFailed);
        }
    }

    /// <summary>
    /// The meeting when <paramref name="actor"/> may read it now, by the documents' own rule (MeetingDocumentService):
    /// an internal user by <see cref="MeetingReadPolicy"/>, a guest only through a current session of this very
    /// meeting while it is Published or InProgress.
    /// </summary>
    private async Task<Meeting?> FindReadableAsync(Guid meetingId, ParticipantActor actor, CancellationToken ct)
    {
        if (actor.Guest is { } guest)
        {
            if (!guest.IsValid || guest.MeetingId != meetingId || !await guests.CanReadAsync(guest, ct)) return null;
            var guestMeeting = await meetings.FindWorkflowAsync(meetingId, ct: ct);
            return guestMeeting is not null && GuestAccessRules.MeetingOpen(guestMeeting.Status) ? guestMeeting : null;
        }
        var meeting = await meetings.FindWorkflowAsync(meetingId, ct: ct);
        return meeting is not null && actor.UserId is { } userId && MeetingReadPolicy.CanRead(meeting, userId) ? meeting : null;
    }

    private Task<bool> IsAttachedAsync(Guid meetingId, Guid meetingDocumentId, bool includeDrafts, CancellationToken ct)
        => db.MeetingDocuments.Where(d => d.Id == meetingDocumentId && d.MeetingId == meetingId && d.RemovedAt == null)
            .VisibleTo(includeDrafts).AnyAsync(ct);
}
