using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Email;

public sealed class EmailQueueProcessorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeEmailMessageRepository _messages = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeEmailSender _sender;

    public EmailQueueProcessorTests() => _sender = new(_unitOfWork);

    private EmailQueueProcessor Processor(int maxAttempts = 3, string[]? undeliverable = null) => new(
        _messages, _unitOfWork, _sender,
        Options.Create(new EmailDeliveryOptions
        {
            MaxAttempts = maxAttempts,
            RetryDelaySeconds = 60,
            UndeliverableDomains = undeliverable ?? [],
        }),
        new FixedClock(Now), NullLogger<EmailQueueProcessor>.Instance);

    private EmailMessage Queue(string to = "dai.bieu@example.vn", int attempts = 0)
    {
        var message = new EmailMessage
        {
            DeduplicationKey = $"test:{Guid.NewGuid():N}",
            ToAddress = to,
            Subject = "Lời mời",
            AttemptCount = attempts,
        };
        _messages.Rows.Add(message);
        _messages.Next = message;
        return message;
    }

    [Fact]
    public async Task Claims_the_message_in_a_transaction_then_sends_it_and_marks_it_sent()
    {
        var message = Queue();

        var result = await Processor().ProcessNextAsync();

        Assert.Equal(new EmailProcessingResult(EmailProcessingOutcome.Sent, 1), result);
        Assert.Same(message, Assert.Single(_sender.Sent));
        Assert.False(_sender.SentInTransaction);
        Assert.Equal(EmailStatus.Sent, message.Status);
        Assert.Equal("provider-1", message.ProviderMessageId);
        Assert.Null(message.LeaseToken);
        // Exhausted messages are failed first, outside the claim; the claim is saved and committed before the
        // send, and the result is recorded after it.
        Assert.Equal([TransactionMode.None, TransactionMode.ReadCommitted, TransactionMode.None], _unitOfWork.Modes);
        Assert.Equal(["MarkExhaustedAsFailedAsync", "LockNextDueAsync"], _messages.Calls);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits));
        Assert.Same(message, Assert.Single(_messages.Detached));
    }

    [Fact]
    public async Task Nothing_due_answers_no_work_and_sends_nothing()
    {
        var result = await Processor().ProcessNextAsync();

        Assert.Equal(EmailProcessingOutcome.NoWork, result.Outcome);
        Assert.Empty(_sender.Sent);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_messages.Detached);
    }

    [Fact]
    public async Task A_failed_send_with_attempts_left_goes_back_to_the_queue_for_later()
    {
        var message = Queue();
        _sender.Fail = new IOException("SMTP unavailable");

        var result = await Processor(maxAttempts: 3).ProcessNextAsync();

        Assert.Equal(new EmailProcessingResult(EmailProcessingOutcome.RetryScheduled, 1), result);
        Assert.Equal(EmailStatus.Queued, message.Status);
        Assert.Equal(Now.AddSeconds(60), message.NextAttemptAt);
        Assert.Equal("IOException: SMTP unavailable", message.LastError);
    }

    [Fact]
    public async Task A_message_the_provider_refused_fails_for_good_without_a_retry()
    {
        var message = Queue();
        _sender.Fail = new PermanentEmailFailureException("invalid address");

        var result = await Processor(maxAttempts: 8).ProcessNextAsync();

        Assert.Equal(EmailProcessingOutcome.PermanentlyFailed, result.Outcome);
        Assert.Equal(EmailStatus.Failed, message.Status);
        Assert.Null(message.NextAttemptAt);
    }

    [Fact]
    public async Task The_last_attempt_failing_fails_the_message_for_good()
    {
        var message = Queue(attempts: 2);
        _sender.Fail = new IOException("SMTP unavailable");

        var result = await Processor(maxAttempts: 3).ProcessNextAsync();

        Assert.Equal(new EmailProcessingResult(EmailProcessingOutcome.PermanentlyFailed, 3), result);
        Assert.Equal(EmailStatus.Failed, message.Status);
    }

    [Fact]
    public async Task An_address_that_never_receives_mail_is_failed_without_sending()
    {
        var message = Queue(to: "demo@trustcabinet.local");

        var result = await Processor(undeliverable: ["trustcabinet.local"]).ProcessNextAsync();

        Assert.Equal(EmailProcessingOutcome.PermanentlyFailed, result.Outcome);
        Assert.Empty(_sender.Sent);
        Assert.Equal(EmailStatus.Failed, message.Status);
        Assert.Contains("UndeliverableDomains", message.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_lease_another_worker_took_over_is_reported_and_not_recorded()
    {
        Queue();
        _messages.LeaseLost = true;

        var result = await Processor().ProcessNextAsync();

        Assert.Equal(EmailProcessingOutcome.LeaseLost, result.Outcome);
        Assert.Single(_sender.Sent);
    }

    [Fact]
    public async Task A_storage_failure_while_claiming_escapes_to_the_worker_and_sends_nothing()
    {
        Queue();
        _messages.FailLock = new PersistenceUnavailableException();

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => Processor().ProcessNextAsync());

        Assert.Empty(_sender.Sent);
        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }
}
