using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using Xunit.Abstractions;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Agenda;

[CollectionDefinition("Agenda performance", DisableParallelization = true)]
public sealed class AgendaPerformanceCollection;

[Collection("Agenda performance")]
public sealed class AgendaPerformanceTests(AgendaFixture fixture, ITestOutputHelper output) : IClassFixture<AgendaFixture>
{
    [Fact]
    public async Task Snapshot_query_count_does_not_grow_with_item_or_document_count()
    {
        var small = await fixture.Seed(count: 1);
        var large = await fixture.Seed(count: 50);
        await using (var db = fixture.Db())
        {
            db.MeetingDocuments.AddRange(large.Items.SelectMany(item => Enumerable.Range(0, 4).Select(n =>
                AgendaFixture.TestDocument(large.MeetingId, $"Doc {n}", large.Secretary, item))));
            await db.SaveChangesAsync();
        }
        using var a = fixture.Client(small.Member);
        using var b = fixture.Client(large.Member);
        var before = fixture.QueryCount;
        (await a.GetAsync(small.Url)).EnsureSuccessStatusCode();
        var smallCount = fixture.QueryCount - before;
        before = fixture.QueryCount;
        (await b.GetAsync(large.Url)).EnsureSuccessStatusCode();
        var largeCount = fixture.QueryCount - before;
        Assert.Equal(smallCount, largeCount);
        Assert.InRange(largeCount, 1, 12);
        output.WriteLine($"Snapshot queries: 1 item={smallCount}; 50 items/200 linked documents={largeCount}");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Measure_100_active_users_and_realtime_resync_without_claiming_deployment_certification()
    {
        var s = await fixture.Seed(count: 50);
        var users = Enumerable.Range(0, 100).Select(i => new User { FullName = $"Load {i}",
            Email = $"{Guid.NewGuid():N}@load.test", PasswordHash = "test-only" }).ToArray();
        await using (var db = fixture.Db())
        {
            db.Users.AddRange(users);
            db.MeetingParticipants.AddRange(users.Select(u => new MeetingParticipant { MeetingId = s.MeetingId, UserId = u.Id }));
            db.MeetingDocuments.AddRange(s.Items.SelectMany(item => Enumerable.Range(0, 2).Select(n =>
                AgendaFixture.TestDocument(s.MeetingId, $"Doc {n}", s.Secretary, item))));
            await db.SaveChangesAsync();
        }
        var clients = users.Select(u => fixture.Client(u.Id)).ToArray();
        try
        {
            // Warm the model, query compilation, and pool before taking representative samples.
            await Task.WhenAll(clients.Select(async c => (await c.GetAsync(s.Url)).EnsureSuccessStatusCode()));
            var durations = new ConcurrentBag<double>();
            for (var round = 0; round < 3; round++)
                await Task.WhenAll(clients.Select(async c => {
                    var watch = Stopwatch.StartNew();
                    (await c.GetAsync(s.Url)).EnsureSuccessStatusCode();
                    durations.Add(watch.Elapsed.TotalMilliseconds);
                }));
            var hubs = users.Select(u => fixture.Hub(u.Id)).ToArray();
            var endToEnd = new ConcurrentBag<double>();
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var count = 0;
            var eventWatch = new Stopwatch();
            try
            {
                for (var index = 0; index < hubs.Length; index++)
                {
                    var client = clients[index];
                    hubs[index].On<AgendaChange>("agendaChanged", async change => {
                        var snapshot = await client.GetFromJsonAsync<JsonElement>(s.Url);
                        Assert.Contains(snapshot.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == change.AgendaItemId);
                        endToEnd.Add(eventWatch.Elapsed.TotalMilliseconds);
                        if (Interlocked.Increment(ref count) == 100) done.TrySetResult();
                    });
                    await hubs[index].StartAsync();
                    await hubs[index].InvokeAsync("JoinMeeting", s.MeetingId);
                }
                using var secretary = fixture.Client(s.Secretary);
                eventWatch.Start();
                await AssertCode(await secretary.PostAsJsonAsync(s.Url, Input()), 201, "MSG60");
                await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            finally { foreach (var hub in hubs) await hub.DisposeAsync(); }
            static double P95(IEnumerable<double> values) { var sorted = values.Order().ToArray(); return sorted[(int)Math.Ceiling(sorted.Length * .95) - 1]; }
            var result = new { measuredAt = DateTimeOffset.UtcNow, os = Environment.OSVersion.ToString(), cpuCount = Environment.ProcessorCount,
                runtime = Environment.Version.ToString(), transport = "SignalR LongPolling via ASP.NET TestServer; configured PostgreSQL test connection",
                activeUsers = 100, agendaItems = 50, linkedDocuments = 100, apiSamples = durations.Count,
                apiP95Milliseconds = P95(durations), apiTargetMet = P95(durations) <= 1000,
                realtimeSamples = endToEnd.Count, realtimeP95Milliseconds = P95(endToEnd), realtimeTargetMet = P95(endToEnd) <= 2000 };
            output.WriteLine(JsonSerializer.Serialize(result));
            var folder = Path.Combine(AppContext.BaseDirectory, "performance-results");
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, "agenda-performance.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            Assert.Equal(300, durations.Count);
            Assert.Equal(100, endToEnd.Count);
        }
        finally { foreach (var client in clients) client.Dispose(); }
    }
}
