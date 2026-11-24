using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Audit;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Application.GuestAccess;

/// <summary>UC-04: open the invitation link, send and verify the one-time code, then the guest's own session and profile.</summary>
internal sealed class GuestAccessService(IGuestSessionRepository sessions, IGuestCredentials credentials,
    IOptions<GuestAccessOptions> options, IGuestVerificationCodeSender codeSender, IUnitOfWork unitOfWork,
    IAuditLogRepository audit, TimeProvider clock, ILogger<GuestAccessService> logger) : IGuestAccessService
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
            var invitation = await unitOfWork.ExecuteAsync(token => sessions.FindOpenAsync(invitationId, token),
                TransactionMode.None, ct);
            return invitation is null ? Invalid
                : new(Data: new GuestInvitationView(invitation.Id, Meeting(invitation.Meeting), Mask(invitation.Email)));
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Could not read guest invitation {InvitationId}.", invitationId);
            return Unavailable;
        }
    }

    public async Task<GuestAccessResult> SendCodeAsync(Guid invitationId, string? linkToken, CancellationToken ct)
    {
        if (!credentials.IsLinkToken(invitationId, linkToken)) return Invalid;
        var limits = options.Value;
        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
        string toAddress = "";
        string invitedName = "";
        string meetingTitle = "";
        GuestVerificationChallenge? challenge = null;
        GuestAccessResult? refusal;
        try
        {
            refusal = await unitOfWork.ExecuteAsync<GuestAccessResult?>(async token =>
            {
                var invitation = await sessions.LockOpenAsync(invitationId, token);
                if (invitation is null) return Invalid;
                var now = clock.GetUtcNow();
                // The codes of the last 24 hours, newest first: the first one sets the resend wait, the count the daily cap.
                var since = now.AddDays(-1);
                var recent = await sessions.ListRecentChallengeTimesAsync(invitationId, since, limits.MaxCodesPerDay, token);
                if (recent.Count > 0 && recent[0].AddSeconds(limits.ResendIntervalSeconds) - now is var wait && wait > TimeSpan.Zero)
                    return new(GuestAccessFailure.TooSoon, RetryAfterSeconds: (int)Math.Ceiling(wait.TotalSeconds));
                if (recent.Count >= limits.MaxCodesPerDay)
                    return new(GuestAccessFailure.DailyLimit,
                        RetryAfterSeconds: (int)Math.Ceiling((recent[^1].AddDays(1) - now).TotalSeconds));
                // Codes older than the window have expired and no longer count, so nothing needs them: the table stays
                // bounded per invitation. A session row, not its code, is the evidence of a verification.
                await sessions.DeleteChallengesNotNewerThanAsync(invitationId, since, token);
                challenge = new GuestVerificationChallenge
                {
                    InvitationId = invitationId, CreatedAt = now, UpdatedAt = now,
                    ExpiresAt = now.AddMinutes(limits.CodeLifetimeMinutes),
                };
                challenge.CodeHash = credentials.CodeHash(challenge.Id, code);
                sessions.AddChallenge(challenge);
                await unitOfWork.SaveChangesAsync(token);
                toAddress = invitation.Email;
                invitedName = invitation.InvitedName;
                meetingTitle = invitation.Meeting.Title;
                return null;
            }, TransactionMode.ReadCommitted, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Could not save a verification code for guest invitation {InvitationId}.", invitationId);
            return Unavailable;
        }
        if (refusal is not null) return refusal;

        // Sent after commit, outside the row lock. The code goes straight to the transport, never into
        // the stored email queue, so the only copy at rest is the hash.
        var sent = false;
        try
        {
            sent = await codeSender.SendAsync(toAddress, invitedName, meetingTitle, code,
                TimeSpan.FromMinutes(limits.CodeLifetimeMinutes), ct);
        }
        finally
        {
            // Nobody received this code (the send failed, or the client went away mid-send): drop it, even
            // after cancellation, so the guest may retry at once and the previous code stays usable.
            if (!sent && challenge is not null)
            {
                try
                {
                    await unitOfWork.ExecuteAsync(token => sessions.DeleteChallengeAsync(challenge.Id, token),
                        TransactionMode.None, CancellationToken.None);
                }
                catch (PersistenceException ex)
                {
                    logger.LogError(ex.InnerException ?? ex, "Could not remove an undelivered code of guest invitation {InvitationId}.", invitationId);
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
            return await unitOfWork.ExecuteAsync(async token =>
            {
                var invitation = await sessions.LockOpenAsync(invitationId, token);
                if (invitation is null) return Invalid;
                var now = clock.GetUtcNow();
                var recent = await sessions.ListLatestChallengesAsync(invitationId, token);
                var challenge = recent.FirstOrDefault();
                if (challenge is null || challenge.ConsumedAt is not null || challenge.ExpiresAt <= now) return InvalidCode;
                if (challenge.FailedAttempts >= limits.MaxFailedAttempts) return Locked;
                // A refusal tells how many tries the live code has left, so the lock (MSG372) does not come unannounced.
                GuestAccessResult Wrong(int failed) => InvalidCode with { RemainingAttempts = limits.MaxFailedAttempts - failed };
                if (code is not { Length: 6 } || !code.All(char.IsAsciiDigit)) return Wrong(challenge.FailedAttempts);
                challenge.UpdatedAt = now;
                if (!Matches(challenge, code))
                {
                    // The code this one replaced, typed before the new email arrived: still refused, but it is the
                    // guest's own code, not a guess, so it does not count against the new one.
                    if (recent is [_, var replaced] && Matches(replaced, code)) return Wrong(challenge.FailedAttempts);
                    challenge.FailedAttempts++;
                    await unitOfWork.SaveChangesAsync(token);
                    return challenge.FailedAttempts >= limits.MaxFailedAttempts ? Locked : Wrong(challenge.FailedAttempts);
                }
                challenge.ConsumedAt = now;
                var session = new GuestSession
                {
                    InvitationId = invitationId, CreatedAt = now, UpdatedAt = now,
                    ExpiresAt = now.AddHours(limits.SessionLifetimeHours),
                };
                sessions.AddSession(session);
                // BR-009–BR-011 — no actor (a Guest has no account); the invitation goes under the key the audit log reads Guests by.
                audit.Add(new AuditLogEntry
                {
                    Action = AuditActions.GuestVerified, ObjectType = AuditActions.GuestSessionObjectType, ObjectId = session.Id,
                    OccurredAt = now, CreatedAt = now, UpdatedAt = now,
                    Details = JsonSerializer.Serialize(new { meetingId = invitation.MeetingId, guestInvitationId = invitationId }),
                });
                await unitOfWork.SaveChangesAsync(token);
                var identity = new MeetingGuestSession(invitation.MeetingId, invitationId, session.Id);
                return new(Data: new GuestVerified(credentials.AccessToken(identity, session.ExpiresAt), session.ExpiresAt,
                    invitation.MeetingId, invitation.SelfDeclaredDisplayName is not null));
            }, TransactionMode.ReadCommitted, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Could not verify a code for guest invitation {InvitationId}.", invitationId);
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
            var current = await unitOfWork.ExecuteAsync(token => sessions.FindSessionAsync(session, tracked: false, token),
                TransactionMode.None, ct);
            return current is null ? Ended : new(Data: View(current));
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Could not read guest session {SessionId}.", session.SessionId);
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
            return await unitOfWork.ExecuteAsync(async token =>
            {
                var current = await sessions.FindSessionAsync(session, tracked: true, token);
                if (current is null) return Ended;
                if (errors.Count > 0) return new(GuestAccessFailure.InvalidProfile, Errors: errors);
                // The name is declared once per invitation (PO 01/11/2026, SCRUM-116): the roster, votes and
                // minutes already show it. Sending the same values again (a retry) is harmless.
                if (current.Invitation.SelfDeclaredDisplayName is { } declared)
                    return declared == name && current.Invitation.SelfDeclaredJobTitle == title
                        ? new(Data: View(current))
                        : new(GuestAccessFailure.ProfileLocked);
                // UC-04: shown as self-declared; InvitedName and Email stay the invitation evidence.
                current.Invitation.SelfDeclaredDisplayName = name;
                current.Invitation.SelfDeclaredJobTitle = title;
                current.Invitation.UpdatedAt = clock.GetUtcNow();
                await unitOfWork.SaveChangesAsync(token);
                return new(Data: View(current));
            }, TransactionMode.None, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Could not save the profile of guest session {SessionId}.", session.SessionId);
            return Unavailable;
        }
    }

    public async Task<GuestAccessResult> SignOutAsync(MeetingGuestSession session, CancellationToken ct)
    {
        try
        {
            await unitOfWork.ExecuteAsync(token => sessions.RevokeSessionAsync(
                session.SessionId, session.InvitationId, clock.GetUtcNow(), token), TransactionMode.None, ct);
            return new();
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Could not end guest session {SessionId}.", session.SessionId);
            return Unavailable;
        }
    }

    private static GuestSessionView View(GuestSession session) => new(session.Id, session.ExpiresAt,
        Meeting(session.Invitation.Meeting), session.Invitation.Email, session.Invitation.InvitedName,
        session.Invitation.SelfDeclaredDisplayName, session.Invitation.SelfDeclaredJobTitle,
        session.Invitation.SelfDeclaredDisplayName is not null,
        session.Invitation.Meeting.Sensitivity != Sensitivity.Secret);

    internal static GuestMeetingView Meeting(Meeting m)
        => new(m.Id, m.Title, m.Status.ToString(), m.PlannedStartTime, m.PlannedEndTime);

    /// <summary>"ng***@example.com": enough for the guest to recognise the address, not enough to learn it.</summary>
    internal static string Mask(string address)
    {
        var at = address.LastIndexOf('@');
        if (at <= 0) return "***";
        return $"{address[..(at > 3 ? 2 : 1)]}***{address[at..]}";
    }
}
