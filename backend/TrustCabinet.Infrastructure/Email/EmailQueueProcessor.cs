using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Email;

internal enum EmailProcessingOutcome
{
    NoWork,
    Sent,
    RetryScheduled,
    PermanentlyFailed,
    LeaseLost,
}

internal sealed record EmailProcessingResult(EmailProcessingOutcome Outcome, int AttemptCount = 0);

internal sealed class EmailQueueProcessor(
    TrustCabinetDbContext db,
    IEmailSender sender,
    IOptions<EmailDeliveryOptions> options,
    TimeProvider time,
    ILogger<EmailQueueProcessor> logger)
{
    public async Task<EmailProcessingResult> ProcessNextAsync(CancellationToken ct = default)
    {
        var message = await ClaimNextAsync(ct);
        if (message is null)
        {
            return new EmailProcessingResult(EmailProcessingOutcome.NoWork);
        }

        try
        {
            await sender.SendAsync(message, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Do not release the claim during forced shutdown. The persisted lease makes the
            // attempt recoverable without allowing a second worker to send it immediately.
            throw;
        }
        catch (Exception exception)
        {
            return await HandleSendFailureAsync(message, exception, ct);
        }

        // Finalization errors deliberately escape to the worker. SMTP may already have accepted
        // the message, so recording that database error as a send failure would be misleading.
        var sentAt = time.GetUtcNow();
        if (!await FinalizeSentAsync(message.Id, message.LeaseToken!.Value, sentAt, ct))
        {
            return LeaseLost(message);
        }

        logger.LogInformation(
            "Sent invitation email {EmailMessageId} for meeting {MeetingId} to {ToAddress} "
            + "on attempt {AttemptCount}.",
            message.Id,
            message.MeetingId,
            message.ToAddress,
            message.AttemptCount);
        return new EmailProcessingResult(EmailProcessingOutcome.Sent, message.AttemptCount);
    }

    private async Task<EmailProcessingResult> HandleSendFailureAsync(
        EmailMessage message,
        Exception exception,
        CancellationToken ct)
    {
        var failedAt = time.GetUtcNow();
        var exhausted = message.AttemptCount >= options.Value.MaxAttempts;
        var nextAttemptAt = exhausted
            ? (DateTimeOffset?)null
            : failedAt.Add(RetryDelay(message.AttemptCount));
        var error = Truncate($"{exception.GetType().Name}: {exception.Message}", 2000);

        if (!await FinalizeFailureAsync(
                message.Id,
                message.LeaseToken!.Value,
                exhausted,
                error,
                nextAttemptAt,
                failedAt,
                ct))
        {
            return LeaseLost(message);
        }

        if (exhausted)
        {
            logger.LogError(
                exception,
                "Invitation email {EmailMessageId} for meeting {MeetingId} permanently failed "
                + "after {AttemptCount} attempts.",
                message.Id,
                message.MeetingId,
                message.AttemptCount);
            return new EmailProcessingResult(
                EmailProcessingOutcome.PermanentlyFailed, message.AttemptCount);
        }

        logger.LogWarning(
            exception,
            "Invitation email {EmailMessageId} for meeting {MeetingId} failed on attempt "
            + "{AttemptCount}; retry is scheduled for {NextAttemptAt}.",
            message.Id,
            message.MeetingId,
            message.AttemptCount,
            nextAttemptAt);
        return new EmailProcessingResult(
            EmailProcessingOutcome.RetryScheduled, message.AttemptCount);
    }

    private async Task<EmailMessage?> ClaimNextAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var leaseToken = Guid.NewGuid();
        EmailMessage? message;

        if (db.Database.IsRelational())
        {
            if (!string.Equals(
                    db.Database.ProviderName,
                    "Npgsql.EntityFrameworkCore.PostgreSQL",
                    StringComparison.Ordinal))
            {
                throw new NotSupportedException(
                    "The durable email queue currently requires PostgreSQL.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var candidates = await db.EmailMessages
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM email_messages
                    WHERE
                        (status = 'Queued'
                            AND (next_attempt_at IS NULL OR next_attempt_at <= {now}))
                        OR (status = 'Processing' AND lease_expires_at <= {now})
                    ORDER BY created_at, id
                    FOR UPDATE SKIP LOCKED
                    LIMIT 1
                    """)
                .ToListAsync(ct);
            message = candidates.SingleOrDefault();

            if (message is not null)
            {
                Claim(message, leaseToken, now);
                await db.SaveChangesAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
        else
        {
            // The non-relational path exists for isolated unit tests. Production uses the locked
            // PostgreSQL query above so two application instances cannot claim the same row.
            message = await db.EmailMessages
                .Where(item =>
                    (item.Status == EmailStatus.Queued
                        && (item.NextAttemptAt == null || item.NextAttemptAt <= now))
                    || (item.Status == EmailStatus.Processing && item.LeaseExpiresAt <= now))
                .OrderBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .FirstOrDefaultAsync(ct);

            if (message is not null)
            {
                Claim(message, leaseToken, now);
                await db.SaveChangesAsync(ct);
            }
        }

        if (message is not null)
        {
            db.Entry(message).State = EntityState.Detached;
        }

        return message;
    }

    private void Claim(EmailMessage message, Guid leaseToken, DateTimeOffset now)
    {
        message.Status = EmailStatus.Processing;
        message.AttemptCount++;
        message.NextAttemptAt = null;
        message.LeaseToken = leaseToken;
        message.LeaseExpiresAt = now.AddSeconds(options.Value.ProcessingLeaseSeconds);
        message.UpdatedAt = now;
    }

    private async Task<bool> FinalizeSentAsync(
        Guid id,
        Guid leaseToken,
        DateTimeOffset sentAt,
        CancellationToken ct)
    {
        if (db.Database.IsRelational())
        {
            var updated = await ClaimedMessage(id, leaseToken)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.Status, EmailStatus.Sent)
                        .SetProperty(item => item.SentAt, sentAt)
                        .SetProperty(item => item.LastError, string.Empty)
                        .SetProperty(item => item.NextAttemptAt, (DateTimeOffset?)null)
                        .SetProperty(item => item.LeaseToken, (Guid?)null)
                        .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                        .SetProperty(item => item.UpdatedAt, sentAt),
                    ct);
            return updated == 1;
        }

        var message = await ClaimedMessage(id, leaseToken).SingleOrDefaultAsync(ct);
        if (message is null)
        {
            return false;
        }

        message.Status = EmailStatus.Sent;
        message.SentAt = sentAt;
        message.LastError = string.Empty;
        message.NextAttemptAt = null;
        message.LeaseToken = null;
        message.LeaseExpiresAt = null;
        message.UpdatedAt = sentAt;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<bool> FinalizeFailureAsync(
        Guid id,
        Guid leaseToken,
        bool exhausted,
        string error,
        DateTimeOffset? nextAttemptAt,
        DateTimeOffset failedAt,
        CancellationToken ct)
    {
        var status = exhausted ? EmailStatus.Failed : EmailStatus.Queued;
        if (db.Database.IsRelational())
        {
            var updated = await ClaimedMessage(id, leaseToken)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.Status, status)
                        .SetProperty(item => item.LastError, error)
                        .SetProperty(item => item.NextAttemptAt, nextAttemptAt)
                        .SetProperty(item => item.LeaseToken, (Guid?)null)
                        .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                        .SetProperty(item => item.UpdatedAt, failedAt),
                    ct);
            return updated == 1;
        }

        var message = await ClaimedMessage(id, leaseToken).SingleOrDefaultAsync(ct);
        if (message is null)
        {
            return false;
        }

        message.Status = status;
        message.LastError = error;
        message.NextAttemptAt = nextAttemptAt;
        message.LeaseToken = null;
        message.LeaseExpiresAt = null;
        message.UpdatedAt = failedAt;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<EmailMessage> ClaimedMessage(Guid id, Guid leaseToken)
        => db.EmailMessages.Where(item =>
            item.Id == id
            && item.Status == EmailStatus.Processing
            && item.LeaseToken == leaseToken);

    private EmailProcessingResult LeaseLost(EmailMessage message)
    {
        logger.LogWarning(
            "Email attempt {AttemptCount} for {EmailMessageId} could not be finalized because "
            + "its processing lease was no longer owned by this worker.",
            message.AttemptCount,
            message.Id);
        return new EmailProcessingResult(EmailProcessingOutcome.LeaseLost, message.AttemptCount);
    }

    private TimeSpan RetryDelay(int attemptCount)
    {
        var multiplier = 1 << Math.Min(Math.Max(attemptCount - 1, 0), 10);
        return TimeSpan.FromSeconds(checked(options.Value.RetryDelaySeconds * multiplier));
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
