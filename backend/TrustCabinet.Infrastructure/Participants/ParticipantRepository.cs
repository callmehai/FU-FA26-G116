using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Participants;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Participants;

public sealed class ParticipantRepository(TrustCabinetDbContext db) : IParticipantRepository
{
    public async Task<ParticipantSnapshot?> LoadAsync(Guid meetingId, bool forUpdate, CancellationToken ct)
    {
        if (forUpdate)
        {
            var locked = await db.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {meetingId} FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);
            if (locked.Count == 0) return null;
        }
        var meetings = db.Meetings.AsSplitQuery()
            .Include(m => m.Participants.Where(p => p.AssignmentStatus == AssignmentStatus.Current)).ThenInclude(p => p.User).AsQueryable();
        var meeting = await (forUpdate ? meetings : meetings.AsNoTracking()).SingleOrDefaultAsync(m => m.Id == meetingId, ct);
        if (meeting is null) return null;
        var guestQuery = db.MeetingGuestInvitations.Where(g => g.MeetingId == meetingId && g.RevokedAt == null);
        var guests = await (forUpdate ? guestQuery : guestQuery.AsNoTracking()).ToListAsync(ct);
        return new(meeting, guests);
    }

    public async Task LockUserAsync(Guid userId, CancellationToken ct)
        => await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR SHARE")
            .AsNoTracking().ToArrayAsync(ct);

    public async Task<IReadOnlyList<User>> LockUsersAsync(Guid[] userIds, CancellationToken ct)
        => await db.Users.FromSqlInterpolated(
                $"SELECT * FROM users WHERE id = ANY({userIds.Order().ToArray()}) ORDER BY id FOR SHARE")
            .AsNoTracking().ToArrayAsync(ct);

    public async Task<(int Total, IReadOnlyList<ParticipantOption> Items)> ListInvitationOptionsAsync(
        ParticipantSnapshot? snapshot, ParticipantQuery query, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking().Where(u => u.IsActive);
        if (snapshot is not null)
        {
            var excluded = snapshot
                .Meeting.Participants.Where(ParticipantRules.Current)
                .Select(p => p.UserId)
                .ToArray();
            var meetingId = snapshot.Meeting.Id;
            users = users.Where(u =>
                !excluded.Contains(u.Id)
                && !db.MeetingGuestInvitations.Any(g =>
                    g.MeetingId == meetingId && g.RevokedAt == null && g.Email == u.Email
                )
            );
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern =
                "%"
                + query
                    .Search.Trim()
                    .Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("%", "\\%", StringComparison.Ordinal)
                    .Replace("_", "\\_", StringComparison.Ordinal)
                + "%";
            users = users.Where(u =>
                EF.Functions.ILike(u.FullName, pattern, "\\")
                || EF.Functions.ILike(u.Email, pattern, "\\")
            );
        }
        var total = await users.CountAsync(ct);
        var offset = (long)(query.Page - 1) * query.PageSize;
        IReadOnlyList<ParticipantOption> items =
            offset >= total
                ? []
                : await users
                    .OrderBy(u => u.FullName)
                    .ThenBy(u => u.Id)
                    .Skip((int)offset)
                    .Take(query.PageSize)
                    .Select(u => new ParticipantOption(u.Id, u.FullName, u.Email))
                    .ToArrayAsync(ct);
        return (total, items);
    }

    public async Task<IReadOnlyList<Guid>> ListActiveReaderIdsAsync(Guid[] candidateUserIds, CancellationToken ct)
        => await db
            .Users.AsNoTracking()
            .Where(u => candidateUserIds.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id)
            .ToArrayAsync(ct);

    public async Task<IReadOnlyList<AgendaItem>> ListPresentedItemsAsync(
        Guid meetingId, Guid? presenterUserId, Guid? presenterGuestInvitationId, CancellationToken ct)
        => await db.AgendaItems.Where(i => i.MeetingId == meetingId
            && (presenterUserId != null ? i.PresenterId == presenterUserId : i.PresenterGuestInvitationId == presenterGuestInvitationId)).ToListAsync(ct);

    public Task<bool> ExistsActiveGuestWithEmailsAsync(Guid meetingId, string[] emails, CancellationToken ct)
        => db.MeetingGuestInvitations.AnyAsync(
            g => g.MeetingId == meetingId && g.RevokedAt == null && emails.Contains(g.Email), ct);

    public Task<bool> ExistsActiveGuestWithEmailAsync(Guid meetingId, string email, CancellationToken ct)
        => db.MeetingGuestInvitations.AnyAsync(
            g => g.MeetingId == meetingId && g.RevokedAt == null && g.Email == email, ct);

    public Task<bool> ExistsCurrentParticipantWithEmailAsync(Guid meetingId, string email, CancellationToken ct)
        => db.MeetingParticipants.AnyAsync(
            p =>
                p.MeetingId == meetingId
                && p.AssignmentStatus == AssignmentStatus.Current
                && p.User.Email == email,
            ct);

    public void Add(MeetingParticipant participant, User account)
    {
        db.MeetingParticipants.Add(participant);
        // Use the tracked instance already loaded by EF, or attach the account without inserting it.
        participant.User = db.Users.Local.FirstOrDefault(u => u.Id == account.Id) ?? account;
        if (db.Entry(participant.User).State == EntityState.Detached)
            db.Attach(participant.User);
    }

    public void AddMany(IReadOnlyList<(MeetingParticipant Participant, User Account)> items)
    {
        var trackedUsers = db.Users.Local.ToDictionary(u => u.Id);
        var detectChanges = db.ChangeTracker.AutoDetectChangesEnabled;
        // Batch inserts do not need a full graph scan for every outbox payload or user lookup.
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var (participant, account) in items)
            {
                db.MeetingParticipants.Add(participant);
                // Use the tracked instance already loaded by EF, or attach the account without inserting it.
                participant.User = trackedUsers.GetValueOrDefault(account.Id) ?? account;
                if (db.Entry(participant.User).State == EntityState.Detached)
                    db.Attach(participant.User);
            }
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = detectChanges;
        }
    }

    public void Add(MeetingGuestInvitation guest) => db.MeetingGuestInvitations.Add(guest);

    public Task<IReadOnlyList<MeetingGuestInvitation>> ListStagedGuestsAsync(Guid meetingId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<MeetingGuestInvitation>>(
            db.MeetingGuestInvitations.Local.Where(g => g.MeetingId == meetingId).ToList());
    }

    public Task<VotingSession?> FindOpenVoteAsync(Guid meetingId, CancellationToken ct)
        => db.VotingSessions.SingleOrDefaultAsync(
            v => v.MeetingId == meetingId && v.Status == VotingStatus.Open, ct);

    public Task<bool> ExistsVoteReceiptWithEmailAsync(Guid votingSessionId, string email, CancellationToken ct)
        => db.VoteReceipts.AnyAsync(
            r => r.VotingSessionId == votingSessionId && r.GuestInvitation!.Email == email, ct);

    public Task<VoteEligibleVoter?> FindEligibleVoterByEmailAsync(Guid votingSessionId, string email, CancellationToken ct)
        => db.VoteEligibleVoters.FirstOrDefaultAsync(
            e => e.VotingSessionId == votingSessionId && e.GuestInvitation!.Email == email, ct);
}
