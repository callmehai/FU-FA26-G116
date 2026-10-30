namespace TrustCabinet.Domain.GuestAccess;

/// <summary>UC-04 outcomes. Each maps to one message code at the endpoint (docs/03-api/16-truy-cap-khach-uc04.md).</summary>
public enum GuestAccessFailure { None, InvalidInvitation, InvalidCode, CodeLocked, TooSoon, Unavailable }

public sealed record GuestMeetingView(Guid Id, string Title, string Status,
    DateTimeOffset PlannedStartTime, DateTimeOffset PlannedEndTime);
public sealed record GuestInvitationView(Guid InvitationId, GuestMeetingView Meeting, string MaskedEmail);
public sealed record GuestCodeSent(int ExpiresInSeconds, int ResendAfterSeconds);
public sealed record GuestVerified(Guid SessionId, Guid MeetingId, DateTimeOffset ExpiresAt);

public sealed record GuestAccessResult(GuestAccessFailure Failure = GuestAccessFailure.None, object? Data = null,
    int RetryAfterSeconds = 0);

/// <summary>
/// The anonymous half of UC-04: everything a guest does with the invitation link before holding a
/// guest token. Every call takes the link token; a wrong one is indistinguishable from a missing invitation.
/// </summary>
public interface IGuestAccessService
{
    Task<GuestAccessResult> OpenAsync(Guid invitationId, string? linkToken, CancellationToken ct);
    Task<GuestAccessResult> SendCodeAsync(Guid invitationId, string? linkToken, CancellationToken ct);
    Task<GuestAccessResult> VerifyAsync(Guid invitationId, string? linkToken, string? code, CancellationToken ct);
}
