using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Users;
using TrustCabinet.Web.Endpoints;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Calls;

public sealed class CallEndpointsTests : IDisposable
{
    private readonly Factory _factory = new();
    private readonly Guid _meetingId = Guid.CreateVersion7();

    public void Dispose() => _factory.Dispose();

    private sealed class Factory : TrustCabinetApiFactory
    {
        internal FakeCallRepository Calls { get; } = new();

        internal FakeLiveKitGateway LiveKit { get; } = new();

        internal FakeUserAccountRepository Accounts { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserAccountRepository>();
                services.AddSingleton<IUserAccountRepository>(Accounts);
                services.RemoveAll<ICallRepository>();
                services.AddSingleton<ICallRepository>(Calls);
                services.RemoveAll<ILiveKitGateway>();
                services.AddSingleton<ILiveKitGateway>(LiveKit);
            });
        }

        internal HttpClient ClientFor(User user)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", Services.GetRequiredService<ITokenIssuer>().CreateAccessToken(user, []));
            return client;
        }
    }

    private User GivenUser(string email)
    {
        var user = new User { FullName = email, Email = email, PasswordHash = "test-hash", RoleId = BuiltInRoles.Member };
        _factory.Accounts.WithUser(user);
        return user;
    }

    private string CallPath => $"/api/meetings/{_meetingId}/call";

    [Fact]
    public async Task Invited_member_sees_the_room_and_gets_a_token()
    {
        var member = GivenUser("member@trustcabinet.local");
        _factory.Calls.Invite(_meetingId, member.Id);
        var client = _factory.ClientFor(member);

        var room = await client.GetFromJsonAsync<CallRoomResponse>(CallPath);
        Assert.Equal("Open", room!.Status);
        Assert.Equal("Member", room.MyRole);

        var join = await client.PostAsync($"{CallPath}/join", null);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        var ticket = await join.Content.ReadFromJsonAsync<CallJoinResponse>();
        Assert.Equal(CallRoomNames.ForMeeting(_meetingId), ticket!.RoomName);
        Assert.Equal(member.Id.ToString("D"), ticket.Identity);
    }

    [Fact]
    public async Task Someone_not_invited_gets_the_same_empty_404_as_an_unknown_meeting()
    {
        var client = _factory.ClientFor(GivenUser("outsider@trustcabinet.local"));

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(CallPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"{CallPath}/join", null)).StatusCode);
    }

    [Fact]
    public async Task Anonymous_callers_are_rejected()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsync($"{CallPath}/join", null)).StatusCode);

    [Theory]
    [InlineData(MeetingStatus.Published, MeetingMode.Online, HttpStatusCode.Conflict, "MSG117")]
    [InlineData(MeetingStatus.InProgress, MeetingMode.InPerson, HttpStatusCode.Conflict, "MSG280")]
    public async Task Join_refusals_carry_their_code(
        MeetingStatus status, MeetingMode mode, HttpStatusCode expected, string code)
    {
        var member = GivenUser("member@trustcabinet.local");
        _factory.Calls.Invite(_meetingId, member.Id, status: status, mode: mode);

        var response = await _factory.ClientFor(member).PostAsync($"{CallPath}/join", null);

        await AssertError(response, expected, code);
    }

    [Fact]
    public async Task Join_without_livekit_configured_is_503()
    {
        var member = GivenUser("member@trustcabinet.local");
        _factory.Calls.Invite(_meetingId, member.Id);
        _factory.LiveKit.IsConfigured = false;

        await AssertError(await _factory.ClientFor(member).PostAsync($"{CallPath}/join", null),
            HttpStatusCode.ServiceUnavailable, "MSG281");
    }

    [Fact]
    public async Task Database_failures_have_their_own_codes()
    {
        var member = GivenUser("member@trustcabinet.local");
        _factory.Calls.ReadThrows = new TimeoutException();
        var client = _factory.ClientFor(member);

        await AssertError(await client.GetAsync(CallPath), HttpStatusCode.InternalServerError, "MSG283");
        await AssertError(await client.PostAsync($"{CallPath}/join", null), HttpStatusCode.InternalServerError, "MSG118");
        await AssertError(await client.PostAsync($"{CallPath}/participants/{Guid.CreateVersion7()}/mute", null),
            HttpStatusCode.InternalServerError, "MSG284");
    }

    [Fact]
    public async Task Only_the_chair_mutes_and_only_someone_with_a_microphone()
    {
        var chair = GivenUser("chair@trustcabinet.local");
        var member = GivenUser("member@trustcabinet.local");
        _factory.Calls.Invite(_meetingId, chair.Id, MeetingRole.Chair).Invite(_meetingId, member.Id);

        await AssertError(
            await _factory.ClientFor(member).PostAsync($"{CallPath}/participants/{chair.Id}/mute", null),
            HttpStatusCode.Forbidden, "MSG123");

        var chairClient = _factory.ClientFor(chair);
        await AssertError(
            await chairClient.PostAsync($"{CallPath}/participants/{member.Id}/mute", null),
            HttpStatusCode.Conflict, "MSG282");

        _factory.LiveKit.Join(CallRoomNames.ForMeeting(_meetingId), member.Id,
            new RoomTrack("TR_mic", CallTrackSource.Microphone, Muted: false));
        Assert.Equal(HttpStatusCode.NoContent,
            (await chairClient.PostAsync($"{CallPath}/participants/{member.Id}/mute", null)).StatusCode);

        _factory.LiveKit.Unavailable = new LiveKitUnavailableException("down");
        await AssertError(
            await chairClient.PostAsync($"{CallPath}/participants/{member.Id}/mute", null),
            HttpStatusCode.BadGateway, "MSG124");
    }

    [Fact]
    public async Task Mute_in_a_room_that_is_not_open_is_MSG117()
    {
        var chair = GivenUser("chair@trustcabinet.local");
        _factory.Calls.Invite(_meetingId, chair.Id, MeetingRole.Chair, status: MeetingStatus.Ended);

        await AssertError(
            await _factory.ClientFor(chair).PostAsync($"{CallPath}/participants/{Guid.CreateVersion7()}/mute", null),
            HttpStatusCode.Conflict, "MSG117");
    }

    [Fact]
    public async Task Webhook_with_a_bad_signature_is_401_and_a_good_one_is_handled()
    {
        var client = _factory.CreateClient();
        var body = new StringContent("{}");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/livekit/webhook", body)).StatusCode);

        var member = Guid.CreateVersion7();
        _factory.LiveKit.NextWebhook = new CallWebhookEvent(
            "EV_1", "participant_joined", CallRoomNames.ForMeeting(_meetingId), member.ToString("D"),
            DateTimeOffset.UtcNow, null, CallTrackSource.Unknown, DateTimeOffset.UtcNow);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/livekit/webhook", new StringContent("{}"))).StatusCode);
        Assert.Equal(member, Assert.Single(_factory.Calls.Stints).UserId);
    }

    [Fact]
    public async Task Webhook_is_503_when_livekit_is_not_configured()
    {
        _factory.LiveKit.IsConfigured = false;

        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await _factory.CreateClient().PostAsync("/api/livekit/webhook", new StringContent("{}"))).StatusCode);
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CallErrorResponse>();
        Assert.Equal(code, body!.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));
    }
}
