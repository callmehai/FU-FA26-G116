using Npgsql;

namespace TrustCabinet.Web.Tests;

/// <summary>
/// Connection strings for the tests that run against the dev PostgreSQL from docker-compose.yml.
///
/// The password is never in the repo. It comes from the POSTGRES_PASSWORD environment variable
/// (CI sets it), or else from the .env next to TrustCabinet.sln — the same file docker compose
/// read to create the database, so a developer never types it twice.
/// </summary>
internal static class TestPostgres
{
    private const string PasswordKey = "POSTGRES_PASSWORD";

    private static readonly Lazy<string> Password = new(ReadPassword);

    /// <summary>Each test class names its own database so runs never touch the dev data.</summary>
    public static string ConnectionString(string database) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Database = database,
            Username = "trustcabinet",
            Password = Password.Value,
        }.ConnectionString;

    private static string ReadPassword() =>
        Environment.GetEnvironmentVariable(PasswordKey)
        ?? ReadFromDotEnv()
        ?? throw new InvalidOperationException(
            $"No PostgreSQL password for the tests. Set {PasswordKey} in the .env at the repo root "
            + "(cp .env.example .env) or as an environment variable.");

    private static string? ReadFromDotEnv()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (!File.Exists(Path.Combine(dir.FullName, "TrustCabinet.sln")))
                continue;

            var dotEnv = Path.Combine(dir.FullName, ".env");
            if (!File.Exists(dotEnv))
                return null;

            var prefix = PasswordKey + "=";
            return File.ReadLines(dotEnv)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
                .Select(line => line[prefix.Length..].Trim().Trim('"', '\''))
                .LastOrDefault();
        }

        return null;
    }
}
