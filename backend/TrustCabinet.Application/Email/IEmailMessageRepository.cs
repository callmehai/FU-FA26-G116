using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Email;

/// <summary>
/// The outgoing email queue: queuing a message, claiming the next one for a worker, recording how its attempt ended
/// and what the provider later reported about it.
/// </summary>
public interface IEmailMessageRepository
{
    /// <summary>Whether a message with this deduplication key has already been queued.</summary>
    Task<bool> ExistsWithDeduplicationKeyAsync(string deduplicationKey, CancellationToken ct = default);

    /// <summary>The id of the message queued under this deduplication key, if any.</summary>
    Task<Guid?> FindIdByDeduplicationKeyAsync(string deduplicationKey, CancellationToken ct = default);

    /// <summary>Stages the message; it is written by the next <c>SaveChangesAsync</c>.</summary>
    void Add(EmailMessage message);

    /// <summary>
    /// Fails every message that has used up its attempts and is still waiting, or whose processing lease expired.
    /// Runs immediately, not staged; returns the number of messages failed. Throws <see cref="NotSupportedException"/>
    /// unless the store is PostgreSQL: the queue relies on its row locks.
    /// </summary>
    Task<int> MarkExhaustedAsFailedAsync(int maxAttempts, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>
    /// Takes an exclusive lock on the oldest message that is due (queued and past its retry time, or processing with
    /// an expired lease) and has attempts left, skipping a message another worker holds, and returns it for update.
    /// Null when none is due. Needs the caller's transaction: the lock lasts until it ends.
    /// </summary>
    Task<EmailMessage?> LockNextDueAsync(int maxAttempts, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Stops change tracking for the message, so later saves leave the row to the lease-checked updates.</summary>
    void Detach(EmailMessage message);

    /// <summary>
    /// Records that the claimed message was sent, if this lease still holds it. Runs immediately, not staged; returns
    /// the number of rows updated (0 when the lease was lost).
    /// </summary>
    Task<int> MarkSentAsync(Guid id, Guid leaseToken, string? providerMessageId, DateTimeOffset sentAt,
        CancellationToken ct = default);

    /// <summary>
    /// Records a failed attempt of the claimed message, if this lease still holds it: back to the queue until
    /// <paramref name="nextAttemptAt"/>, or failed for good. Runs immediately, not staged; returns the number of rows
    /// updated (0 when the lease was lost).
    /// </summary>
    Task<int> MarkAttemptFailedAsync(Guid id, Guid leaseToken, EmailStatus status, string error,
        DateTimeOffset? nextAttemptAt, DateTimeOffset failedAt, CancellationToken ct = default);

    /// <summary>
    /// Stores a provider's delivery report on the message it names (by provider id, or by row id when the report
    /// carries one), unless that message already holds a later report. Runs immediately, not staged; returns the
    /// number of rows updated.
    /// </summary>
    Task<int> RecordDeliveryReportAsync(string providerMessageId, Guid? rowId, EmailDeliveryState state,
        DateTimeOffset occurredAt, string detail, DateTimeOffset updatedAt, CancellationToken ct = default);
}
