using System.Net;
using System.Net.Http.Json;

namespace TrustCabinet.Web.Tests;

/// <summary>
/// Smoke test for the one endpoint that exists. Its real job is to prove the whole chain works:
/// the app boots in memory, routing resolves, and CI can run tests at all. Delete or replace it
/// once real endpoints land — do not let it become the only test in the project.
/// </summary>
public class HealthEndpointTests : IClassFixture<TrustCabinetApiFactory>
{
    private readonly TrustCabinetApiFactory _factory;

    public HealthEndpointTests(TrustCabinetApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_ok_with_service_identity()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
        Assert.Equal("TrustCabinet.Web", body.Service);
    }

    private sealed record HealthResponse(string Status, string Service, string Version, DateTimeOffset TimeUtc);
}
