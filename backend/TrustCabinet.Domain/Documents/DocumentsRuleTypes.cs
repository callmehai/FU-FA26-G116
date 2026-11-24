namespace TrustCabinet.Domain.Documents;

/// <summary>
/// Why a change to a meeting's documents was refused: an upload (UC-36a AF-1), an addition from the
/// library (UC-36a), a removal (UC-36c), or a link to an agenda item (UC-36b, UC-36d) — SCRUM-107, SCRUM-58.
/// Each endpoint maps these to its own message codes.
/// </summary>
public enum DocumentUploadFailure
{
    None, NotFound, Inactive, Forbidden, Ended, Cancelled, Invalid, TooMany, StoreFailed,
    /// <summary>A selected library document is missing or not the actor's (BR-072, BR-073).</summary>
    LibraryUnavailable,
    /// <summary>A selected library document's file is already in the meeting (BR-072, BR-073).</summary>
    AlreadyInMeeting,
    /// <summary>The meeting document was removed or belongs to another meeting (BR-074–BR-076).</summary>
    NotInMeeting,
    /// <summary>The agenda item does not exist in this meeting (BR-078).</summary>
    AgendaItemNotFound,
    /// <summary>A selected document is already linked to the agenda item (UC-36b), or attached to the vote (UC-68a).</summary>
    AlreadyLinked,
    /// <summary>The document is not linked to the agenda item.</summary>
    NotLinked,
    /// <summary>The files do not fit the system's storage limit (ADR-0034).</summary>
    StorageFull,
}

/// <summary>A file part as the request delivered it, before anything about it is trusted.</summary>
public sealed record DocumentUploadCandidate(string? FileName, long Length, Func<Stream> OpenRead);

/// <summary>A file that passed <see cref="DocumentFileRules.ValidateAsync"/>.</summary>
public sealed record DocumentUploadFile(string FileName, string ContentType, long Length, string ContentSha256,
    int? PageCount, Func<Stream> OpenRead);

public sealed record UploadedMeetingDocument(Guid Id, string FileName, string ContentType, long ByteSize, int? PageCount);

public sealed record DocumentUploadOutcome(DocumentUploadFailure Failure = DocumentUploadFailure.None,
    IReadOnlyList<UploadedMeetingDocument>? Documents = null, IReadOnlyList<DocumentUploadFile>? Files = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);
