namespace TrustCabinet.Domain.Votes;

public enum VoteFailure
{
    None, NotFound, Inactive, Invalid, UnsupportedMedia, TooLarge, ListReadFailed, DetailReadFailed, SaveFailed,
    DraftDenied, OpenDenied, AnotherOpen, NoEligibleVoters,
    OptionMissing, OptionUnavailable, NotEligible, NotAccepting, AlreadyCast,
    CloseDenied, ConclusionInvalid, ConclusionRecorded, ConclusionDenied,
    /// <summary>A vote document still in the meeting has no read confirmation from the voter (BR-094, BR-095, UC-65).</summary>
    DocumentsUnread,
    /// <summary>A selected meeting document was removed or belongs to another meeting, or none was selected.</summary>
    DocumentNotInMeeting,
    /// <summary>A selected meeting document is already attached to the vote (UC-68a).</summary>
    DocumentAlreadyAttached,
    /// <summary>The document is no longer attached to the vote, or no longer in the meeting.</summary>
    DocumentNotAttached,
    /// <summary>An uploaded file is not a valid PDF or DOCX of at most 10 MB (BR-069, UC-36a, UC-39, NFR).</summary>
    FilesInvalid,
    /// <summary>More files, or more bytes, than one upload takes (UC-36a, UC-36b, UC-39).</summary>
    FilesTooMany,
    /// <summary>The files do not fit the system's storage limit (ADR-0034).</summary>
    StorageFull,
    /// <summary>A selected library document is missing or not the actor's (BR-072, BR-073).</summary>
    LibraryUnavailable,
    /// <summary>UC-42: the meeting, or the document as a vote document, is not available to the caller.</summary>
    DocumentUnavailable,
    /// <summary>UC-42: the reader has not reached the document's last page (UC-42).</summary>
    LastPageNotReached
}

/// <summary>
/// One request's body, already parsed by the endpoint. Fields belong to different operations: a
/// draft's configuration (create, update), the chosen option (cast), the Chair's conclusion, the documents to attach
/// (meeting or library document IDs) or detach, or a read confirmation.
/// </summary>
public sealed record VoteInput(string? Title = null, string? Description = null, IReadOnlyList<string?>? Options = null,
    bool? IsAnonymous = null, IReadOnlyList<string?>? EligibleRoles = null, bool OpenImmediately = false,
    Guid? OptionId = null, string? Conclusion = null, string? Note = null, IReadOnlyList<Guid>? DocumentIds = null,
    Guid? MeetingDocumentId = null, bool? LastPageReached = null, VoteFailure ParseFailure = VoteFailure.None);

/// <summary>Who is asking: an internal user or a Guest invitation, never both.</summary>
public sealed record VoteViewer(Guid? UserId, Guid? GuestInvitationId);

public sealed record VotePermissions(bool CanManageDraft, bool CanOpen, bool CanClose, bool CanRecordConclusion);

public sealed record VoteActions(bool CanEdit, bool CanDelete, bool CanAttachDocuments, bool CanOpen, bool CanCast,
    bool CanClose, bool CanRecordConclusion, bool CanViewResult);
