using TrustCabinet.Application.Participants;
using TrustCabinet.Domain.Votes;

namespace TrustCabinet.Application.Votes;

/// <summary>
/// <see cref="AttachDocuments"/>, <see cref="AttachFromLibrary"/>, <see cref="AttachUpload"/> and
/// <see cref="DetachDocument"/> change a draft's documents (UC-68a, UC-68b, SCRUM-96).
/// </summary>
public enum VoteOperation
{
    Create, Update, Delete, Open, Cast, Close, RecordConclusion,
    AttachDocuments, AttachFromLibrary, AttachUpload, DetachDocument
}

public sealed record VoteOutcome(VoteFailure Failure = VoteFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null, Guid? VoteId = null);

public sealed record VoteConclusionView(string Value, string Note, Guid? ConcludedById, string? ConcludedByName,
    DateTimeOffset? ConcludedAt);
public sealed record VoteSummary(Guid Id, string Title, string Status, bool IsAnonymous, IReadOnlyList<string> EligibleRoles,
    DateTimeOffset? OpenedAt, DateTimeOffset? ClosedAt, int EligibleCount, int ReceiptCount, bool HasVoted,
    VoteConclusionView? Conclusion, int DocumentCount, VoteActions Actions);
public sealed record VoteListSnapshot(Guid MeetingId, string MeetingStatus, DateTimeOffset ServerTime,
    VotePermissions Permissions, IReadOnlyList<VoteSummary> Votes);

public sealed record VoteOptionView(Guid Id, int Position, string Label);
/// <summary>Null <see cref="Percentage"/> when nobody voted (BR-098).</summary>
public sealed record VoteOptionResult(Guid OptionId, string Label, int Count, double? Percentage);
/// <summary>A public vote's eligible voter and what they chose; null <see cref="OptionId"/> = did not vote (BR-096, BR-097).</summary>
public sealed record VoteVoterResult(Guid Id, string Kind, string DisplayName, Guid? OptionId);
/// <summary>The tally of a closed vote. <see cref="Voters"/> is null for a secret one (BR-096, BR-097).</summary>
public sealed record VoteResult(int BallotCount, IReadOnlyList<VoteOptionResult> Options, IReadOnlyList<VoteVoterResult>? Voters);
public sealed record VoteDetail(Guid MeetingId, string MeetingStatus, DateTimeOffset ServerTime, Guid Id, string Title,
    string Description, string Status, bool IsAnonymous, IReadOnlyList<string> EligibleRoles, IReadOnlyList<VoteOptionView> Options,
    DateTimeOffset? OpenedAt, DateTimeOffset? ClosedAt, int EligibleCount, int ReceiptCount, bool IsEligible, bool HasVoted,
    VoteConclusionView? Conclusion, int DocumentCount, IReadOnlyList<VoteDocumentView> Documents, VoteResult? Result,
    VoteActions Actions);

/// <summary>
/// A vote document still in the meeting, with when the caller confirmed reading it; null = not yet (UC-65 step 2).
/// <see cref="Id"/> is the MeetingDocument ID, the one the viewer and UC-42 take.
/// </summary>
public sealed record VoteDocumentView(Guid Id, string FileName, string ContentType, long ByteSize, int? PageCount,
    DateTimeOffset? ReadConfirmedAt);

/// <summary>UC-42: the caller's confirmation, new or the one recorded before (AF-1).</summary>
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
    /// <summary><paramref name="managersOnly"/> for a draft change: drafts are seen by the Chair and Secretary only (BR-086, BR-087).</summary>
    Task PublishAsync(VoteChange change, bool managersOnly, CancellationToken ct);
}

/// <summary>
/// What ending a meeting does to its votes (PO 08/11/2026, SCRUM-28): a draft that was never opened can no longer be,
/// so the system deletes it and records one audit row per draft. Open and Closed votes are evidence and are kept
/// (BR-086, BR-087). The caller owns the meeting lock, the transaction, SaveChanges and publishing the returned changes.
/// </summary>
public interface IVoteMeetingEnd
{
    /// <param name="endedById">The Chair who ended it; null when the system ended a meeting left running (ADR-0062).</param>
    Task<IReadOnlyList<VoteChange>> DeleteDraftsAsync(Guid meetingId, Guid? endedById, DateTimeOffset endedAt,
        CancellationToken ct = default);

    /// <summary>
    /// The same for a cancelled meeting (ADR-0067): it never starts, so its drafts can never be opened either. Called by
    /// the organizer's cancel, the system's (BR-029) and the retention job for drafts left before this existed.
    /// </summary>
    /// <param name="cancelledById">The organizer who cancelled it; null when the system did.</param>
    Task<IReadOnlyList<VoteChange>> DeleteDraftsOnCancelAsync(Guid meetingId, Guid? cancelledById, DateTimeOffset at,
        CancellationToken ct = default);
}

public interface IVoteService
{
    Task<VoteOutcome> ListAsync(Guid meetingId, ParticipantActor actor, CancellationToken ct = default);
    Task<VoteOutcome> GetAsync(Guid meetingId, Guid voteId, ParticipantActor actor, CancellationToken ct = default);
    Task<VoteOutcome> MutateAsync(Guid meetingId, Guid? voteId, ParticipantActor actor, VoteOperation operation,
        VoteInput input, CancellationToken ct = default);

    /// <summary>UC-68a AF-1: who may attach to this draft, checked before the files are read.</summary>
    Task<VoteOutcome> CheckAttachUploadAsync(Guid meetingId, Guid voteId, ParticipantActor actor, CancellationToken ct = default);

    /// <summary>
    /// UC-68a AF-1: the files become meeting documents, library documents of the uploader and documents of the vote,
    /// all in one save or not at all (UC-36a, UC-36b).
    /// </summary>
    Task<VoteOutcome> AttachUploadAsync(Guid meetingId, Guid voteId, ParticipantActor actor,
        IReadOnlyList<Domain.Documents.DocumentUploadFile> files, CancellationToken ct = default);

    /// <summary>UC-42: records that the caller read a vote document to its last page (UC-42, BR-096, BR-097).</summary>
    Task<VoteOutcome> ConfirmReadAsync(Guid meetingId, Guid meetingDocumentId, ParticipantActor actor, VoteInput input,
        CancellationToken ct = default);
}
