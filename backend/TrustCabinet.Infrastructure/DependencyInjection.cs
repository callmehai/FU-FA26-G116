using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Infrastructure.Seed;

namespace TrustCabinet.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the database. The connection string comes from configuration under
    /// ConnectionStrings:TrustCabinet — from appsettings in development, from user-secrets or an
    /// environment variable for anything with a real password in it.
    /// </summary>
    public static IServiceCollection AddTrustCabinetDatabase(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TrustCabinet");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No connection string 'TrustCabinet' is configured, so the application cannot "
                + "start. Set one with: dotnet user-secrets set \"ConnectionStrings:TrustCabinet\" "
                + "\"...\" --project backend/TrustCabinet.Web. Setup instructions are in "
                + "docs/02-design/03-co-so-du-lieu.md.");
        }

        services.AddDbContext<TrustCabinetDbContext>(options =>
            options.UseSqlServer(connectionString));

        return services;
    }

    /// <summary>
    /// Applies pending migrations and fills an empty database with demo rows.
    ///
    /// For local development only — the caller checks the environment. A real deployment runs
    /// `dotnet ef database update` as a deliberate, reviewable step, because a web app that
    /// migrates its own database on boot will happily do so from several instances at once.
    /// </summary>
    public static async Task MigrateAndSeedDevelopmentAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrustCabinetDbContext>();

        await db.Database.MigrateAsync();
        await DevelopmentDataSeeder.SeedAsync(db);
    }
}
