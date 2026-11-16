using TrustCabinet.Application.GuestAccess;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// UC-04's invitations, challenges and sessions in lists, as the guest use case reads and stages them.
/// Locks are recorded, never taken; nothing here saves.
/// </summary>
public sealed class FakeGuestSessionRepository : IGuestSessionRepository
{
    public List<MeetingGuestInvitation> Invitations { get; } = [];
    public List<GuestVerificationChallenge> Challenges { get; } = [];
    public List<GuestSession> Sessions { get; } = [];

    /// <summary>Locks in order, as (invitation or session id, exclusive).</summary>
    public List<(Guid Id, bool Exclusive)> Locks { get; } = [];

    /// <summary>The moment reads judge recency and expiry against; tests move it.</summary>
    public DateTimeOffset Now { get; set; }

    /// <summary>Thrown by every read in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    /// <summary>Sessions the last <see cref="RevokeSessionAsync"/> ended, in order.</summary>
    public List<(Guid SessionId, Guid InvitationId, DateTimeOffset At)> Revoked { get; } = [];

    /// <summary>Challenges the deletes removed, in order.</summary>
    public List<Guid> DeletedChallenges { get; } = [];

    public Task<MeetingGuestInvitation?> FindOpenAsync(Guid invitationId, CancellationToken ct)
        => Read(() => Invitations.SingleOrDefault(i => i.Id == invitationId && i.RevokedAt == null
            && Domain.GuestAccess.GuestAccessRules.MeetingOpen(i.Meeting.Status)));

    public Task<MeetingGuestInvitation?> LockOpenAsync(Guid invitationId, CancellationToken ct)
    {
        Locks.Add((invitationId, true));
        return FindOpenAsync(invitationId, ct);
    }

    public Task<IReadOnlyList<DateTimeOffset>> ListRecentChallengeTimesAsync(
        Guid invitationId, DateTimeOffset since, int take, CancellationToken ct)
        => Read<IReadOnlyList<DateTimeOffset>>(() => Challenges
            .Where(c => c.InvitationId == invitationId && c.CreatedAt > since)
            .OrderByDescending(c => c.CreatedAt).Select(c => c.CreatedAt).Take(take).ToArray());

    public Task<int> DeleteChallengesNotNewerThanAsync(Guid invitationId, DateTimeOffset since, CancellationToken ct)
        => Read(() =>
        {
            var old = Challenges.Where(c => c.InvitationId == invitationId && c.CreatedAt <= since).ToList();
            foreach (var challenge in old) Challenges.Remove(challenge);
            DeletedChallenges.AddRange(old.Select(c => c.Id));
            return old.Count;
        });

    public void AddChallenge(GuestVerificationChallenge challenge) => Challenges.Add(challenge);

    public Task<int> DeleteChallengeAsync(Guid challengeId, CancellationToken ct)
        => Read(() =>
        {
            var challenge = Challenges.SingleOrDefault(c => c.Id == challengeId);
            if (challenge is null) return 0;
            Challenges.Remove(challenge);
            DeletedChallenges.Add(challengeId);
            return 1;
        });

    public Task<IReadOnlyList<GuestVerificationChallenge>> ListLatestChallengesAsync(
        Guid invitationId, CancellationToken ct)
        => Read<IReadOnlyList<GuestVerificationChallenge>>(() => Challenges
            .Where(c => c.InvitationId == invitationId)
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).Take(2).ToArray());

    public void AddSession(GuestSession session) => Sessions.Add(session);

    public Task<GuestSession?> FindSessionAsync(MeetingGuestSession session, bool tracked, CancellationToken ct)
        => Read(() => !session.IsValid ? null : Sessions.SingleOrDefault(s =>
            s.Id == session.SessionId && s.InvitationId == session.InvitationId
            && s.RevokedAt == null && s.ExpiresAt > Now && s.Invitation.MeetingId == session.MeetingId
            && s.Invitation.RevokedAt == null
            && Domain.GuestAccess.GuestAccessRules.MeetingOpen(s.Invitation.Meeting.Status)));

    public Task<int> RevokeSessionAsync(Guid sessionId, Guid invitationId, DateTimeOffset now, CancellationToken ct)
        => Read(() =>
        {
            var session = Sessions.SingleOrDefault(s => s.Id == sessionId && s.InvitationId == invitationId
                && s.RevokedAt == null);
            if (session is null) return 0;
            session.RevokedAt = now;
            session.UpdatedAt = now;
            Revoked.Add((sessionId, invitationId, now));
            return 1;
        });

    public Task LockSessionSharedAsync(Guid sessionId, CancellationToken ct)
    {
        Locks.Add((sessionId, false));
        return Fail is { } failure ? Task.FromException(failure) : Task.CompletedTask;
    }

    private Task<T> Read<T>(Func<T> answer) => Fail is { } failure ? Task.FromException<T>(failure) : Task.FromResult(answer());
}

/// <summary>
/// Guest links, code hashes and tokens without secrets: the link check is a lookup, the hash names its
/// inputs, and the token carries the session it was minted for.
/// </summary>
public sealed class FakeGuestCredentials : IGuestCredentials
{
    public HashSet<Guid> ValidLinks { get; } = [];

    /// <summary>The only token <see cref="IsLinkToken"/> accepts, for invitations in <see cref="ValidLinks"/>.</summary>
    public string ExpectedToken { get; set; } = "link";

    public List<(Guid ChallengeId, string Code)> Hashes { get; } = [];

    public bool IsLinkToken(Guid invitationId, string? token)
        => token == ExpectedToken && ValidLinks.Contains(invitationId);

    public string CodeHash(Guid challengeId, string code)
    {
        Hashes.Add((challengeId, code));
        return $"hash:{challengeId:D}:{code}";
    }

    public string AccessToken(MeetingGuestSession session, DateTimeOffset expiresAt)
        => $"token:{session.SessionId:D}:{expiresAt:O}";
}

/// <summary>Guest code deliveries in order, as (address, code); <see cref="Sent"/> is what each answers.</summary>
public sealed class FakeGuestCodeSender : IGuestVerificationCodeSender
{
    public List<(string ToAddress, string Code)> Calls { get; } = [];
    public bool Sent { get; set; } = true;

    public Task<bool> SendAsync(
        string toAddress, string invitedName, string meetingTitle, string code, TimeSpan validFor, CancellationToken ct = default)
    {
        Calls.Add((toAddress, code));
        return Task.FromResult(Sent);
    }
}
