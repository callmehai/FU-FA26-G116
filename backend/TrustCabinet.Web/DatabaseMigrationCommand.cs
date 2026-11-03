using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web;

/// <summary>
/// ADR-0047: the deliberate migration step of a deployment. <c>dotnet TrustCabinet.Web.dll --migrate</c> applies every
/// pending EF Core migration and the reference data (<see cref="DependencyInjection.MigrateForDeploymentAsync"/>), then
/// exits — 0 on success, 1 on any failure — without starting the web server. The deploy workflow runs it as
/// <c>docker compose run --rm app --migrate</c> before it switches the app container to the new image, so a migration
/// that fails stops the deploy while the previous version keeps serving.
///
/// <para>
/// <c>--migrate --seed-demo</c> also adds the demo rows of local development, demo accounts with the public password
/// included. It exists only to rebuild the shared test server from an empty database (deploy/README.md); a real
/// deployment never passes it.
/// </para>
///
/// Only the database is registered: the command needs no signing key, Redis, LiveKit or file store, so it cannot fail
/// on their configuration. Any other argument after the switch is read as configuration
/// (<c>--ConnectionStrings:TrustCabinet=...</c>), as the web host would.
/// </summary>
public static class DatabaseMigrationCommand
{
    public const string Argument = "--migrate";
    public const string SeedDemoArgument = "--seed-demo";

    public static async Task<int> RunAsync(string[] arguments)
    {
        var seedDemo = arguments.Contains(SeedDemoArgument, StringComparer.Ordinal);
        // The web builder rather than a generic host, for the configuration the app itself would read:
        // ASPNETCORE_ENVIRONMENT, appsettings.{Environment}.json and, in Development, user-secrets. It is never run.
        var builder = WebApplication.CreateBuilder(
            arguments.Where(argument => argument != SeedDemoArgument).ToArray());

        WebApplication host;
        try
        {
            builder.Services.AddTrustCabinetDatabase(builder.Configuration);
            host = builder.Build();
        }
        catch (Exception exception)
        {
            // No host means no logger yet: a missing connection string still has to reach the deploy log.
            await Console.Error.WriteLineAsync($"Migration not started: {exception.Message}");
            return 1;
        }

        // Disposed before returning, so the console logger flushes before the process exits.
        await using (host)
        {
            var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseMigrationCommand));
            try
            {
                var latest = await host.Services.MigrateForDeploymentAsync(seedDemo);
                logger.LogInformation(
                    "Database is up to date at migration {Migration}{Demo}",
                    latest ?? "(none)",
                    seedDemo ? ", demo rows seeded" : "");
                return 0;
            }
            catch (Exception exception)
            {
                logger.LogCritical(exception, "Migration failed");
                return 1;
            }
        }
    }
}
