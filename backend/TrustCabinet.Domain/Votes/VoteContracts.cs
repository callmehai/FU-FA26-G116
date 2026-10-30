using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Domain.Votes;

/// <summary>
/// <see cref="AttachDocuments"/>, <see cref="AttachFromLibrary"/>, <see cref="AttachUpload"/> and
/// <see cref="DetachDocument"/> change a draft's documents (UC-76a, UC-76b, SCRUM-96).
/// </summary>
public enum VoteOperation
{
    Create, Update, Delete, Open, Cast, Close, RecordConclusion,
    AttachDocuments, AttachFromLibrary, AttachUpload, DetachDocument
}

public enum VoteFailure
{
    None, NotFound, Inactive, Invalid, UnsupportedMedia, TooLarge, ListReadFailed, DetailReadFailed, SaveFailed,
    DraftDenied, OpenDenied, AnotherOpen, NoEligibleVoters,
    OptionMissing, OptionUnavailable, NotEligible, NotAccepting, AlreadyCast,
    CloseDenied, ConclusionInvalid, ConclusionRecorded, ConclusionDenied,
    /// <summary>A vote document still in the meeting has no read confirmation from the voter (BR-289, BR-292).</summary>
    DocumentsUnread,
    /// <summary>A selected meeting document was removed or belongs to another meeting, or none was selected.</summary>
    DocumentNotInMeeting,
    /// <summary>A selected meeting document is already attached to the vote (BR-287).</summary>
    DocumentAlreadyAttached,
    /// <summary>The document is no longer attached to the vote, or no longer in the meeting.</summary>
    DocumentNotAttached,
    /// <summary>An uploaded file is not a valid PDF or DOCX of at most 50 MB (BR-187, BR-214, BR-215).</summary>
    FilesInvalid,
    /// <summary>More files, or more bytes, than one upload takes (BR-319).</summary>
    FilesTooMany,
    /// <summary>The files do not fit the system's storage limit (ADR-0034).</summary>
    StorageFull,
    /// <summary>A selected library document is missing or not the actor's (BR-209).</summary>
    LibraryUnavailable,
    /// <summary>A selected library document's file is already in the meeting (BR-193).</summary>
    AlreadyInMeeting,
    /// <summary>UC-74: the meeting, or the document as a vote document, is not available to the caller.</summary>
    DocumentUnavailable,
    /// <summary>UC-74: the reader has not reached the document's last page (BR-290).</summary>
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

public sealed record VoteOutcome(VoteFailure Failure = VoteFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null, Guid? VoteId = null);

/// <summary>Who is asking: an internal user or a Guest invitation, never both.</summary>
public sealed record VoteViewer(Guid? UserId, Guid? GuestInvitationId);

public sealed record VotePermissions(bool CanManageDraft, bool CanOpen, bool CanClose, bool CanRecordConclusion);
public sealed record VoteActions(bool CanEdit, bool CanDelete, bool CanAttachDocuments, bool CanOpen, bool CanCast,
    bool CanClose, bool CanRecordConclusion, bool CanViewResult);
public sealed record VoteConclusionView(string Value, string Note, Guid? ConcludedById, string? ConcludedByName,
    DateTimeOffset? ConcludedAt);
public sealed record VoteSummary(Guid Id, string Title, string Status, bool IsAnonymous, IReadOnlyList<string> EligibleRoles,
    DateTimeOffset? OpenedAt, DateTimeOffset? ClosedAt, int EligibleCount, int ReceiptCount, bool HasVoted,
    VoteConclusionView? Conclusion, int DocumentCount, VoteActions Actions);
public sealed record VoteListSnapshot(Guid MeetingId, string MeetingStatus, DateTimeOffset ServerTime,
    VotePermissions Permissions, IReadOnlyList<VoteSummary> Votes);

public sealed record VoteOptionView(Guid Id, int Position, string Label);
/// <summary>Null <see cref="Percentage"/> when nobody voted (BR-261).</summary>
public sealed record VoteOptionResult(Guid OptionId, string Label, int Count, double? Percentage);
/// <summary>A public vote's eligible voter and what they chose; null <see cref="OptionId"/> = did not vote (BR-273).</summary>
public sealed record VoteVoterResult(Guid Id, string Kind, string DisplayName, Guid? OptionId);
/// <summary>The tally of a closed vote. <see cref="Voters"/> is null for a secret one (BR-259).</summary>
public sealed record VoteResult(int BallotCount, IReadOnlyList<VoteOptionResult> Options, IReadOnlyList<VoteVoterResult>? Voters);
public sealed record VoteDetail(Guid MeetingId, string MeetingStatus, DateTimeOffset ServerTime, Guid Id, string Title,
    string Description, string Status, bool IsAnonymous, IReadOnlyList<string> EligibleRoles, IReadOnlyList<VoteOptionView> Options,
    DateTimeOffset? OpenedAt, DateTimeOffset? ClosedAt, int EligibleCount, int ReceiptCount, bool IsEligible, bool HasVoted,
    VoteConclusionView? Conclusion, int DocumentCount, IReadOnlyList<VoteDocumentView> Documents, VoteResult? Result,
    VoteActions Actions);

/// <summary>
/// A vote document still in the meeting, with when the caller confirmed reading it; null = not yet (UC-55 step 2).
/// <see cref="Id"/> is the MeetingDocument ID, the one the viewer and UC-74 take.
/// </summary>
public sealed record VoteDocumentView(Guid Id, string FileName, string ContentType, long ByteSize, int? PageCount,
    DateTimeOffset? ReadConfirmedAt);

/// <summary>UC-74: the caller's confirmation, new or the one recorded before (AF-1).</summary>
public sealed record DocumentReadView(Guid MeetingDocumentId, DateTimeOffset ConfirmedAt, bool AlreadyConfirmed);

/// <summary>
/// Pushed on <c>/hubs/meetings</c> as <c>voteChanged</c>. It only says what happened: readers refetch, which is
/// reauthorized. <see cref="Title"/> is set only when a vote opens, for the room's "Bỏ phiếu" toast; a ballot event
/// never names who voted.
/// </summary>
public sealed record VoteChange(Guid EventId, Guid MeetingId, Guid VoteId, string ChangeType, string? Title,
    DateTimeOffset OccurredAt);

public interface IVoteChangePublisher
{
    /// <summary><paramref name="managersOnly"/> for a draft change: drafts are seen by the Chair and Secretary only (BR-253).</summary>
    Task PublishAsync(VoteChange change, bool managersOnly, CancellationToken ct);
}

public interface IVoteService
{
    Task<VoteOutcome> ListAsync(Guid meetingId, ParticipantActor actor, CancellationToken ct = default);
    Task<VoteOutcome> GetAsync(Guid meetingId, Guid voteId, ParticipantActor actor, CancellationToken ct = default);
    Task<VoteOutcome> MutateAsync(Guid meetingId, Guid? voteId, ParticipantActor actor, VoteOperation operation,
        VoteInput input, CancellationToken ct = default);

    /// <summary>UC-76a AF-1: who may attach to this draft, checked before the files are read.</summary>
    Task<VoteOutcome> CheckAttachUploadAsync(Guid meetingId, Guid voteId, ParticipantActor actor, CancellationToken ct = default);

    /// <summary>
    /// UC-76a AF-1: the files become meeting documents, library documents of the uploader and documents of the vote,
    /// all in one save or not at all (BR-194, BR-304).
    /// </summary>
    Task<VoteOutcome> AttachUploadAsync(Guid meetingId, Guid voteId, ParticipantActor actor,
        IReadOnlyList<Documents.DocumentUploadFile> files, CancellationToken ct = default);

    /// <summary>UC-74: records that the caller read a vote document to its last page (BR-290, BR-291, BR-293).</summary>
    Task<VoteOutcome> ConfirmReadAsync(Guid meetingId, Guid meetingDocumentId, ParticipantActor actor, VoteInput input,
        CancellationToken ct = default);
}
