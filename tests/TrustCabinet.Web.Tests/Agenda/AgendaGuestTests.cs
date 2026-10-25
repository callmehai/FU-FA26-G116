using TrustCabinet.Domain.Meetings;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Agenda;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Agenda;

public sealed class AgendaGuestTests(AgendaFixture fixture) : IClassFixture<AgendaFixture>
{
    private MeetingGuestSession Grant(Guid meetingId, Guid? invitation = null)
    {
        var identity = new MeetingGuestSession(meetingId, invitation ?? Guid.NewGuid(), Guid.NewGuid());
        fixture.Guests.Sessions[identity.SessionId] = new(identity, fixture.Clock.Now.AddHours(1));
        return identity;
    }

    // Test-only wire format. UC-04 owns the production token/session format and resolver.
    private static string Token(MeetingGuestSession identity, string? subject = null, DateTimeOffset? expires = null)
        => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("TrustCabinet", "TrustCabinet",
            [new Claim("sub", subject ?? $"guest:{identity.SessionId:D}"), new Claim("test_guest", "true"),
             new Claim("test_meeting", identity.MeetingId.ToString()), new Claim("test_invitation", identity.InvitationId.ToString()),
             new Claim("test_session", identity.SessionId.ToString())], expires: (expires ?? DateTimeOffset.UtcNow.AddMinutes(5)).UtcDateTime,
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AgendaFixture.SigningKey)), SecurityAlgorithms.HmacSha256)));

    private HttpClient Client(MeetingGuestSession identity, string? subject = null)
    {
        var client = fixture.Client();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(identity, subject));
        return client;
    }

    private HubConnection Hub(MeetingGuestSession identity) => new HubConnectionBuilder()
        .WithUrl("http://localhost/hubs/meetings", options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.HttpMessageHandlerFactory = _ => fixture.Host.Server.CreateHandler();
            options.AccessTokenProvider = () => Task.FromResult<string?>(Token(identity));
        }).Build();

    [Fact]
    public async Task Guest_snapshot_has_member_document_metadata_but_no_write_permissions_or_user_identity()
    {
        var s = await fixture.Seed();
        var guest = Grant(s.MeetingId);
        using var secretary = fixture.Client(s.Secretary);
        await AssertCode(await secretary.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input(documents: [s.DocumentId])), 200, "MSG61");
        using var client = Client(guest);
        var snapshot = await client.GetFromJsonAsync<JsonElement>(s.Url);
        Assert.All(snapshot.GetProperty("permissions").EnumerateObject(), p => Assert.False(p.Value.GetBoolean()));
        var items = snapshot.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(3, items.Length);
        Assert.All(items, i =>
        {
            Assert.True(i.GetProperty("canReadDocuments").GetBoolean());
            Assert.All(i.GetProperty("actions").EnumerateObject(), p => Assert.False(p.Value.GetBoolean()));
        });
        Assert.Equal(s.DocumentId, Assert.Single(items[0].GetProperty("documents").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.DoesNotContain("@agenda.test", snapshot.GetRawText());
        Assert.DoesNotContain("private/test", snapshot.GetRawText());
        await using var db = fixture.Db();
        Assert.False(await db.Users.AnyAsync(u => u.Id == guest.InvitationId || u.Id == guest.SessionId));
        var other = await fixture.Seed();
        using var foreign = await client.GetAsync(other.Url);
        using var missing = await client.GetAsync($"/api/meetings/{Guid.NewGuid()}/agenda");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Empty(await foreign.Content.ReadAsStringAsync());
        Assert.Empty(await missing.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Guest_cannot_use_options_or_any_mutation_even_with_colliding_user_subject(bool collision)
    {
        var s = await fixture.Seed();
        var guest = Grant(s.MeetingId);
        using var client = Client(guest, collision ? s.Secretary.ToString() : null);
        var before = await client.GetStringAsync(s.Url);
        var events = fixture.Events.Count;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(s.Url + "/editor-options")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}/conclusion", new { conclusion = "Guest cannot write" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(s.Url, Input())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync(s.Url + $"/{s.Items[0]}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(s.Url + "/order", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(s.Url + $"/{s.Items[0]}/current", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(s.Url + $"/{s.Items[0]}/complete", null)).StatusCode);
        Assert.Equal(before, await client.GetStringAsync(s.Url));
        Assert.Equal(events, fixture.Events.Count);
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("expired")]
    [InlineData("unverified")]
    [InlineData("missing")]
    public async Task Current_guest_access_is_rechecked_on_each_read(string reason)
    {
        var s = await fixture.Seed();
        var guest = Grant(s.MeetingId);
        using var client = Client(guest);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(s.Url)).StatusCode);
        var access = fixture.Guests.Sessions[guest.SessionId];
        if (reason == "missing") fixture.Guests.Sessions.TryRemove(guest.SessionId, out _);
        else fixture.Guests.Sessions[guest.SessionId] = reason switch
        {
            "revoked" => access with { Revoked = true },
            "expired" => access with { ExpiresAt = fixture.Clock.Now },
            _ => access with { Verified = false }
        };
        using var denied = await client.GetAsync(s.Url);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Empty(await denied.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(MeetingStatus.Draft, 404)]
    [InlineData(MeetingStatus.WaitingApproval, 404)]
    [InlineData(MeetingStatus.Rejected, 404)]
    [InlineData(MeetingStatus.Approved, 404)]
    [InlineData(MeetingStatus.Published, 200)]
    [InlineData(MeetingStatus.Ended, 200)]
    public async Task Guest_keeps_member_meeting_visibility(MeetingStatus status, int expected)
    {
        var s = await fixture.Seed(status);
        using var client = Client(Grant(s.MeetingId));
        Assert.Equal(expected, (int)(await client.GetAsync(s.Url)).StatusCode);
    }

    [Fact]
    public async Task Default_adapter_denies_access_even_when_identity_resolver_is_installed()
    {
        var s = await fixture.Seed();
        var guest = Grant(s.MeetingId);
        await using var host = fixture.Host.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAgendaGuestAccess>();
            services.AddScoped<IAgendaGuestAccess, UnavailableAgendaGuestAccess>();
        }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(guest));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(s.Url)).StatusCode);
        Assert.Empty(await new UnavailableAgendaGuestAccess().ListReadersAsync(s.MeetingId, default));
        Assert.Null(new UnavailableMeetingGuestIdentityResolver().Resolve(new ClaimsPrincipal(new ClaimsIdentity("test"))));
    }

    [Fact]
    public async Task Invalid_identity_expired_token_and_adapter_failure_never_return_data()
    {
        var s = await fixture.Seed();
        var guest = Grant(s.MeetingId);
        using var invalid = Client(guest with { InvitationId = Guid.Empty });
        Assert.Equal(HttpStatusCode.NotFound, (await invalid.GetAsync(s.Url)).StatusCode);
        using var client = Client(guest);
        fixture.Guests.FailRead = true;
        try { await AssertCode(await client.GetAsync(s.Url), 500, "MSG128"); }
        finally { fixture.Guests.FailRead = false; }
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(guest, expires: DateTimeOffset.UtcNow.AddSeconds(-5)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(s.Url)).StatusCode);
    }

    [Fact]
    public async Task Guest_transport_is_scoped_to_meeting_and_session_and_rechecks_recipients()
    {
        var s = await fixture.Seed();
        var other = await fixture.Seed();
        var first = Grant(s.MeetingId);
        var second = Grant(s.MeetingId, first.InvitationId);
        var foreign = Grant(other.MeetingId);
        await using var a = Hub(first);
        await using var b = Hub(second);
        await using var c = Hub(foreign);
        var ar = Channel.CreateUnbounded<AgendaChange>();
        var br = Channel.CreateUnbounded<AgendaChange>();
        var cr = Channel.CreateUnbounded<AgendaChange>();
        a.On<AgendaChange>("agendaChanged", e => ar.Writer.TryWrite(e));
        b.On<AgendaChange>("agendaChanged", e => br.Writer.TryWrite(e));
        c.On<AgendaChange>("agendaChanged", e => cr.Writer.TryWrite(e));
        await Task.WhenAll(a.StartAsync(), b.StartAsync(), c.StartAsync());
        await a.InvokeAsync("JoinMeeting", s.MeetingId);
        await b.InvokeAsync("JoinMeeting", s.MeetingId);
        await c.InvokeAsync("JoinMeeting", other.MeetingId);
        var denied = await Assert.ThrowsAsync<HubException>(() => a.InvokeAsync("JoinMeeting", other.MeetingId));
        var missing = await Assert.ThrowsAsync<HubException>(() => a.InvokeAsync("JoinMeeting", Guid.NewGuid()));
        Assert.Equal(denied.Message, missing.Message);
        using var secretary = fixture.Client(s.Secretary);
        var created = await AssertCode(await secretary.PostAsJsonAsync(s.Url, Input()), 201, "MSG60");
        var change = await ar.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(created.GetProperty("agendaItemId").GetGuid(), change.AgendaItemId);
        await br.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        using var guestClient = Client(first);
        var snapshot = await guestClient.GetFromJsonAsync<JsonElement>(s.Url);
        Assert.Equal(4, snapshot.GetProperty("items").GetArrayLength());
        await NoEvent(cr.Reader);

        fixture.Guests.Sessions[first.SessionId] = fixture.Guests.Sessions[first.SessionId] with { ExpiresAt = fixture.Clock.Now };
        await AssertCode(await secretary.PostAsJsonAsync(s.Url, Input()), 201, "MSG60");
        await br.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await NoEvent(ar.Reader);
        await Assert.ThrowsAsync<HubException>(() => a.InvokeAsync("JoinMeeting", s.MeetingId));
        await a.InvokeAsync("LeaveMeeting", s.MeetingId);
        await a.InvokeAsync("LeaveMeeting", s.MeetingId);

        fixture.Guests.FailReaders = true;
        try { await AssertCode(await secretary.PostAsJsonAsync(s.Url, Input()), 201, "MSG60"); }
        finally { fixture.Guests.FailReaders = false; }
        await NoEvent(br.Reader);
        await b.StopAsync();
        await b.StartAsync();
        await b.InvokeAsync("JoinMeeting", s.MeetingId);
        using var secondClient = Client(second);
        Assert.Equal(6, (await secondClient.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items").GetArrayLength());
        fixture.Guests.Sessions[second.SessionId] = fixture.Guests.Sessions[second.SessionId] with { Revoked = true };
        await AssertCode(await secretary.PostAsJsonAsync(s.Url, Input()), 201, "MSG60");
        await NoEvent(br.Reader);
    }

    private static async Task NoEvent(ChannelReader<AgendaChange> reader)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await reader.ReadAsync(timeout.Token));
    }
}
