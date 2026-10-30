using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Domain.GuestAccess;

/// <summary>UC-04 outcomes. Each maps to one message code at the endpoint (docs/03-api/16-truy-cap-khach-uc04.md).</summary>
public enum GuestAccessFailure { None, InvalidInvitation, InvalidCode, CodeLocked, TooSoon, Unavailable, InvalidProfile, SessionEnded }

public sealed record GuestMeetingView(Guid Id, string Title, string Status,
    DateTimeOffset PlannedStartTime, DateTimeOffset PlannedEndTime);
public sealed record GuestInvitationView(Guid InvitationId, GuestMeetingView Meeting, string MaskedEmail);
public sealed record GuestCodeSent(int ExpiresInSeconds, int ResendAfterSeconds);
public sealed record GuestVerified(string AccessToken, DateTimeOffset ExpiresAt, Guid MeetingId, bool ProfileCompleted);
public sealed record GuestSessionView(Guid SessionId, DateTimeOffset ExpiresAt, GuestMeetingView Meeting, string Email,
    string InvitedName, string? DisplayName, string? JobTitle, bool ProfileCompleted);

public sealed record GuestAccessResult(GuestAccessFailure Failure = GuestAccessFailure.None, object? Data = null,
    int RetryAfterSeconds = 0, IReadOnlyDictionary<string, string[]>? Errors = null);

/// <summary>
/// UC-04. The first three calls are anonymous and take the link token, a wrong one being indistinguishable
/// from a missing invitation; the rest act on the caller's own guest session.
/// </summary>
public interface IGuestAccessService
{
    Task<GuestAccessResult> OpenAsync(Guid invitationId, string? linkToken, CancellationToken ct);
    Task<GuestAccessResult> SendCodeAsync(Guid invitationId, string? linkToken, CancellationToken ct);
    Task<GuestAccessResult> VerifyAsync(Guid invitationId, string? linkToken, string? code, CancellationToken ct);
    Task<GuestAccessResult> GetSessionAsync(MeetingGuestSession session, CancellationToken ct);
    Task<GuestAccessResult> SaveProfileAsync(MeetingGuestSession session, string? displayName, string? jobTitle, CancellationToken ct);
    /// <summary>Ends this session only; other sessions of the invitation are untouched. Repeating it is harmless.</summary>
    Task<GuestAccessResult> SignOutAsync(MeetingGuestSession session, CancellationToken ct);
}
