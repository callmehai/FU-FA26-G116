using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Web.Tests.Participants;
using Xunit.Abstractions;

namespace TrustCabinet.Web.Tests.Attendance;

public sealed class AttendancePerformanceTests(ParticipantFixture f, ITestOutputHelper output) : IClassFixture<ParticipantFixture>
{
    [Fact]
    public async Task Roster_reads_are_batched_and_measure_one_hundred_concurrent_authorized_callers()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        await using var db = f.Db();
        var users = Enumerable.Range(0, 100).Select(i => new User { FullName = $"Attendance reader {i:D3}",
            Email = $"{Guid.CreateVersion7():N}@attendance-load.test", PasswordHash = "test-only" }).ToArray();
        db.Users.AddRange(users);
        foreach (var user in users)
        {
            var p = new MeetingParticipant { MeetingId = s.MeetingId, UserId = user.Id };
            db.MeetingParticipants.Add(p);
        }
        await db.SaveChangesAsync(); using var secretary = f.Client(s.Secretary);
        await secretary.GetAsync(s.Url + "/attendance?pageSize=100");
        f.Queries.Count = 0;
        var page = await secretary.GetFromJsonAsync<JsonElement>(s.Url + "/attendance?pageSize=100");
        Assert.Equal(103, page.GetProperty("total").GetInt32()); Assert.Equal(100, page.GetProperty("items").GetArrayLength());
        Assert.InRange(f.Queries.Count, 1, 10); output.WriteLine($"GET DB reader commands: {f.Queries.Count}");
        for (var round = 1; round <= 3; round++)
        {
            var timings = await Task.WhenAll(users.Select(async user =>
            {
                using var client = f.Client(user.Id); var watch = Stopwatch.StartNew();
                using var response = await client.GetAsync(s.Url + "/attendance?pageSize=100");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode); return watch.ElapsedMilliseconds;
            }));
            var sorted = timings.Order().ToArray();
            output.WriteLine($"round={round}; 100 distinct concurrent callers; rows=103; p50={sorted[49]}ms; p95={sorted[94]}ms; max={sorted[^1]}ms; errors=0; transport=TestServer; PostgreSQL pool=20");
        }
        // Report latency rather than imposing a hardware-dependent timing assertion on shared CI.
    }
}
