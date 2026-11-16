using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.GuestAccess;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.GuestAccess;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.GuestAccess;

public sealed class GuestSessionRepository(TrustCabinetDbContext db, TimeProvider? clock = null) : IGuestSessionRepository
{
    private DateTimeOffset Now => (clock ?? TimeProvider.System).GetUtcNow();

    /// <summary>UC-04 EX-1: an invitation works only while it stands and its meeting is Published or InProgress.</summary>
    private static readonly Expression<Func<MeetingGuestInvitation, bool>> OpenInvitation = g => g.RevokedAt == null
        && GuestAccessRules.OpenStatuses.Contains(g.Meeting.Status);

    /// <summary>
    /// Not logged out, not expired, invitation standing, meeting open to guests (<see cref="GuestAccessRules"/>).
    /// Shared by the guest reads (agenda, roster, call room, speech clips) through this one definition.
    /// </summary>
    internal static IQueryable<GuestSession> CurrentSessions(TrustCabinetDbContext db, DateTimeOffset now)
        => db.GuestSessions.AsNoTracking().Where(x => x.RevokedAt == null && x.ExpiresAt > now && x.Invitation.RevokedAt == null
            && GuestAccessRules.OpenStatuses.Contains(x.Invitation.Meeting.Status));

    public Task<MeetingGuestInvitation?> FindOpenAsync(Guid invitationId, CancellationToken ct)
        => db.MeetingGuestInvitations.AsNoTracking().Include(g => g.Meeting).Where(OpenInvitation)
            .SingleOrDefaultAsync(g => g.Id == invitationId, ct);

    /// <summary>Serializes sends and verifications of one invitation, and waits out a concurrent revocation.</summary>
    public async Task<MeetingGuestInvitation?> LockOpenAsync(Guid invitationId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Locking the invitation requires the caller's guest transaction.");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM meeting_guest_invitations WHERE id = {invitationId} FOR UPDATE", ct);
        return await FindOpenAsync(invitationId, ct);
    }

    public async Task<IReadOnlyList<DateTimeOffset>> ListRecentChallengeTimesAsync(
        Guid invitationId, DateTimeOffset since, int take, CancellationToken ct)
        => await db.GuestVerificationChallenges.Where(c => c.InvitationId == invitationId && c.CreatedAt > since)
            .OrderByDescending(c => c.CreatedAt).Select(c => c.CreatedAt).Take(take).ToListAsync(ct);

    public Task<int> DeleteChallengesNotNewerThanAsync(Guid invitationId, DateTimeOffset since, CancellationToken ct)
        => db.GuestVerificationChallenges.Where(c => c.InvitationId == invitationId && c.CreatedAt <= since)
            .ExecuteDeleteAsync(ct);

    public void AddChallenge(GuestVerificationChallenge challenge)
        => db.GuestVerificationChallenges.Add(challenge);

    public Task<int> DeleteChallengeAsync(Guid challengeId, CancellationToken ct)
        => db.GuestVerificationChallenges.Where(c => c.Id == challengeId).ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<GuestVerificationChallenge>> ListLatestChallengesAsync(
        Guid invitationId, CancellationToken ct)
        => await db.GuestVerificationChallenges.Where(c => c.InvitationId == invitationId)
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).Take(2).ToListAsync(ct);

    public void AddSession(GuestSession session) => db.GuestSessions.Add(session);

    /// <summary>The caller's own session while it still grants access; its token's claims must all agree.</summary>
    public Task<GuestSession?> FindSessionAsync(MeetingGuestSession session, bool tracked, CancellationToken ct)
    {
        if (!session.IsValid) return Task.FromResult<GuestSession?>(null);
        var query = CurrentSessions(db, Now).Include(x => x.Invitation).ThenInclude(g => g.Meeting)
            .Where(x => x.Id == session.SessionId && x.InvitationId == session.InvitationId && x.Invitation.MeetingId == session.MeetingId);
        return (tracked ? query.AsTracking() : query).SingleOrDefaultAsync(ct);
    }

    public Task<int> RevokeSessionAsync(Guid sessionId, Guid invitationId, DateTimeOffset now, CancellationToken ct)
        => db.GuestSessions
            .Where(x => x.Id == sessionId && x.InvitationId == invitationId && x.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAt, now).SetProperty(x => x.UpdatedAt, now), ct);

    public async Task LockSessionSharedAsync(Guid sessionId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Locking the session requires the caller's comment transaction.");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM guest_sessions WHERE id = {sessionId} FOR SHARE", ct);
    }
}
