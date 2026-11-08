using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Speaking;
using TrustCabinet.Web.Tests.Participants;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Speaking;

/// <summary>SCRUM-27, UC-79 Register to Speak; BR-337, BR-338, BR-339.</summary>
public sealed class SpeakingEndpointTests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    private static string Queue(ParticipantFixture.Scenario s) => s.Url + "/speaking-requests";

    /// <summary>Puts the person in the meeting room as LiveKit would report them (UC-79 step 2).</summary>
    private async Task InRoom(ParticipantFixture.Scenario s, CallMember member)
    {
        await using var scope = f.Host.Services.CreateAsyncScope();
        var calls = scope.ServiceProvider.GetRequiredService<ICallRepository>();
        var session = await calls.EnsureActiveRoomSessionAsync(s.MeetingId, s.Chair, f.Clock.Now);
        await calls.RecordJoinAsync(session.Id, member, f.Clock.Now);
    }

    private async Task<Guid> Leave(ParticipantFixture.Scenario s, CallMember member)
    {
        await using var scope = f.Host.Services.CreateAsyncScope();
        var calls = scope.ServiceProvider.GetRequiredService<ICallRepository>();
        var session = (await calls.FindActiveRoomSessionAsync(s.MeetingId))!;
        await calls.RecordLeaveAsync(session.Id, member, f.Clock.Now);
        return session.Id;
    }

    /// <summary>A verified guest of the meeting, with a real session row so the room can hold them.</summary>
    private async Task<(HttpClient Client, MeetingGuestInvitation Invitation, Guid SessionId)> GuestInRoom(
        ParticipantFixture.Scenario s)
    {
        var invitation = await Attendance.AttendanceEndpointTests.Guest(f, s);
        await using (var db = f.Db())
        {
            db.MeetingGuestInvitations.Attach(invitation);
            invitation.SelfDeclaredDisplayName = "Khách mời A";
            await db.SaveChangesAsync();
        }
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

    private static Guid Mine(JsonElement queue) => queue.GetProperty("myRequestId").GetGuid();

    private static string[] Statuses(JsonElement queue)
        => queue.GetProperty("requests").EnumerateArray().Select(r => r.GetProperty("status").GetString()!).ToArray();

    [Fact]
    public async Task Raising_twice_keeps_one_request_and_the_queue_follows_request_order()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var secretary = f.Client(s.Secretary); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member)); await InRoom(s, CallMember.User(s.Secretary));

        var first = await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created);
        // UC-79 EX-2: the hand stays raised, nothing changes.
        var again = await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.OK);
        Assert.Equal(Mine(first), Mine(again));
        await Json(await secretary.PostAsync(Queue(s), null), HttpStatusCode.Created);

        var queue = await Json(await chair.GetAsync(Queue(s)), HttpStatusCode.OK);
        Assert.True(queue.GetProperty("canManage").GetBoolean());
        var names = queue.GetProperty("requests").EnumerateArray().Select(r => r.GetProperty("displayName").GetString()!).ToArray();
        Assert.Equal(new[] { "SCRUM20 User 3", "SCRUM20 User 1" }, names);
        Assert.Equal("Member", queue.GetProperty("requests")[0].GetProperty("role").GetString());
        Assert.False((await Json(await member.GetAsync(Queue(s)), HttpStatusCode.OK)).GetProperty("canManage").GetBoolean());

        await using var db = f.Db();
        Assert.Equal(1, await db.SpeakingRequests.CountAsync(r => r.MeetingId == s.MeetingId && r.UserId == s.Member));
    }

    [Fact]
    public async Task Simultaneous_raises_by_one_person_store_one_request()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        await InRoom(s, CallMember.User(s.Member));
        var clients = Enumerable.Range(0, 5).Select(_ => f.Client(s.Member)).ToArray();
        var responses = await Task.WhenAll(clients.Select(c => c.PostAsync(Queue(s), null)));
        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode, r.StatusCode.ToString()));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        await using var db = f.Db();
        Assert.Equal(1, await db.SpeakingRequests.CountAsync(r => r.MeetingId == s.MeetingId));
        foreach (var c in clients) c.Dispose();
    }

    [Fact]
    public async Task A_turn_runs_invited_speaking_finished_and_its_times_are_kept()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member));
        var id = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));

        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await Json(await chair.PostAsync($"{Queue(s)}/{id}/invite", null), HttpStatusCode.OK);
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var started = await Json(await member.PostAsync($"{Queue(s)}/{id}/start", null), HttpStatusCode.OK);
        Assert.Equal(["Speaking"], Statuses(started));
        f.Clock.Now = f.Clock.Now.AddMinutes(3);
        var finished = await Json(await member.PostAsync($"{Queue(s)}/{id}/finish", null), HttpStatusCode.OK);
        Assert.Empty(Statuses(finished));

        // BR-339: who spoke and for how long stays with the meeting.
        await using var db = f.Db();
        var row = await db.SpeakingRequests.SingleAsync(r => r.Id == id);
        Assert.Equal(SpeakingRequestStatus.Finished, row.Status);
        Assert.Equal(TimeSpan.FromMinutes(3), row.EndedAt - row.StartedAt);
        Assert.NotNull(row.InvitedAt);
    }

    [Fact]
    public async Task Only_the_current_chair_manages_the_queue_and_one_person_holds_the_floor()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var secretary = f.Client(s.Secretary); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member)); await InRoom(s, CallMember.User(s.Secretary));
        var first = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        var second = Mine(await Json(await secretary.PostAsync(Queue(s), null), HttpStatusCode.Created));

        // Calling the Chair's API directly is refused (ticket test plan).
        foreach (var action in new[] { "invite", "decline", "move-up", "move-down" })
            await Code(await member.PostAsync($"{Queue(s)}/{second}/{action}", null), 403, "MSG457");

        var moved = await Json(await chair.PostAsync($"{Queue(s)}/{second}/move-up", null), HttpStatusCode.OK);
        Assert.Equal(second, moved.GetProperty("requests")[0].GetProperty("id").GetGuid());
        await Json(await chair.PostAsync($"{Queue(s)}/{second}/invite", null), HttpStatusCode.OK);
        await Code(await chair.PostAsync($"{Queue(s)}/{first}/invite", null), 409, "MSG459");

        // UC-79 AF-3: the Chair declines; the hand is lowered and the request leaves the queue.
        await Json(await chair.PostAsync($"{Queue(s)}/{first}/decline", null), HttpStatusCode.OK);
        await Code(await chair.PostAsync($"{Queue(s)}/{first}/decline", null), 409, "MSG460");

        // UC-79 EX-4: after a handover the old Chair has no controls, at once.
        await using (var db = f.Db())
        {
            var chairRow = await db.MeetingParticipants.SingleAsync(p => p.Id == s.ChairAssignment);
            var memberRow = await db.MeetingParticipants.SingleAsync(p => p.Id == s.MemberAssignment);
            // One current Chair at a time (ux_current_meeting_chair): step down first, then hand over.
            chairRow.Role = MeetingRole.Member;
            await db.SaveChangesAsync();
            memberRow.Role = MeetingRole.Chair;
            await db.SaveChangesAsync();
        }
        await Code(await chair.PostAsync($"{Queue(s)}/{second}/finish", null), 403, "MSG457");
        await Json(await member.PostAsync($"{Queue(s)}/{second}/finish", null), HttpStatusCode.OK);
    }

    [Fact]
    public async Task Raising_needs_an_in_progress_meeting_the_room_and_a_readable_meeting()
    {
        var published = await f.Seed(MeetingStatus.Published);
        using (var member = f.Client(published.Member))
            await Code(await member.PostAsync(Queue(published), null), 409, "MSG117");

        var s = await f.Seed(MeetingStatus.InProgress);
        using var notInRoom = f.Client(s.Member);
        await Code(await notInRoom.PostAsync(Queue(s), null), 409, "MSG458");

        // Someone outside the meeting learns nothing about it.
        using var outsider = f.Client(s.Outsider);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(Queue(s))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsync(Queue(s), null)).StatusCode);

        // Another person's request cannot be lowered or started by someone else.
        using var secretary = f.Client(s.Secretary);
        await InRoom(s, CallMember.User(s.Member)); await InRoom(s, CallMember.User(s.Secretary));
        var id = Mine(await Json(await notInRoom.PostAsync(Queue(s), null), HttpStatusCode.Created));
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.DeleteAsync($"{Queue(s)}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await notInRoom.DeleteAsync($"{Queue(s)}/{Guid.CreateVersion7()}")).StatusCode);
        await Json(await notInRoom.DeleteAsync($"{Queue(s)}/{id}"), HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_guest_raises_a_hand_takes_the_turn_and_shows_as_a_guest()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair);
        var (guest, invitation, _) = await GuestInRoom(s);

        var id = Mine(await Json(await guest.PostAsync(Queue(s), null), HttpStatusCode.Created));
        var queue = await Json(await chair.GetAsync(Queue(s)), HttpStatusCode.OK);
        var row = queue.GetProperty("requests")[0];
        Assert.Equal(("Guest", "Khách mời A", "Guest"),
            (row.GetProperty("kind").GetString(), row.GetProperty("displayName").GetString(), row.GetProperty("role").GetString()));

        await Json(await chair.PostAsync($"{Queue(s)}/{id}/invite", null), HttpStatusCode.OK);
        await Json(await guest.PostAsync($"{Queue(s)}/{id}/start", null), HttpStatusCode.OK);
        await Json(await guest.PostAsync($"{Queue(s)}/{id}/finish", null), HttpStatusCode.OK);
        await using var db = f.Db();
        var saved = await db.SpeakingRequests.SingleAsync(r => r.Id == id);
        Assert.Equal((null, invitation.Id, SpeakingRequestStatus.Finished), (saved.UserId, saved.GuestInvitationId, saved.Status));
    }

    [Fact]
    public async Task Leaving_the_room_or_being_removed_ends_the_open_request()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var secretary = f.Client(s.Secretary); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member));
        var (guest, _, guestSession) = await GuestInRoom(s);
        var memberRequest = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        var guestRequest = Mine(await Json(await guest.PostAsync(Queue(s), null), HttpStatusCode.Created));

        // UC-79 EX-3: the person left the room; the Chair's queue moves up.
        await Leave(s, CallMember.User(s.Member));
        await Leave(s, CallMember.Guest(guestSession));
        await using (var db = f.Db())
            Assert.All(await db.SpeakingRequests.Where(r => r.Id == memberRequest || r.Id == guestRequest).ToListAsync(),
                r => Assert.Equal(SpeakingRequestStatus.Cancelled, r.Status));

        // Removed from the meeting while waiting: the hand is lowered too (BR-337).
        await InRoom(s, CallMember.User(s.Member));
        var again = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        await Code(await Write(secretary, s.Url + $"/participants/{s.MemberAssignment}", HttpMethod.Delete), 200, "MSG103");
        await using (var db = f.Db())
            Assert.Equal(SpeakingRequestStatus.Cancelled, (await db.SpeakingRequests.SingleAsync(r => r.Id == again)).Status);
        Assert.Empty(Statuses(await Json(await chair.GetAsync(Queue(s)), HttpStatusCode.OK)));
    }

    [Fact]
    public async Task Ending_the_meeting_finishes_the_turn_and_cancels_the_queue()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var secretary = f.Client(s.Secretary); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member)); await InRoom(s, CallMember.User(s.Secretary));
        var speaking = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        var waiting = Mine(await Json(await secretary.PostAsync(Queue(s), null), HttpStatusCode.Created));
        await Json(await chair.PostAsync($"{Queue(s)}/{speaking}/invite", null), HttpStatusCode.OK);
        await Json(await member.PostAsync($"{Queue(s)}/{speaking}/start", null), HttpStatusCode.OK);

        await using (var scope = f.Host.Services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IMeetingLifecycleService>()
                .TransitionAsync(s.MeetingId, new MeetingTransitionCommand(MeetingTransitionAction.End), s.Chair);
            Assert.True(result.Succeeded);
        }

        await using var db = f.Db();
        var rows = await db.SpeakingRequests.Where(r => r.MeetingId == s.MeetingId).ToDictionaryAsync(r => r.Id);
        Assert.Equal(SpeakingRequestStatus.Finished, rows[speaking].Status);
        Assert.NotNull(rows[speaking].EndedAt);
        Assert.Equal(SpeakingRequestStatus.Cancelled, rows[waiting].Status);
        await Code(await member.PostAsync(Queue(s), null), 409, "MSG117");
    }

    [Fact]
    public async Task Readers_hear_the_queue_change_and_only_the_invited_person_hears_the_invitation()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var secretary = f.Client(s.Secretary); using var chair = f.Client(s.Chair);
        await InRoom(s, CallMember.User(s.Member));
        await using var memberHub = f.Hub(member); await using var secretaryHub = f.Hub(secretary);
        var memberChanged = Channel.CreateUnbounded<SpeakingChange>(); var secretaryChanged = Channel.CreateUnbounded<SpeakingChange>();
        var memberInvited = Channel.CreateUnbounded<SpeakingChange>(); var secretaryInvited = Channel.CreateUnbounded<SpeakingChange>();
        memberHub.On<SpeakingChange>("speakingChanged", e => memberChanged.Writer.TryWrite(e));
        secretaryHub.On<SpeakingChange>("speakingChanged", e => secretaryChanged.Writer.TryWrite(e));
        memberHub.On<SpeakingChange>("speakingInvited", e => memberInvited.Writer.TryWrite(e));
        secretaryHub.On<SpeakingChange>("speakingInvited", e => secretaryInvited.Writer.TryWrite(e));
        await Task.WhenAll(memberHub.StartAsync(), secretaryHub.StartAsync());
        await memberHub.InvokeAsync("JoinMeeting", s.MeetingId);
        await secretaryHub.InvokeAsync("JoinMeeting", s.MeetingId);

        var id = Mine(await Json(await member.PostAsync(Queue(s), null), HttpStatusCode.Created));
        var raised = await Next(secretaryChanged.Reader);
        Assert.Equal(("Raise", id), (raised.ChangeType, raised.RequestId));
        await Next(memberChanged.Reader);

        await Json(await chair.PostAsync($"{Queue(s)}/{id}/invite", null), HttpStatusCode.OK);
        Assert.Equal("Invite", (await Next(memberInvited.Reader)).ChangeType);
        Assert.Equal("Invite", (await Next(secretaryChanged.Reader)).ChangeType);
        using var timeout = new CancellationTokenSource(250);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await secretaryInvited.Reader.ReadAsync(timeout.Token));
    }

    [Fact]
    public async Task A_database_failure_reads_nothing_and_saves_nothing()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member);
        await InRoom(s, CallMember.User(s.Member));
        f.Queries.FailMeetingRead = true;
        try
        {
            await Code(await member.GetAsync(Queue(s)), 500, "MSG461");
            await Code(await member.PostAsync(Queue(s), null), 503, "MSG462");
        }
        finally { f.Queries.FailMeetingRead = false; }
        await using var db = f.Db();
        Assert.False(await db.SpeakingRequests.AnyAsync(r => r.MeetingId == s.MeetingId));
    }

    private static async Task<SpeakingChange> Next(ChannelReader<SpeakingChange> events)
        => await events.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
}
