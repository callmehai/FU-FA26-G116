using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.GuestAccess;
using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Infrastructure.GuestAccess;

/// <summary>UC-04 steps 1–4: open the invitation link, send the one-time code, verify it into a guest session.</summary>
internal sealed class GuestAccessService(TrustCabinetDbContext db, GuestCredentials credentials,
    IOptions<GuestAccessOptions> options, IOptions<EmailDeliveryOptions> email, IEmailSender sender,
    TimeProvider clock, ILogger<GuestAccessService> logger) : IGuestAccessService
{
    private static readonly GuestAccessResult Invalid = new(GuestAccessFailure.InvalidInvitation);
    private static readonly GuestAccessResult Unavailable = new(GuestAccessFailure.Unavailable);
    private static readonly GuestAccessResult InvalidCode = new(GuestAccessFailure.InvalidCode);
    private static readonly GuestAccessResult Locked = new(GuestAccessFailure.CodeLocked);

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
            var last = await db.GuestVerificationChallenges.Where(c => c.InvitationId == invitationId)
                .MaxAsync(c => (DateTimeOffset?)c.CreatedAt, ct);
            var wait = last + TimeSpan.FromSeconds(limits.ResendIntervalSeconds) - now;
            if (wait > TimeSpan.Zero)
                return new(GuestAccessFailure.TooSoon, RetryAfterSeconds: (int)Math.Ceiling(wait.Value.TotalSeconds));
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
        if (await PasswordResetEmailSender.SendNowAsync(sender, email.Value, logger,
                CodeEmail(invitation, code, limits.CodeLifetimeMinutes), "guest verification", ct))
            return new(Data: new GuestCodeSent(limits.CodeLifetimeMinutes * 60, limits.ResendIntervalSeconds));

        // Nobody received this code: drop it so the guest may retry at once and the previous code stays usable.
        try { await db.GuestVerificationChallenges.Where(c => c.Id == challenge.Id).ExecuteDeleteAsync(ct); }
        catch (Exception ex) when (IsPersistence(ex))
        {
            logger.LogError(ex, "Could not remove an undelivered code of guest invitation {InvitationId}.", invitationId);
        }
        return Unavailable;
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
            var challenge = await db.GuestVerificationChallenges.Where(c => c.InvitationId == invitationId)
                .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).FirstOrDefaultAsync(ct);
            if (challenge is null || challenge.ConsumedAt is not null || challenge.ExpiresAt <= now) return InvalidCode;
            if (challenge.FailedAttempts >= limits.MaxFailedAttempts) return Locked;
            if (code is not { Length: 6 } || !code.All(char.IsAsciiDigit)) return InvalidCode;
            challenge.UpdatedAt = now;
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(credentials.CodeHash(challenge.Id, code)),
                    Encoding.ASCII.GetBytes(challenge.CodeHash)))
            {
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
            return new(Data: new GuestVerified(session.Id, invitation.MeetingId, session.ExpiresAt));
        }
        catch (Exception ex) when (IsPersistence(ex))
        {
            logger.LogError(ex, "Could not verify a code for guest invitation {InvitationId}.", invitationId);
            return Unavailable;
        }
    }

    /// <summary>UC-04 EX-1: an invitation works only while it stands and its meeting is Published or InProgress.</summary>
    internal static readonly Expression<Func<MeetingGuestInvitation, bool>> OpenInvitation = g => g.RevokedAt == null
        && (g.Meeting.Status == MeetingStatus.Published || g.Meeting.Status == MeetingStatus.InProgress);

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
        return $"{address[..(at > 2 ? 2 : 1)]}***{address[at..]}";
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
