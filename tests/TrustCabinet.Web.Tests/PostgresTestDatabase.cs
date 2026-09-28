using Npgsql;

namespace TrustCabinet.Web.Tests;

/// <summary>Builds integration-test connections without committing a local database password.</summary>
internal static class PostgresTestDatabase
{
    private const string ConnectionStringEnvironmentVariable =
        "TRUSTCABINET_TEST_CONNECTION_STRING";

    public static string ConnectionString(string database)
    {
        var configured = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return WithDatabase(configured, database);
        }

        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")
            ?? ReadDotEnvValue("POSTGRES_PASSWORD");
        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException(
                $"PostgreSQL integration tests need {ConnectionStringEnvironmentVariable}, "
                + "POSTGRES_PASSWORD, or POSTGRES_PASSWORD in the repository .env file.");
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Database = database,
            Username = "trustcabinet",
            Password = password,
        }.ConnectionString;
    }

    private static string WithDatabase(string connectionString, string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = database,
        };
        return builder.ConnectionString;
    }

    private static string? ReadDotEnvValue(string key)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TrustCabinet.sln")))
            {
                var path = Path.Combine(directory.FullName, ".env");
                if (!File.Exists(path))
                {
                    return null;
                }

                var prefix = $"{key}=";
                var line = File.ReadLines(path)
                    .FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
                return line is null ? null : Unquote(line[prefix.Length..].Trim());
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string Unquote(string value)
        => value.Length >= 2
            && ((value[0] == '\"' && value[^1] == '\"')
                || (value[0] == '\'' && value[^1] == '\''))
            ? value[1..^1]
            : value;
}
