using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Agenda;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Agenda;

public sealed class AgendaConcurrencyTests(AgendaFixture fixture) : IClassFixture<AgendaFixture>
{
    [Fact]
    public async Task Concurrent_creates_append_distinct_positions()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync(s.Url, Input()))).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        await using var db = fixture.Db();
        var positions = await db.AgendaItems.Where(i => i.MeetingId == s.MeetingId).Select(i => i.Position).ToListAsync();
        Assert.Equal(11, positions.Distinct().Count());
        Assert.Equal(15, positions.Max());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_start_has_one_winner_and_one_current(bool sameItem)
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Chair);
        var responses = await Task.WhenAll(client.PostAsync(s.Url + $"/{s.Items[0]}/current", null),
            client.PostAsync(s.Url + $"/{s.Items[sameItem ? 0 : 1]}/current", null)).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var db = fixture.Db();
        var current = Assert.Single(await db.AgendaItems.Where(i => i.MeetingId == s.MeetingId && i.Status == AgendaItemStatus.InProgress).ToListAsync());
        Assert.Equal(fixture.Clock.Now, current.ActualStartedAt);
        var completes = await Task.WhenAll(client.PostAsync(s.Url + $"/{current.Id}/complete", null), client.PostAsync(s.Url + $"/{current.Id}/complete", null));
        Assert.Single(completes, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(completes, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Two_reorders_from_one_snapshot_do_not_silently_overwrite()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        var snapshot = await client.GetFromJsonAsync<JsonElement>(s.Url);
        var token = snapshot.GetProperty("orderVersion").GetString();
        var responses = await Task.WhenAll(
            client.PutAsJsonAsync(s.Url + "/order", new { orderVersion = token, pendingItemIds = s.Items.Reverse().ToArray() }),
            client.PutAsJsonAsync(s.Url + "/order", new { orderVersion = token, pendingItemIds = new[] { s.Items[1], s.Items[0], s.Items[2] } }));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        await AssertCode(Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict), 409, "MSG143");
    }

    [Theory]
    [InlineData("start", "MSG141", 409)]
    [InlineData("ended", "MSG67", 409)]
    [InlineData("role", "MSG67", 403)]
    [InlineData("removed", "", 404)]
    public async Task Waiting_mutation_rechecks_state_and_role_after_lock(string change, string code, int status)
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        await using var locked = fixture.Db();
        await using var transaction = await locked.Database.BeginTransactionAsync();
        await locked.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {s.MeetingId} FOR UPDATE").ToListAsync();
        var request = client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input(duration: 30));
        await WaitForLockWaiter(locked, s.MeetingId);
        if (change == "start")
        {
            var item = await locked.AgendaItems.SingleAsync(i => i.Id == s.Items[0]);
            item.Status = AgendaItemStatus.InProgress;
            item.ActualStartedAt = fixture.Clock.Now;
        }
        else if (change == "ended") (await locked.Meetings.SingleAsync(m => m.Id == s.MeetingId)).Status = MeetingStatus.Ended;
        else
        {
            var participant = await locked.MeetingParticipants.SingleAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Secretary);
            if (change == "role") participant.Role = MeetingRole.Member;
            else locked.Remove(participant);
        }
        await locked.SaveChangesAsync();
        await transaction.CommitAsync();
        var response = await request.WaitAsync(TimeSpan.FromSeconds(10));
        if (status == 404) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        else await AssertCode(response, status, code);
        await using var check = fixture.Db();
        Assert.Equal(20, (await check.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).PlannedDurationMinutes);
    }

    // Observing PostgreSQL's lock wait guarantees the request is queued before the winning commit.
    private async Task WaitForLockWaiter(Infrastructure.TrustCabinetDbContext db, Guid meetingId)
    {
        // pg_stat_activity is cached within a transaction; observe from a separate connection.
        await using var observer = fixture.Db();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!timeout.IsCancellationRequested)
        {
            var waiting = await observer.Database.SqlQueryRaw<int>("""
                SELECT count(*)::int AS "Value" FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock'
                    AND query LIKE '%FOR UPDATE%' AND pid <> pg_backend_pid()
                """).SingleAsync(timeout.Token);
            if (waiting > 0) return;
            await Task.Delay(10, timeout.Token);
        }
        throw new TimeoutException($"No lock waiter for meeting {meetingId}");
    }

    [Theory]
    [InlineData("start")]
    [InlineData("delete")]
    [InlineData("create")]
    public async Task Reorder_waiting_behind_a_changed_set_detects_stale_snapshot(string change)
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        var snapshot = await client.GetFromJsonAsync<JsonElement>(s.Url);
        await using var locked = fixture.Db();
        await using var transaction = await locked.Database.BeginTransactionAsync();
        await locked.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {s.MeetingId} FOR UPDATE").ToListAsync();
        var request = client.PutAsJsonAsync(s.Url + "/order", new { orderVersion = snapshot.GetProperty("orderVersion").GetString(), pendingItemIds = s.Items.Reverse().ToArray() });
        await WaitForLockWaiter(locked, s.MeetingId);
        var item = await locked.AgendaItems.SingleAsync(i => i.Id == s.Items[0]);
        if (change == "start") { item.Status = AgendaItemStatus.InProgress; item.ActualStartedAt = fixture.Clock.Now; }
        else if (change == "delete") locked.Remove(item);
        else locked.AgendaItems.Add(new() { MeetingId = s.MeetingId, Title = "New", Position = 8, PlannedDurationMinutes = 10 });
        await locked.SaveChangesAsync();
        await transaction.CommitAsync();
        await AssertCode(await request.WaitAsync(TimeSpan.FromSeconds(10)), 409, "MSG143");
    }

    [Fact]
    public async Task End_helper_has_no_implicit_save_and_rolls_back_with_its_caller()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Chair);
        await AssertCode(await client.PostAsync(s.Url + $"/{s.Items[0]}/current", null), 200, "MSG64");
        await using (var db = fixture.Db())
        {
            var helper = new AgendaMeetingEnd(db);
            await Assert.ThrowsAsync<InvalidOperationException>(() => helper.PrepareAsync(s.MeetingId, fixture.Clock.Now));
            await using var transaction = await db.Database.BeginTransactionAsync();
            var change = await helper.PrepareAsync(s.MeetingId, fixture.Clock.Now.AddSeconds(7));
            Assert.Equal("MeetingEnded", change!.ChangeType);
            await using (var before = fixture.Db()) Assert.Equal(AgendaItemStatus.InProgress, (await before.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).Status);
            var meeting = await db.Meetings.SingleAsync(m => m.Id == s.MeetingId);
            meeting.Status = MeetingStatus.Ended;
            meeting.ActualEndedAt = fixture.Clock.Now.AddSeconds(7);
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        await using var check = fixture.Db();
        Assert.Equal(AgendaItemStatus.InProgress, (await check.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).Status);
        Assert.Equal(MeetingStatus.InProgress, (await check.Meetings.SingleAsync(m => m.Id == s.MeetingId)).Status);
        await using var committed = await check.Database.BeginTransactionAsync();
        var completed = await new AgendaMeetingEnd(check).PrepareAsync(s.MeetingId, fixture.Clock.Now.AddSeconds(7));
        Assert.NotNull(completed);
        await check.SaveChangesAsync();
        await committed.CommitAsync();
        Assert.Equal(2, await check.AgendaItems.CountAsync(i => i.MeetingId == s.MeetingId && i.Status == AgendaItemStatus.Pending));
    }
}
