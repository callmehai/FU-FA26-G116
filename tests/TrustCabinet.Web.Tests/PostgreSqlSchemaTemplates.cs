using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests;

/// <summary>How a test database gets its schema. Equal values share one template.</summary>
internal sealed record TestSchema
{
    private const string ApplicationHistoryTable = "__ef_migrations_history";

    private TestSchema(bool ensureCreated, string? historyTable, string? targetMigration)
    {
        IsEnsureCreated = ensureCreated;
        HistoryTable = historyTable;
        TargetMigration = targetMigration;
    }

    /// <summary>Built from the model by <c>EnsureCreated</c>: no migration history, no migration data.</summary>
    public static TestSchema EnsureCreated { get; } = new(true, null, null);

    /// <summary>Every migration applied, history in <c>__ef_migrations_history</c> as the application keeps it.</summary>
    public static TestSchema Migrated { get; } = new(false, ApplicationHistoryTable, null);

    /// <summary>Every migration applied, history in EF Core's default <c>__EFMigrationsHistory</c>.</summary>
    public static TestSchema MigratedWithDefaultHistoryTable { get; } = new(false, null, null);

    /// <summary>Migrated up to and including <paramref name="migration"/>, history as the application keeps it.</summary>
    public static TestSchema MigratedTo(string migration) => new(false, ApplicationHistoryTable, migration);

    public bool IsEnsureCreated { get; }

    /// <summary>The history table, or null for EF Core's default.</summary>
    public string? HistoryTable { get; }

    /// <summary>The last migration applied, or null for all of them.</summary>
    public string? TargetMigration { get; }
}

/// <summary>
/// Builds each schema once per test run and creates test databases as copies of it.
///
/// Applying every migration costs about a second, and the tests that need a fresh database per
/// test case paid it per case. PostgreSQL's <c>CREATE DATABASE … TEMPLATE</c> copies a database
/// in a fraction of that, so the schema is built once into a template and every test database
/// starts as an exact copy of it — the same tables, constraints, triggers and migration rows a
/// test would have got from running the migrations or <c>EnsureCreated</c> itself.
///
/// A template is unique to this process, so a migration edited between runs can never be served
/// from a stale copy, and it is dropped when the test process exits. A run that is killed before
/// that leaves its templates behind; the next run drops any older than <see cref="AbandonedAfter"/>,
/// which no live run can still be using. Nothing ever connects to a template after it is built:
/// PostgreSQL refuses to copy a database that has a session open.
/// </summary>
internal static class PostgreSqlSchemaTemplates
{
    private const string TemplatePrefix = "trustcabinet_template_";
    private static readonly TimeSpan AbandonedAfter = TimeSpan.FromHours(12);

    private static readonly ConcurrentDictionary<TestSchema, Lazy<Task<string>>> Templates = new();
    private static readonly ConcurrentQueue<(string AdminConnectionString, string Database)> Built = new();
    private static readonly object AbandonedSweepLock = new();
    private static Task? _abandonedSweep;

    static PostgreSqlSchemaTemplates()
        => AppDomain.CurrentDomain.ProcessExit += (_, _) => DropTemplates();

    /// <summary>
    /// Creates the database named in <paramref name="connectionString"/> as a copy of the
    /// <paramref name="schema"/> template. The database must not exist yet.
    /// </summary>
    public static async Task CreateDatabaseAsync(string connectionString, TestSchema schema)
    {
        var template = await Templates
            .GetOrAdd(schema, key => new Lazy<Task<string>>(() => BuildTemplateAsync(connectionString, key)))
            .Value;

        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        await using var admin = new NpgsqlConnection(AdminConnectionString(connectionString));
        await admin.OpenAsync();
        await using var create = admin.CreateCommand();
        create.CommandText = $"CREATE DATABASE {Quote(settings.Database!)} TEMPLATE {Quote(template)}";
        await create.ExecuteNonQueryAsync();
    }

    private static async Task<string> BuildTemplateAsync(string connectionString, TestSchema schema)
    {
        // The suffix is a version 7 GUID, so the name carries its creation time; see CreatedAt.
        var name = $"{TemplatePrefix}{Guid.CreateVersion7():N}";
        var adminConnectionString = AdminConnectionString(connectionString);
        lock (AbandonedSweepLock)
        {
            _abandonedSweep ??= DropAbandonedTemplatesAsync(adminConnectionString);
        }

        await _abandonedSweep;
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            create.CommandText = $"CREATE DATABASE {Quote(name)}";
            await create.ExecuteNonQueryAsync();
        }

        Built.Enqueue((adminConnectionString, name));

        // No pooling: a pooled connection left idle on the template would make every copy fail.
        var templateConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = name,
            Pooling = false,
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(templateConnectionString, npgsql =>
            {
                if (schema.HistoryTable is { } historyTable)
                {
                    npgsql.MigrationsHistoryTable(historyTable);
                }
            })
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = new TrustCabinetDbContext(options);
        if (schema.IsEnsureCreated)
        {
            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            await db.GetService<IMigrator>().MigrateAsync(schema.TargetMigration);
        }

        return name;
    }

    private static async Task DropAbandonedTemplatesAsync(string adminConnectionString)
    {
        var names = new List<string>();
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using (var list = admin.CreateCommand())
        {
            list.CommandText = "SELECT datname FROM pg_database WHERE starts_with(datname, $1)";
            list.Parameters.Add(new NpgsqlParameter { Value = TemplatePrefix });
            await using var reader = await list.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }
        }

        var cutoff = DateTimeOffset.UtcNow - AbandonedAfter;
        foreach (var name in names.Where(name => CreatedAt(name) < cutoff))
        {
            try
            {
                await using var drop = admin.CreateCommand();
                drop.CommandText = $"DROP DATABASE IF EXISTS {Quote(name)}";
                await drop.ExecuteNonQueryAsync();
            }
            catch (PostgresException)
            {
                // Still in use after all, or dropped by another run at the same moment: leave it.
            }
        }
    }

    /// <summary>When a template was built, read from the version 7 GUID that ends its name.</summary>
    private static DateTimeOffset? CreatedAt(string name)
    {
        var suffix = name[TemplatePrefix.Length..];
        return suffix.Length == 32
            && long.TryParse(suffix[..12], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var unixMilliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds)
            : null;
    }

    private static void DropTemplates()
        => Parallel.ForEach(Built, template =>
        {
            try
            {
                using var admin = new NpgsqlConnection(template.AdminConnectionString);
                admin.Open();
                using var drop = admin.CreateCommand();
                drop.CommandText = $"DROP DATABASE IF EXISTS {Quote(template.Database)}";
                drop.ExecuteNonQuery();
            }
            catch (NpgsqlException)
            {
                // The process is exiting; the next run drops what is left once it is abandoned.
            }
        });

    private static string AdminConnectionString(string connectionString)
        => new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres",
            Pooling = false,
        }.ConnectionString;

    private static string Quote(string identifier)
    {
        using var identifiers = new NpgsqlCommandBuilder();
        return identifiers.QuoteIdentifier(identifier);
    }
}
