using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.GuestAccess.GuestAccessFixture;

namespace TrustCabinet.Web.Tests.GuestAccess;

/// <summary>
/// SCRUM-120: the production guest adapters behind Agenda (UC-26), Participants and the meeting hub,
/// driven by sessions opened through the real UC-04 flow rather than test adapters.
/// </summary>
public sealed class GuestMeetingAccessTests(GuestAccessFixture f) : IClassFixture<GuestAccessFixture>
{
    private static string Agenda(Guid meetingId) => $"/api/meetings/{meetingId}/agenda";

    [Fact]
    public async Task Guest_reads_the_invited_meeting_like_a_member_and_nothing_else()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        var other = await f.Seed(MeetingStatus.InProgress);
        int users;
        await using (var db = f.Db()) users = await db.Users.CountAsync();
        var (guest, _) = await f.SignIn(s);

        var snapshot = await Expect(await guest.GetAsync(Agenda(s.MeetingId)), 200);
        Assert.All(snapshot.GetProperty("permissions").EnumerateObject(), p => Assert.False(p.Value.GetBoolean()));
        Assert.All(snapshot.GetProperty("items").EnumerateArray(), i =>
            Assert.All(i.GetProperty("actions").EnumerateObject(), p => Assert.False(p.Value.GetBoolean())));
        using (var foreign = await guest.GetAsync(Agenda(other.MeetingId)))
        {
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
            Assert.Empty(await foreign.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync(Agenda(s.MeetingId) + "/editor-options")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.PostAsJsonAsync(Agenda(s.MeetingId), new { title = "Khách thêm" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.PutAsJsonAsync(Agenda(s.MeetingId) + $"/{s.ItemId}", new { title = "Khách sửa" })).StatusCode);

        var roster = await Expect(await guest.GetAsync($"/api/meetings/{s.MeetingId}/participants"), 200);
        var row = roster.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("kind").GetString() == "Guest");
        Assert.Equal("Khách tự khai", row.GetProperty("selfDeclaredDisplayName").GetString());
        Assert.Equal("Khách mời", row.GetProperty("invitedName").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/meetings/{other.MeetingId}/participants")).StatusCode);
        await Expect(await guest.PutAsJsonAsync($"/api/meetings/{s.MeetingId}/invitation-response", new { response = "Accepted" }), 200, "MSG105");

        // Internal APIs never accept a guest token: no Guid subject, no capability.
        foreach (var url in new[] { "/api/meetings", $"/api/meetings/{s.MeetingId}", $"/api/meetings/{s.MeetingId}/documents",
                     "/api/notifications", "/api/documents", "/api/users" })
            Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/auth/me")).StatusCode);

        await using var check = f.Db();
        Assert.Equal(users, await check.Users.CountAsync());
    }

    [Fact]
    public async Task Meeting_is_closed_until_the_guest_declares_a_display_name()
    {
        var s = await f.Seed();
        var (guest, _) = await f.SignIn(s, profile: false);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync(Agenda(s.MeetingId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/meetings/{s.MeetingId}/participants")).StatusCode);
        await Expect(await guest.PutAsJsonAsync("/api/guest-access/session/profile", new { displayName = "Khách" }), 200, "MSG376");
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync(Agenda(s.MeetingId))).StatusCode);
    }

    [Fact]
    public async Task Logout_revocation_and_expiry_end_access_per_session_or_per_invitation()
    {
        var s = await f.Seed();
        var (first, _) = await f.SignIn(s);
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var (second, _) = await f.SignIn(s);

        await first.DeleteAsync("/api/guest-access/session");
        Assert.Equal(HttpStatusCode.NotFound, (await first.GetAsync(Agenda(s.MeetingId))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync(Agenda(s.MeetingId))).StatusCode);

        await using (var db = f.Db())
            await db.MeetingGuestInvitations.Where(g => g.Id == s.InvitationId).ExecuteUpdateAsync(set => set
                .SetProperty(g => g.RevokedAt, f.Clock.Now).SetProperty(g => g.RevokedById, s.SecretaryId));
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync(Agenda(s.MeetingId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"/api/meetings/{s.MeetingId}/participants")).StatusCode);

        var later = await f.Seed();
        var (expiring, _) = await f.SignIn(later);
        f.Clock.Now = f.Clock.Now.AddHours(12);
        Assert.Equal(HttpStatusCode.NotFound, (await expiring.GetAsync(Agenda(later.MeetingId))).StatusCode);
    }

    [Fact]
    public async Task Guest_loses_every_read_when_the_meeting_ends_while_members_keep_theirs()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        var (guest, _) = await f.SignIn(s);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync(Agenda(s.MeetingId))).StatusCode);

        await using (var db = f.Db())
            await db.Meetings.Where(m => m.Id == s.MeetingId).ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, MeetingStatus.Ended));

        foreach (var path in new[] { "/agenda", "/participants", "/participant-context", "/invitation-response", "/attendance" })
        {
            using var denied = await guest.GetAsync($"/api/meetings/{s.MeetingId}{path}");
            Assert.True(denied.StatusCode == HttpStatusCode.NotFound, $"{path}: {(int)denied.StatusCode}");
        }
        await Expect(await guest.GetAsync("/api/guest-access/session"), 404, "MSG378");
        using var secretary = f.UserClient(s.SecretaryId);
        Assert.Equal(HttpStatusCode.OK, (await secretary.GetAsync(Agenda(s.MeetingId))).StatusCode);
    }

    [Fact]
    public async Task Hub_delivers_to_current_sessions_only_and_refuses_them_after_the_meeting_ends()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        var (first, firstToken) = await f.SignIn(s);
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var (_, secondToken) = await f.SignIn(s);
        var firstEvents = new ConcurrentQueue<JsonElement>();
        var secondEvents = new ConcurrentQueue<JsonElement>();
        await using var firstHub = Hub(firstToken);
        await using var secondHub = Hub(secondToken);
        firstHub.On<JsonElement>("agendaChanged", firstEvents.Enqueue);
        secondHub.On<JsonElement>("agendaChanged", secondEvents.Enqueue);
        await firstHub.StartAsync();
        await secondHub.StartAsync();
        await firstHub.InvokeAsync("JoinMeeting", s.MeetingId);
        await secondHub.InvokeAsync("JoinMeeting", s.MeetingId);
        var other = await f.Seed(MeetingStatus.InProgress);
        await Assert.ThrowsAsync<HubException>(() => firstHub.InvokeAsync("JoinMeeting", other.MeetingId));

        using var secretary = f.UserClient(s.SecretaryId);
        await Edit(secretary, s, "Mục đã sửa");
        await Until(() => firstEvents.Count == 1 && secondEvents.Count == 1);

        await first.DeleteAsync("/api/guest-access/session");
        await Edit(secretary, s, "Mục sửa lần hai");
        await Until(() => secondEvents.Count == 2);
        await Task.Delay(300);
        Assert.Single(firstEvents);

        await using (var db = f.Db())
            await db.Meetings.Where(m => m.Id == s.MeetingId).ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, MeetingStatus.Ended));
        await Assert.ThrowsAsync<HubException>(() => secondHub.InvokeAsync("JoinMeeting", s.MeetingId));
        await using var late = Hub(secondToken);
        await late.StartAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => late.InvokeAsync("JoinMeeting", s.MeetingId));
    }

    [Fact]
    public async Task Failure_to_read_the_session_is_an_error_not_a_grant()
    {
        var s = await f.Seed();
        var (guest, _) = await f.SignIn(s);
        f.ReadFailure.Enabled = true;
        try { await Expect(await guest.GetAsync(Agenda(s.MeetingId)), 500, "MSG128"); }
        finally { f.ReadFailure.Enabled = false; }
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync(Agenda(s.MeetingId))).StatusCode);
    }

    private static async Task Edit(HttpClient secretary, Scenario s, string title)
        => (await secretary.PutAsJsonAsync(Agenda(s.MeetingId) + $"/{s.ItemId}", new { title })).EnsureSuccessStatusCode();

    private HubConnection Hub(string token) => new HubConnectionBuilder()
        .WithUrl("http://localhost/hubs/meetings", options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.HttpMessageHandlerFactory = _ => f.Host.Server.CreateHandler();
            options.AccessTokenProvider = () => Task.FromResult<string?>(token);
        }).Build();

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(50);
        Assert.True(condition());
    }
}
