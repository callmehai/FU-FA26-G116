using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Votes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Votes;

namespace TrustCabinet.Infrastructure.Votes;

public sealed class VoteRepository(TrustCabinetDbContext db, IParticipantGuestAccess guests) : IVoteRepository
{
    /// <summary>
    /// Every vote write takes the meeting row first, so it serializes with ending the meeting and with roster
    /// changes. A ballot only shares it: ballots run side by side, and one-person-one-vote is the unique index.
    /// </summary>
    public async Task LockMeetingAsync(Guid meetingId, bool shared, CancellationToken ct)
        => await (shared
            ? db.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {meetingId} FOR SHARE")
            : db.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {meetingId} FOR UPDATE"))
            .AsNoTracking().ToArrayAsync(ct);

    /// <summary>Keeps the caller's account from being deactivated between the access check and the commit.</summary>
    public async Task ShareUserAsync(Guid userId, CancellationToken ct)
        => await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR SHARE")
            .AsNoTracking().ToArrayAsync(ct);

    /// <summary>The vote, tracked with its options, after locking its row: shared for a ballot, exclusive otherwise.</summary>
    public async Task<VotingSession?> LockAsync(Guid meetingId, Guid voteId, bool shared, CancellationToken ct)
    {
        var locked = await (shared
            ? db.VotingSessions.FromSqlInterpolated($"SELECT * FROM voting_sessions WHERE id = {voteId} AND meeting_id = {meetingId} FOR SHARE")
            : db.VotingSessions.FromSqlInterpolated($"SELECT * FROM voting_sessions WHERE id = {voteId} AND meeting_id = {meetingId} FOR UPDATE"))
            .AsNoTracking().ToArrayAsync(ct);
        if (locked.Length == 0) return null;
        return await db.VotingSessions.Include(v => v.Options.OrderBy(o => o.Position))
            .SingleAsync(v => v.Id == voteId, ct);
    }

    public Task<bool> AnotherOpenAsync(Guid meetingId, Guid? voteId, CancellationToken ct)
        => db.VotingSessions.AnyAsync(v => v.MeetingId == meetingId && v.Status == VotingStatus.Open && v.Id != voteId, ct);

    public Task<bool> IsEligibleAsync(Guid voteId, VoteViewer viewer, CancellationToken ct)
        => db.VoteEligibleVoters.AnyAsync(e => e.VotingSessionId == voteId
            && ((viewer.UserId != null && e.UserId == viewer.UserId)
                || (viewer.GuestInvitationId != null && e.GuestInvitationId == viewer.GuestInvitationId)), ct);

    public Task<bool> HasVotedAsync(Guid voteId, VoteViewer viewer, CancellationToken ct)
        => db.VoteReceipts.AnyAsync(r => r.VotingSessionId == voteId
            && ((viewer.UserId != null && r.VoterId == viewer.UserId)
                || (viewer.GuestInvitationId != null && r.GuestInvitationId == viewer.GuestInvitationId)), ct);

    /// <summary>The meeting's votes oldest first; drafts only for the Chair and Secretary (BR-086, BR-087).</summary>
    public async Task<IReadOnlyList<VoteRow>> ListAsync(Guid meetingId, bool includeDrafts, VoteViewer viewer, CancellationToken ct)
        => await db.VotingSessions.AsNoTracking().Where(v => v.MeetingId == meetingId && (includeDrafts || v.Status != VotingStatus.Draft))
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Id).Select(Row(viewer)).ToListAsync(ct);

    public async Task<VoteRow?> FindAsync(Guid meetingId, Guid voteId, VoteViewer viewer, CancellationToken ct)
        => await db.VotingSessions.AsNoTracking().Where(v => v.MeetingId == meetingId && v.Id == voteId)
            .Select(Row(viewer)).SingleOrDefaultAsync(ct);

    /// <summary>
    /// The vote's documents still in the meeting, oldest attached first, with when <paramref name="viewer"/> confirmed
    /// reading each. One removed from the meeting no longer shows or counts (UC-65).
    /// </summary>
    public async Task<IReadOnlyList<VoteDocumentView>> DocumentsAsync(Guid voteId, VoteViewer viewer, CancellationToken ct)
    {
        var userId = viewer.UserId;
        var guestId = viewer.GuestInvitationId;
        return await db.VoteDocuments.AsNoTracking()
            .Where(l => l.VotingSessionId == voteId && l.MeetingDocument.RemovedAt == null)
            .OrderBy(l => l.MeetingDocument.AddedAt).ThenBy(l => l.MeetingDocumentId)
            .Select(l => new VoteDocumentView(l.MeetingDocumentId, l.MeetingDocument.FileName,
                l.MeetingDocument.StoredFile.ContentType, l.MeetingDocument.StoredFile.ByteSize,
                l.MeetingDocument.StoredFile.PageCount,
                l.MeetingDocument.ReadConfirmations
                    .Where(c => (userId != null && c.UserId == userId) || (guestId != null && c.GuestInvitationId == guestId))
                    .Select(c => (DateTimeOffset?)c.ConfirmedAt).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public Task<HashSet<Guid>> AttachedDocumentIdsAsync(Guid voteId, CancellationToken ct)
        => db.VoteDocuments.Where(l => l.VotingSessionId == voteId).Select(l => l.MeetingDocumentId).ToHashSetAsync(ct);

    /// <summary>
    /// Whether the meeting document is in the meeting and attached to a vote the reader may see: any vote for the
    /// Chair and the Secretary, an Open or Closed one for everyone else, since a draft is theirs alone (BR-086, BR-087).
    /// </summary>
    public Task<bool> IsVisibleVoteDocumentAsync(Guid meetingId, Guid meetingDocumentId, bool includeDrafts, CancellationToken ct)
        => db.VoteDocuments.AnyAsync(l => l.MeetingDocumentId == meetingDocumentId && l.MeetingDocument.MeetingId == meetingId
            && l.MeetingDocument.RemovedAt == null && l.VotingSession.MeetingId == meetingId
            && (includeDrafts || l.VotingSession.Status != VotingStatus.Draft), ct);

    /// <summary>
    /// Whether <paramref name="viewer"/> is on the fixed voter list of a vote that links the document, and whether one
    /// of those votes is Open. Only a voter waiting to cast needs a confirmation.
    /// </summary>
    public async Task<(bool Eligible, bool Open)> ReadRequirementAsync(Guid meetingDocumentId, VoteViewer viewer,
        CancellationToken ct)
    {
        var userId = viewer.UserId;
        var guestId = viewer.GuestInvitationId;
        var statuses = await db.VoteDocuments.AsNoTracking()
            .Where(l => l.MeetingDocumentId == meetingDocumentId
                && l.VotingSession.EligibleVoters.Any(e => (userId != null && e.UserId == userId)
                    || (guestId != null && e.GuestInvitationId == guestId)))
            .Select(l => l.VotingSession.Status).ToListAsync(ct);
        return (statuses.Count != 0, statuses.Contains(VotingStatus.Open));
    }

    public Task<DocumentReadConfirmation?> FindReadConfirmationAsync(Guid meetingDocumentId, VoteViewer viewer, CancellationToken ct)
        => db.DocumentReadConfirmations.AsNoTracking().SingleOrDefaultAsync(c => c.MeetingDocumentId == meetingDocumentId
            && ((viewer.UserId != null && c.UserId == viewer.UserId)
                || (viewer.GuestInvitationId != null && c.GuestInvitationId == viewer.GuestInvitationId)), ct);

    public async Task<IReadOnlyList<VotingOption>> OptionsAsync(Guid voteId, CancellationToken ct)
        => await db.VotingOptions.AsNoTracking().Where(o => o.VotingSessionId == voteId)
            .OrderBy(o => o.Position).ToListAsync(ct);

    /// <summary>
    /// Counts a ballot for <paramref name="optionId"/>. Every option of the vote is rewritten in the one statement,
    /// adding one to the chosen and nothing to the rest, and <c>updated_at</c> is left alone: had only the chosen
    /// row changed, its row version (<c>xmin</c>) and timestamp would match the voter's receipt from the same
    /// transaction and give a secret choice away (BR-096, BR-097). Atomic in SQL, so ballots cast side by side add up.
    /// </summary>
    public Task<int> CountBallotAsync(Guid voteId, Guid optionId, CancellationToken ct)
        => db.VotingOptions.Where(o => o.VotingSessionId == voteId).ExecuteUpdateAsync(
            set => set.SetProperty(o => o.BallotCount, o => o.BallotCount + (o.Id == optionId ? 1 : 0)), ct);

    /// <summary>
    /// The tally, read from each option's counter. Only a public vote lists who chose what (BR-096, BR-097); a secret one
    /// stored nothing that could (BR-096, BR-097).
    /// </summary>
    public async Task<VoteResult> ResultAsync(Guid meetingId, Guid voteId, bool isAnonymous, CancellationToken ct)
    {
        var options = await OptionsAsync(voteId, ct);
        var ballots = options.Sum(o => o.BallotCount);
        var tally = options.Select(o => new VoteOptionResult(o.Id, o.Label, o.BallotCount,
            VoteRules.Percentage(o.BallotCount, ballots))).ToArray();
        if (isAnonymous) return new(ballots, tally, null);

        var eligible = await db.VoteEligibleVoters.AsNoTracking().Where(e => e.VotingSessionId == voteId)
            .Select(e => new { e.UserId, e.GuestInvitationId, UserName = e.User!.FullName, GuestName = e.GuestInvitation!.InvitedName })
            .ToListAsync(ct);
        var choices = await db.VoteChoices.AsNoTracking().Where(c => c.VotingSessionId == voteId)
            .Select(c => new { c.VoterId, c.GuestInvitationId, c.VotingOptionId }).ToListAsync(ct);
        var byUser = choices.Where(c => c.VoterId.HasValue).ToDictionary(c => c.VoterId!.Value, c => c.VotingOptionId);
        var byGuest = choices.Where(c => c.GuestInvitationId.HasValue).ToDictionary(c => c.GuestInvitationId!.Value, c => c.VotingOptionId);
        var guestIds = eligible.Where(e => e.GuestInvitationId.HasValue).Select(e => e.GuestInvitationId!.Value).ToArray();
        var profiles = guestIds.Length == 0 ? new Dictionary<Guid, GuestDisplayProfile>()
            : await guests.GetProfilesAsync(meetingId, guestIds, ct);
        var voters = eligible.Select(e => e.UserId is { } userId
                ? new VoteVoterResult(userId, "Internal", e.UserName, byUser.TryGetValue(userId, out var o) ? o : null)
                : new VoteVoterResult(e.GuestInvitationId!.Value, "Guest",
                    profiles.GetValueOrDefault(e.GuestInvitationId.Value)?.DisplayName ?? e.GuestName,
                    byGuest.TryGetValue(e.GuestInvitationId.Value, out var g) ? g : null))
            .OrderBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(v => v.Id).ToArray();
        return new(ballots, tally, voters);
    }

    public Task<bool> IsActiveUserAsync(Guid userId, CancellationToken ct)
        => db.Users.AnyAsync(u => u.Id == userId && u.IsActive, ct);

    public Task<int> CountMeetingDocumentsAsync(Guid meetingId, Guid[] meetingDocumentIds, CancellationToken ct)
        => db.MeetingDocuments.CountAsync(d => meetingDocumentIds.Contains(d.Id) && d.MeetingId == meetingId && d.RemovedAt == null, ct);

    public Task<VoteDocument?> FindDocumentLinkAsync(Guid voteId, Guid? meetingDocumentId, CancellationToken ct)
        => db.VoteDocuments.SingleOrDefaultAsync(l => l.VotingSessionId == voteId
            && l.MeetingDocumentId == meetingDocumentId && l.MeetingDocument.RemovedAt == null, ct);

    public async Task<IReadOnlyList<VoteDraft>> ListDraftsAsync(Guid meetingId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Deleting drafts at meeting end requires the caller's meeting transaction and lock.");
        var drafts = await db.VotingSessions
            .Where(v => v.MeetingId == meetingId && v.Status == VotingStatus.Draft)
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Id)
            .Select(v => new
            {
                Vote = v,
                Options = v.Options.OrderBy(o => o.Position).Select(o => o.Label).ToArray(),
                Documents = v.Documents.Select(d => d.MeetingDocumentId).ToArray()
            })
            .ToListAsync(ct);
        return drafts.Select(d => new VoteDraft(d.Vote, d.Options, d.Documents)).ToArray();
    }

    public void Add(VotingSession vote) => db.VotingSessions.Add(vote);
    public void Remove(VotingSession vote) => db.VotingSessions.Remove(vote);
    public void Add(VotingOption option) => db.VotingOptions.Add(option);
    public void RemoveOptions(IEnumerable<VotingOption> options) => db.VotingOptions.RemoveRange(options);
    public void AddEligibleVoters(IEnumerable<VoteEligibleVoter> voters) => db.VoteEligibleVoters.AddRange(voters);
    public void Add(VoteReceipt receipt) => db.VoteReceipts.Add(receipt);
    public void Add(VoteChoice choice) => db.VoteChoices.Add(choice);
    public void AddDocumentLinks(IEnumerable<VoteDocument> links) => db.VoteDocuments.AddRange(links);
    public void Remove(VoteDocument link) => db.VoteDocuments.Remove(link);
    public void Add(DocumentReadConfirmation confirmation) => db.DocumentReadConfirmations.Add(confirmation);

    private static Expression<Func<VotingSession, VoteRow>> Row(VoteViewer viewer)
    {
        var userId = viewer.UserId;
        var guestId = viewer.GuestInvitationId;
        return v => new VoteRow(v.Id, v.Title, v.Description, v.Status, v.IsAnonymous, v.EligibleRoles,
            v.OpenedAt, v.ClosedAt, v.EligibleCount, v.Receipts.Count, v.Documents.Count(d => d.MeetingDocument.RemovedAt == null),
            v.EligibleVoters.Any(e => (userId != null && e.UserId == userId) || (guestId != null && e.GuestInvitationId == guestId)),
            v.Receipts.Any(r => (userId != null && r.VoterId == userId) || (guestId != null && r.GuestInvitationId == guestId)),
            v.Conclusion, v.ConclusionNote, v.ConcludedById, v.ConcludedBy != null ? v.ConcludedBy.FullName : null, v.ConcludedAt);
    }
}
