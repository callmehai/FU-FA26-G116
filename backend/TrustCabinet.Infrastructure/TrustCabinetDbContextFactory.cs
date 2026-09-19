using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrustCabinet.Infrastructure;

/// <summary>
/// Used only by the `dotnet ef` tooling, which has to build a context without starting the web
/// application. Adding a migration therefore needs no connection string and no running database —
/// EF only reads the model. The value below is never used at runtime.
/// </summary>
public class TrustCabinetDbContextFactory : IDesignTimeDbContextFactory<TrustCabinetDbContext>
{
    public TrustCabinetDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("TRUSTCABINET_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=trustcabinet;Username=trustcabinet;Password=";

        var options = new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TrustCabinetDbContext(options);
    }
}
