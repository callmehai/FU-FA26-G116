namespace TrustCabinet.Application.Retention;

/// <summary>
/// ADR-0067 — the periodic clean-up of data nothing needs any more (Luật 91/2025/QH15: personal data is kept only as long
/// as its purpose). Each rule is its own call, so a failure in one leaves the others to run; <see cref="RunAsync"/> runs
/// them all. Never touched: meeting status history and the audit log (append-only evidence), issued minutes, guest
/// sessions, and voice samples, which follow their consent rather than a clock.
/// </summary>
public interface IDataRetentionService
{
    /// <summary>Every rule once; a rule that throws is logged and the next one still runs.</summary>
    Task<DataRetentionRun> RunAsync(CancellationToken ct = default);

    /// <summary>
    /// Audio files under the recordings folder that no live recording and no sentence points at, last written longer ago
    /// than the grace period: left by a failed delete, an upload cut off, or a crash in the middle of a mix.
    /// </summary>
    Task<(int Files, long Bytes)> DeleteOrphanAudioAsync(CancellationToken ct = default);

    /// <summary>Vote drafts still left in a cancelled meeting (a cancel now deletes them; this catches older ones).</summary>
    Task<int> DeleteCancelledMeetingDraftsAsync(CancellationToken ct = default);

    /// <summary>Notifications read and older than the read limit, and any older than the overall limit.</summary>
    Task<int> PruneNotificationsAsync(CancellationToken ct = default);

    /// <summary>Guest verification codes expired for longer than the limit. Guest sessions stay as evidence.</summary>
    Task<int> PruneGuestChallengesAsync(CancellationToken ct = default);

    /// <summary>
    /// The text, HTML and calendar attachment of emails sent (or given up on) longer ago than the limit. The row stays:
    /// to whom, the subject, when, and whether it was delivered.
    /// </summary>
    Task<int> ScrubEmailBodiesAsync(CancellationToken ct = default);
}

public sealed record DataRetentionRun(int OrphanAudioFiles, long OrphanAudioBytes, int CancelledMeetingDrafts,
    int Notifications, int GuestChallenges, int EmailBodies);
