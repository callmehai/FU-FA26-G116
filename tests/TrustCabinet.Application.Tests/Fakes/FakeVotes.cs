using TrustCabinet.Application.Votes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Votes;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// Votes as the vote use cases see them: tracked votes by id, the rows the list and detail read, and fixed answers
/// for the checks the moved queries ran. Locks and reads are recorded in <see cref="Calls"/>, what is staged in
/// <see cref="Added"/> and <see cref="Removed"/>; nothing here saves.
/// </summary>
public sealed class FakeVoteRepository : IVoteRepository
{
    public Dictionary<Guid, VotingSession> Votes { get; } = [];
    public List<VoteRow> Rows { get; } = [];
    public HashSet<Guid> ActiveUsers { get; } = [];
    public bool AnotherOpen { get; set; }

    /// <summary>Votes the caller is on the fixed voter list of, and votes the caller already holds a receipt for.</summary>
    public HashSet<Guid> EligibleIn { get; } = [];
    public HashSet<Guid> VotedIn { get; } = [];

    public Dictionary<Guid, List<VoteDocumentView>> Documents { get; } = [];
    public VoteResult Result { get; set; } = new(0, [], null);
    public bool VisibleDocument { get; set; } = true;
    public (bool Eligible, bool Open) ReadRequirement { get; set; } = (true, true);

    /// <summary>Answers <see cref="FindReadConfirmationAsync"/>; the first entry is used once, then the next.</summary>
    public Queue<DocumentReadConfirmation?> ReadConfirmations { get; } = [];

    public List<VoteDraft> Drafts { get; } = [];
    public int LiveMeetingDocuments { get; set; }

    public List<string> Calls { get; } = [];
    public List<object> Added { get; } = [];
    public List<object> Removed { get; } = [];
    public List<(Guid VoteId, Guid OptionId)> CountedBallots { get; } = [];

    /// <summary>Thrown by every read in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    public Task LockMeetingAsync(Guid meetingId, bool shared, CancellationToken ct)
        => Lock(shared ? "lock-meeting:shared" : "lock-meeting:exclusive");

    public Task ShareUserAsync(Guid userId, CancellationToken ct) => Lock("lock-user");

    public Task<VotingSession?> LockAsync(Guid meetingId, Guid voteId, bool shared, CancellationToken ct)
        => Read(() => Record(shared ? "lock-vote:shared" : "lock-vote:exclusive",
            Votes.GetValueOrDefault(voteId) is { } v && v.MeetingId == meetingId ? v : null));

    public Task<bool> AnotherOpenAsync(Guid meetingId, Guid? voteId, CancellationToken ct) => Read(() => AnotherOpen);

    public Task<bool> IsEligibleAsync(Guid voteId, VoteViewer viewer, CancellationToken ct) => Read(() => EligibleIn.Contains(voteId));

    public Task<bool> HasVotedAsync(Guid voteId, VoteViewer viewer, CancellationToken ct) => Read(() => VotedIn.Contains(voteId));

    public Task<IReadOnlyList<VoteRow>> ListAsync(Guid meetingId, bool includeDrafts, VoteViewer viewer, CancellationToken ct)
        => Read<IReadOnlyList<VoteRow>>(() => Rows.Where(r => includeDrafts || r.Status != VotingStatus.Draft).ToArray());

    public Task<VoteRow?> FindAsync(Guid meetingId, Guid voteId, VoteViewer viewer, CancellationToken ct)
        => Read(() => Rows.SingleOrDefault(r => r.Id == voteId));

    public Task<IReadOnlyList<VoteDocumentView>> DocumentsAsync(Guid voteId, VoteViewer viewer, CancellationToken ct)
        => Read<IReadOnlyList<VoteDocumentView>>(() => Documents.GetValueOrDefault(voteId) ?? []);

    public Task<HashSet<Guid>> AttachedDocumentIdsAsync(Guid voteId, CancellationToken ct)
        => Read(() => Added.OfType<VoteDocument>().Where(l => l.VotingSessionId == voteId).Select(l => l.MeetingDocumentId).ToHashSet());

    public Task<bool> IsVisibleVoteDocumentAsync(Guid meetingId, Guid meetingDocumentId, bool includeDrafts, CancellationToken ct)
        => Read(() => VisibleDocument);

    public Task<(bool Eligible, bool Open)> ReadRequirementAsync(Guid meetingDocumentId, VoteViewer viewer, CancellationToken ct)
        => Read(() => ReadRequirement);

    public Task<DocumentReadConfirmation?> FindReadConfirmationAsync(Guid meetingDocumentId, VoteViewer viewer, CancellationToken ct)
        => Read(() => Record("find-read-confirmation", ReadConfirmations.TryDequeue(out var c) ? c : null));

    public Task<IReadOnlyList<VotingOption>> OptionsAsync(Guid voteId, CancellationToken ct)
        => Read<IReadOnlyList<VotingOption>>(() => Votes.GetValueOrDefault(voteId)?.Options.OrderBy(o => o.Position).ToArray() ?? []);

    public Task<int> CountBallotAsync(Guid voteId, Guid optionId, CancellationToken ct)
        => Read(() =>
        {
            CountedBallots.Add((voteId, optionId));
            return 1;
        });

    /// <summary>Runs when the tally is read, so a test can see whether that was before or after the commit.</summary>
    public Action? OnResult { get; set; }

    public Task<VoteResult> ResultAsync(Guid meetingId, Guid voteId, bool isAnonymous, CancellationToken ct)
        => Read(() =>
        {
            OnResult?.Invoke();
            return Result;
        });

    public Task<bool> IsActiveUserAsync(Guid userId, CancellationToken ct) => Read(() => ActiveUsers.Contains(userId));

    public Task<int> CountMeetingDocumentsAsync(Guid meetingId, Guid[] meetingDocumentIds, CancellationToken ct)
        => Read(() => LiveMeetingDocuments);

    public Task<VoteDocument?> FindDocumentLinkAsync(Guid voteId, Guid? meetingDocumentId, CancellationToken ct)
        => Read(() => Added.OfType<VoteDocument>().SingleOrDefault(l => l.VotingSessionId == voteId && l.MeetingDocumentId == meetingDocumentId));

    public Task<IReadOnlyList<VoteDraft>> ListDraftsAsync(Guid meetingId, CancellationToken ct)
        => Read<IReadOnlyList<VoteDraft>>(() => Drafts.Where(d => d.Vote.MeetingId == meetingId).ToArray());

    public void Add(VotingSession vote) => Added.Add(vote);
    public void Remove(VotingSession vote) => Removed.Add(vote);
    public void Add(VotingOption option) => Added.Add(option);
    public void RemoveOptions(IEnumerable<VotingOption> options) => Removed.AddRange(options);
    public void AddEligibleVoters(IEnumerable<VoteEligibleVoter> voters) => Added.AddRange(voters);
    public void Add(VoteReceipt receipt) => Added.Add(receipt);
    public void Add(VoteChoice choice) => Added.Add(choice);
    public void AddDocumentLinks(IEnumerable<VoteDocument> links) => Added.AddRange(links);
    public void Remove(VoteDocument link) => Removed.Add(link);
    public void Add(DocumentReadConfirmation confirmation) => Added.Add(confirmation);

    private T Record<T>(string name, T answer)
    {
        Calls.Add(name);
        return answer;
    }

    private Task<T> Read<T>(Func<T> answer) => Fail is { } failure ? Task.FromException<T>(failure) : Task.FromResult(answer());
    private Task Lock(string name) => Read(() => Record(name, true));
}

/// <summary>Vote events in order, with the commit count at the time, so a test can see each went out after the commit.</summary>
public sealed class FakeVoteChangePublisher(FakeUnitOfWork unitOfWork) : IVoteChangePublisher
{
    public List<(VoteChange Change, bool ManagersOnly, int Commits)> Published { get; } = [];

    public Task PublishAsync(VoteChange change, bool managersOnly, CancellationToken ct)
    {
        Published.Add((change, managersOnly, unitOfWork.Commits));
        return Task.CompletedTask;
    }
}
