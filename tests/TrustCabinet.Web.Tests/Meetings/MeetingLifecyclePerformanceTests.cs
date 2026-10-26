using System.Diagnostics;
using System.Net.Http.Json;
using Xunit.Abstractions;

namespace TrustCabinet.Web.Tests.Meetings;

[CollectionDefinition("Lifecycle performance", DisableParallelization = true)]
public sealed class LifecyclePerformanceCollection;

[Collection("Lifecycle performance")]
public sealed class MeetingLifecyclePerformanceTests(MeetingLifecycleFixture fixture, ITestOutputHelper output) : IClassFixture<MeetingLifecycleFixture>
{
    [Fact, Trait("Category", "Performance")]
    public async Task Measure_100_independent_meetings_with_healthy_and_failing_provider()
    {
        var warm = await fixture.Seed();
        using (var client = fixture.Client(warm.Chair))
        {
            (await client.PostAsJsonAsync($"/api/meetings/{warm.MeetingId}/transitions", new { action = "Start" })).EnsureSuccessStatusCode();
            (await client.PostAsJsonAsync($"/api/meetings/{warm.MeetingId}/transitions", new { action = "End" })).EnsureSuccessStatusCode();
        }
        var scenarios = new List<MeetingLifecycleFixture.Scenario>();
        for (var i = 0; i < 100; i++) scenarios.Add(await fixture.Seed(count: 0));
        foreach (var action in new[] { "Start", "End" })
        {
            var times = await Task.WhenAll(scenarios.Select(async s =>
            {
                using var client = fixture.Client(s.Chair);
                var watch = Stopwatch.StartNew();
                (await client.PostAsJsonAsync($"/api/meetings/{s.MeetingId}/transitions", new { action })).EnsureSuccessStatusCode();
                return watch.Elapsed.TotalMilliseconds;
            }));
            Array.Sort(times);
            output.WriteLine($"{action}: callers=100, pool=20, P50={times[49]:F1}ms, P95={times[94]:F1}ms, max={times[^1]:F1}ms; provider=fake healthy; target API P95<=1000ms, form<=3000ms");
        }
        var outage = await fixture.Seed(Domain.Enums.MeetingStatus.InProgress, count: 0);
        try
        {
            fixture.ProviderUnavailable = true;
            using var client = fixture.Client(outage.Chair);
            var watch = Stopwatch.StartNew();
            (await client.PostAsJsonAsync($"/api/meetings/{outage.MeetingId}/transitions", new { action = "End" })).EnsureSuccessStatusCode();
            output.WriteLine($"End provider immediate outage: {watch.Elapsed.TotalMilliseconds:F1}ms; timeout budget per post-commit effect=2000ms; not a network timeout measurement.");
        }
        finally { fixture.ProviderUnavailable = false; }
    }
}
