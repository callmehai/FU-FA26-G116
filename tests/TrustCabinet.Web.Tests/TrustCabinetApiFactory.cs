using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrustCabinet.Infrastructure.Documents;

namespace TrustCabinet.Web.Tests;

/// <summary>
/// Boots the application in memory for endpoint tests.
///
/// The environment is "Testing" rather than "Development" on purpose: it keeps the startup
/// migrate-and-seed step switched off, so a test run never touches a real database. The
/// connection string is a syntactically valid placeholder — registering the provider needs one,
/// but nothing here opens a connection.
/// </summary>
public class TrustCabinetApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:TrustCabinet",
            "Host=localhost;Port=5432;Database=trustcabinet_tests;Username=none;Password=not-used");

        // Same idea for Redis and the signing key: registering the services needs both present and
        // well-formed, and nothing here opens a connection or verifies a real token. The endpoint
        // tests substitute their own in-memory store, so no test depends on a Redis being up.
        builder.UseSetting("ConnectionStrings:Redis", "localhost:6379");
        builder.UseSetting("Authentication:SigningKey", "khoa-ky-chi-dung-trong-test-du-32-ky-tu");
        builder.ConfigureServices(services =>
        {
            var notificationWorker = services.SingleOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(NotificationWorker));
            if (notificationWorker is not null)
            {
                services.Remove(notificationWorker);
            }

            // Same for the call room reconciliation: it would reach for a real LiveKit.
            var callRoomJanitor = services.SingleOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(CallRoomJanitor));
            if (callRoomJanitor is not null)
            {
                services.Remove(callRoomJanitor);
            }

            // And the orphan purge: a test that swaps in a configured store must decide itself when
            // files are purged, not race a background run.
            var orphanPurge = services.SingleOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(OrphanFilePurgeWorker));
            if (orphanPurge is not null)
            {
                services.Remove(orphanPurge);
            }
        });
    }
}
