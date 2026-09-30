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
    private static readonly string ConnectionString =
        PostgresTestDatabase.ConnectionString("trustcabinet_email_tests");
    private static readonly DateTimeOffset Now =
        new(2026, 10, 1, 2, 0, 0, TimeSpan.Zero);

    private TrustCabinetDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = NewContext();
        try
        {
            await _db.Database.EnsureDeletedAsync();
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
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
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

    private static TrustCabinetDbContext NewContext() => new(
        new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    private static EmailQueueProcessor Processor(
        TrustCabinetDbContext db,
        IEmailSender sender)
        => new(
            db,
            sender,
            Options.Create(new EmailDeliveryOptions
            {
                MaxAttempts = 3,
                RetryDelaySeconds = 1,
                PollingIntervalSeconds = 1,
                ProcessingLeaseSeconds = 120,
            }),
            new FixedTimeProvider(Now),
            NullLogger<EmailQueueProcessor>.Instance);

    private static EmailMessage QueuedMessage() => new()
    {
        ToAddress = "recipient@trustcabinet.local",
        Subject = "Meeting invitation",
        Body = "Invitation body",
        Status = EmailStatus.Queued,
        CreatedAt = Now,
        UpdatedAt = Now,
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
        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
