using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Participants;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.GuestAccess;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Documents;

public sealed class MeetingDocumentService(IMeetingRepository meetings, IMeetingDocumentRepository documents,
    IMeetingDocumentAttacher attacher, StoredFileWriter writer, IFileStorage storage, IAuditService audit,
    IAgendaGuestAccess guests, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<MeetingDocumentService> logger) : IMeetingDocumentService
{
    public async Task<MeetingDocumentListOutcome> ListAsync(Guid meetingId, ParticipantActor reader, CancellationToken ct = default)
    {
        try
        {
            return await unitOfWork.ExecuteAsync(async token =>
            {
                // Same answer for a meeting that does not exist and one the caller cannot see (SCRUM-22).
                if (await FindReadableAsync(meetingId, reader, token) is not { } meeting) return new MeetingDocumentListOutcome(DocumentReadFailure.NotFound);
                var list = await documents.ListAsync(meetingId, token);
                // A draft vote and its documents are the Chair's and the Secretary's alone (BR-086, BR-087).
                var includeDrafts = SeesDrafts(meeting, reader);
                var votes = await documents.ListVoteGroupsAsync(meetingId, includeDrafts, reader.UserId,
                    reader.Guest?.InvitationId, token);
                // UC-42 takes a confirmation only while the meeting is InProgress.
                if (meeting.Status != MeetingStatus.InProgress) votes = [.. votes.Select(v => v with { CanConfirmRead = false })];
                return new MeetingDocumentListOutcome(List: list with { Votes = votes });
            }, TransactionMode.None, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to list documents of meeting {MeetingId}", meetingId);
            return new(DocumentReadFailure.ReadFailed);
        }
    }

    public async Task<MeetingDocumentContentOutcome> OpenContentAsync(Guid meetingId, Guid meetingDocumentId,
        ParticipantActor reader, bool download, CancellationToken ct = default)
    {
        Meeting? meeting;
        AttachedDocumentFile? file;
        try
        {
            (meeting, file) = await unitOfWork.ExecuteAsync(async token =>
            {
                var found = await FindReadableAsync(meetingId, reader, token);
                if (found is null) return ((Meeting?)null, (AttachedDocumentFile?)null);
                return (found, await documents.FindAttachedFileAsync(meetingId, meetingDocumentId,
                    SeesDrafts(found, reader), token));
            }, TransactionMode.None, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to look up document {DocumentId} of meeting {MeetingId}", meetingDocumentId, meetingId);
            return new(DocumentReadFailure.ReadFailed);
        }
        // Removed, never in this meeting, or only on a draft vote the reader may not see: the same answer, so an ID
        // reveals nothing (BR-074–BR-076, BR-086, BR-087).
        if (meeting is null) return new(DocumentReadFailure.NotFound);
        if (file is null) return new(DocumentReadFailure.NotFound);
        if (download && meeting.Sensitivity == Sensitivity.Secret) return new(DocumentReadFailure.DownloadForbidden);

        Stream content;
        try
        {
            content = await storage.OpenReadAsync(file.StorageKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // An unconfigured store or the store being down read as "try again"; an object gone for good does not.
            logger.LogError(ex, "Failed to open stored object {StorageKey} for document {DocumentId}", file.StorageKey,
                meetingDocumentId);
            return new(ex is FileNotFoundException ? DocumentReadFailure.FileMissing : DocumentReadFailure.ReadFailed);
        }

        if (meeting.Sensitivity == Sensitivity.Secret)
        {
            // Recorded only once the file is actually there to hand over, and before it is: no
            // audit row, no content (BR-011).
            try
            {
                // A guest has no user row, so its invitation names who read it.
                object details = reader.Guest is { } guest
                    ? new { meetingId, fileName = file.FileName, sensitivity = meeting.Sensitivity.ToString(),
                        guestInvitationId = guest.InvitationId }
                    : new { meetingId, fileName = file.FileName, sensitivity = meeting.Sensitivity.ToString() };
                // Only a view reaches here: downloading a Secret meeting's document was refused above (BR-081).
                await audit.RecordAsync(AuditActions.DocumentViewed,
                    AuditActions.MeetingDocumentObjectType, meetingDocumentId, reader.UserId, details, ct);
            }
            catch (Exception ex)
            {
                await content.DisposeAsync();
                if (ex is not PersistenceException persistence) throw;
                logger.LogError(persistence.InnerException ?? ex, "Failed to audit access to document {DocumentId}", meetingDocumentId);
                return new(DocumentReadFailure.ReadFailed);
            }
        }

        return new(Content: new(content, file.FileName, file.ContentType, file.ByteSize));
    }

    /// <summary>Whether <paramref name="reader"/> sees draft votes and their documents: the Chair and the Secretary (BR-086, BR-087).</summary>
    internal static bool SeesDrafts(Meeting meeting, ParticipantActor reader)
        => reader.UserId is { } userId && ParticipantRules.IsManager(meeting, userId);

    /// <summary>
    /// The meeting when <paramref name="reader"/> may read it now: an internal user by <see cref="MeetingReadPolicy"/>,
    /// a guest only through a current UC-04 session of this very meeting, rechecked on every call (SCRUM-120 rules:
    /// not logged out or expired, invitation standing, meeting Published or InProgress). Persistence errors propagate.
    /// </summary>
    private async Task<Meeting?> FindReadableAsync(Guid meetingId, ParticipantActor reader, CancellationToken ct)
    {
        if (reader.Guest is { } guest)
        {
            if (!guest.IsValid || guest.MeetingId != meetingId || !await guests.CanReadAsync(guest, ct)) return null;
            // Rechecked on the meeting read here, as Agenda does: it may have ended since the session check.
            var guestMeeting = await meetings.FindWorkflowAsync(meetingId, ct: ct);
            return guestMeeting is not null && GuestAccessRules.MeetingOpen(guestMeeting.Status) ? guestMeeting : null;
        }
        var meeting = await meetings.FindWorkflowAsync(meetingId, ct: ct);
        return meeting is not null && reader.UserId is { } userId && MeetingReadPolicy.CanRead(meeting, userId) ? meeting : null;
    }

    public async Task<DocumentUploadOutcome> CheckUploadAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var access = await unitOfWork.ExecuteAsync(async token =>
            {
                var meeting = await meetings.FindWorkflowAsync(meetingId, ct: token);
                return DocumentFileRules.UploadAccess(meeting, actorId, await meetings.IsUserActiveAsync(actorId, token));
            }, TransactionMode.None, ct);
            if (access == DocumentUploadFailure.None && !writer.IsConfigured)
            {
                logger.LogError("Document upload refused: FileStorage is not configured.");
                return new(DocumentUploadFailure.StoreFailed);
            }
            return new(access);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to check document upload access for meeting {MeetingId}", meetingId);
            return new(DocumentUploadFailure.StoreFailed);
        }
    }

    public async Task<DocumentUploadOutcome> UploadAsync(Guid meetingId, Guid actorId,
        IReadOnlyList<DocumentUploadFile> files, CancellationToken ct = default)
        => await writer.WriteAsync(actorId, files, async (stored, now, token) =>
        {
            // Re-checked under the meeting lock: the meeting may have ended, or the actor lost
            // the role, while the files were uploading (EX-1, EX-6).
            var (meeting, access) = await LockAsync(meetingId, actorId, token);
            if (access != DocumentUploadFailure.None) return new DocumentUploadOutcome(access);
            var added = await attacher.AddUploadsAsync(meeting!, actorId, stored, now, null, null, token);
            await unitOfWork.SaveChangesAsync(token);
            return new DocumentUploadOutcome(Documents: added);
        }, outcome => outcome.Failure == DocumentUploadFailure.None, new(DocumentUploadFailure.StoreFailed),
            new(DocumentUploadFailure.StorageFull), ct);

    public Task<DocumentUploadOutcome> AddFromLibraryAsync(Guid meetingId, Guid actorId, IReadOnlyList<Guid> documentIds,
        CancellationToken ct = default)
        => InMeetingLockAsync(meetingId, actorId, "add library documents to",
            (meeting, now, token) => attacher.AddLibraryDocumentsAsync(meeting, actorId, documentIds, now, null, null, token), ct);

    public Task<DocumentUploadOutcome> RemoveAsync(Guid meetingId, Guid meetingDocumentId, Guid actorId,
        CancellationToken ct = default)
        => InMeetingLockAsync(meetingId, actorId, "remove a document from", async (meeting, now, token) =>
        {
            var document = await documents.FindForRemoveAsync(meeting.Id, meetingDocumentId, token);
            // Removed, or never in this meeting: the same answer (BR-074–BR-076).
            if (document is null) return (DocumentUploadFailure.NotInMeeting, []);
            // The row and its comments stay as history, out of every list (BR-011, BR-074–BR-076).
            document.RemovedAt = now;
            document.RemovedById = actorId;
            document.UpdatedAt = now;
            documents.RemoveAgendaItemLinks(document.AgendaItemLinks);
            return (DocumentUploadFailure.None, (IReadOnlyList<UploadedMeetingDocument>)[]);
        }, ct);

    public Task<DocumentUploadOutcome> AttachAsync(Guid meetingId, Guid agendaItemId, Guid actorId,
        DocumentAttachSource source, IReadOnlyList<Guid> documentIds, CancellationToken ct = default)
        => InMeetingLockAsync(meetingId, actorId, "link documents in", async (meeting, now, token) =>
        {
            if (!await documents.AgendaItemExistsAsync(meeting.Id, agendaItemId, token))
                return (DocumentUploadFailure.AgendaItemNotFound, (IReadOnlyList<UploadedMeetingDocument>)[]);
            if (source == DocumentAttachSource.Library)
                return await attacher.AddLibraryDocumentsAsync(meeting, actorId, documentIds, now, agendaItemId, null, token);

            var ids = documentIds.Distinct().ToArray();
            var found = await documents.FindLinkStatesAsync(meeting.Id, agendaItemId, ids, token);
            if (ids.Length == 0 || found.Count != ids.Length) return (DocumentUploadFailure.NotInMeeting, (IReadOnlyList<UploadedMeetingDocument>)[]);
            if (found.Any(d => d.Linked)) return (DocumentUploadFailure.AlreadyLinked, (IReadOnlyList<UploadedMeetingDocument>)[]);
            // Links to other items stay as they are (UC-36b).
            documents.AddAgendaItemLinks(ids.Select(id => new MeetingDocumentAgendaItem
                { MeetingDocumentId = id, AgendaItemId = agendaItemId }));
            return (DocumentUploadFailure.None, (IReadOnlyList<UploadedMeetingDocument>)[]);
        }, ct);

    public async Task<DocumentUploadOutcome> CheckAttachUploadAsync(Guid meetingId, Guid agendaItemId, Guid actorId,
        CancellationToken ct = default)
    {
        var access = await CheckUploadAsync(meetingId, actorId, ct);
        if (access.Failure != DocumentUploadFailure.None) return access;
        try
        {
            return await unitOfWork.ExecuteAsync(async token =>
                await documents.AgendaItemExistsAsync(meetingId, agendaItemId, token) ? access : new(DocumentUploadFailure.AgendaItemNotFound),
                TransactionMode.None, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to check agenda item {AgendaItemId} of meeting {MeetingId}", agendaItemId, meetingId);
            return new(DocumentUploadFailure.StoreFailed);
        }
    }

    public async Task<DocumentUploadOutcome> AttachUploadAsync(Guid meetingId, Guid agendaItemId, Guid actorId,
        IReadOnlyList<DocumentUploadFile> files, CancellationToken ct = default)
        => await writer.WriteAsync(actorId, files, async (stored, now, token) =>
        {
            var (meeting, access) = await LockAsync(meetingId, actorId, token);
            if (access != DocumentUploadFailure.None) return new DocumentUploadOutcome(access);
            if (!await documents.AgendaItemExistsAsync(meetingId, agendaItemId, token))
                return new DocumentUploadOutcome(DocumentUploadFailure.AgendaItemNotFound);
            var added = await attacher.AddUploadsAsync(meeting!, actorId, stored, now, agendaItemId, null, token);
            await unitOfWork.SaveChangesAsync(token);
            return new DocumentUploadOutcome(Documents: added);
        }, outcome => outcome.Failure == DocumentUploadFailure.None, new(DocumentUploadFailure.StoreFailed),
            new(DocumentUploadFailure.StorageFull), ct);

    public Task<DocumentUploadOutcome> DetachAsync(Guid meetingId, Guid agendaItemId, Guid meetingDocumentId,
        Guid actorId, CancellationToken ct = default)
        => InMeetingLockAsync(meetingId, actorId, "unlink a document in", async (meeting, _, token) =>
        {
            if (!await documents.ExistsAsync(meeting.Id, meetingDocumentId, token))
                return (DocumentUploadFailure.NotInMeeting, (IReadOnlyList<UploadedMeetingDocument>)[]);
            if (!await documents.AgendaItemExistsAsync(meeting.Id, agendaItemId, token))
                return (DocumentUploadFailure.AgendaItemNotFound, (IReadOnlyList<UploadedMeetingDocument>)[]);
            var link = await documents.FindAgendaItemLinkAsync(meetingDocumentId, agendaItemId, token);
            if (link is null) return (DocumentUploadFailure.NotLinked, (IReadOnlyList<UploadedMeetingDocument>)[]);
            // Only this link: with none left the document shows as a general one (UC-36b, UC-36d).
            documents.RemoveAgendaItemLink(link);
            return (DocumentUploadFailure.None, (IReadOnlyList<UploadedMeetingDocument>)[]);
        }, ct);

    /// <summary>
    /// Locks the meeting and checks who may change its documents now: its current Chair or Secretary until it
    /// ends (BR-070, BR-071, UC-36b, UC-36d).
    /// </summary>
    private async Task<(Meeting? Meeting, DocumentUploadFailure Access)> LockAsync(Guid meetingId, Guid actorId,
        CancellationToken ct)
    {
        var meeting = await meetings.FindWorkflowAsync(meetingId, true, ct);
        return (meeting, DocumentFileRules.UploadAccess(meeting, actorId, await meetings.IsUserActiveAsync(actorId, ct)));
    }

    /// <summary>
    /// Runs a change of the meeting's documents in one transaction under the meeting lock, after the access
    /// check, and saves it only when <paramref name="change"/> succeeds. The outcome lists the meeting
    /// documents the change added.
    /// </summary>
    private async Task<DocumentUploadOutcome> InMeetingLockAsync(Guid meetingId, Guid actorId, string what,
        Func<Meeting, DateTimeOffset, CancellationToken, Task<(DocumentUploadFailure Failure, IReadOnlyList<UploadedMeetingDocument> Added)>> change,
        CancellationToken ct)
    {
        try
        {
            return await unitOfWork.ExecuteAsync(async token =>
            {
                var (meeting, access) = await LockAsync(meetingId, actorId, token);
                if (access != DocumentUploadFailure.None) return new DocumentUploadOutcome(access);
                var (failure, added) = await change(meeting!, clock.GetUtcNow(), token);
                if (failure != DocumentUploadFailure.None) return new DocumentUploadOutcome(failure);
                await unitOfWork.SaveChangesAsync(token);
                return new DocumentUploadOutcome(Documents: added);
            }, TransactionMode.ReadCommitted, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to {What} meeting {MeetingId}", what, meetingId);
            return new(DocumentUploadFailure.StoreFailed);
        }
    }
}
