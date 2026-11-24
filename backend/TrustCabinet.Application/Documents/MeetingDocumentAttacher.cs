using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Documents;

/// <summary>
/// The two ways files become meeting documents, shared by meetings (UC-36a, UC-36b AF-1, UC-68a AF-1) and votes
/// (UC-68a AF-2): the library documents (no byte is copied, ADR-0018) and the uploads. Runs inside the caller's
/// transaction: the caller saves and commits.
/// </summary>
public sealed class MeetingDocumentAttacher(IMeetingDocumentRepository documents, IPersonalLibraryRepository library)
    : IMeetingDocumentAttacher
{
    /// <inheritdoc/>
    public async Task<(DocumentUploadFailure Failure, IReadOnlyList<UploadedMeetingDocument> Added)>
        AddLibraryDocumentsAsync(Meeting meeting, Guid actorId, IReadOnlyList<Guid> documentIds, DateTimeOffset now,
            Guid? agendaItemId, Guid? voteId, CancellationToken ct)
    {
        var ids = documentIds.Distinct().ToArray();
        var rows = await documents.FindLibraryDocumentsAsync(actorId, ids, ct);
        if (ids.Length == 0 || rows.Count != ids.Length) return (DocumentUploadFailure.LibraryUnavailable, []);
        var files = rows.Select(d => d.StoredFileId).ToArray();
        var inMeeting = await documents.FindMeetingFilesAsync(meeting.Id, files, agendaItemId, voteId, ct);
        var links = new List<MeetingDocumentAgendaItem>();
        var voteLinks = new List<VoteDocument>();
        if (inMeeting.Count > 0)
        {
            // The general group has nothing more to add for a file already in the meeting (BR-072, BR-073).
            if (agendaItemId is null && voteId is null) return (DocumentUploadFailure.AlreadyInMeeting, []);
            // For an item or a vote, the meeting's own document is linked instead, as if chosen from the meeting.
            if (inMeeting.Any(d => d.Linked)) return (DocumentUploadFailure.AlreadyLinked, []);
            foreach (var existing in inMeeting)
            {
                if (agendaItemId is { } linkItem)
                    links.Add(new() { MeetingDocumentId = existing.Id, AgendaItemId = linkItem });
                if (voteId is { } linkVote)
                    voteLinks.Add(new() { VotingSessionId = linkVote, MeetingDocumentId = existing.Id });
            }
        }

        // Links for the files already in the meeting are staged first, as before the move.
        if (links.Count > 0) documents.AddAgendaItemLinks(links);
        if (voteLinks.Count > 0) documents.AddVoteDocuments(voteLinks);
        var added = new List<UploadedMeetingDocument>();
        foreach (var document in rows.Where(d => inMeeting.All(m => m.StoredFileId != d.StoredFileId)))
        {
            // A new row even for a file removed earlier: its old comments do not come back (BR-074–BR-076).
            var meetingDocument = new MeetingDocument { MeetingId = meeting.Id, StoredFileId = document.StoredFileId,
                StoredFile = document.StoredFile, DocumentId = document.Id, FileName = document.FileName,
                AddedById = actorId, AddedAt = now, CreatedAt = now, UpdatedAt = now };
            if (agendaItemId is { } item) meetingDocument.AgendaItemLinks.Add(new() { AgendaItemId = item });
            if (voteId is { } vote) meetingDocument.VoteLinks.Add(new() { VotingSessionId = vote });
            documents.AddMeetingDocument(meetingDocument);
            added.Add(new(meetingDocument.Id, document.FileName, document.StoredFile.ContentType,
                document.StoredFile.ByteSize, document.StoredFile.PageCount));
        }
        return (DocumentUploadFailure.None, added);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UploadedMeetingDocument>> AddUploadsAsync(Meeting meeting, Guid actorId,
        StoredFile[] stored, DateTimeOffset now, Guid? agendaItemId, Guid? voteId, CancellationToken ct)
    {
        // The upload folder must not be deleted or moved away while it is being filled.
        await library.LockLibraryAsync(actorId, ct);
        var folder = await documents.FindUploadFolderAsync(actorId, meeting.Id, ct);
        if (folder is null)
        {
            // UC-36a first upload, UC-36a recreated at the root once the old one is deleted.
            folder = new Folder { OwnerId = actorId, MeetingId = meeting.Id,
                Name = DocumentFileRules.UploadFolderName(meeting), CreatedAt = now, UpdatedAt = now };
            documents.AddFolder(folder);
        }

        var added = new List<UploadedMeetingDocument>();
        foreach (var file in stored)
        {
            // Two uploads of the same name or bytes are still two documents (BR-068).
            var document = new Document { OwnerId = actorId, FolderId = folder.Id, StoredFileId = file.Id,
                FileName = file.OriginalFileName, CreatedAt = now, UpdatedAt = now };
            var meetingDocument = new MeetingDocument { MeetingId = meeting.Id, StoredFileId = file.Id,
                DocumentId = document.Id, FileName = file.OriginalFileName, AddedById = actorId, AddedAt = now,
                CreatedAt = now, UpdatedAt = now };
            if (agendaItemId is { } item) meetingDocument.AgendaItemLinks.Add(new() { AgendaItemId = item });
            if (voteId is { } vote) meetingDocument.VoteLinks.Add(new() { VotingSessionId = vote });
            documents.AddUpload(document, meetingDocument);
            added.Add(new(meetingDocument.Id, file.OriginalFileName, file.ContentType, file.ByteSize, file.PageCount));
        }
        return added;
    }
}
