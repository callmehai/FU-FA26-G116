using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Agenda;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Agenda;

public sealed class AgendaHubTests(AgendaFixture fixture) : IClassFixture<AgendaFixture>
{
    [Fact]
    public async Task Real_transport_delivers_committed_changes_only_to_joined_authorized_readers()
    {
        var s = await fixture.Seed();
        var other = await fixture.Seed();
        using var secretary = fixture.Client(s.Secretary);
        using var member = fixture.Client(s.Member);
        using var chair = fixture.Client(s.Chair);
        await using var secretaryHub = fixture.Hub(s.Secretary);
        await using var viewer = fixture.Hub(s.Member);
        await using var outsider = fixture.Hub(s.Outsider);
        await using var otherHub = fixture.Hub(other.Member);
        var received = Channel.CreateUnbounded<AgendaChange>();
        var excluded = new ConcurrentQueue<AgendaChange>();
        viewer.On<AgendaChange>("agendaChanged", e => received.Writer.TryWrite(e));
        outsider.On<AgendaChange>("agendaChanged", e => excluded.Enqueue(e));
        otherHub.On<AgendaChange>("agendaChanged", e => excluded.Enqueue(e));
        await Task.WhenAll(secretaryHub.StartAsync(), viewer.StartAsync(), outsider.StartAsync(), otherHub.StartAsync());
        await secretaryHub.InvokeAsync("JoinMeeting", s.MeetingId);
        await otherHub.InvokeAsync("JoinMeeting", other.MeetingId);
        var forbidden = await Assert.ThrowsAsync<HubException>(() => outsider.InvokeAsync("JoinMeeting", s.MeetingId));
        var nonexistent = await Assert.ThrowsAsync<HubException>(() => outsider.InvokeAsync("JoinMeeting", Guid.NewGuid()));
        Assert.Equal(forbidden.Message, nonexistent.Message);
        await AssertCode(await secretary.PostAsJsonAsync(s.Url, Input()), 201, "MSG60");
        await viewer.InvokeAsync("JoinMeeting", s.MeetingId);
        await AssertNoEvent(received.Reader);

        var created = await AssertCode(await secretary.PostAsJsonAsync(s.Url, Input()), 201, "MSG60");
        var id = created.GetProperty("agendaItemId").GetGuid();
        var change = await Next(received.Reader, "Created");
        Assert.Equal(id, change.AgendaItemId);
        var snapshot = await member.GetFromJsonAsync<JsonElement>(s.Url);
        Assert.Contains(snapshot.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == id);
        await AssertCode(await secretary.PutAsJsonAsync(s.Url + $"/{id}", Input("Updated")), 200, "MSG61");
        await Next(received.Reader, "Updated");
        await AssertCode(await secretary.DeleteAsync(s.Url + $"/{id}"), 200, "MSG62");
        await Next(received.Reader, "Deleted");
        snapshot = await member.GetFromJsonAsync<JsonElement>(s.Url);
        var reversed = snapshot.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Reverse().ToArray();
        await AssertCode(await secretary.PutAsJsonAsync(s.Url + "/order", new { orderVersion = snapshot.GetProperty("orderVersion").GetString(), pendingItemIds = reversed }), 200, "MSG63");
        var reorder = await Next(received.Reader, "Reordered");
        Assert.Null(reorder.Title);
        await AssertCode(await chair.PostAsync(s.Url + $"/{reversed[0]}/current", null), 200, "MSG64");
        await Next(received.Reader, "Started");
        await AssertCode(await chair.PostAsync(s.Url + $"/{reversed[0]}/complete", null), 200, "MSG65");
        await Next(received.Reader, "Completed");
        Assert.Empty(excluded);

        await viewer.InvokeAsync("LeaveMeeting", s.MeetingId);
        await viewer.InvokeAsync("LeaveMeeting", s.MeetingId);
        await secretary.PostAsJsonAsync(s.Url, Input());
        await AssertNoEvent(received.Reader);
        await viewer.StopAsync();
        await viewer.StartAsync();
        await viewer.InvokeAsync("JoinMeeting", s.MeetingId);
        snapshot = await member.GetFromJsonAsync<JsonElement>(s.Url);
        Assert.Equal(5, snapshot.GetProperty("items").GetArrayLength());
        await secretary.PostAsJsonAsync(s.Url, Input());
        await Next(received.Reader, "Created");
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("removed")]
    [InlineData("delegated")]
    public async Task Stale_subscription_stops_receiving_after_loss_of_access(string reason)
    {
        var s = await fixture.Seed();
        using var secretary = fixture.Client(s.Secretary);
        await using var viewer = fixture.Hub(s.Member);
        var received = Channel.CreateUnbounded<AgendaChange>();
        viewer.On<AgendaChange>("agendaChanged", e => received.Writer.TryWrite(e));
        await viewer.StartAsync();
        await viewer.InvokeAsync("JoinMeeting", s.MeetingId);
        await secretary.PostAsJsonAsync(s.Url, Input());
        await Next(received.Reader, "Created");
        await using (var db = fixture.Db())
        {
            if (reason == "inactive") (await db.Users.SingleAsync(u => u.Id == s.Member)).IsActive = false;
            else
            {
                var row = await db.MeetingParticipants.SingleAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Member);
                if (reason == "removed") db.Remove(row);
                else row.Response = Domain.Enums.InvitationResponse.Delegated;
            }
            await db.SaveChangesAsync();
        }
        await secretary.PostAsJsonAsync(s.Url, Input());
        await AssertNoEvent(received.Reader);
    }

    [Fact]
    public async Task Noop_and_failed_write_emit_nothing_and_leave_is_connection_scoped()
    {
        var s = await fixture.Seed();
        using var secretary = fixture.Client(s.Secretary);
        await using var first = fixture.Hub(s.Member);
        await using var second = fixture.Hub(s.Member);
        var received = Channel.CreateUnbounded<AgendaChange>();
        second.On<AgendaChange>("agendaChanged", e => received.Writer.TryWrite(e));
        await Task.WhenAll(first.StartAsync(), second.StartAsync());
        await first.InvokeAsync("JoinMeeting", s.MeetingId);
        await second.InvokeAsync("JoinMeeting", s.MeetingId);
        await first.InvokeAsync("LeaveMeeting", s.MeetingId);
        await secretary.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input());
        await Next(received.Reader, "Updated");
        await secretary.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input());
        fixture.SaveFailure.Enabled = true;
        try { await AssertCode(await secretary.PostAsJsonAsync(s.Url, Input()), 503, "MSG68"); }
        finally { fixture.SaveFailure.Enabled = false; }
        await AssertNoEvent(received.Reader);
    }

    [Fact]
    public async Task Query_tokens_are_hub_only_and_expiring_connection_closes()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(s.Url + "?access_token=" + AgendaFixture.Token(s.Member))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/hubs/meetings/negotiate?negotiateVersion=1&access_token=" + AgendaFixture.Token(s.Member), null)).StatusCode);
        using var expired = fixture.Client(s.Member);
        expired.DefaultRequestHeaders.Authorization = new("Bearer", AgendaFixture.Token(s.Member, DateTimeOffset.UtcNow.AddSeconds(-5)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await expired.GetAsync(s.Url)).StatusCode);
        await using var hub = fixture.Hub(s.Member, DateTimeOffset.UtcNow.AddSeconds(2));
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.Closed += _ => { closed.TrySetResult(); return Task.CompletedTask; };
        await hub.StartAsync();
        await hub.InvokeAsync("JoinMeeting", s.MeetingId);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(HubConnectionState.Disconnected, hub.State);
    }

    private static async Task<AgendaChange> Next(ChannelReader<AgendaChange> reader, string type)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var change = await reader.ReadAsync(timeout.Token);
        Assert.Equal(type, change.ChangeType);
        return change;
    }
    private static async Task AssertNoEvent(ChannelReader<AgendaChange> reader)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await reader.ReadAsync(timeout.Token));
    }
}
