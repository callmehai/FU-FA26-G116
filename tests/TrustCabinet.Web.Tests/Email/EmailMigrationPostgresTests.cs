using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
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

    private static readonly string ConnectionString =
        PostgresTestDatabase.ConnectionString("trustcabinet_email_migration_tests");

    private TrustCabinetDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = new TrustCabinetDbContext(
            new DbContextOptionsBuilder<TrustCabinetDbContext>()
                .UseNpgsql(
                    ConnectionString,
                    npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
                .UseSnakeCaseNamingConvention()
                .Options);

        try
        {
            await _db.Database.EnsureDeletedAsync();
        }
        catch (NpgsqlException exception)
        {
            throw new InvalidOperationException(
                "Email migration tests need the development PostgreSQL. Start it with "
                + "`docker compose up -d postgres`.",
                exception);
        }

        await MigrateToAsync(BeforeReliableEmailDelivery);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
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
