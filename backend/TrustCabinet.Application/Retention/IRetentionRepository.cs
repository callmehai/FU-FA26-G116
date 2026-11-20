namespace TrustCabinet.Application.Retention;

/// <summary>
/// What the retention job (ADR-0067) reads and removes in storage. The deletes and the clearing run immediately as one
/// statement each, not staged for <c>IUnitOfWork.SaveChangesAsync</c>: each touches rows nothing else is editing.
/// </summary>
public interface IRetentionRepository
{
    /// <summary>
    /// Which of <paramref name="paths"/> a recording that is not erased, or a sentence, still names. An erased recording's
    /// path does not count: its row says the audio is gone. One query per table.
    /// </summary>
    Task<IReadOnlySet<string>> FindReferencedAudioAsync(IReadOnlyCollection<string> paths, CancellationToken ct);

    /// <summary>Up to <paramref name="take"/> cancelled meetings that still have a vote draft. One query.</summary>
    Task<IReadOnlyList<Guid>> ListCancelledMeetingsWithDraftsAsync(int take, CancellationToken ct);

    /// <summary>Deletes notifications created before <paramref name="createdBefore"/>, and those read and created before
    /// <paramref name="readCreatedBefore"/>; returns how many.</summary>
    Task<int> DeleteNotificationsAsync(DateTimeOffset createdBefore, DateTimeOffset readCreatedBefore, CancellationToken ct);

    /// <summary>Deletes guest verification codes that expired before <paramref name="expiredBefore"/>; returns how many.</summary>
    Task<int> DeleteGuestChallengesAsync(DateTimeOffset expiredBefore, CancellationToken ct);

    /// <summary>
    /// Clears the text, HTML and calendar attachment of emails sent or given up on, created before
    /// <paramref name="createdBefore"/> and not cleared yet; returns how many.
    /// </summary>
    Task<int> ScrubEmailBodiesAsync(DateTimeOffset createdBefore, DateTimeOffset now, CancellationToken ct);
}
