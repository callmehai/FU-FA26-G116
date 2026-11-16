using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Email;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Email;

public sealed class EmailMessageRepository(TrustCabinetDbContext db) : IEmailMessageRepository
{
    public Task<bool> ExistsWithDeduplicationKeyAsync(string deduplicationKey, CancellationToken ct = default)
        => db.EmailMessages.AnyAsync(e => e.DeduplicationKey == deduplicationKey, ct);

    public Task<Guid?> FindIdByDeduplicationKeyAsync(string deduplicationKey, CancellationToken ct = default)
        => db.EmailMessages.Where(e => e.DeduplicationKey == deduplicationKey)
            .Select(e => (Guid?)e.Id)
            .SingleOrDefaultAsync(ct);

    public void Add(EmailMessage message) => db.EmailMessages.Add(message);

    public Task<int> MarkExhaustedAsFailedAsync(int maxAttempts, DateTimeOffset now, CancellationToken ct = default)
    {
        if (!string.Equals(
                db.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                "The durable email queue requires PostgreSQL.");
        }

        return db.EmailMessages
            .Where(item => item.AttemptCount >= maxAttempts
                && (item.Status == EmailStatus.Queued
                    || (item.Status == EmailStatus.Processing && item.LeaseExpiresAt <= now)))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, EmailStatus.Failed)
                    .SetProperty(
                        item => item.LastError,
                        "Email reached the maximum attempt count before delivery was confirmed.")
                    .SetProperty(item => item.NextAttemptAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.LeaseToken, (Guid?)null)
                    .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.UpdatedAt, now),
                ct);
    }

    /// <summary>Exclusive lock (<c>FOR UPDATE SKIP LOCKED</c>) on at most one <c>email_messages</c> row.</summary>
    public async Task<EmailMessage?> LockNextDueAsync(int maxAttempts, DateTimeOffset now, CancellationToken ct = default)
    {
        var candidates = await db.EmailMessages
            .FromSqlInterpolated($"""
                SELECT *
                FROM email_messages
                WHERE attempt_count < {maxAttempts}
                  AND (
                    (status = 'Queued'
                        AND (next_attempt_at IS NULL OR next_attempt_at <= {now}))
                    OR (status = 'Processing' AND lease_expires_at <= {now})
                  )
                ORDER BY created_at, id
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .ToListAsync(ct);
        return candidates.SingleOrDefault();
    }

    public void Detach(EmailMessage message) => db.Entry(message).State = EntityState.Detached;

    public Task<int> MarkSentAsync(Guid id, Guid leaseToken, string? providerMessageId, DateTimeOffset sentAt,
        CancellationToken ct = default)
        => ClaimedMessage(id, leaseToken)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, EmailStatus.Sent)
                    .SetProperty(item => item.SentAt, sentAt)
                    .SetProperty(item => item.ProviderMessageId, providerMessageId)
                    .SetProperty(item => item.LastError, string.Empty)
                    .SetProperty(item => item.NextAttemptAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.LeaseToken, (Guid?)null)
                    .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.UpdatedAt, sentAt),
                ct);

    public Task<int> MarkAttemptFailedAsync(Guid id, Guid leaseToken, EmailStatus status, string error,
        DateTimeOffset? nextAttemptAt, DateTimeOffset failedAt, CancellationToken ct = default)
        => ClaimedMessage(id, leaseToken)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, status)
                    .SetProperty(item => item.LastError, error)
                    .SetProperty(item => item.NextAttemptAt, nextAttemptAt)
                    .SetProperty(item => item.LeaseToken, (Guid?)null)
                    .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.UpdatedAt, failedAt),
                ct);

    public Task<int> RecordDeliveryReportAsync(string providerMessageId, Guid? rowId, EmailDeliveryState state,
        DateTimeOffset occurredAt, string detail, DateTimeOffset updatedAt, CancellationToken ct = default)
        => db.EmailMessages
            .Where(m => (m.ProviderMessageId == providerMessageId
                    || (rowId != null && m.Id == rowId))
                && (m.DeliveryStateAt == null || m.DeliveryStateAt <= occurredAt))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(m => m.DeliveryState, state)
                    .SetProperty(m => m.DeliveryStateAt, occurredAt)
                    .SetProperty(m => m.DeliveryDetail, detail)
                    .SetProperty(m => m.ProviderMessageId, providerMessageId)
                    .SetProperty(m => m.UpdatedAt, updatedAt),
                ct);

    private IQueryable<EmailMessage> ClaimedMessage(Guid id, Guid leaseToken)
        => db.EmailMessages.Where(item =>
            item.Id == id
            && item.Status == EmailStatus.Processing
            && item.LeaseToken == leaseToken);
}
