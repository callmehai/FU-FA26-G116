using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.GuestAccess.GuestAccessFixture;

namespace TrustCabinet.Web.Tests.GuestAccess;

/// <summary>
/// SCRUM-59: a guest verified through the real UC-04 flow, in the meeting room — the real repository and PostgreSQL,
/// only LiveKit faked. Covers the recording-notice evidence, guest stints and revocation taking the guest out.
/// </summary>
public sealed class GuestCallRoomTests(GuestAccessFixture f) : IClassFixture<GuestAccessFixture>
{
    private static string Call(Guid meetingId) => $"/api/meetings/{meetingId}/call";

    private static readonly object Confirmed = new { recordingNoticeAcknowledged = true };

    private async Task<Scenario> OnlineMeeting(MeetingStatus status = MeetingStatus.InProgress)
    {
        var s = await f.Seed(status);
        await using var db = f.Db();
        await db.Meetings.Where(m => m.Id == s.MeetingId).ExecuteUpdateAsync(set => set.SetProperty(m => m.Mode, MeetingMode.Online));
        return s;
    }

    private static async Task<Guid> SessionId(HttpClient guest)
        => (await Expect(await guest.GetAsync("/api/guest-access/session"), 200)).GetProperty("sessionId").GetGuid();

    [Fact]
    public async Task Guest_joins_after_confirming_the_recording_notice_which_is_kept_with_their_ip()
    {
        var s = await OnlineMeeting();
        var (guest, _) = await f.SignIn(s);
        var session = await SessionId(guest);
        int participants;
        await using (var db = f.Db()) participants = await db.MeetingParticipants.CountAsync(p => p.MeetingId == s.MeetingId);

        var room = await Expect(await guest.GetAsync(Call(s.MeetingId)), 200);
        Assert.Equal(("Open", "Guest"), (room.GetProperty("status").GetString(), room.GetProperty("myRole").GetString()));
        // The pre-join notice states how long the recording is kept (Recordings:RetentionDays).
        Assert.Equal(180, room.GetProperty("retentionDays").GetInt32());

        await Expect(await guest.PostAsync(Call(s.MeetingId) + "/join", null), 400, "MSG380");
        await using (var db = f.Db())
            Assert.False(await db.AuditLog.AnyAsync(a => a.ObjectId == session));

        var ticket = await Expect(await guest.PostAsJsonAsync(Call(s.MeetingId) + "/join", Confirmed), 200);
        Assert.Equal(CallMember.Guest(session).Identity, ticket.GetProperty("identity").GetString());
        Assert.Equal(CallRoomNames.ForMeeting(s.MeetingId), ticket.GetProperty("roomName").GetString());
        Assert.Equal("Khách tự khai", f.LiveKit.LastTicket!.Value.Name);

        await using var check = f.Db();
        var admitted = await check.AuditLog.SingleAsync(a => a.ObjectId == session);
        Assert.Equal((AuditActions.CallGuestAdmitted, AuditActions.GuestSessionObjectType, (Guid?)null),
            (admitted.Action, admitted.ObjectType, admitted.ActorId));
        // The row's IpAddress is the caller's remote address (AuditService); TestServer has none to give.
        var details = JsonDocument.Parse(admitted.Details).RootElement;
        Assert.Equal(s.MeetingId, details.GetProperty("meetingId").GetGuid());
        Assert.Equal(s.InvitationId, details.GetProperty("invitationId").GetGuid());
        Assert.True(details.GetProperty("recordingNoticeAcknowledged").GetBoolean());
        // Never written as an internal participant.
        Assert.Equal(participants, await check.MeetingParticipants.CountAsync(p => p.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task Guest_waits_for_the_meeting_to_start_and_never_sees_another_meetings_room()
    {
        var s = await OnlineMeeting(MeetingStatus.Published);
        var other = await OnlineMeeting();
        var (guest, _) = await f.SignIn(s);
        var (unnamed, _) = await f.SignIn(other, profile: false);

        Assert.Equal("NotStarted", (await Expect(await guest.GetAsync(Call(s.MeetingId)), 200)).GetProperty("status").GetString());
        await Expect(await guest.PostAsJsonAsync(Call(s.MeetingId) + "/join", Confirmed), 409, "MSG117");

        foreach (var response in new[]
                 {
                     await guest.GetAsync(Call(other.MeetingId)),
                     await guest.PostAsJsonAsync(Call(other.MeetingId) + "/join", Confirmed),
                     // UC-04 step 5 first: no display name, no room.
                     await unnamed.PostAsJsonAsync(Call(other.MeetingId) + "/join", Confirmed),
                 })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Guest_visit_is_a_guest_stint_and_revoking_the_invitation_takes_the_guest_out_at_once()
    {
        var s = await OnlineMeeting();
        var (guest, _) = await f.SignIn(s);
        var member = CallMember.Guest(await SessionId(guest));
        var room = CallRoomNames.ForMeeting(s.MeetingId);
        await Expect(await guest.PostAsJsonAsync(Call(s.MeetingId) + "/join", Confirmed), 200);

        // LiveKit reports the guest in.
        f.LiveKit.Join(room, member);
        f.LiveKit.NextWebhook = new CallWebhookEvent("EV_" + Guid.NewGuid().ToString("N"), "participant_joined", room,
            member.Identity, DateTimeOffset.UtcNow, null, CallTrackSource.Unknown, DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.OK, (await f.Host.CreateClient().PostAsync("/api/livekit/webhook", new StringContent("{}"))).StatusCode);

        await using (var db = f.Db())
        {
            var stint = await db.CallParticipants.SingleAsync(p => p.CallSession.MeetingId == s.MeetingId);
            Assert.Equal((null, member.GuestSessionId, null), (stint.UserId, stint.GuestSessionId, stint.LeftAt));
            Assert.Null((await db.CallSessions.SingleAsync(c => c.MeetingId == s.MeetingId)).StartedById);
        }
        Assert.DoesNotContain(f.LiveKit.Removed, r => r.Room == room);

        // The secretary revokes the invitation: the guest is out of the room without waiting for the janitor.
        using var secretary = f.UserClient(s.SecretaryId);
        var tag = (await secretary.GetAsync($"/api/meetings/{s.MeetingId}/participant-context")).Headers.ETag!.ToString();
        using var revoke = new HttpRequestMessage(HttpMethod.Delete, $"/api/meetings/{s.MeetingId}/guest-invitations/{s.InvitationId}");
        revoke.Headers.TryAddWithoutValidation("If-Match", tag);
        await Expect(await secretary.SendAsync(revoke), 200, "MSG103");

        Assert.Contains((room, member.Identity), f.LiveKit.Removed);
        await using (var db = f.Db())
            Assert.NotNull((await db.CallParticipants.SingleAsync(p => p.GuestSessionId == member.GuestSessionId)).LeftAt);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.PostAsJsonAsync(Call(s.MeetingId) + "/join", Confirmed)).StatusCode);
    }

    [Fact]
    public async Task Room_access_checks_the_invitation_the_token_names()
    {
        var s = await OnlineMeeting();
        var (guest, _) = await f.SignIn(s);
        var session = await SessionId(guest);
        await using var db = f.Db();
        var calls = new Infrastructure.Calls.CallRepository(db, f.Clock);

        Assert.NotNull(await calls.FindGuestRoomAccessAsync(s.MeetingId, session, s.InvitationId));
        // The webhook only knows the session.
        Assert.NotNull(await calls.FindGuestRoomAccessAsync(s.MeetingId, session, null));
        Assert.Null(await calls.FindGuestRoomAccessAsync(s.MeetingId, session, Guid.CreateVersion7()));
    }

    [Fact]
    public async Task A_stint_must_name_exactly_one_of_a_user_and_a_guest_session()
    {
        var s = await OnlineMeeting();
        var (guest, _) = await f.SignIn(s);
        var session = await SessionId(guest);
        await using var db = f.Db();
        var call = new Domain.Entities.CallSession
        {
            Kind = CallKind.MeetingRoom, MeetingId = s.MeetingId, Status = CallStatus.Ended, StartedAt = f.Clock.Now,
        };
        db.CallSessions.Add(call);
        await db.SaveChangesAsync();

        foreach (var (user, guestSession) in new (Guid?, Guid?)[] { (null, null), (s.SecretaryId, session) })
        {
            await using var attempt = f.Db();
            attempt.CallParticipants.Add(new Domain.Entities.CallParticipant { CallSessionId = call.Id, UserId = user, GuestSessionId = guestSession });
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => attempt.SaveChangesAsync());
            Assert.Equal("ck_call_participants_one_member", Assert.IsType<Npgsql.PostgresException>(error.InnerException).ConstraintName);
        }
    }
}
