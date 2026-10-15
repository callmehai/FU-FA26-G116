using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using Xunit.Abstractions;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Participants;

public sealed class ParticipantConcurrencyTests(ParticipantFixture f, ITestOutputHelper output) : IClassFixture<ParticipantFixture>
{
    [Fact]
    public async Task Competing_chair_swaps_use_revision_and_preserve_exactly_one_chair_and_secretary()
    {
        var s = await f.Seed(); using var secretary = f.Client(s.Secretary);
        var body = new
        {
            changes = new[] { new { participantId = s.ChairAssignment, role = "Member" },
            new { participantId = s.MemberAssignment, role = "Chair" } },
            chairReplacement = new { outgoingParticipantId = s.ChairAssignment, incomingParticipantId = s.MemberAssignment, confirmed = true }
        };
        var results = await Task.WhenAll(Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put, body),
            Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put, body));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        await Code(Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict), 409, "MSG252");
        await using var db = f.Db();
        Assert.Equal(1, await db.MeetingParticipants.CountAsync(p => p.MeetingId == s.MeetingId && p.Role == MeetingRole.Chair));
        Assert.Equal(1, await db.MeetingParticipants.CountAsync(p => p.MeetingId == s.MeetingId && p.Role == MeetingRole.Secretary));
        Assert.Equal(2, await db.MeetingInvitationDispatches.CountAsync(d => d.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task Response_racing_removal_cannot_resurrect_assignment_or_access()
    {
        var s = await f.Seed(); using var secretary = f.Client(s.Secretary); using var member = f.Client(s.Member);
        var results = await Task.WhenAll(Write(member, s.Url + "/invitation-response", HttpMethod.Put, new { response = "Accepted" }),
            Write(secretary, s.Url + $"/participants/{s.MemberAssignment}", HttpMethod.Delete));
        Assert.Equal(HttpStatusCode.OK, results[1].StatusCode);
        Assert.Contains(results[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound });
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync(s.Url + "/invitation-response")).StatusCode);
        await using var db = f.Db(); var p = (await db.MeetingParticipants.FindAsync(s.MemberAssignment))!;
        Assert.Equal(AssignmentStatus.Removed, p.AssignmentStatus); Assert.Equal(AttendanceStatus.Unknown, p.Attendance);
        Assert.Equal(results[0].IsSuccessStatusCode ? InvitationResponse.Accepted : InvitationResponse.Pending, p.Response);
    }

    [Fact]
    public async Task Removed_organizer_keeps_independent_meeting_access_but_not_participant_grants()
    {
        var s = await f.Seed(); using var secretary = f.Client(s.Secretary); using var organizer = f.Client(s.Organizer);
        var added = await Code(await Write(secretary, s.Url + "/participants", HttpMethod.Post, new
        {
            userIds = new[] { s.Organizer },
            role = "Member",
            additionalPermissions = new[] { "ViewMeetingParticipants" }
        }), 201, "MSG96");
        Assert.Equal(HttpStatusCode.OK, (await organizer.GetAsync(s.Url + "/participants")).StatusCode);
        var id = added.GetProperty("createdIds")[0].GetGuid();
        await Code(await Write(secretary, s.Url + $"/participants/{id}", HttpMethod.Delete, etag: await Tag(secretary, s.Url)), 200, "MSG103");
        Assert.Equal(HttpStatusCode.OK, (await organizer.GetAsync(s.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organizer.GetAsync(s.Url + "/participants")).StatusCode);
    }

    [Fact]
    public async Task Five_hundred_rows_are_paged_and_one_hundred_recipient_batch_avoids_n_plus_one_reads()
    {
        var s = await f.Seed();
        await using var db = f.Db();
        var users = Enumerable.Range(0, 600).Select(i => new User { FullName = $"Load {i:D4}", Email = $"{Guid.CreateVersion7():N}@load.test", PasswordHash = "test-only" }).ToArray();
        db.Users.AddRange(users);
        db.MeetingParticipants.AddRange(users.Take(500).Select(u => new MeetingParticipant { MeetingId = s.MeetingId, UserId = u.Id }));
        await db.SaveChangesAsync(); using var secretary = f.Client(s.Secretary);
        await secretary.GetAsync(s.Url + "/participants");
        var timings = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            f.Queries.Count = 0; var sw = Stopwatch.StartNew();
            var page = await secretary.GetFromJsonAsync<JsonElement>(s.Url + "/participants?page=5&pageSize=100");
            timings.Add(sw.ElapsedMilliseconds);
            Assert.Equal(503, page.GetProperty("total").GetInt32()); Assert.Equal(100, page.GetProperty("items").GetArrayLength());
            Assert.InRange(f.Queries.Count, 1, 10);
        }
        f.Queries.Count = 0; var batch = Stopwatch.StartNew();
        await Code(await Write(secretary, s.Url + "/participants", HttpMethod.Post,
            new { userIds = users.Skip(500).Select(u => u.Id).ToArray(), role = "Member" }), 201, "MSG96");
        output.WriteLine($"503-row roster milliseconds: {string.Join(",", timings)}; median={timings.Order().ElementAt(2)}; 100-recipient batch={batch.ElapsedMilliseconds}ms; DB commands={f.Queries.Count}");
        Assert.Equal(100, await db.MeetingInvitationDispatches.CountAsync(d => d.MeetingId == s.MeetingId));
        for (var round = 1; round <= 3; round++)
        {
            var measurements = await Task.WhenAll(Enumerable.Range(0, 100).Select(async _ =>
            {
                using var client = f.Client(s.Secretary);
                var watch = Stopwatch.StartNew();
                using var roster = await client.GetAsync(s.Url + "/participants?pageSize=100");
                var rosterMs = watch.ElapsedMilliseconds;
                watch.Restart();
                using var responses = await client.GetAsync(s.Url + "/invitation-responses?pageSize=100");
                Assert.Equal(HttpStatusCode.OK, roster.StatusCode); Assert.Equal(HttpStatusCode.OK, responses.StatusCode);
                return new[] { rosterMs, watch.ElapsedMilliseconds };
            }));
            var sorted = measurements.SelectMany(x => x).Order().ToArray();
            output.WriteLine($"100 concurrent callers, round {round}, 603 rows: p50={sorted[99]}ms p95={sorted[189]}ms max={sorted[^1]}ms; 200 requests, errors=0.");
        }
    }

    [Fact]
    public async Task Development_scenarios_are_insert_only_and_recovery_is_available_in_preparation_states()
    {
        await using var db = f.Db();
        var originalCount = await db.Meetings.CountAsync();
        foreach (var address in new[] { "thuky", "chutoa", "thanhvien" })
            db.Users.Add(new() { FullName = address, Email = address + "@trustcabinet.local", PasswordHash = "test-only" });
        await db.SaveChangesAsync();
        await Infrastructure.Seed.ParticipantDevelopmentDataSeeder.SeedAsync(db);
        var meetings = await db.Meetings.Where(m => m.Title.StartsWith("[SCRUM20-DEV]")).Include(m => m.Participants).ToArrayAsync();
        Assert.Equal(7, meetings.Length);
        Assert.All(meetings.Where(m => ParticipantRules.Preparation(m.Status)), m => Assert.True(ParticipantRules.CanRecover(m, m.CreatedById)));
        meetings[0].Title = "Edited by tester"; await db.SaveChangesAsync();
        await Infrastructure.Seed.ParticipantDevelopmentDataSeeder.SeedAsync(db);
        Assert.Equal("Edited by tester", (await db.Meetings.FindAsync(meetings[0].Id))!.Title);
        Assert.Equal(originalCount + 7, await db.Meetings.CountAsync());
    }
}
