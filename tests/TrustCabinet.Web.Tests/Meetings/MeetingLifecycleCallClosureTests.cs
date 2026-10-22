using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed partial class MeetingLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Join_between_meeting_end_and_provider_confirmation_uses_room_closure_time(bool existingSession)
    {
        var s = await fixture.Seed(MeetingStatus.InProgress);
        var meetingEndedAt = fixture.Clock.Now;
        var joinedAt = meetingEndedAt.AddSeconds(1);
        var confirmedAt = meetingEndedAt.AddSeconds(2);
        if (existingSession)
        {
            await using var scope = fixture.Host.Services.CreateAsyncScope();
            var calls = scope.ServiceProvider.GetRequiredService<ICallRepository>();
            var session = await calls.EnsureActiveRoomSessionAsync(s.MeetingId, s.Chair, meetingEndedAt.AddMinutes(-5));
            await calls.RecordJoinAsync(session.Id, s.Chair, meetingEndedAt.AddMinutes(-5));
        }
        fixture.BeforeRoomClosed = async (id, ct) =>
        {
            Assert.Equal(s.MeetingId, id);
            await using var scope = fixture.Host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ICallRoomService>().HandleWebhookAsync(new(
                "late-join", "participant_joined", CallRoomNames.ForMeeting(id), s.Member.ToString("D"),
                joinedAt, null, CallTrackSource.Unknown, joinedAt), ct);
            fixture.Clock.Now = confirmedAt;
        };
        try { await Expect(await Act(s, "End"), 200, "MSG111"); }
        finally { fixture.BeforeRoomClosed = null; fixture.Clock.Now = meetingEndedAt; }

        await using (var check = fixture.Db())
        {
            Assert.Equal(meetingEndedAt, (await check.Meetings.FindAsync(s.MeetingId))!.ActualEndedAt);
            Assert.Equal(meetingEndedAt, (await check.MeetingStatusHistory.SingleAsync(h => h.MeetingId == s.MeetingId)).OccurredAt);
            var session = await check.CallSessions.SingleAsync(c => c.MeetingId == s.MeetingId);
            Assert.Equal(CallStatus.Ended, session.Status);
            Assert.Equal(confirmedAt, session.EndedAt);
            Assert.True(session.EndedAt >= session.StartedAt);
            var stints = await check.CallParticipants.Where(p => p.CallSessionId == session.Id).ToArrayAsync();
            Assert.Equal(existingSession ? 2 : 1, stints.Length);
            Assert.All(stints, p => { Assert.Equal(confirmedAt, p.LeftAt); Assert.True(p.LeftAt >= p.JoinedAt); });
        }
        await using (var scope = fixture.Host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ICallRoomService>().ReconcileAsync();
        await using var final = fixture.Db();
        Assert.Equal(confirmedAt, (await final.CallSessions.SingleAsync(c => c.MeetingId == s.MeetingId)).EndedAt);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("join")]
    [InlineData("leave")]
    public async Task Session_closure_cannot_precede_existing_connection_evidence(string latestEvidence)
    {
        var s = await fixture.Seed(MeetingStatus.InProgress);
        var now = fixture.Clock.Now;
        var latest = now.AddSeconds(5);
        Guid sessionId;
        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var calls = scope.ServiceProvider.GetRequiredService<ICallRepository>();
            var session = await calls.EnsureActiveRoomSessionAsync(s.MeetingId, s.Chair,
                latestEvidence == "start" ? latest : now.AddMinutes(-1));
            sessionId = session.Id;
            if (latestEvidence != "start")
            {
                await calls.RecordJoinAsync(session.Id, s.Chair, latestEvidence == "join" ? latest : now);
                if (latestEvidence == "leave") await calls.RecordLeaveAsync(session.Id, s.Chair, latest);
                await calls.RecordJoinAsync(session.Id, s.Member, now);
            }
            await calls.EndSessionAsync(session.Id, now);
            await calls.EndSessionAsync(session.Id, now.AddMinutes(1));
        }
        await using var check = fixture.Db();
        var closed = (await check.CallSessions.FindAsync(sessionId))!;
        Assert.Equal(latest, closed.EndedAt);
        Assert.True(closed.EndedAt >= closed.StartedAt);
        Assert.All(await check.CallParticipants.Where(p => p.CallSessionId == sessionId).ToArrayAsync(), p =>
        {
            Assert.True(p.LeftAt >= p.JoinedAt);
            Assert.True(p.LeftAt <= closed.EndedAt);
        });
    }
}
