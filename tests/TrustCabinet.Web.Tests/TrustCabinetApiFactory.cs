using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

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
    }
}
