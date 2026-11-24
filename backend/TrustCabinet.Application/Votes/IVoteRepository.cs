using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Votes;

namespace TrustCabinet.Application.Votes;

/// <summary>A vote as the list and the detail show it, with the caller's own standing in it.</summary>
public sealed record VoteRow(Guid Id, string Title, string Description, VotingStatus Status, bool IsAnonymous,
    List<MeetingRole> EligibleRoles, DateTimeOffset? OpenedAt, DateTimeOffset? ClosedAt, int EligibleCount,
    int ReceiptCount, int DocumentCount, bool IsEligible, bool HasVoted, VoteConclusion? Conclusion,
    string ConclusionNote, Guid? ConcludedById, string? ConcludedByName, DateTimeOffset? ConcludedAt);

/// <summary>A draft deleted at meeting end, tracked, with what the audit row records of it.</summary>
public sealed record VoteDraft(VotingSession Vote, string[] Options, Guid[] Documents);

/// <summary>
/// Loads and stores votes, their ballots and the reading of their documents (FE-11, SCRUM-28, SCRUM-96). Writes
/// are staged and saved by <c>IUnitOfWork.SaveChangesAsync</c>, except where a method says it runs immediately.
/// </summary>
public interface IVoteRepository
{
    /// <summary>
    /// Locks the meeting row: shared (<c>FOR SHARE</c>) for a ballot or a read confirmation, exclusive
    /// (<c>FOR UPDATE</c>) for every other vote write. Every vote write takes it first, so it serializes with ending
    /// the meeting and with roster changes; ballots only share it, so they run side by side.
    /// </summary>
    Task LockMeetingAsync(Guid meetingId, bool shared, CancellationToken ct);

    /// <summary>
    /// Locks the caller's account row shared (<c>FOR SHARE</c>), taken after the meeting: keeps the account from
    /// being deactivated between the access check and the commit.
    /// </summary>
    Task ShareUserAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Locks the vote row, after the meeting and the caller: shared for a ballot, exclusive otherwise. Returns the
    /// vote tracked with its options in position order, or null when it is not a vote of the meeting.
    /// </summary>
    Task<VotingSession?> LockAsync(Guid meetingId, Guid voteId, bool shared, CancellationToken ct);

    Task<bool> AnotherOpenAsync(Guid meetingId, Guid? voteId, CancellationToken ct);
    Task<bool> IsEligibleAsync(Guid voteId, VoteViewer viewer, CancellationToken ct);
    Task<bool> HasVotedAsync(Guid voteId, VoteViewer viewer, CancellationToken ct);

    /// <summary>The meeting's votes oldest first; drafts only for the Chair and Secretary (BR-086, BR-087).</summary>
    Task<IReadOnlyList<VoteRow>> ListAsync(Guid meetingId, bool includeDrafts, VoteViewer viewer, CancellationToken ct);

    Task<VoteRow?> FindAsync(Guid meetingId, Guid voteId, VoteViewer viewer, CancellationToken ct);

    /// <summary>
    /// The vote's documents still in the meeting, oldest attached first, with when <paramref name="viewer"/> confirmed
    /// reading each. One removed from the meeting no longer shows or counts (UC-65).
    /// </summary>
    Task<IReadOnlyList<VoteDocumentView>> DocumentsAsync(Guid voteId, VoteViewer viewer, CancellationToken ct);

    Task<HashSet<Guid>> AttachedDocumentIdsAsync(Guid voteId, CancellationToken ct);

    /// <summary>
    /// Whether the meeting document is in the meeting and attached to a vote the reader may see: any vote for the
    /// Chair and the Secretary, an Open or Closed one for everyone else, since a draft is theirs alone (BR-086, BR-087).
    /// </summary>
    Task<bool> IsVisibleVoteDocumentAsync(Guid meetingId, Guid meetingDocumentId, bool includeDrafts, CancellationToken ct);

    /// <summary>
    /// Whether <paramref name="viewer"/> is on the fixed voter list of a vote that links the document, and whether one
    /// of those votes is Open. Only a voter waiting to cast needs a confirmation.
    /// </summary>
    Task<(bool Eligible, bool Open)> ReadRequirementAsync(Guid meetingDocumentId, VoteViewer viewer, CancellationToken ct);

    Task<DocumentReadConfirmation?> FindReadConfirmationAsync(Guid meetingDocumentId, VoteViewer viewer, CancellationToken ct);

    Task<IReadOnlyList<VotingOption>> OptionsAsync(Guid voteId, CancellationToken ct);

    /// <summary>
    /// Counts a ballot for <paramref name="optionId"/>, every option of the vote rewritten in one statement so a
    /// secret choice is not given away (BR-096, BR-097). Runs immediately, not staged; returns the rows updated.
    /// </summary>
    Task<int> CountBallotAsync(Guid voteId, Guid optionId, CancellationToken ct);

    /// <summary>
    /// The tally, read from each option's counter. Only a public vote lists who chose what (BR-096, BR-097); a secret one
    /// stored nothing that could (BR-096, BR-097).
    /// </summary>
    Task<VoteResult> ResultAsync(Guid meetingId, Guid voteId, bool isAnonymous, CancellationToken ct);

    /// <summary>Whether the account exists and is active.</summary>
    Task<bool> IsActiveUserAsync(Guid userId, CancellationToken ct);

    /// <summary>How many of <paramref name="meetingDocumentIds"/> are documents of the meeting not removed from it.</summary>
    Task<int> CountMeetingDocumentsAsync(Guid meetingId, Guid[] meetingDocumentIds, CancellationToken ct);

    /// <summary>The vote's link to a document still in the meeting, tracked, or null.</summary>
    Task<VoteDocument?> FindDocumentLinkAsync(Guid voteId, Guid? meetingDocumentId, CancellationToken ct);

    /// <summary>
    /// The meeting's drafts oldest first, tracked, with their option labels and document IDs. Requires the caller's
    /// transaction, in which the caller holds the meeting lock: throws <see cref="InvalidOperationException"/> when
    /// none is open, since no vote write may run between this read and the deletion.
    /// </summary>
    Task<IReadOnlyList<VoteDraft>> ListDraftsAsync(Guid meetingId, CancellationToken ct);

    void Add(VotingSession vote);
    void Remove(VotingSession vote);
    void Add(VotingOption option);
    void RemoveOptions(IEnumerable<VotingOption> options);
    void AddEligibleVoters(IEnumerable<VoteEligibleVoter> voters);
    void Add(VoteReceipt receipt);
    void Add(VoteChoice choice);
    void AddDocumentLinks(IEnumerable<VoteDocument> links);
    void Remove(VoteDocument link);
    void Add(DocumentReadConfirmation confirmation);
}
