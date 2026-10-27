using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Web.Tests.Email;

/// <summary>
/// PostgreSQL coverage for the queue claim that the in-memory provider cannot verify.
/// </summary>
public sealed class EmailQueuePostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlTestDatabase _database = new(
        PostgresTestDatabase.ConnectionString("trustcabinet_email_tests"));
    private static readonly DateTimeOffset Now =
        new(2026, 10, 1, 2, 0, 0, TimeSpan.Zero);

    private TrustCabinetDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = NewContext();
        try
        {
            await _db.Database.EnsureCreatedAsync();
        }
        catch (NpgsqlException exception)
        {
            throw new InvalidOperationException(
                "Email queue PostgreSQL integration tests need the CI/development PostgreSQL. "
                + "Set TRUSTCABINET_TEST_CONNECTION_STRING, POSTGRES_PASSWORD, or the repository "
                + ".env file, then run `docker compose up -d postgres`.", exception);
        }
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task A_second_worker_cannot_claim_an_email_with_an_active_lease()
    {
        var email = QueuedMessage();
        _db.EmailMessages.Add(email);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await using var firstDb = NewContext();
        await using var secondDb = NewContext();
        var firstSender = new BlockingSender();
        var firstProcessor = Processor(firstDb, firstSender);
        var secondProcessor = Processor(secondDb, new RecordingSender());

        var firstAttempt = firstProcessor.ProcessNextAsync();
        await firstSender.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            var secondAttempt = await secondProcessor.ProcessNextAsync();
            Assert.Equal(EmailProcessingOutcome.NoWork, secondAttempt.Outcome);
        }
        finally
        {
            firstSender.Release();
        }

        Assert.Equal(EmailProcessingOutcome.Sent, (await firstAttempt).Outcome);

        _db.ChangeTracker.Clear();
        var stored = await _db.EmailMessages.SingleAsync(item => item.Id == email.Id);
        Assert.Equal(EmailStatus.Sent, stored.Status);
        Assert.Equal(1, stored.AttemptCount);
        Assert.Null(stored.LeaseToken);
        Assert.Null(stored.LeaseExpiresAt);
    }

    [Fact]
    public async Task Processor_sends_queued_messages_oldest_first_and_marks_them_sent()
    {
        var older = QueuedMessage("older@trustcabinet.local", Now);
        var newer = QueuedMessage("newer@trustcabinet.local", Now.AddMinutes(1));
        _db.EmailMessages.AddRange(newer, older);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var sender = new RecordingSender();
        var processor = Processor(_db, sender);

        Assert.Equal(EmailProcessingOutcome.Sent, (await processor.ProcessNextAsync()).Outcome);
        Assert.Equal(EmailProcessingOutcome.Sent, (await processor.ProcessNextAsync()).Outcome);
        Assert.Equal(EmailProcessingOutcome.NoWork, (await processor.ProcessNextAsync()).Outcome);

        Assert.Equal(
            ["older@trustcabinet.local", "newer@trustcabinet.local"],
            sender.Recipients);
        Assert.All(await _db.EmailMessages.ToListAsync(), message =>
        {
            Assert.Equal(EmailStatus.Sent, message.Status);
            Assert.Equal(1, message.AttemptCount);
            Assert.Equal(Now, message.SentAt);
        });
    }

    [Fact]
    public async Task Processor_retries_then_preserves_a_permanent_failure_for_audit()
    {
        _db.EmailMessages.Add(QueuedMessage("failed@trustcabinet.local", Now));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var sender = new FailingSender("SMTP unavailable");
        var time = new AdjustableTimeProvider(Now);
        var processor = Processor(_db, sender, time, maxAttempts: 2, retryDelaySeconds: 60);

        var first = await processor.ProcessNextAsync();
        Assert.Equal(EmailProcessingOutcome.RetryScheduled, first.Outcome);
        var afterFirst = await _db.EmailMessages.AsNoTracking().SingleAsync();
        Assert.Equal(EmailStatus.Queued, afterFirst.Status);
        Assert.Equal(Now.AddMinutes(1), afterFirst.NextAttemptAt);

        time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(EmailProcessingOutcome.NoWork, (await processor.ProcessNextAsync()).Outcome);

        time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(
            EmailProcessingOutcome.PermanentlyFailed,
            (await processor.ProcessNextAsync()).Outcome);
        var afterSecond = await _db.EmailMessages.AsNoTracking().SingleAsync();
        Assert.Equal(EmailStatus.Failed, afterSecond.Status);
        Assert.Equal(2, afterSecond.AttemptCount);
        Assert.Null(afterSecond.NextAttemptAt);
        Assert.Contains("SMTP unavailable", afterSecond.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Undeliverable_domain_fails_at_once_without_reaching_smtp()
    {
        _db.EmailMessages.AddRange(
            QueuedMessage("thuky@TrustCabinet.Local", Now),
            QueuedMessage("someone@example.com", Now.AddMinutes(1)));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var sender = new RecordingSender();
        var processor = Processor(_db, sender, undeliverableDomains: ["trustcabinet.local"]);

        Assert.Equal(EmailProcessingOutcome.PermanentlyFailed, (await processor.ProcessNextAsync()).Outcome);
        Assert.Equal(EmailProcessingOutcome.Sent, (await processor.ProcessNextAsync()).Outcome);

        Assert.Equal(["someone@example.com"], sender.Recipients);
        var skipped = await _db.EmailMessages.AsNoTracking().SingleAsync(m => m.ToAddress == "thuky@TrustCabinet.Local");
        Assert.Equal(EmailStatus.Failed, skipped.Status);
        Assert.Null(skipped.NextAttemptAt);
        Assert.Contains("UndeliverableDomains", skipped.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Expired_claim_at_maximum_attempts_is_failed_without_another_send()
    {
        var exhausted = QueuedMessage("exhausted@trustcabinet.local", Now.AddMinutes(-1));
        exhausted.Status = EmailStatus.Processing;
        exhausted.AttemptCount = 3;
        exhausted.LeaseToken = Guid.NewGuid();
        exhausted.LeaseExpiresAt = Now.AddSeconds(-1);
        _db.EmailMessages.Add(exhausted);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var sender = new RecordingSender();

        var result = await Processor(_db, sender, maxAttempts: 3).ProcessNextAsync();

        Assert.Equal(EmailProcessingOutcome.NoWork, result.Outcome);
        Assert.Empty(sender.Recipients);
        var stored = await _db.EmailMessages.AsNoTracking().SingleAsync();
        Assert.Equal(EmailStatus.Failed, stored.Status);
        Assert.Null(stored.LeaseToken);
        Assert.Null(stored.LeaseExpiresAt);
    }

    [Fact]
    public async Task Host_shutdown_after_smtp_acceptance_does_not_cancel_sent_finalization()
    {
        var email = QueuedMessage();
        _db.EmailMessages.Add(email);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        using var shutdown = new CancellationTokenSource();
        var processor = Processor(_db, new CancelAfterAcceptedSender(shutdown));

        var result = await processor.ProcessNextAsync(shutdown.Token);

        Assert.Equal(EmailProcessingOutcome.Sent, result.Outcome);
        var stored = await _db.EmailMessages.AsNoTracking().SingleAsync();
        Assert.Equal(EmailStatus.Sent, stored.Status);
        Assert.Equal(Now, stored.SentAt);
    }

    [Fact]
    public async Task Complete_send_attempt_times_out_before_its_lease_can_expire()
    {
        _db.EmailMessages.Add(QueuedMessage());
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var result = await Processor(
                _db,
                new NeverCompletingSender(),
                sendAttemptTimeoutSeconds: 1,
                processingLeaseSeconds: 10)
            .ProcessNextAsync();

        Assert.Equal(EmailProcessingOutcome.RetryScheduled, result.Outcome);
        var stored = await _db.EmailMessages.AsNoTracking().SingleAsync();
        Assert.Equal(EmailStatus.Queued, stored.Status);
        Assert.Null(stored.LeaseToken);
        Assert.Null(stored.LeaseExpiresAt);
    }

    [Fact]
    public async Task Deduplication_is_per_business_event_not_per_meeting_and_recipient()
    {
        var meetingId = Guid.CreateVersion7();
        var invitation = QueuedMessage();
        invitation.DeduplicationKey = $"meeting:{meetingId:D}:published:user-1";
        var reschedule = QueuedMessage();
        reschedule.ToAddress = invitation.ToAddress;
        reschedule.DeduplicationKey = $"meeting:{meetingId:D}:rescheduled:1:user-1";
        _db.EmailMessages.AddRange(invitation, reschedule);

        await _db.SaveChangesAsync();

        Assert.Equal(2, await _db.EmailMessages.CountAsync());
    }

    private TrustCabinetDbContext NewContext() => new(
        new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(_database.DataSource)
            .UseSnakeCaseNamingConvention()
            .Options);

    private static EmailQueueProcessor Processor(
        TrustCabinetDbContext db,
        IEmailSender sender,
        TimeProvider? time = null,
        int maxAttempts = 3,
        int retryDelaySeconds = 60,
        int sendAttemptTimeoutSeconds = 60,
        int processingLeaseSeconds = 120,
        string[]? undeliverableDomains = null)
        => new(
            db,
            sender,
            Options.Create(new EmailDeliveryOptions
            {
                MaxAttempts = maxAttempts,
                RetryDelaySeconds = retryDelaySeconds,
                PollingIntervalSeconds = 1,
                ProcessingLeaseSeconds = processingLeaseSeconds,
                SendAttemptTimeoutSeconds = sendAttemptTimeoutSeconds,
                UndeliverableDomains = undeliverableDomains ?? [],
            }),
            time ?? new FixedTimeProvider(Now),
            NullLogger<EmailQueueProcessor>.Instance);

    private static EmailMessage QueuedMessage(
        string recipient = "recipient@trustcabinet.local",
        DateTimeOffset? createdAt = null) => new()
    {
        DeduplicationKey = $"test:{Guid.CreateVersion7():D}",
        ToAddress = recipient,
        Subject = "Meeting invitation",
        Body = "Invitation body",
        Status = EmailStatus.Queued,
        CreatedAt = createdAt ?? Now,
        UpdatedAt = createdAt ?? Now,
    };

    private sealed class BlockingSender : IEmailSender
    {
        private readonly TaskCompletionSource _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Entered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Entered.TrySetResult();
            await _release.Task.WaitAsync(ct);
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class RecordingSender : IEmailSender
    {
        public List<string> Recipients { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Recipients.Add(message.ToAddress);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingSender(string message) : IEmailSender
    {
        public Task SendAsync(EmailMessage email, CancellationToken ct = default)
            => Task.FromException(new InvalidOperationException(message));
    }

    private sealed class CancelAfterAcceptedSender(CancellationTokenSource shutdown) : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            shutdown.Cancel();
            return Task.CompletedTask;
        }
    }

    private sealed class NeverCompletingSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
            => Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now = now.Add(duration);
    }
}
