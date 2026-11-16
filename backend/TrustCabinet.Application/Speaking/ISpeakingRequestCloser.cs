namespace TrustCabinet.Application.Speaking;

/// <summary>
/// Closes the open speaking requests of people who are no longer there (BR-337). Runs inside the caller's
/// transaction; the caller saves. Implemented by the speaking request repository since CA-13 (SCRUM-178), so the
/// Participants module needs no more of the Speaking module than this.
/// </summary>
public interface ISpeakingRequestCloser
{
    /// <param name="userId">Only this member's requests; with <paramref name="guestInvitationIds"/> null, everyone's.</param>
    /// <param name="guestInvitationIds">Only these guests' requests; null with <paramref name="userId"/> null, everyone's.</param>
    /// <param name="endedAt">When the person left or the meeting ended.</param>
    /// <param name="now">When the rows are written.</param>
    /// <returns>How many requests were closed, so a caller tells the panels only when something changed.</returns>
    Task<int> CloseOpenAsync(Guid meetingId, Guid? userId, IReadOnlyCollection<Guid>? guestInvitationIds,
        DateTimeOffset endedAt, DateTimeOffset now, CancellationToken ct);
}
