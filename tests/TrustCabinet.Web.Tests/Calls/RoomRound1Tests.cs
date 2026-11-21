using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Application.Authentication;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Calls;
using TrustCabinet.Application.Users;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Web.Tests.Participants;
using TrustCabinet.Web.Tests.Users;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Calls;

/// <summary>
/// Round 1 gap tests for FE-08 Online Room (Report 5.2.2): the <c>Manual only</c> cases of
/// <c>cases/FE-08/api.json</c> that the lightweight fake call room can answer (room state, join,
/// chair mute, the LiveKit webhook). LiveKit itself is the <see cref="FakeLiveKitGateway"/>.
/// </summary>
public sealed class RoomCallRound1Tests : IDisposable
{
    private readonly Factory _factory = new();
    private readonly Guid _meetingId = Guid.CreateVersion7();

    public void Dispose() => _factory.Dispose();

    private sealed class Factory : TrustCabinetApiFactory
    {
        internal FakeCallRepository Calls { get; } = new();

        internal FakeLiveKitGateway LiveKit { get; } = new();

        internal FakeUserAccountRepository Accounts { get; } = new();

        internal FakeAuditService Audit { get; } = new();

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
                services.RemoveAll<IAuditService>();
                services.AddSingleton<IAuditService>(Audit);
            });
        }

        internal HttpClient ClientFor(User user)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
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

    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode status)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task NoBody(HttpResponseMessage response, HttpStatusCode status)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {text}");
        Assert.Equal(string.Empty, text);
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await Json(response, status);
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Unconfigured_livekit_reports_the_room_unavailable()
    {
        // ROOM_API_IT06: the build has no LiveKit settings, but the room state itself still loads.
        var member = GivenUser("member@trustcabinet.local");
        _factory.Calls.Invite(_meetingId, member.Id);
        _factory.LiveKit.IsConfigured = false;

        var room = await Json(await _factory.ClientFor(member).GetAsync(CallPath), HttpStatusCode.OK);
        Assert.Equal("Open", room.GetProperty("status").GetString());
        Assert.False(room.GetProperty("available").GetBoolean());
        Assert.Equal(JsonValueKind.Null, room.GetProperty("participantCount").ValueKind);
    }

    [Fact]
    public async Task Deactivated_member_reads_nothing()
    {
        // ROOM_API_IT11: the token is still valid, but the account is gone.
        var member = GivenUser("member@trustcabinet.local");
        _factory.Calls.Invite(_meetingId, member.Id);
        var client = _factory.ClientFor(member);
        member.IsActive = false;

        await NoBody(await client.GetAsync(CallPath), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deactivated_member_joins_nothing()
    {
        // ROOM_API_IT22: the token is still valid, but the account is gone, so no ticket is issued.
        var member = GivenUser("member@trustcabinet.local");
        _factory.Calls.Invite(_meetingId, member.Id);
        var client = _factory.ClientFor(member);
        member.IsActive = false;

        await NoBody(await client.PostAsync($"{CallPath}/join", null), HttpStatusCode.Forbidden);
        Assert.Null(_factory.LiveKit.LastTicket);
    }

    [Fact]
    public async Task Former_chair_mutes_nothing_after_handover()
    {
        // ROOM_API_IT32: the role is re-read on every request, so the handed-over Chair is refused at once.
        var chair = GivenUser("chair@trustcabinet.local");
        var member = GivenUser("member@trustcabinet.local");
        var secretary = GivenUser("secretary@trustcabinet.local");
        _factory.Calls.Invite(_meetingId, chair.Id, MeetingRole.Chair)
            .Invite(_meetingId, member.Id)
            .Invite(_meetingId, secretary.Id, MeetingRole.Secretary);
        _factory.LiveKit.Join(CallRoomNames.ForMeeting(_meetingId), secretary.Id,
            new RoomTrack("TR_sec", CallTrackSource.Microphone, Muted: false));

        // UC-73 hands the Chair role to the member; the fake access rows move with it.
        _factory.Calls.Access[(_meetingId, chair.Id)] =
            _factory.Calls.Access[(_meetingId, chair.Id)] with { Role = MeetingRole.Member };
        _factory.Calls.Access[(_meetingId, member.Id)] =
            _factory.Calls.Access[(_meetingId, member.Id)] with { Role = MeetingRole.Chair };

        await AssertError(
            await _factory.ClientFor(chair).PostAsync($"{CallPath}/participants/{secretary.Id}/mute", null),
            HttpStatusCode.Forbidden, "MSG123");
        Assert.Empty(_factory.LiveKit.Muted);
    }

    [Fact]
    public async Task Outsider_mutes_nothing()
    {
        // ROOM_API_IT38: someone not on the roster learns nothing about the room, as for an unknown meeting.
        var member = GivenUser("member@trustcabinet.local");
        _factory.Calls.Invite(_meetingId, member.Id);
        var outsider = GivenUser("outsider@trustcabinet.local");

        await NoBody(
            await _factory.ClientFor(outsider).PostAsync($"{CallPath}/participants/{member.Id}/mute", null),
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Anonymous_mute_is_rejected()
    {
        // ROOM_API_IT39.
        var response = await _factory.CreateClient()
            .PostAsync($"{CallPath}/participants/{Guid.CreateVersion7()}/mute", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Mute_without_livekit_is_unavailable()
    {
        // ROOM_API_IT40: the Chair asks, but the room provider is not configured on this build.
        var chair = GivenUser("chair@trustcabinet.local");
        var member = GivenUser("member@trustcabinet.local");
        _factory.Calls.Invite(_meetingId, chair.Id, MeetingRole.Chair).Invite(_meetingId, member.Id);
        _factory.LiveKit.Join(CallRoomNames.ForMeeting(_meetingId), member.Id,
            new RoomTrack("TR_mic", CallTrackSource.Microphone, Muted: false));
        _factory.LiveKit.IsConfigured = false;

        var body = await Json(
            await _factory.ClientFor(chair).PostAsync($"{CallPath}/participants/{member.Id}/mute", null),
            HttpStatusCode.ServiceUnavailable);
        Assert.Equal("MSG281", body.GetProperty("code").GetString());
        Assert.Equal("Phòng họp trực tuyến chưa sẵn sàng. Vui lòng liên hệ quản trị viên.",
            body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Oversized_webhook_body_is_rejected()
    {
        // ROOM_API_IT45: a webhook body is a few KB; anything over 1 MB is not LiveKit.
        var body = new ByteArrayContent(new byte[1024 * 1024 + 1]);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            (await _factory.CreateClient().PostAsync("/api/livekit/webhook", body)).StatusCode);
        Assert.Empty(_factory.Calls.Stints);
    }
}

/// <summary>
/// Round 1 gap tests for FE-08 (Report 5.2.2) that need the real database: delegation, the speaking
/// queue (UC-79) and the Secretary's notes (UC-82). Room presence is staged as LiveKit would report it.
/// </summary>
public sealed class SpeakingNotesRound1Tests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    private static string Queue(ParticipantFixture.Scenario s) => s.Url + "/speaking-requests";

    private static string Notes(ParticipantFixture.Scenario s) => $"/api/meetings/{s.MeetingId}/notes";

    private async Task InRoom(ParticipantFixture.Scenario s, CallMember member)
    {
        await using var scope = f.Host.Services.CreateAsyncScope();
        var calls = scope.ServiceProvider.GetRequiredService<ICallRepository>();
        var session = await calls.EnsureActiveRoomSessionAsync(s.MeetingId, s.Chair, f.Clock.Now);
        await calls.RecordJoinAsync(session.Id, member, f.Clock.Now);
    }

    /// <summary>Ends the stint without the room's cleanup, as if the person now uses the meeting page only.</summary>
    private async Task StintClosedQuietly(ParticipantFixture.Scenario s, Guid userId)
    {
        await using var db = f.Db();
        await db.CallParticipants.Where(p => p.UserId == userId && p.LeftAt == null && p.CallSession.MeetingId == s.MeetingId)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.LeftAt, f.Clock.Now));
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode status)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static Guid Mine(JsonElement queue) => queue.GetProperty("myRequestId").GetGuid();

    private static async Task NoBody(HttpResponseMessage response, HttpStatusCode status)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {text}");
        Assert.Equal(string.Empty, text);
    }

    private async Task<SpeakingRequestStatus> StatusOf(Guid id)
    {
        await using var db = f.Db();
        return (await db.SpeakingRequests.SingleAsync(r => r.Id == id)).Status;
    }

    [Fact]
    public async Task Delegated_member_reads_no_room()
    {
        // ROOM_API_IT09: a member who delegated attendance gave their seat away (UC-37 PRE-3, BR-323).
        var s = await f.Seed(MeetingStatus.InProgress);
        await using (var db = f.Db())
        {
            var meeting = await db.Meetings.SingleAsync(m => m.Id == s.MeetingId);
            meeting.Mode = MeetingMode.Online;
            var participant = await db.MeetingParticipants
                .SingleAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Member);
            participant.Response = InvitationResponse.Delegated;
            await db.SaveChangesAsync();
        }

        using var member = f.Client(s.Member);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/meetings/{s.MeetingId}/call")).StatusCode);
    }

    [Fact]
    public async Task Organizer_outside_the_roster_still_reads_the_queue()
    {
        // ROOM_API_IT56: any reader of the meeting sees the queue, but only the Chair manages it.
        var s = await f.Seed(MeetingStatus.InProgress);
        using var organizer = f.Client(s.Organizer);

        var queue = await Json(await organizer.GetAsync(Queue(s)), HttpStatusCode.OK);
        Assert.False(queue.GetProperty("canManage").GetBoolean());
    }

    [Fact]
    public async Task Invited_person_lowers_the_hand_and_frees_the_floor()
    {
        // ROOM_API_IT72: lowering an invited hand cancels it, so the Chair can invite the next person.
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var secretary = f.Client(s.Secretary); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member)); await InRoom(s, CallMember.User(s.Secretary));
        var invited = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        var waiting = Mine(await Json(await secretary.PostAsync(Queue(s), null), HttpStatusCode.Created));
        await Json(await chair.PostAsync($"{Queue(s)}/{invited}/invite", null), HttpStatusCode.OK);

        await Json(await member.DeleteAsync($"{Queue(s)}/{invited}"), HttpStatusCode.OK);
        await using (var db = f.Db())
        {
            var row = await db.SpeakingRequests.SingleAsync(r => r.Id == invited);
            Assert.Equal(SpeakingRequestStatus.Cancelled, row.Status);
            Assert.NotNull(row.EndedAt);
        }
        await Json(await chair.PostAsync($"{Queue(s)}/{waiting}/invite", null), HttpStatusCode.OK);
    }

    [Fact]
    public async Task Lowering_a_hand_while_speaking_is_refused()
    {
        // ROOM_API_IT75: a speaking turn is closed with finish, not by lowering the hand (409 MSG460).
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member));
        var id = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        await Json(await chair.PostAsync($"{Queue(s)}/{id}/invite", null), HttpStatusCode.OK);
        await Json(await member.PostAsync($"{Queue(s)}/{id}/start", null), HttpStatusCode.OK);

        await Code(await member.DeleteAsync($"{Queue(s)}/{id}"), 409, "MSG460");
        Assert.Equal(SpeakingRequestStatus.Speaking, await StatusOf(id));
    }

    [Fact]
    public async Task Lowering_a_hand_from_outside_the_room_is_refused()
    {
        // ROOM_API_IT76: the browser lost its room connection, so the hand stays raised (409 MSG458).
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member);
        await InRoom(s, CallMember.User(s.Member));
        var id = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        await StintClosedQuietly(s, s.Member);

        await Code(await member.DeleteAsync($"{Queue(s)}/{id}"), 409, "MSG458");
        Assert.Equal(SpeakingRequestStatus.Queued, await StatusOf(id));
    }

    [Fact]
    public async Task Anonymous_speaking_lower_is_rejected()
    {
        // ROOM_API_IT77.
        var s = await f.Seed(MeetingStatus.InProgress);
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.DeleteAsync($"{Queue(s)}/{Guid.CreateVersion7()}")).StatusCode);
    }

    [Fact]
    public async Task Chair_invites_from_the_meeting_page()
    {
        // ROOM_API_IT79: the Chair runs the queue from the room or the meeting page alike (BR-338).
        // Deviation on UC-79 PRE-2 (noted on SCRUM-27): the code lets the Chair invite without being in the room.
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member));
        var id = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));

        await Json(await chair.PostAsync($"{Queue(s)}/{id}/invite", null), HttpStatusCode.OK);
    }

    [Fact]
    public async Task Inviting_a_lowered_hand_is_refused()
    {
        // ROOM_API_IT82: a lowered hand is no longer queued (409 MSG460).
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member));
        var id = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        await Json(await member.DeleteAsync($"{Queue(s)}/{id}"), HttpStatusCode.OK);

        await Code(await chair.PostAsync($"{Queue(s)}/{id}/invite", null), 409, "MSG460");
    }

    [Fact]
    public async Task Starting_a_turn_that_was_not_invited_is_refused()
    {
        // ROOM_API_IT86: only an invited hand can start (409 MSG460).
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member);
        await InRoom(s, CallMember.User(s.Member));
        var id = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));

        await Code(await member.PostAsync($"{Queue(s)}/{id}/start", null), 409, "MSG460");
        Assert.Equal(SpeakingRequestStatus.Queued, await StatusOf(id));
    }

    [Fact]
    public async Task Starting_a_turn_from_outside_the_room_is_refused()
    {
        // ROOM_API_IT88: an invited person who left the room cannot start (409 MSG458).
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member));
        var id = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        await Json(await chair.PostAsync($"{Queue(s)}/{id}/invite", null), HttpStatusCode.OK);
        await StintClosedQuietly(s, s.Member);

        await Code(await member.PostAsync($"{Queue(s)}/{id}/start", null), 409, "MSG458");
        Assert.Equal(SpeakingRequestStatus.Invited, await StatusOf(id));
    }

    [Fact]
    public async Task Anonymous_speaking_start_is_rejected()
    {
        // ROOM_API_IT89.
        var s = await f.Seed(MeetingStatus.InProgress);
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync($"{Queue(s)}/{Guid.CreateVersion7()}/start", null)).StatusCode);
    }

    [Fact]
    public async Task Chair_finishes_an_invited_turn_that_never_started()
    {
        // ROOM_API_IT91: finishing an invited turn cancels it and frees the floor for the next person.
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var secretary = f.Client(s.Secretary); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member)); await InRoom(s, CallMember.User(s.Secretary));
        var invited = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        await Json(await chair.PostAsync($"{Queue(s)}/{invited}/invite", null), HttpStatusCode.OK);

        await Json(await chair.PostAsync($"{Queue(s)}/{invited}/finish", null), HttpStatusCode.OK);
        await using (var db = f.Db())
        {
            var row = await db.SpeakingRequests.SingleAsync(r => r.Id == invited);
            Assert.Equal(SpeakingRequestStatus.Cancelled, row.Status);
            Assert.NotNull(row.EndedAt);
        }
        var waiting = Mine(await Json(await secretary.PostAsync(Queue(s), null), HttpStatusCode.Created));
        await Json(await chair.PostAsync($"{Queue(s)}/{waiting}/invite", null), HttpStatusCode.OK);
    }

    [Fact]
    public async Task Anonymous_speaking_finish_is_rejected()
    {
        // ROOM_API_IT96.
        var s = await f.Seed(MeetingStatus.InProgress);
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync($"{Queue(s)}/{Guid.CreateVersion7()}/finish", null)).StatusCode);
    }

    [Fact]
    public async Task Declining_an_invited_person_frees_the_floor()
    {
        // ROOM_API_IT98: declining withdraws the invitation (AF-3), so the next hand can be invited at once.
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var secretary = f.Client(s.Secretary); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member)); await InRoom(s, CallMember.User(s.Secretary));
        var invited = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        var waiting = Mine(await Json(await secretary.PostAsync(Queue(s), null), HttpStatusCode.Created));
        await Json(await chair.PostAsync($"{Queue(s)}/{invited}/invite", null), HttpStatusCode.OK);

        await Json(await chair.PostAsync($"{Queue(s)}/{invited}/decline", null), HttpStatusCode.OK);
        Assert.Equal(SpeakingRequestStatus.Cancelled, await StatusOf(invited));
        await Json(await chair.PostAsync($"{Queue(s)}/{waiting}/invite", null), HttpStatusCode.OK);
    }

    [Fact]
    public async Task Anonymous_speaking_decline_is_rejected()
    {
        // ROOM_API_IT101.
        var s = await f.Seed(MeetingStatus.InProgress);
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync($"{Queue(s)}/{Guid.CreateVersion7()}/decline", null)).StatusCode);
    }

    [Fact]
    public async Task Anonymous_speaking_move_up_is_rejected()
    {
        // ROOM_API_IT106.
        var s = await f.Seed(MeetingStatus.InProgress);
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync($"{Queue(s)}/{Guid.CreateVersion7()}/move-up", null)).StatusCode);
    }

    [Fact]
    public async Task Anonymous_speaking_move_down_is_rejected()
    {
        // ROOM_API_IT111.
        var s = await f.Seed(MeetingStatus.InProgress);
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync($"{Queue(s)}/{Guid.CreateVersion7()}/move-down", null)).StatusCode);
    }

    [Fact]
    public async Task Deactivated_secretary_reads_no_notes()
    {
        // ROOM_API_IT120: the token is still valid, but the account is gone.
        var s = await f.Seed(MeetingStatus.InProgress);
        using var secretary = f.Client(s.Secretary);
        Assert.Equal(HttpStatusCode.OK, (await secretary.GetAsync(Notes(s))).StatusCode);

        await using (var db = f.Db())
            await db.Users.Where(u => u.Id == s.Secretary)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));

        await NoBody(await secretary.GetAsync(Notes(s)), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_notes_save_is_rejected()
    {
        // ROOM_API_IT135: nothing is written without a signed-in Secretary.
        var s = await f.Seed(MeetingStatus.InProgress);
        using var anonymous = f.Client();
        var response = await anonymous.PutAsJsonAsync(Notes(s),
            new { agendaItemId = (Guid?)null, body = "x", updatedAt = (DateTimeOffset?)null });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var db = f.Db();
        Assert.False(await db.MeetingNotes.AnyAsync(n => n.MeetingId == s.MeetingId));
    }
}
