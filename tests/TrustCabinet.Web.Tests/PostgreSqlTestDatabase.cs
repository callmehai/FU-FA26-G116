using Npgsql;

namespace TrustCabinet.Web.Tests;

/// <summary>Owns a unique database and connection pool for one PostgreSQL test case.</summary>
internal sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    private readonly string _databaseName;
    private readonly string _adminConnectionString;

    public NpgsqlDataSource DataSource { get; }

    public PostgreSqlTestDatabase(string templateConnectionString)
    {
        var settings = new NpgsqlConnectionStringBuilder(templateConnectionString);
        _databaseName = $"{settings.Database}_{Guid.CreateVersion7():N}";
        settings.Database = _databaseName;
        DataSource = NpgsqlDataSource.Create(settings.ConnectionString);

        settings.Database = "postgres";
        settings.Pooling = false;
        _adminConnectionString = settings.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        // Close only this test's pool before dropping its database. Never terminate sessions or
        // clear another test's pool: both make parallel runs depend on server-wide privileges.
        await DataSource.DisposeAsync();
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        using var identifiers = new NpgsqlCommandBuilder();
        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS {identifiers.QuoteIdentifier(_databaseName)}";
        await drop.ExecuteNonQueryAsync();
    }
}
