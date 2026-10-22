using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Infrastructure.Calls;

namespace TrustCabinet.Web.Tests.Meetings;

/// <summary>Explicit manual smoke runner; not an optional/quietly skipped CI test.</summary>
public static class MeetingLifecycleLiveKitSmoke
{
    public static async Task RunAsync()
    {
        // File-based runners build under the OS temp directory, outside the solution tree.
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_TRUSTCABINET_WEB", Path.GetFullPath("backend/TrustCabinet.Web"));
        var secret = Environment.GetEnvironmentVariable("SCRUM69_LIVEKIT_SECRET")
            ?? throw new InvalidOperationException("Set SCRUM69_LIVEKIT_SECRET for the isolated local LiveKit server.");
        var services = new ServiceCollection();
        services.AddHttpClient(LiveKitGateway.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(5));
        await using var clients = services.BuildServiceProvider();
        var gateway = new LiveKitGateway(Options.Create(new LiveKitOptions
        {
            ApiUrl = "http://localhost:7880", PublicUrl = "ws://localhost:7880",
            ApiKey = "scrum69-test", ApiSecret = secret,
        }), clients.GetRequiredService<IHttpClientFactory>());
        var fixture = new MeetingLifecycleFixture { GatewayOverride = gateway };
        await fixture.InitializeAsync();
        using var node = new Process { StartInfo = new ProcessStartInfo("node")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false,
            CreateNoWindow = true,
        } };
        node.StartInfo.ArgumentList.Add("docs/03-api/postman/meeting-lifecycle-livekit-smoke.mjs");
        var nodeStarted = false;
        try
        {
            var s = await fixture.Seed(count: 0);
            using var http = fixture.Client(s.Chair);
            (await http.PostAsJsonAsync($"/api/meetings/{s.MeetingId}/transitions", new { action = "Start" })).EnsureSuccessStatusCode();
            await using var scope = fixture.Host.Services.CreateAsyncScope();
            var rooms = scope.ServiceProvider.GetRequiredService<ICallRoomService>();
            var chair = await rooms.JoinAsync(s.MeetingId, s.Chair);
            var member = await rooms.JoinAsync(s.MeetingId, s.Member);
            nodeStarted = node.Start();
            Assert.True(nodeStarted);
            var joined = await Command(node, new { action = "connect", tickets = new[] { chair.Value, member.Value } });
            Assert.Equal(2, joined.GetProperty("connected").GetInt32());
            Assert.Equal(2, (await gateway.ListParticipantsAsync(CallRoomNames.ForMeeting(s.MeetingId), default)).Count);
            var elapsed = Stopwatch.StartNew();
            var response = await http.PostAsJsonAsync($"/api/meetings/{s.MeetingId}/transitions", new { action = "End" });
            response.EnsureSuccessStatusCode();
            Assert.Equal("MSG111", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            Assert.Equal(2, (await Command(node, new { action = "disconnected" })).GetProperty("disconnected").GetInt32());
            Console.WriteLine($"LiveKit v1.13.7: two media-free clients disconnected after End in {elapsed.Elapsed.TotalMilliseconds:F1}ms.");
            Assert.Equal(CallOutcome.RoomNotOpen, (await rooms.JoinAsync(s.MeetingId, s.Chair)).Outcome);
            var accepted = (await Command(node, new { action = "reconnect" })).GetProperty("accepted").GetBoolean();
            Console.WriteLine($"Previously issued token accepted after End: {accepted}.");
            if (accepted)
            {
                elapsed.Restart();
                await rooms.ReconcileAsync();
                await Command(node, new { action = "disconnected" });
                Console.WriteLine($"One explicit reconciliation disconnected the old-token client in {elapsed.Elapsed.TotalMilliseconds:F1}ms; production janitor interval=30s (plus provider/retry latency).");
            }
            Assert.Empty(await gateway.ListParticipantsAsync(CallRoomNames.ForMeeting(s.MeetingId), default));
            await node.StandardInput.WriteLineAsync("{\"action\":\"stop\"}");
            node.StandardInput.Close();
            await node.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(0, node.ExitCode);
            await MeasureIndependentMeetings(fixture);
        }
        finally
        {
            if (nodeStarted && !node.HasExited) node.Kill(entireProcessTree: true);
            await fixture.DisposeAsync();
        }
    }

    private static async Task<JsonElement> Command(Process process, object command)
    {
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(command, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.NotNull(line);
        var result = JsonDocument.Parse(line).RootElement.Clone();
        Assert.False(result.TryGetProperty("failed", out _), "LiveKit browser command failed.");
        return result;
    }

    private static async Task MeasureIndependentMeetings(MeetingLifecycleFixture fixture)
    {
        var meetings = new List<MeetingLifecycleFixture.Scenario>();
        for (var i = 0; i < 100; i++) meetings.Add(await fixture.Seed(count: 0));
        foreach (var action in new[] { "Start", "End" })
        {
            var times = await Task.WhenAll(meetings.Select(async meeting =>
            {
                using var client = fixture.Client(meeting.Chair);
                var elapsed = Stopwatch.StartNew();
                (await client.PostAsJsonAsync($"/api/meetings/{meeting.MeetingId}/transitions", new { action })).EnsureSuccessStatusCode();
                return elapsed.Elapsed.TotalMilliseconds;
            }));
            Array.Sort(times);
            Console.WriteLine($"Real local LiveKit / {action}: 100 callers, 100 independent meetings, P50={times[49]:F1}ms, P95={times[94]:F1}ms, max={times[^1]:F1}ms. No media load.");
        }
    }
}
