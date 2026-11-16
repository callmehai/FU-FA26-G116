using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Email;

public enum EmailProcessingOutcome
{
    NoWork,
    Sent,
    RetryScheduled,
    PermanentlyFailed,
    LeaseLost,
}

public sealed record EmailProcessingResult(EmailProcessingOutcome Outcome, int AttemptCount = 0);

public interface IEmailQueueProcessor
{
    Task<EmailProcessingResult> ProcessNextAsync(CancellationToken ct = default);
}

internal sealed class EmailQueueProcessor(
    IEmailMessageRepository messages,
    IUnitOfWork unitOfWork,
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

        string? providerMessageId;
        try
        {
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            attemptCts.CancelAfter(TimeSpan.FromSeconds(options.Value.SendAttemptTimeoutSeconds));
            providerMessageId = await sender.SendAsync(message, attemptCts.Token);
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
                message.Id, message.LeaseToken!.Value, providerMessageId, sentAt, CancellationToken.None))
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
        // A message the provider refused as such gets the same answer every time; retrying only delays the record.
        var exhausted = exception is PermanentEmailFailureException
            || message.AttemptCount >= options.Value.MaxAttempts;
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

        await unitOfWork.ExecuteAsync(
            token => messages.MarkExhaustedAsFailedAsync(options.Value.MaxAttempts, now, token),
            TransactionMode.None, ct);

        var message = await unitOfWork.ExecuteAsync(async token =>
        {
            var message = await messages.LockNextDueAsync(options.Value.MaxAttempts, now, token);

            if (message is not null)
            {
                Claim(message, leaseToken, now);
                await unitOfWork.SaveChangesAsync(token);
            }

            return message;
        }, TransactionMode.ReadCommitted, ct);

        if (message is not null)
        {
            messages.Detach(message);
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
        string? providerMessageId,
        DateTimeOffset sentAt,
        CancellationToken ct)
    {
        var updated = await unitOfWork.ExecuteAsync(
            token => messages.MarkSentAsync(id, leaseToken, providerMessageId, sentAt, token),
            TransactionMode.None, ct);
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
        var updated = await unitOfWork.ExecuteAsync(
            token => messages.MarkAttemptFailedAsync(id, leaseToken, status, error, nextAttemptAt, failedAt, token),
            TransactionMode.None, ct);
        return updated == 1;
    }

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
