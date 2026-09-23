using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace TrustCabinet.Web.Tests;

/// <summary>
/// SCRUM-62: the SPA fallback must not answer API paths. The bug only shows once
/// <c>wwwroot/index.html</c> exists, and wwwroot is build output that may be absent in a test run,
/// so the fixture points the web root at a folder of its own holding a stand-in index.html.
/// </summary>
public class SpaFallbackTests : IClassFixture<SpaFallbackTests.WithBuiltFrontendFactory>
{
    private readonly WithBuiltFrontendFactory _factory;

    public SpaFallbackTests(WithBuiltFrontendFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/duong-dan-khong-ton-tai")]
    // Fails the {id:guid} constraint, so no /api/users route matches it.
    [InlineData("/api/users/notaguid")]
    [InlineData("/hubs/khong-ton-tai")]
    public async Task Unknown_api_path_returns_404_not_the_spa(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(WithBuiltFrontendFactory.Marker, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Real_api_route_is_not_shadowed_by_a_fallback()
    {
        // Passes on main too: it does not reproduce SCRUM-62, it guards the design choice. Fixing
        // the bug with an /api/{**path} → 404 endpoint instead would fail here — no body means no
        // Content-Type, the matcher discards the JSON-body endpoint, and that 404 answers instead
        // of the auth gate.
        var response = await _factory.CreateClient().PostAsync("/api/users", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_still_answers_with_a_built_frontend()
    {
        // HealthEndpointTests runs without an index.html; this is the same call with one present.
        var response = await _factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Client_side_route_still_serves_index_html()
    {
        var response = await _factory.CreateClient().GetAsync("/meetings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(WithBuiltFrontendFactory.Marker, await response.Content.ReadAsStringAsync());
    }

    public sealed class WithBuiltFrontendFactory : TrustCabinetApiFactory
    {
        public const string Marker = "spa-fallback-test-index";

        private readonly string _webRoot =
            Directory.CreateTempSubdirectory("trustcabinet-webroot-").FullName;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            File.WriteAllText(
                Path.Combine(_webRoot, "index.html"),
                $"<!doctype html><title>{Marker}</title>");
            builder.UseWebRoot(_webRoot);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            // Reached from both Dispose and DisposeAsync.
            if (Directory.Exists(_webRoot))
            {
                Directory.Delete(_webRoot, recursive: true);
            }
        }
    }
}
