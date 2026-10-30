using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Votes;

namespace TrustCabinet.Infrastructure.Votes;

/// <summary>A vote as the list and the detail show it, with the caller's own standing in it.</summary>
public sealed record VoteRow(Guid Id, string Title, string Description, VotingStatus Status, bool IsAnonymous,
    List<MeetingRole> EligibleRoles, DateTimeOffset? OpenedAt, DateTimeOffset? ClosedAt, int EligibleCount,
    int ReceiptCount, int DocumentCount, bool IsEligible, bool HasVoted, VoteConclusion? Conclusion,
    string ConclusionNote, Guid? ConcludedById, string? ConcludedByName, DateTimeOffset? ConcludedAt);

public sealed class VoteRepository(TrustCabinetDbContext db, IParticipantGuestAccess guests)
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

    /// <summary>The meeting's votes oldest first; drafts only for the Chair and Secretary (BR-253).</summary>
    public async Task<IReadOnlyList<VoteRow>> ListAsync(Guid meetingId, bool includeDrafts, VoteViewer viewer, CancellationToken ct)
        => await db.VotingSessions.AsNoTracking().Where(v => v.MeetingId == meetingId && (includeDrafts || v.Status != VotingStatus.Draft))
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Id).Select(Row(viewer)).ToListAsync(ct);

    public async Task<VoteRow?> FindAsync(Guid meetingId, Guid voteId, VoteViewer viewer, CancellationToken ct)
        => await db.VotingSessions.AsNoTracking().Where(v => v.MeetingId == meetingId && v.Id == voteId)
            .Select(Row(viewer)).SingleOrDefaultAsync(ct);

    /// <summary>
    /// The vote's documents still in the meeting, oldest attached first, with when <paramref name="viewer"/> confirmed
    /// reading each. One removed from the meeting no longer shows or counts (BR-292).
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
    /// Chair and the Secretary, an Open or Closed one for everyone else, since a draft is theirs alone (BR-253).
    /// </summary>
    public Task<bool> IsVisibleVoteDocumentAsync(Guid meetingId, Guid meetingDocumentId, bool includeDrafts, CancellationToken ct)
        => db.VoteDocuments.AnyAsync(l => l.MeetingDocumentId == meetingDocumentId && l.MeetingDocument.MeetingId == meetingId
            && l.MeetingDocument.RemovedAt == null && l.VotingSession.MeetingId == meetingId
            && (includeDrafts || l.VotingSession.Status != VotingStatus.Draft), ct);

    public Task<DocumentReadConfirmation?> FindReadConfirmationAsync(Guid meetingDocumentId, VoteViewer viewer, CancellationToken ct)
        => db.DocumentReadConfirmations.AsNoTracking().SingleOrDefaultAsync(c => c.MeetingDocumentId == meetingDocumentId
            && ((viewer.UserId != null && c.UserId == viewer.UserId)
                || (viewer.GuestInvitationId != null && c.GuestInvitationId == viewer.GuestInvitationId)), ct);

    public async Task<IReadOnlyList<VotingOption>> OptionsAsync(Guid voteId, CancellationToken ct)
        => await db.VotingOptions.AsNoTracking().Where(o => o.VotingSessionId == voteId)
            .OrderBy(o => o.Position).ToListAsync(ct);

    /// <summary>
    /// The tally, counted from the stored choices rather than kept in a column, so it cannot drift from them. Only a
    /// public vote lists who chose what (BR-273); a secret one has nothing that could (BR-259).
    /// </summary>
    public async Task<VoteResult> ResultAsync(Guid meetingId, Guid voteId, bool isAnonymous, CancellationToken ct)
    {
        var options = await OptionsAsync(voteId, ct);
        var counts = await db.VoteChoices.Where(c => c.VotingSessionId == voteId).GroupBy(c => c.VotingOptionId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        var ballots = counts.Values.Sum();
        var tally = options.Select(o => new VoteOptionResult(o.Id, o.Label, counts.GetValueOrDefault(o.Id),
            VoteRules.Percentage(counts.GetValueOrDefault(o.Id), ballots))).ToArray();
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
