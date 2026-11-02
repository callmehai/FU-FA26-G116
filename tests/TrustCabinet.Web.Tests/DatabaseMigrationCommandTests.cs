using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests;

/// <summary>
/// ADR-0047: <c>--migrate</c>, the step the test server's deploy runs before it starts a new version. Its exit code is
/// what stops a deploy, so both outcomes are asserted on a real PostgreSQL.
/// </summary>
public sealed class DatabaseMigrationCommandTests
{
    [Fact]
    public async Task Migrate_applies_pending_migrations_and_reference_data_then_exits_zero()
    {
        await using var database = new PostgreSqlTestDatabase(PostgresTestDatabase.ConnectionString("trustcabinet_migrate_command"));
        await using var db = Context(database);
        var migrations = db.Database.GetMigrations().ToList();
        await database.CreateFromTemplateAsync(TestSchema.MigratedTo(migrations[^2]));
        // A database restored without InitialCreate's rows: the command must put the matrix back.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM role_capabilities");

        var exitCode = await DatabaseMigrationCommand.RunAsync([Setting(database)]);

        Assert.Equal(0, exitCode);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.NotEqual(0, await db.RoleCapabilities.CountAsync());
        Assert.False(await db.Users.AnyAsync(), "Without --seed-demo the command must not create any account.");

        // A deploy that brings no new migration runs it again on an up-to-date database.
        Assert.Equal(0, await DatabaseMigrationCommand.RunAsync([Setting(database)]));
    }

    [Fact]
    public async Task Seed_demo_adds_the_demo_accounts()
    {
        await using var database = new PostgreSqlTestDatabase(PostgresTestDatabase.ConnectionString("trustcabinet_migrate_demo"));
        await using var db = Context(database);
        await database.CreateFromTemplateAsync(TestSchema.Migrated);

        var exitCode = await DatabaseMigrationCommand.RunAsync(
            [DatabaseMigrationCommand.SeedDemoArgument, Setting(database)]);

        Assert.Equal(0, exitCode);
        Assert.True(await db.Users.AnyAsync(user => user.Email == "quantri@trustcabinet.local"));
    }

    [Theory]
    // Nothing listens on port 1: the deploy must stop instead of starting a version on a database it never reached.
    [InlineData("Host=127.0.0.1;Port=1;Database=trustcabinet;Username=none;Password=none;Timeout=3")]
    // No connection string at all fails before a host exists, and still has to fail the step.
    [InlineData("")]
    public async Task Migrate_exits_non_zero_when_it_cannot_migrate(string connectionString)
    {
        var exitCode = await DatabaseMigrationCommand.RunAsync([$"--ConnectionStrings:TrustCabinet={connectionString}"]);

        Assert.Equal(1, exitCode);
    }

    /// <summary>
    /// Unpooled: the command's connections must be closed when it returns, or the test cannot drop its database
    /// (and clearing every pool would reach into other tests' connections).
    /// </summary>
    private static string Setting(PostgreSqlTestDatabase database)
        => "--ConnectionStrings:TrustCabinet="
            + new NpgsqlConnectionStringBuilder(database.ConnectionString) { Pooling = false }.ConnectionString;

    private static TrustCabinetDbContext Context(PostgreSqlTestDatabase database)
        => new(new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(database.DataSource, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention()
            .Options);
}
