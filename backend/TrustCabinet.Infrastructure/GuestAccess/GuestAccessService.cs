using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.GuestAccess;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Infrastructure.GuestAccess;

/// <summary>UC-04: open the invitation link, send and verify the one-time code, then the guest's own session and profile.</summary>
internal sealed class GuestAccessService(TrustCabinetDbContext db, GuestCredentials credentials,
    IOptions<GuestAccessOptions> options, IOptions<EmailDeliveryOptions> email, IEmailSender sender,
    TimeProvider clock, ILogger<GuestAccessService> logger) : IGuestAccessService
{
    private static readonly GuestAccessResult Invalid = new(GuestAccessFailure.InvalidInvitation);
    private static readonly GuestAccessResult Unavailable = new(GuestAccessFailure.Unavailable);
    private static readonly GuestAccessResult InvalidCode = new(GuestAccessFailure.InvalidCode);
    private static readonly GuestAccessResult Locked = new(GuestAccessFailure.CodeLocked);
    private static readonly GuestAccessResult Ended = new(GuestAccessFailure.SessionEnded);
    private const int ProfileFieldLength = 200;

    public async Task<GuestAccessResult> OpenAsync(Guid invitationId, string? linkToken, CancellationToken ct)
    {
        if (!credentials.IsLinkToken(invitationId, linkToken)) return Invalid;
        try
        {
            var invitation = await FindOpenAsync(invitationId, ct);
            return invitation is null ? Invalid
                : new(Data: new GuestInvitationView(invitation.Id, Meeting(invitation.Meeting), Mask(invitation.Email)));
        }
        catch (Exception ex) when (IsPersistence(ex))
        {
            logger.LogError(ex, "Could not read guest invitation {InvitationId}.", invitationId);
            return Unavailable;
        }
    }

    public async Task<GuestAccessResult> SendCodeAsync(Guid invitationId, string? linkToken, CancellationToken ct)
    {
        if (!credentials.IsLinkToken(invitationId, linkToken)) return Invalid;
        var limits = options.Value;
        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
        MeetingGuestInvitation? invitation;
        GuestVerificationChallenge challenge;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            invitation = await LockOpenAsync(invitationId, ct);
            if (invitation is null) return Invalid;
            var now = clock.GetUtcNow();
            // The codes of the last 24 hours, newest first: the first one sets the resend wait, the count the daily cap.
            var since = now.AddDays(-1);
            var recent = await db.GuestVerificationChallenges.Where(c => c.InvitationId == invitationId && c.CreatedAt > since)
                .OrderByDescending(c => c.CreatedAt).Select(c => c.CreatedAt).Take(limits.MaxCodesPerDay).ToListAsync(ct);
            if (recent.Count > 0 && recent[0].AddSeconds(limits.ResendIntervalSeconds) - now is var wait && wait > TimeSpan.Zero)
                return new(GuestAccessFailure.TooSoon, RetryAfterSeconds: (int)Math.Ceiling(wait.TotalSeconds));
            if (recent.Count >= limits.MaxCodesPerDay)
                return new(GuestAccessFailure.DailyLimit,
                    RetryAfterSeconds: (int)Math.Ceiling((recent[^1].AddDays(1) - now).TotalSeconds));
            // Codes older than the window have expired and no longer count, so nothing needs them: the table stays
            // bounded per invitation. A session row, not its code, is the evidence of a verification.
            await db.GuestVerificationChallenges.Where(c => c.InvitationId == invitationId && c.CreatedAt <= since)
                .ExecuteDeleteAsync(ct);
            challenge = new GuestVerificationChallenge
            {
                InvitationId = invitationId, CreatedAt = now, UpdatedAt = now,
                ExpiresAt = now.AddMinutes(limits.CodeLifetimeMinutes),
            };
            challenge.CodeHash = credentials.CodeHash(challenge.Id, code);
            db.GuestVerificationChallenges.Add(challenge);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (IsPersistence(ex))
        {
            logger.LogError(ex, "Could not save a verification code for guest invitation {InvitationId}.", invitationId);
            return Unavailable;
        }

        // Sent after commit, outside the row lock. The code goes straight to the transport, never into
        // the stored email queue, so the only copy at rest is the hash.
        var sent = false;
        try
        {
            sent = await PasswordResetEmailSender.SendNowAsync(sender, email.Value, logger,
                CodeEmail(invitation, code, limits.CodeLifetimeMinutes), "guest verification", ct);
        }
        finally
        {
            // Nobody received this code (the send failed, or the client went away mid-send): drop it, even
            // after cancellation, so the guest may retry at once and the previous code stays usable.
            if (!sent)
            {
                try { await db.GuestVerificationChallenges.Where(c => c.Id == challenge.Id).ExecuteDeleteAsync(CancellationToken.None); }
                catch (Exception ex) when (IsPersistence(ex))
                {
                    logger.LogError(ex, "Could not remove an undelivered code of guest invitation {InvitationId}.", invitationId);
                }
            }
        }
        return sent ? new(Data: new GuestCodeSent(limits.CodeLifetimeMinutes * 60, limits.ResendIntervalSeconds)) : Unavailable;
    }

    public async Task<GuestAccessResult> VerifyAsync(Guid invitationId, string? linkToken, string? code, CancellationToken ct)
    {
        if (!credentials.IsLinkToken(invitationId, linkToken)) return Invalid;
        var limits = options.Value;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var invitation = await LockOpenAsync(invitationId, ct);
            if (invitation is null) return Invalid;
            var now = clock.GetUtcNow();
            var recent = await db.GuestVerificationChallenges.Where(c => c.InvitationId == invitationId)
                .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).Take(2).ToListAsync(ct);
            var challenge = recent.FirstOrDefault();
            if (challenge is null || challenge.ConsumedAt is not null || challenge.ExpiresAt <= now) return InvalidCode;
            if (challenge.FailedAttempts >= limits.MaxFailedAttempts) return Locked;
            if (code is not { Length: 6 } || !code.All(char.IsAsciiDigit)) return InvalidCode;
            challenge.UpdatedAt = now;
            if (!Matches(challenge, code))
            {
                // The code this one replaced, typed before the new email arrived: still refused, but it is the
                // guest's own code, not a guess, so it does not count against the new one.
                if (recent is [_, var replaced] && Matches(replaced, code)) return InvalidCode;
                challenge.FailedAttempts++;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return challenge.FailedAttempts >= limits.MaxFailedAttempts ? Locked : InvalidCode;
            }
            challenge.ConsumedAt = now;
            var session = new GuestSession
            {
                InvitationId = invitationId, CreatedAt = now, UpdatedAt = now,
                ExpiresAt = now.AddHours(limits.SessionLifetimeHours),
            };
            db.GuestSessions.Add(session);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            var identity = new MeetingGuestSession(invitation.MeetingId, invitationId, session.Id);
            return new(Data: new GuestVerified(credentials.AccessToken(identity, session.ExpiresAt), session.ExpiresAt,
                invitation.MeetingId, invitation.SelfDeclaredDisplayName is not null));
        }
        catch (Exception ex) when (IsPersistence(ex))
        {
            logger.LogError(ex, "Could not verify a code for guest invitation {InvitationId}.", invitationId);
            return Unavailable;
        }
    }

    private bool Matches(GuestVerificationChallenge challenge, string code)
        => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(credentials.CodeHash(challenge.Id, code)),
            Encoding.ASCII.GetBytes(challenge.CodeHash));

    public async Task<GuestAccessResult> GetSessionAsync(MeetingGuestSession session, CancellationToken ct)
    {
        try
        {
            var current = await FindSessionAsync(session, tracked: false, ct);
            return current is null ? Ended : new(Data: View(current));
        }
        catch (Exception ex) when (IsPersistence(ex))
        {
            logger.LogError(ex, "Could not read guest session {SessionId}.", session.SessionId);
            return Unavailable;
        }
    }

    public async Task<GuestAccessResult> SaveProfileAsync(MeetingGuestSession session, string? displayName, string? jobTitle,
        CancellationToken ct)
    {
        var name = displayName?.Trim() ?? "";
        var title = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim();
        var errors = new Dictionary<string, string[]>();
        // Shown to every member of the meeting: control and format characters (NUL, U+202E…) are refused,
        // as for document names, so a name cannot hide or reverse text on the roster.
        if (name.Length is 0 or > ProfileFieldLength || name.Any(DocumentFileRules.IsInvisible))
            errors["displayName"] = [$"Tên hiển thị bắt buộc, tối đa {ProfileFieldLength} ký tự, không chứa ký tự điều khiển."];
        if (title?.Length > ProfileFieldLength || title?.Any(DocumentFileRules.IsInvisible) == true)
            errors["jobTitle"] = [$"Chức vụ tối đa {ProfileFieldLength} ký tự, không chứa ký tự điều khiển."];
        try
        {
            var current = await FindSessionAsync(session, tracked: true, ct);
            if (current is null) return Ended;
            if (errors.Count > 0) return new(GuestAccessFailure.InvalidProfile, Errors: errors);
            // BR-180: shown as self-declared; InvitedName and Email stay the invitation evidence.
            current.Invitation.SelfDeclaredDisplayName = name;
            current.Invitation.SelfDeclaredJobTitle = title;
            current.Invitation.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return new(Data: View(current));
        }
        catch (Exception ex) when (IsPersistence(ex))
        {
            logger.LogError(ex, "Could not save the profile of guest session {SessionId}.", session.SessionId);
            return Unavailable;
        }
    }

    public async Task<GuestAccessResult> SignOutAsync(MeetingGuestSession session, CancellationToken ct)
    {
        try
        {
            var now = clock.GetUtcNow();
            await db.GuestSessions
                .Where(x => x.Id == session.SessionId && x.InvitationId == session.InvitationId && x.RevokedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAt, now).SetProperty(x => x.UpdatedAt, now), ct);
            return new();
        }
        catch (Exception ex) when (IsPersistence(ex))
        {
            logger.LogError(ex, "Could not end guest session {SessionId}.", session.SessionId);
            return Unavailable;
        }
    }

    /// <summary>The caller's own session while it still grants access; its token's claims must all agree.</summary>
    private Task<GuestSession?> FindSessionAsync(MeetingGuestSession session, bool tracked, CancellationToken ct)
    {
        if (!session.IsValid) return Task.FromResult<GuestSession?>(null);
        var query = CurrentSessions(db, clock.GetUtcNow()).Include(x => x.Invitation).ThenInclude(g => g.Meeting)
            .Where(x => x.Id == session.SessionId && x.InvitationId == session.InvitationId && x.Invitation.MeetingId == session.MeetingId);
        return (tracked ? query.AsTracking() : query).SingleOrDefaultAsync(ct);
    }

    private static GuestSessionView View(GuestSession session) => new(session.Id, session.ExpiresAt,
        Meeting(session.Invitation.Meeting), session.Invitation.Email, session.Invitation.InvitedName,
        session.Invitation.SelfDeclaredDisplayName, session.Invitation.SelfDeclaredJobTitle,
        session.Invitation.SelfDeclaredDisplayName is not null);

    /// <summary>UC-04 EX-1: an invitation works only while it stands and its meeting is Published or InProgress.</summary>
    internal static readonly Expression<Func<MeetingGuestInvitation, bool>> OpenInvitation = g => g.RevokedAt == null
        && GuestAccessRules.OpenStatuses.Contains(g.Meeting.Status);

    /// <summary>
    /// Not logged out, not expired, invitation standing, meeting open to guests (<see cref="GuestAccessRules"/>).
    /// </summary>
    internal static IQueryable<GuestSession> CurrentSessions(TrustCabinetDbContext db, DateTimeOffset now)
        => db.GuestSessions.AsNoTracking().Where(x => x.RevokedAt == null && x.ExpiresAt > now && x.Invitation.RevokedAt == null
            && GuestAccessRules.OpenStatuses.Contains(x.Invitation.Meeting.Status));

    private Task<MeetingGuestInvitation?> FindOpenAsync(Guid invitationId, CancellationToken ct)
        => db.MeetingGuestInvitations.AsNoTracking().Include(g => g.Meeting).Where(OpenInvitation)
            .SingleOrDefaultAsync(g => g.Id == invitationId, ct);

    /// <summary>Serializes sends and verifications of one invitation, and waits out a concurrent revocation.</summary>
    private async Task<MeetingGuestInvitation?> LockOpenAsync(Guid invitationId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM meeting_guest_invitations WHERE id = {invitationId} FOR UPDATE", ct);
        return await FindOpenAsync(invitationId, ct);
    }

    internal static GuestMeetingView Meeting(Meeting m)
        => new(m.Id, m.Title, m.Status.ToString(), m.PlannedStartTime, m.PlannedEndTime);

    /// <summary>"ng***@example.com": enough for the guest to recognise the address, not enough to learn it.</summary>
    internal static string Mask(string address)
    {
        var at = address.LastIndexOf('@');
        if (at <= 0) return "***";
        return $"{address[..(at > 3 ? 2 : 1)]}***{address[at..]}";
    }

    internal static bool IsPersistence(Exception ex) => ex is DbUpdateException or NpgsqlException or TimeoutException;

    private static EmailMessage CodeEmail(MeetingGuestInvitation invitation, string code, int minutes) => new()
    {
        // Never stored — the field is required by the type.
        DeduplicationKey = "guest-verification",
        ToAddress = invitation.Email,
        Subject = "Mã xác minh truy cập cuộc họp TrustCabinet",
        Body = $"""
            Xin chào {invitation.InvitedName},

            Mã xác minh để vào cuộc họp "{invitation.Meeting.Title}" là: {code}

            Mã có hiệu lực trong {minutes} phút và chỉ dùng được một lần. Nếu bạn không yêu cầu mã này, hãy bỏ qua email.
            """,
    };
}
