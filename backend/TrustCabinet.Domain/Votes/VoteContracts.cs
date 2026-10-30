using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Domain.Votes;

public enum VoteOperation { Create, Update, Delete, Open, Cast, Close, RecordConclusion }

public enum VoteFailure
{
    None, NotFound, Inactive, Invalid, UnsupportedMedia, TooLarge, ListReadFailed, DetailReadFailed, SaveFailed,
    DraftDenied, OpenDenied, AnotherOpen, NoEligibleVoters,
    OptionMissing, OptionUnavailable, NotEligible, NotAccepting, AlreadyCast,
    CloseDenied, ConclusionInvalid, ConclusionRecorded, ConclusionDenied
}

/// <summary>
/// One request's body, already parsed by the endpoint. Fields belong to different operations: a
/// draft's configuration (create, update), the chosen option (cast), or the Chair's conclusion.
/// </summary>
public sealed record VoteInput(string? Title = null, string? Description = null, IReadOnlyList<string?>? Options = null,
    bool? IsAnonymous = null, IReadOnlyList<string?>? EligibleRoles = null, bool OpenImmediately = false,
    Guid? OptionId = null, string? Conclusion = null, string? Note = null, VoteFailure ParseFailure = VoteFailure.None);

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
    VoteConclusionView? Conclusion, int DocumentCount, VoteResult? Result, VoteActions Actions);

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
}
