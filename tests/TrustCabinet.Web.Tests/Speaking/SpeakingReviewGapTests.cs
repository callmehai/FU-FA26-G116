using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Speaking;
using TrustCabinet.Web.Tests.Participants;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Speaking;

/// <summary>
/// SCRUM-27 review: the gaps the first test file leaves open — races with leaving the room, a guest revoked
/// mid-queue, the room closing through the LiveKit webhook, the Chair acting from outside the room, the ends of the
/// queue, and the authentication answers (UC-79, BR-337 → BR-339).
/// </summary>
public sealed class SpeakingReviewGapTests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    private static string Queue(ParticipantFixture.Scenario s) => s.Url + "/speaking-requests";

    private async Task InRoom(ParticipantFixture.Scenario s, CallMember member)
    {
        await using var scope = f.Host.Services.CreateAsyncScope();
        var calls = scope.ServiceProvider.GetRequiredService<ICallRepository>();
        var session = await calls.EnsureActiveRoomSessionAsync(s.MeetingId, s.Chair, f.Clock.Now);
        await calls.RecordJoinAsync(session.Id, member, f.Clock.Now);
    }

    private async Task Leave(ParticipantFixture.Scenario s, CallMember member)
    {
        await using var scope = f.Host.Services.CreateAsyncScope();
        var calls = scope.ServiceProvider.GetRequiredService<ICallRepository>();
        var session = (await calls.FindActiveRoomSessionAsync(s.MeetingId))!;
        await calls.RecordLeaveAsync(session.Id, member, f.Clock.Now);
    }

    private async Task Webhook(ParticipantFixture.Scenario s, string name, string? identity)
    {
        await using var scope = f.Host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICallRoomService>().HandleWebhookAsync(new CallWebhookEvent(
            "EV_" + Guid.NewGuid().ToString("N"), name, CallRoomNames.ForMeeting(s.MeetingId), identity, null, null,
            CallTrackSource.Unknown, f.Clock.Now));
    }

    /// <summary>Ends the person's stint without the room's cleanup, as if they now use the meeting page only.</summary>
    private async Task StintClosedQuietly(ParticipantFixture.Scenario s, Guid userId)
    {
        await using var db = f.Db();
        await db.CallParticipants.Where(p => p.UserId == userId && p.LeftAt == null && p.CallSession.MeetingId == s.MeetingId)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.LeftAt, f.Clock.Now));
    }

    private async Task<(HttpClient Client, MeetingGuestInvitation Invitation, Guid SessionId)> GuestInRoom(
        ParticipantFixture.Scenario s)
    {
        var invitation = await Attendance.AttendanceEndpointTests.Guest(f, s);
        var client = f.Guest(s.MeetingId, invitation.Id);
        var sessionId = f.Guests.Sessions.Values.Single(x => x.Identity.InvitationId == invitation.Id).Identity.SessionId;
        await using (var db = f.Db())
        {
            db.GuestSessions.Add(new GuestSession { Id = sessionId, InvitationId = invitation.Id, ExpiresAt = f.Clock.Now.AddHours(1) });
            await db.SaveChangesAsync();
        }
        await InRoom(s, CallMember.Guest(sessionId));
        return (client, invitation, sessionId);
    }

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

    private static Guid Mine(JsonElement queue) => queue.GetProperty("myRequestId").GetGuid();

    private static Guid[] Ids(JsonElement queue)
        => queue.GetProperty("requests").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToArray();

    private async Task<SpeakingRequestStatus> StatusOf(Guid id)
    {
        await using var db = f.Db();
        return (await db.SpeakingRequests.SingleAsync(r => r.Id == id)).Status;
    }

    [Fact]
    public async Task A_leave_arriving_while_a_hand_is_raised_waits_for_it_and_closes_it()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member));

        // The raise holds the meeting row when it locks the user row; the leave starts right then.
        Task? leave = null;
        var leaveWaited = false;
        f.Queries.BeforeUserLock = async () =>
        {
            leave = Task.Run(() => Leave(s, CallMember.User(s.Member)));
            await Task.Delay(500);
            leaveWaited = !leave.IsCompleted;
        };
        HttpResponseMessage raised;
        try { raised = await member.PostAsync(Queue(s), null); }
        finally { f.Queries.BeforeUserLock = null; }

        var id = Mine(await Json(raised, HttpStatusCode.Created));
        Assert.NotNull(leave);
        Assert.True(leaveWaited, "The leave did not wait for the meeting row held by the raise.");
        await leave!.WaitAsync(TimeSpan.FromSeconds(10));

        // No hand left raised by someone who is gone (BR-337, UC-79 EX-3).
        Assert.Equal(SpeakingRequestStatus.Cancelled, await StatusOf(id));
        Assert.Empty(Ids(await Json(await chair.GetAsync(Queue(s)), HttpStatusCode.OK)));
        await Code(await member.PostAsync(Queue(s), null), 409, "MSG458");
    }

    [Fact]
    public async Task Revoking_a_guest_mid_queue_frees_the_floor_and_shuts_the_guest_out()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var secretary = f.Client(s.Secretary); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member));
        var (guest, invitation, _) = await GuestInRoom(s);
        var guestRequest = Mine(await Json(await guest.PostAsync(Queue(s), null), HttpStatusCode.Created));
        var memberRequest = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        await Json(await chair.PostAsync($"{Queue(s)}/{guestRequest}/invite", null), HttpStatusCode.OK);

        await Code(await Write(secretary, s.Url + $"/guest-invitations/{invitation.Id}", HttpMethod.Delete,
            etag: await Tag(secretary, s.Url)), 200, "MSG103");

        Assert.Equal(SpeakingRequestStatus.Cancelled, await StatusOf(guestRequest));
        Assert.Equal(SpeakingRequestStatus.Queued, await StatusOf(memberRequest));
        Assert.Equal([memberRequest], Ids(await Json(await chair.GetAsync(Queue(s)), HttpStatusCode.OK)));
        // BR-338: the revoked guest no longer holds the floor, so the next person can be invited.
        await Json(await chair.PostAsync($"{Queue(s)}/{memberRequest}/invite", null), HttpStatusCode.OK);

        // The revoked guest learns nothing more about the meeting.
        await NoBody(await guest.GetAsync(Queue(s)), HttpStatusCode.NotFound);
        await NoBody(await guest.PostAsync(Queue(s), null), HttpStatusCode.NotFound);
        await NoBody(await guest.PostAsync($"{Queue(s)}/{guestRequest}/finish", null), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Livekit_webhooks_close_the_hand_of_whoever_left_and_room_finished_closes_the_rest()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var secretary = f.Client(s.Secretary); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member)); await InRoom(s, CallMember.User(s.Secretary));
        var (guest, _, _) = await GuestInRoom(s);
        var speaking = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        var secretaryRequest = Mine(await Json(await secretary.PostAsync(Queue(s), null), HttpStatusCode.Created));
        var guestRequest = Mine(await Json(await guest.PostAsync(Queue(s), null), HttpStatusCode.Created));
        await Json(await chair.PostAsync($"{Queue(s)}/{speaking}/invite", null), HttpStatusCode.OK);
        await Json(await member.PostAsync($"{Queue(s)}/{speaking}/start", null), HttpStatusCode.OK);

        await using var chairHub = f.Hub(chair);
        var changed = Channel.CreateUnbounded<SpeakingChange>();
        chairHub.On<SpeakingChange>("speakingChanged", e => changed.Writer.TryWrite(e));
        await chairHub.StartAsync();
        await chairHub.InvokeAsync("JoinMeeting", s.MeetingId);

        // participant_left: only the person who left (UC-79 EX-3).
        await Webhook(s, "participant_left", s.Secretary.ToString("D"));
        Assert.Equal(SpeakingRequestStatus.Cancelled, await StatusOf(secretaryRequest));
        Assert.Equal(SpeakingRequestStatus.Speaking, await StatusOf(speaking));
        Assert.Equal("Left", (await changed.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).ChangeType);

        f.Clock.Now = f.Clock.Now.AddMinutes(2);
        await Webhook(s, "room_finished", null);

        await using (var db = f.Db())
        {
            var rows = await db.SpeakingRequests.Where(r => r.MeetingId == s.MeetingId).ToDictionaryAsync(r => r.Id);
            Assert.Equal(SpeakingRequestStatus.Finished, rows[speaking].Status);
            Assert.NotNull(rows[speaking].EndedAt);
            Assert.Equal(SpeakingRequestStatus.Cancelled, rows[guestRequest].Status);
            Assert.False(await db.CallSessions.AnyAsync(c => c.MeetingId == s.MeetingId && c.Status == CallStatus.Active));
        }
        Assert.Equal("Left", (await changed.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).ChangeType);
        Assert.Empty(Ids(await Json(await chair.GetAsync(Queue(s)), HttpStatusCode.OK)));
        await Code(await member.PostAsync(Queue(s), null), 409, "MSG458");
    }

    [Fact]
    public async Task Leaving_without_a_raised_hand_sends_no_speaking_event()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair); using var member = f.Client(s.Member);
        await InRoom(s, CallMember.User(s.Member)); await InRoom(s, CallMember.User(s.Secretary));
        await using var chairHub = f.Hub(chair);
        var changed = Channel.CreateUnbounded<SpeakingChange>();
        chairHub.On<SpeakingChange>("speakingChanged", e => changed.Writer.TryWrite(e));
        await chairHub.StartAsync();
        await chairHub.InvokeAsync("JoinMeeting", s.MeetingId);

        await Webhook(s, "participant_left", s.Secretary.ToString("D"));
        using (var timeout = new CancellationTokenSource(300))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await changed.Reader.ReadAsync(timeout.Token));

        // Someone with a raised hand leaving still tells the panels.
        await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created);
        await changed.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        await Webhook(s, "participant_left", s.Member.ToString("D"));
        Assert.Equal("Left", (await changed.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).ChangeType);
    }

    [Fact]
    public async Task The_chair_finishes_a_turn_from_outside_the_room_and_the_speaker_cannot()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member)); await InRoom(s, CallMember.User(s.Chair));

        // The Chair's own turn, finished after their stint ended (from the meeting page).
        var own = Mine(await Json(await chair.PostAsync(Queue(s), null), HttpStatusCode.Created));
        await Json(await chair.PostAsync($"{Queue(s)}/{own}/invite", null), HttpStatusCode.OK);
        await Json(await chair.PostAsync($"{Queue(s)}/{own}/start", null), HttpStatusCode.OK);
        await StintClosedQuietly(s, s.Chair);
        await Json(await chair.PostAsync($"{Queue(s)}/{own}/finish", null), HttpStatusCode.OK);
        Assert.Equal(SpeakingRequestStatus.Finished, await StatusOf(own));
        // Outside the room the Chair still cannot raise a hand.
        await Code(await chair.PostAsync(Queue(s), null), 409, "MSG458");

        // A member's turn: outside the room they cannot finish it themselves; the Chair can, from anywhere.
        var theirs = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        await Json(await chair.PostAsync($"{Queue(s)}/{theirs}/invite", null), HttpStatusCode.OK);
        await Json(await member.PostAsync($"{Queue(s)}/{theirs}/start", null), HttpStatusCode.OK);
        await StintClosedQuietly(s, s.Member);
        await Code(await member.PostAsync($"{Queue(s)}/{theirs}/finish", null), 409, "MSG458");
        await Json(await chair.PostAsync($"{Queue(s)}/{theirs}/finish", null), HttpStatusCode.OK);
        Assert.Equal(SpeakingRequestStatus.Finished, await StatusOf(theirs));
        await Code(await chair.PostAsync($"{Queue(s)}/{theirs}/finish", null), 409, "MSG460");
    }

    [Fact]
    public async Task Moving_at_the_ends_of_the_queue_changes_nothing_and_only_queued_requests_move()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var secretary = f.Client(s.Secretary); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member)); await InRoom(s, CallMember.User(s.Secretary)); await InRoom(s, CallMember.User(s.Chair));
        var a = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        var b = Mine(await Json(await secretary.PostAsync(Queue(s), null), HttpStatusCode.Created));
        var c = Mine(await Json(await chair.PostAsync(Queue(s), null), HttpStatusCode.Created));

        Assert.Equal([a, b, c], Ids(await Json(await chair.PostAsync($"{Queue(s)}/{a}/move-up", null), HttpStatusCode.OK)));
        Assert.Equal([a, b, c], Ids(await Json(await chair.PostAsync($"{Queue(s)}/{c}/move-down", null), HttpStatusCode.OK)));
        Assert.Equal([b, a, c], Ids(await Json(await chair.PostAsync($"{Queue(s)}/{a}/move-down", null), HttpStatusCode.OK)));

        // The invited person is shown first and is out of the reorderable queue: B is now the top of the queue.
        await Json(await chair.PostAsync($"{Queue(s)}/{c}/invite", null), HttpStatusCode.OK);
        await Code(await chair.PostAsync($"{Queue(s)}/{c}/move-up", null), 409, "MSG460");
        await Code(await chair.PostAsync($"{Queue(s)}/{c}/move-down", null), 409, "MSG460");
        Assert.Equal([c, b, a], Ids(await Json(await chair.PostAsync($"{Queue(s)}/{b}/move-up", null), HttpStatusCode.OK)));
        Assert.Equal([c, b, a], Ids(await Json(await chair.PostAsync($"{Queue(s)}/{a}/move-down", null), HttpStatusCode.OK)));

        // A lowered hand is not in the queue any more.
        await Json(await member.DeleteAsync($"{Queue(s)}/{a}"), HttpStatusCode.OK);
        await Code(await chair.PostAsync($"{Queue(s)}/{a}/move-up", null), 409, "MSG460");
    }

    [Fact]
    public async Task Anonymous_callers_get_401_and_a_deactivated_account_gets_403_without_a_body()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var anonymous = f.Client();
        await InRoom(s, CallMember.User(s.Member));
        var id = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Queue(s))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(Queue(s), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"{Queue(s)}/{id}/invite", null)).StatusCode);

        await using (var db = f.Db())
            await db.Users.Where(u => u.Id == s.Member).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));
        try
        {
            await NoBody(await member.GetAsync(Queue(s)), HttpStatusCode.Forbidden);
            await NoBody(await member.DeleteAsync($"{Queue(s)}/{id}"), HttpStatusCode.Forbidden);
            await NoBody(await member.PostAsync(Queue(s), null), HttpStatusCode.Forbidden);
        }
        finally
        {
            await using var db = f.Db();
            await db.Users.Where(u => u.Id == s.Member).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, true));
        }
        Assert.Equal(SpeakingRequestStatus.Queued, await StatusOf(id));
    }

    [Fact]
    public async Task A_guest_on_two_devices_keeps_the_hand_when_only_the_other_device_leaves()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair);
        var (laptop, invitation, _) = await GuestInRoom(s);
        // The same invitation verified again on a phone: a second guest session, also in the room.
        using var phone = f.Guest(s.MeetingId, invitation.Id);
        var phoneSession = f.Guests.Sessions.Values.Where(x => x.Identity.InvitationId == invitation.Id)
            .Select(x => x.Identity.SessionId).Max();
        await using (var db = f.Db())
        {
            db.GuestSessions.Add(new GuestSession { Id = phoneSession, InvitationId = invitation.Id, ExpiresAt = f.Clock.Now.AddHours(1) });
            await db.SaveChangesAsync();
        }
        await InRoom(s, CallMember.Guest(phoneSession));

        var id = Mine(await Json(await laptop.PostAsync(Queue(s), null), HttpStatusCode.Created));
        await Leave(s, CallMember.Guest(phoneSession));

        // The guest is still in the room on the laptop (UC-79 EX-3 is about the person leaving).
        Assert.Equal(SpeakingRequestStatus.Queued, await StatusOf(id));
        Assert.Equal([id], Ids(await Json(await chair.GetAsync(Queue(s)), HttpStatusCode.OK)));
    }

    [Fact]
    public async Task Someone_elses_request_answers_404_whatever_the_meeting_status()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var secretary = f.Client(s.Secretary); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member));
        var id = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        // The Chair declines, never lowers someone else's hand.
        await NoBody(await chair.DeleteAsync($"{Queue(s)}/{id}"), HttpStatusCode.NotFound);

        await using (var scope = f.Host.Services.CreateAsyncScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<TrustCabinet.Domain.Meetings.IMeetingLifecycleService>()
                .TransitionAsync(s.MeetingId, new TrustCabinet.Domain.Meetings.MeetingTransitionCommand(
                    MeetingTransitionAction.End), s.Chair)).Succeeded);

        // Contract: an owner-only action on someone else's request is 404, as for a request that does not exist.
        await NoBody(await secretary.DeleteAsync($"{Queue(s)}/{id}"), HttpStatusCode.NotFound);
        await NoBody(await secretary.PostAsync($"{Queue(s)}/{id}/start", null), HttpStatusCode.NotFound);
    }
}
