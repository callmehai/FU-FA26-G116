using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests;

/// <summary>
/// /api/health is what the deploy checks before it keeps a new version (ADR-0047), so it has to fail when the
/// version cannot reach its database — otherwise a broken release passes and the previous image is deleted.
/// </summary>
public class HealthEndpointTests : IClassFixture<TrustCabinetApiFactory>
{
    private readonly TrustCabinetApiFactory _factory;

    public HealthEndpointTests(TrustCabinetApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_ok_with_service_identity_when_the_database_answers()
    {
        await using var database = new PostgreSqlTestDatabase(PostgresTestDatabase.ConnectionString("trustcabinet_health"));
        await database.CreateFromTemplateAsync(TestSchema.EnsureCreated);
        await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TrustCabinetDbContext>();
            services.RemoveAll<DbContextOptions<TrustCabinetDbContext>>();
            services.AddDbContext<TrustCabinetDbContext>(options => options.UseNpgsql(database.DataSource).UseSnakeCaseNamingConvention());
        }));

        var response = await factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
        Assert.Equal("ok", body.Database);
        Assert.Equal("TrustCabinet.Web", body.Service);
    }

    [Fact]
    public async Task Health_returns_503_when_the_database_cannot_be_reached()
    {
        // The shared factory's connection string is a placeholder no server accepts.
        var response = await _factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("unavailable", body.Status);
        Assert.Equal("unreachable", body.Database);
    }

    private sealed record HealthResponse(string Status, string Service, string Version, string Database, DateTimeOffset TimeUtc);
}
