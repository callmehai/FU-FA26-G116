using TrustCabinet.Application.Documents;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>The meeting documents in lists, as the use case reads and stages them. Nothing here saves.</summary>
public sealed class FakeMeetingDocumentRepository : IMeetingDocumentRepository
{
    /// <summary>Thrown by every call in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    public MeetingDocumentList? List { get; set; }

    public List<MeetingDocumentVoteGroup> VoteGroups { get; } = [];

    public AttachedDocumentFile? AttachedFile { get; set; }

    public List<MeetingDocument> MeetingDocuments { get; } = [];

    public List<MeetingDocumentAgendaItem> AgendaLinks { get; } = [];

    public List<VoteDocument> VoteLinks { get; } = [];

    public List<Document> LibraryDocuments { get; } = [];

    public List<Folder> Folders { get; } = [];

    public HashSet<(Guid MeetingId, Guid ItemId)> AgendaItems { get; } = [];

    public List<string> Calls { get; } = [];

    private T Read<T>(string name, Func<T> answer)
    {
        Calls.Add(name);
        return Fail is { } failure ? throw failure : answer();
    }

    public Task<Folder?> FindUploadFolderAsync(Guid ownerId, Guid meetingId, CancellationToken ct)
        => Task.FromResult(Read("upload-folder",
            () => Folders.SingleOrDefault(f => f.OwnerId == ownerId && f.MeetingId == meetingId)));

    public void AddFolder(Folder folder)
    {
        if (Fail is { } failure) throw failure;
        Calls.Add("add-folder");
        Folders.Add(folder);
    }

    public void AddStoredFiles(IEnumerable<StoredFile> files)
    {
    }

    public void AddUpload(Document document, MeetingDocument meetingDocument)
    {
        if (Fail is { } failure) throw failure;
        Calls.Add("add-upload");
        LibraryDocuments.Add(document);
        MeetingDocuments.Add(meetingDocument);
    }

    public void AddMeetingDocument(MeetingDocument meetingDocument)
    {
        if (Fail is { } failure) throw failure;
        Calls.Add("add-meeting-document");
        MeetingDocuments.Add(meetingDocument);
    }

    public Task<MeetingDocumentList> ListAsync(Guid meetingId, CancellationToken ct)
        => Task.FromResult(Read("list",
            () => List ?? new MeetingDocumentList(meetingId, null, [], [], [])));

    public Task<IReadOnlyList<MeetingDocumentVoteGroup>> ListVoteGroupsAsync(Guid meetingId, bool includeDrafts,
        Guid? userId, Guid? guestInvitationId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MeetingDocumentVoteGroup>>(Read("vote-groups", () => VoteGroups.ToList()));

    public Task<AttachedDocumentFile?> FindAttachedFileAsync(Guid meetingId, Guid meetingDocumentId,
        bool includeDrafts, CancellationToken ct)
        => Task.FromResult(Read("attached-file", () => AttachedFile));

    public Task<MeetingDocument?> FindForRemoveAsync(Guid meetingId, Guid meetingDocumentId, CancellationToken ct)
        => Task.FromResult(Read("find-for-remove", () => MeetingDocuments
            .SingleOrDefault(d => d.Id == meetingDocumentId && d.MeetingId == meetingId && d.RemovedAt == null)));

    public void RemoveAgendaItemLinks(IEnumerable<MeetingDocumentAgendaItem> links)
    {
        if (Fail is { } failure) throw failure;
        Calls.Add("remove-agenda-links");
        foreach (var link in links.ToList()) AgendaLinks.Remove(link);
    }

    public Task<bool> ExistsAsync(Guid meetingId, Guid meetingDocumentId, CancellationToken ct)
        => Task.FromResult(Read("exists", () => MeetingDocuments
            .Any(d => d.Id == meetingDocumentId && d.MeetingId == meetingId && d.RemovedAt == null)));

    public Task<bool> AgendaItemExistsAsync(Guid meetingId, Guid agendaItemId, CancellationToken ct)
        => Task.FromResult(Read("item-exists", () => AgendaItems.Contains((meetingId, agendaItemId))));

    public Task<IReadOnlyList<MeetingDocumentLinkState>> FindLinkStatesAsync(Guid meetingId, Guid agendaItemId,
        Guid[] documentIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MeetingDocumentLinkState>>(Read("link-states", () => MeetingDocuments
            .Where(d => documentIds.Contains(d.Id) && d.MeetingId == meetingId && d.RemovedAt == null)
            .Select(d => new MeetingDocumentLinkState(d.Id,
                AgendaLinks.Any(l => l.MeetingDocumentId == d.Id && l.AgendaItemId == agendaItemId)))
            .ToList()));

    public void AddAgendaItemLinks(IEnumerable<MeetingDocumentAgendaItem> links)
    {
        if (Fail is { } failure) throw failure;
        Calls.Add("add-agenda-links");
        AgendaLinks.AddRange(links);
    }

    public Task<MeetingDocumentAgendaItem?> FindAgendaItemLinkAsync(Guid meetingDocumentId, Guid agendaItemId,
        CancellationToken ct)
        => Task.FromResult(Read("find-agenda-link", () => AgendaLinks
            .SingleOrDefault(l => l.MeetingDocumentId == meetingDocumentId && l.AgendaItemId == agendaItemId)));

    public void RemoveAgendaItemLink(MeetingDocumentAgendaItem link)
    {
        if (Fail is { } failure) throw failure;
        Calls.Add("remove-agenda-link");
        AgendaLinks.Remove(link);
    }

    public Task<IReadOnlyList<Document>> FindLibraryDocumentsAsync(Guid ownerId, Guid[] documentIds,
        CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Document>>(Read("library-documents", () => LibraryDocuments
            .Where(d => documentIds.Contains(d.Id) && d.OwnerId == ownerId).ToList()));

    public Task<IReadOnlyList<MeetingDocumentFileState>> FindMeetingFilesAsync(Guid meetingId, Guid[] storedFileIds,
        Guid? agendaItemId, Guid? voteId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MeetingDocumentFileState>>(Read("meeting-files", () => MeetingDocuments
            .Where(d => d.MeetingId == meetingId && d.RemovedAt == null && storedFileIds.Contains(d.StoredFileId))
            .Select(d => new MeetingDocumentFileState(d.Id, d.StoredFileId,
                AgendaLinks.Any(l => l.MeetingDocumentId == d.Id && l.AgendaItemId == agendaItemId)
                    || VoteLinks.Any(l => l.MeetingDocumentId == d.Id && l.VotingSessionId == voteId)))
            .ToList()));

    public void AddVoteDocuments(IEnumerable<VoteDocument> links)
    {
        if (Fail is { } failure) throw failure;
        Calls.Add("add-vote-links");
        VoteLinks.AddRange(links);
    }
}

/// <summary>The personal library in lists, as the use case reads and stages it. Nothing here saves.</summary>
public sealed class FakePersonalLibraryRepository : IPersonalLibraryRepository
{
    /// <summary>Thrown by every call in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    public Dictionary<Guid, Folder> Folders { get; } = [];

    public List<Document> Documents { get; } = [];

    public HashSet<Guid> DeactivatedOwners { get; } = [];

    public HashSet<(Guid MeetingId, Guid ReaderId)> MeetingManagers { get; } = [];

    public List<LibraryDocument> DocumentRows { get; } = [];

    public LibraryDocument? DocumentRow { get; set; }

    public List<Guid> Locks { get; } = [];

    public List<string> Calls { get; } = [];

    public int DeletedFolders { get; private set; }

    public int DeletedDocuments { get; private set; }

    private T Read<T>(string name, Func<T> answer)
    {
        Calls.Add(name);
        return Fail is { } failure ? throw failure : answer();
    }

    public Task LockLibraryAsync(Guid ownerId, CancellationToken ct)
    {
        if (Fail is { } failure) throw failure;
        Locks.Add(ownerId);
        return Task.CompletedTask;
    }

    public Task<bool> IsDeactivatedAsync(Guid ownerId, CancellationToken ct)
        => Task.FromResult(Read("deactivated", () => DeactivatedOwners.Contains(ownerId)));

    public Task<bool> FolderExistsAsync(Guid ownerId, Guid folderId, CancellationToken ct)
        => Task.FromResult(Read("folder-exists",
            () => Folders.TryGetValue(folderId, out var folder) && folder.OwnerId == ownerId));

    public Task<bool> IsMeetingManagerAsync(Guid meetingId, Guid readerId, CancellationToken ct)
        => Task.FromResult(Read("meeting-manager", () => MeetingManagers.Contains((meetingId, readerId))));

    public Task<Dictionary<Guid, Guid?>> ListFolderParentsAsync(Guid ownerId, CancellationToken ct)
        => Task.FromResult(Read("folder-parents",
            () => Folders.Values.Where(f => f.OwnerId == ownerId).ToDictionary(f => f.Id, f => f.ParentId)));

    public Task<Folder> FindFolderAsync(Guid folderId, CancellationToken ct)
        => Task.FromResult(Read("find-folder", () => Folders[folderId]));

    public void AddFolder(Folder folder)
    {
        if (Fail is { } failure) throw failure;
        Calls.Add("add-folder");
        Folders[folder.Id] = folder;
    }

    public Task<int> DeleteFolderAsync(Guid folderId, CancellationToken ct)
        => Task.FromResult(Read("delete-folder", () =>
        {
            DeletedFolders++;
            Folders.Remove(folderId);
            return 1;
        }));

    public Task<bool> HasDocumentsAsync(Guid folderId, CancellationToken ct)
        => Task.FromResult(Read("has-documents", () => Documents.Any(d => d.FolderId == folderId)));

    public Task<int> DeleteDocumentAsync(Guid ownerId, Guid documentId, CancellationToken ct)
        => Task.FromResult(Read("delete-document", () =>
        {
            var document = Documents.SingleOrDefault(d => d.Id == documentId && d.OwnerId == ownerId);
            if (document is null) return 0;
            DeletedDocuments++;
            Documents.Remove(document);
            return 1;
        }));

    public Task<Document?> FindDocumentAsync(Guid ownerId, Guid documentId, CancellationToken ct)
        => Task.FromResult(Read("find-document",
            () => Documents.SingleOrDefault(d => d.Id == documentId && d.OwnerId == ownerId)));

    public void AddDocuments(IEnumerable<Document> documents)
    {
        if (Fail is { } failure) throw failure;
        Calls.Add("add-documents");
        Documents.AddRange(documents);
    }

    public Task<IReadOnlyList<LibraryFolderFlag>> ListFolderFlagsAsync(Guid ownerId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<LibraryFolderFlag>>(Read("folder-flags",
            () => Folders.Values.Where(f => f.OwnerId == ownerId)
                .Select(f => new LibraryFolderFlag(f.Id, f.ParentId, f.MeetingId is not null)).ToList()));

    public Task<IReadOnlyList<Folder>> ListFoldersAsync(Guid ownerId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Folder>>(Read("folders",
            () => Folders.Values.Where(f => f.OwnerId == ownerId).OrderBy(f => f.Name).ThenBy(f => f.Id).ToList()));

    /// <summary>Whether hiding Secret files was asked, in call order.</summary>
    public List<bool> DocumentHideFlags { get; } = [];

    public Task<IReadOnlyList<LibraryDocument>> ListDocumentsAsync(Guid readerId, Guid ownerId, Guid? folderId,
        string? search, string? contentType, Guid? meetingId, bool hideSecretFiles, CancellationToken ct)
    {
        DocumentHideFlags.Add(hideSecretFiles);
        return Task.FromResult<IReadOnlyList<LibraryDocument>>(Read("documents", () => DocumentRows.ToList()));
    }

    public Task<AttachedDocumentFile?> FindLibraryFileAsync(Guid ownerId, Guid documentId,
        HashSet<Guid>? hiddenFolderIds, CancellationToken ct)
        => Task.FromResult(Read("library-file", () => Documents
            .Where(d => d.Id == documentId && d.OwnerId == ownerId)
            .Select(d => new AttachedDocumentFile(d.FileName, $"key-{d.StoredFileId}", "application/pdf", 7))
            .SingleOrDefault()));

    public Task<LibraryDocument> GetDocumentRowAsync(Guid documentId, Guid readerId, CancellationToken ct)
        => Task.FromResult(Read("document-row",
            () => DocumentRow ?? throw new InvalidOperationException("No document row staged.")));
}

/// <summary>The stored files in a list, as the writer and the purge read and stamp them.</summary>
public sealed class FakeStoredFileRepository : IStoredFileRepository
{
    /// <summary>Thrown by every call in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    public long UsedBytes { get; set; }

    public List<StoredFile> Added { get; } = [];

    public List<Guid> StorageLocks { get; } = [];

    public List<Guid> Stamped { get; } = [];

    public List<(Guid Id, DateTimeOffset At)> StampedFresh { get; } = [];

    public List<StoredFileRef> Orphans { get; } = [];

    public List<string> Calls { get; } = [];

    private T Read<T>(string name, Func<T> answer)
    {
        Calls.Add(name);
        return Fail is { } failure ? throw failure : answer();
    }

    public Task<long> UsedBytesAsync(CancellationToken ct)
        => Task.FromResult(Read("used-bytes", () => UsedBytes));

    public void AddStoredFiles(IEnumerable<StoredFile> files)
    {
        if (Fail is { } failure) throw failure;
        Calls.Add("add-stored-files");
        Added.AddRange(files);
    }

    public Task LockStorageAsync(CancellationToken ct)
    {
        if (Fail is { } failure) throw failure;
        Calls.Add("lock-storage");
        StorageLocks.Add(Guid.Empty);
        return Task.CompletedTask;
    }

    public Task<int> StampPurgedAsync(Guid[] ids, DateTimeOffset now, CancellationToken ct)
        => Task.FromResult(Read("stamp-purged", () =>
        {
            Stamped.AddRange(ids);
            return ids.Length;
        }));

    public Task<int> TryStampPurgedAsync(Guid id, DateTimeOffset now, CancellationToken ct)
        => Task.FromResult(Read("stamp-fresh", () =>
        {
            StampedFresh.Add((id, now));
            return 1;
        }));

    public Task<IReadOnlyList<StoredFileRef>> ListOrphansAsync(DateTimeOffset cutoff, IReadOnlyList<Guid> exclude,
        int take, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<StoredFileRef>>(Read("orphans",
            () => Orphans.Where(o => !exclude.Contains(o.Id)).Take(take).ToList()));
}

/// <summary>The document comments in a list, as the use case reads and stages them. Nothing here saves.</summary>
public sealed class FakeDocumentCommentRepository : IDocumentCommentRepository
{
    /// <summary>Thrown by every call in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    public bool Attached { get; set; } = true;

    public List<DocumentCommentFlatRow> Rows { get; } = [];

    public (Guid Id, Guid? ParentCommentId)? Parent { get; set; }

    public DocumentPageInfo? PageInfo { get; set; }

    public string GuestName { get; set; } = "Khách";

    public List<DocumentComment> Added { get; } = [];

    public List<Guid> MeetingLocks { get; } = [];

    private T Read<T>(string name, Func<T> answer)
    {
        Calls.Add(name);
        return Fail is { } failure ? throw failure : answer();
    }

    public List<string> Calls { get; } = [];

    public Task LockMeetingSharedAsync(Guid meetingId, CancellationToken ct)
    {
        if (Fail is { } failure) throw failure;
        MeetingLocks.Add(meetingId);
        return Task.CompletedTask;
    }

    public Task<bool> IsAttachedAsync(Guid meetingId, Guid meetingDocumentId, bool includeDrafts,
        CancellationToken ct)
        => Task.FromResult(Read("attached", () => Attached));

    public Task<IReadOnlyList<DocumentCommentFlatRow>> ListAsync(Guid meetingDocumentId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<DocumentCommentFlatRow>>(Read("list", () => Rows.ToList()));

    public Task<(Guid Id, Guid? ParentCommentId)?> FindParentAsync(Guid parentId, Guid meetingDocumentId,
        CancellationToken ct)
        => Task.FromResult(Read<(Guid Id, Guid? ParentCommentId)?>("parent", () => Parent));

    public Task<DocumentPageInfo?> FindPageInfoAsync(Guid meetingId, Guid meetingDocumentId, bool includeDrafts,
        CancellationToken ct)
        => Task.FromResult(Read("page-info", () => PageInfo));

    public Task<string> FindGuestDisplayNameAsync(Guid invitationId, CancellationToken ct)
        => Task.FromResult(Read("guest-name", () => GuestName));

    public void Add(DocumentComment comment)
    {
        if (Fail is { } failure) throw failure;
        Calls.Add("add");
        Added.Add(comment);
    }
}

/// <summary>The meeting document attacher with fixed answers, recording what the use case asked to attach.</summary>
public sealed class FakeMeetingDocumentAttacher : IMeetingDocumentAttacher
{
    public (DocumentUploadFailure Failure, IReadOnlyList<UploadedMeetingDocument> Added) LibraryResult { get; set; } =
        (DocumentUploadFailure.None, []);

    public List<UploadedMeetingDocument> UploadResult { get; set; } = [];

    /// <summary>Thrown by every call in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    public List<string> Calls { get; } = [];

    public Task<(DocumentUploadFailure Failure, IReadOnlyList<UploadedMeetingDocument> Added)>
        AddLibraryDocumentsAsync(Meeting meeting, Guid actorId, IReadOnlyList<Guid> documentIds, DateTimeOffset now,
            Guid? agendaItemId, Guid? voteId, CancellationToken ct)
        => Fail is { } failure ? throw failure
            : Task.FromResult(Record("add-library", LibraryResult));

    public Task<IReadOnlyList<UploadedMeetingDocument>> AddUploadsAsync(Meeting meeting, Guid actorId,
        StoredFile[] stored, DateTimeOffset now, Guid? agendaItemId, Guid? voteId, CancellationToken ct)
        => Fail is { } failure ? throw failure
            : Task.FromResult<IReadOnlyList<UploadedMeetingDocument>>(Record("add-uploads", UploadResult));

    private T Record<T>(string name, T answer)
    {
        Calls.Add(name);
        return answer;
    }
}

/// <summary>The object store in memory, with hooks to fail writes, reads and deletes.</summary>
public sealed class FakeFileStorage : IFileStorage
{
    public Dictionary<string, byte[]> Objects { get; } = [];

    public bool Configured { get; set; } = true;

    /// <summary>Thrown by the next write, in place of storing.</summary>
    public Exception? FailWrite { get; set; }

    /// <summary>Thrown by the next read, in place of opening.</summary>
    public Exception? FailRead { get; set; }

    public bool IsConfigured => Configured;

    public Task WriteAsync(string key, Stream content, long length, string contentType, CancellationToken ct)
    {
        if (FailWrite is { } failure) throw failure;
        using var copy = new MemoryStream();
        content.CopyTo(copy);
        Objects[key] = copy.ToArray();
        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        if (FailRead is { } failure) throw failure;
        return Objects.TryGetValue(key, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes))
            : throw new FileNotFoundException(key);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        Objects.Remove(key);
        return Task.CompletedTask;
    }
}
