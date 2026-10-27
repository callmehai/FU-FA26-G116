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

internal interface IEmailQueueProcessor
{
    Task<EmailProcessingResult> ProcessNextAsync(CancellationToken ct = default);
}

internal sealed class EmailQueueProcessor(
    TrustCabinetDbContext db,
    IEmailSender sender,
    IOptions<EmailDeliveryOptions> options,
    TimeProvider time,
    ILogger<EmailQueueProcessor> logger) : IEmailQueueProcessor
{
    public async Task<EmailProcessingResult> ProcessNextAsync(CancellationToken ct = default)
    {
        var message = await ClaimNextAsync(ct);
        if (message is null)
        {
            return new EmailProcessingResult(EmailProcessingOutcome.NoWork);
        }

        if (options.Value.IsUndeliverable(message.ToAddress))
        {
            return await SkipUndeliverableAsync(message, ct);
        }

        try
        {
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            attemptCts.CancelAfter(TimeSpan.FromSeconds(options.Value.SendAttemptTimeoutSeconds));
            await sender.SendAsync(message, attemptCts.Token);
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
        // SMTP may already have accepted the message. Host shutdown must not cancel this small
        // database update, otherwise the expired lease would cause the same email to be sent again.
        if (!await FinalizeSentAsync(
                message.Id, message.LeaseToken!.Value, sentAt, CancellationToken.None))
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

    /// <summary>
    /// A demo address can never be delivered, so it is failed on its first claim without retries: sending it would
    /// bounce back to the sending mailbox, and enough bounces get that mailbox's sending suspended.
    /// </summary>
    private async Task<EmailProcessingResult> SkipUndeliverableAsync(EmailMessage message, CancellationToken ct)
    {
        if (!await FinalizeFailureAsync(
                message.Id,
                message.LeaseToken!.Value,
                exhausted: true,
                "Not sent: the recipient's domain never receives mail (Email:UndeliverableDomains).",
                nextAttemptAt: null,
                time.GetUtcNow(),
                ct))
        {
            return LeaseLost(message);
        }

        logger.LogInformation(
            "Invitation email {EmailMessageId} for meeting {MeetingId} was not sent: its recipient domain never "
            + "receives mail.",
            message.Id,
            message.MeetingId);
        return new EmailProcessingResult(EmailProcessingOutcome.PermanentlyFailed, message.AttemptCount);
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
        if (!string.Equals(
                db.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                "The durable email queue requires PostgreSQL.");
        }

        await MarkExhaustedMessagesFailedAsync(now, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var candidates = await db.EmailMessages
            .FromSqlInterpolated($"""
                SELECT *
                FROM email_messages
                WHERE attempt_count < {options.Value.MaxAttempts}
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
        var message = candidates.SingleOrDefault();

        if (message is not null)
        {
            Claim(message, leaseToken, now);
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);

        if (message is not null)
        {
            db.Entry(message).State = EntityState.Detached;
        }

        return message;
    }

    private Task<int> MarkExhaustedMessagesFailedAsync(DateTimeOffset now, CancellationToken ct)
        => db.EmailMessages
            .Where(item => item.AttemptCount >= options.Value.MaxAttempts
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
