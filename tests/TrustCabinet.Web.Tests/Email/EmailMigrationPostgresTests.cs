using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests.Email;

/// <summary>
/// Runs SCRUM-15's migration from its shared predecessor so a developer-only schema history can
/// never become a hidden prerequisite.
/// </summary>
public sealed class EmailMigrationPostgresTests : IAsyncLifetime
{
    private const string BeforeReliableEmailDelivery =
        "20260924153656_NotificationManagement";

    private readonly PostgreSqlTestDatabase _database = new(
        PostgresTestDatabase.ConnectionString("trustcabinet_email_migration_tests"));

    private TrustCabinetDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = new TrustCabinetDbContext(
            new DbContextOptionsBuilder<TrustCabinetDbContext>()
                .UseNpgsql(
                    _database.DataSource,
                    npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
                .UseSnakeCaseNamingConvention()
                .Options);

        await MigrateToAsync(BeforeReliableEmailDelivery);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task Migration_preserves_existing_email_jobs_with_distinct_legacy_deduplication_keys()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO email_messages
                (id, created_at, updated_at, to_address, subject, body, status, attempt_count, last_error)
            VALUES
                ({first}, {now}, {now}, 'first@example.vn', 'Legacy first', 'Body one', 'Queued', 0, ''),
                ({second}, {now}, {now}, 'second@example.vn', 'Legacy second', 'Body two', 'Sent', 1, '')
            """);

        await MigrateToAsync();

        var jobs = await _db.EmailMessages.OrderBy(email => email.ToAddress).ToListAsync();
        Assert.Equal(2, jobs.Count);
        Assert.Equal($"legacy-email:{first:D}", jobs[0].DeduplicationKey);
        Assert.Equal($"legacy-email:{second:D}", jobs[1].DeduplicationKey);
        Assert.Equal("Body one", jobs[0].Body);
        Assert.Equal("Body two", jobs[1].Body);
        Assert.Equal(TrustCabinet.Domain.Enums.EmailStatus.Queued, jobs[0].Status);
        Assert.Equal(TrustCabinet.Domain.Enums.EmailStatus.Sent, jobs[1].Status);
    }

    [Fact]
    public async Task Already_upgraded_databases_keep_delivery_keys_leases_and_job_content()
    {
        await MigrateToAsync("20260930080344_ReliableEmailDelivery");
        // Model the state of main deployed before this feature: ReliableEmailDelivery was
        // applied, but the new preservation migration has not run on that database yet.
        await _db.Database.ExecuteSqlRawAsync("""
            DROP TABLE __email_message_delivery_upgrade;
            DELETE FROM __ef_migrations_history
                WHERE migration_id = '20260930080343_StageLegacyEmailJobsForReliableDelivery';
            """);
        var leaseToken = Guid.CreateVersion7();
        var leaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
        var job = new TrustCabinet.Domain.Entities.EmailMessage
        {
            DeduplicationKey = "already-reliable:event:recipient",
            ToAddress = "existing@example.vn",
            Subject = "Existing invitation",
            Body = "Content already rendered",
            CalendarAttachment = "BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n",
            Status = TrustCabinet.Domain.Enums.EmailStatus.Processing,
            AttemptCount = 2,
            LeaseToken = leaseToken,
            LeaseExpiresAt = leaseExpiresAt,
        };
        _db.EmailMessages.Add(job);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await MigrateToAsync();

        var persisted = await _db.EmailMessages.SingleAsync();
        Assert.Equal(job.Id, persisted.Id);
        Assert.Equal(job.DeduplicationKey, persisted.DeduplicationKey);
        Assert.Equal(job.Body, persisted.Body);
        Assert.Equal(job.CalendarAttachment, persisted.CalendarAttachment);
        Assert.Equal(job.Status, persisted.Status);
        Assert.Equal(2, persisted.AttemptCount);
        Assert.Equal(leaseToken, persisted.LeaseToken);
        Assert.Equal(leaseExpiresAt.ToUnixTimeMilliseconds(), persisted.LeaseExpiresAt!.Value.ToUnixTimeMilliseconds());
        Assert.Empty(await _db.Database.SqlQuery<string>($"""
            SELECT table_name AS "Value" FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = '__email_message_delivery_upgrade'
            """).ToListAsync());
    }

    [Fact]
    public async Task Migration_applies_and_reverts_from_the_shared_predecessor()
    {
        await MigrateToAsync();

        Assert.Equal(
            ["deduplication_key", "lease_expires_at", "lease_token", "next_attempt_at"],
            await DeliveryColumnsAsync());
        Assert.Contains(
            "ix_email_messages_deduplication_key",
            await EmailIndexesAsync());

        await MigrateToAsync(BeforeReliableEmailDelivery);

        Assert.Empty(await DeliveryColumnsAsync());
        Assert.Contains("ix_email_messages_status_created_at", await EmailIndexesAsync());
    }

    private Task MigrateToAsync(string? target = null)
        => _db.GetService<IMigrator>().MigrateAsync(target);

    private async Task<string[]> DeliveryColumnsAsync()
        => await _db.Database.SqlQuery<string>($"""
                SELECT column_name AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'email_messages'
                  AND column_name IN (
                    'deduplication_key',
                    'lease_expires_at',
                    'lease_token',
                    'next_attempt_at')
                ORDER BY column_name
                """)
            .ToArrayAsync();

    private async Task<string[]> EmailIndexesAsync()
        => await _db.Database.SqlQuery<string>($"""
                SELECT indexname AS "Value"
                FROM pg_indexes
                WHERE schemaname = 'public'
                  AND tablename = 'email_messages'
                ORDER BY indexname
                """)
            .ToArrayAsync();
}
