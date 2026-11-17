using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web.Tests.Meetings;

/// <summary>
/// ADR-0062: 24 hours after its planned end (or its actual start, when later) the system ends
/// it the way the Chair would. A forgotten "Đang diễn ra" meeting once kept a hybrid room open for ten days.
/// </summary>
public sealed partial class MeetingLifecycleTests
{
    private async Task<MeetingLifecycleFixture.Scenario> SeedRunning(MeetingMode mode, double endedHoursAgo, double startedHoursAgo)
    {
        var s = await fixture.Seed(MeetingStatus.InProgress, mode: mode);
        await using var db = fixture.Db();
        var meeting = await db.Meetings.SingleAsync(m => m.Id == s.MeetingId);
        meeting.PlannedEndTime = fixture.Clock.Now.AddHours(-endedHoursAgo);
        meeting.PlannedStartTime = meeting.PlannedEndTime.AddHours(-1);
        meeting.ActualStartedAt = fixture.Clock.Now.AddHours(-startedHoursAgo);
        await db.SaveChangesAsync();
        return s;
    }

    [Fact]
    public async Task Meetings_left_running_are_ended_by_the_system_twelve_hours_after_their_planned_end()
    {
        var forgotten = await SeedRunning(MeetingMode.Hybrid, endedHoursAgo: 240, startedHoursAgo: 241);
        var overtime = await SeedRunning(MeetingMode.Online, endedHoursAgo: 23, startedHoursAgo: 24);
        // Started long after its planned end: it gets the whole grace from its real start.
        var lateStart = await SeedRunning(MeetingMode.Online, endedHoursAgo: 30, startedHoursAgo: 2);
        var draft = await SeedVote(forgotten.MeetingId, forgotten.Secretary, VotingStatus.Draft, "Nháp bị bỏ quên");
        await using (var db = fixture.Db())
        {
            var item = await db.AgendaItems.SingleAsync(i => i.Id == forgotten.Items[0]);
            item.Status = AgendaItemStatus.InProgress;
            item.ActualStartedAt = fixture.Clock.Now.AddHours(-240);
            await db.SaveChangesAsync();
        }

        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var overdue = scope.ServiceProvider.GetRequiredService<IMeetingOverdueService>();
        Assert.True(await overdue.EndOverrunAsync() >= 1);
        Assert.Equal(0, await overdue.EndOverrunAsync());

        await using var check = fixture.Db();
        var statuses = await check.Meetings.AsNoTracking()
            .Where(m => m.Id == forgotten.MeetingId || m.Id == overtime.MeetingId || m.Id == lateStart.MeetingId)
            .ToDictionaryAsync(m => m.Id);
        Assert.Equal(MeetingStatus.Ended, statuses[forgotten.MeetingId].Status);
        Assert.Equal(fixture.Clock.Now, statuses[forgotten.MeetingId].ActualEndedAt);
        Assert.Equal(MeetingStatus.InProgress, statuses[overtime.MeetingId].Status);
        Assert.Equal(MeetingStatus.InProgress, statuses[lateStart.MeetingId].Status);

        var history = await check.MeetingStatusHistory.SingleAsync(h => h.MeetingId == forgotten.MeetingId);
        Assert.Equal(MeetingHistoryAction.AutoEnd, history.Action);
        Assert.Equal(MeetingStatus.InProgress, history.FromStatus);
        Assert.Equal(MeetingStatus.Ended, history.ToStatus);
        Assert.Null(history.ActorId);
        Assert.Contains("24 giờ", history.Reason);

        // The same end as the Chair's: the current item closes, the draft goes, the room closes, readers are told.
        var item0 = await check.AgendaItems.SingleAsync(i => i.Id == forgotten.Items[0]);
        Assert.Equal(AgendaItemStatus.Done, item0.Status);
        Assert.Equal(fixture.Clock.Now, item0.ActualEndedAt);
        Assert.False(await check.VotingSessions.AnyAsync(v => v.Id == draft));
        Assert.Contains(CallRoomNames.ForMeeting(forgotten.MeetingId), fixture.ClosedRooms);
        Assert.Contains(fixture.Events, e => e.MeetingId == forgotten.MeetingId);

        // The history reads "Hệ thống" with the AutoEnd action.
        var page = System.Text.Json.JsonDocument.Parse(
            await fixture.Client(forgotten.Chair).GetStringAsync($"/api/meetings/{forgotten.MeetingId}/history")).RootElement;
        var row = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal("AutoEnd", row.GetProperty("action").GetString());
        Assert.Equal(MeetingHistoryRecord.SystemActorName, row.GetProperty("actorName").GetString());
    }

    [Fact]
    public async Task Auto_end_sweep_does_not_save_a_failed_end_with_the_next_meeting()
    {
        // The older one comes first in the batch; its save fails, the next one is ended on the same context (SCRUM-212).
        var failing = await SeedRunning(MeetingMode.InPerson, endedHoursAgo: 500, startedHoursAgo: 501);
        var next = await SeedRunning(MeetingMode.InPerson, endedHoursAgo: 400, startedHoursAgo: 401);
        await using (var db = fixture.Db())
        {
            var item = await db.AgendaItems.SingleAsync(i => i.Id == failing.Items[0]);
            item.Status = AgendaItemStatus.InProgress;
            item.ActualStartedAt = fixture.Clock.Now.AddHours(-500);
            await db.SaveChangesAsync();
        }

        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var overdue = scope.ServiceProvider.GetRequiredService<IMeetingOverdueService>();
        fixture.FailSaveOfMeeting = failing.MeetingId;
        try
        {
            Assert.True(await overdue.EndOverrunAsync() >= 1);
        }
        finally
        {
            fixture.FailSaveOfMeeting = null;
        }

        await using var check = fixture.Db();
        var failed = await check.Meetings.AsNoTracking().SingleAsync(m => m.Id == failing.MeetingId);
        Assert.Equal(MeetingStatus.InProgress, failed.Status);
        Assert.Null(failed.ActualEndedAt);
        Assert.False(await check.MeetingStatusHistory.AnyAsync(h => h.MeetingId == failing.MeetingId));
        Assert.Equal(AgendaItemStatus.InProgress,
            (await check.AgendaItems.AsNoTracking().SingleAsync(i => i.Id == failing.Items[0])).Status);
        var ended = await check.Meetings.AsNoTracking().SingleAsync(m => m.Id == next.MeetingId);
        Assert.Equal(MeetingStatus.Ended, ended.Status);
        var history = await check.MeetingStatusHistory.AsNoTracking().SingleAsync(h => h.MeetingId == next.MeetingId);
        Assert.Equal(MeetingHistoryAction.AutoEnd, history.Action);

        // Left to the next sweep, which ends it once.
        await using var again = fixture.Host.Services.CreateAsyncScope();
        Assert.True(await again.ServiceProvider.GetRequiredService<IMeetingOverdueService>().EndOverrunAsync() >= 1);
        Assert.Equal(MeetingStatus.Ended,
            (await check.Meetings.AsNoTracking().SingleAsync(m => m.Id == failing.MeetingId)).Status);
        Assert.Equal(MeetingHistoryAction.AutoEnd, (await check.MeetingStatusHistory.AsNoTracking()
            .SingleAsync(h => h.MeetingId == failing.MeetingId)).Action);
        Assert.Single(await check.MeetingStatusHistory.AsNoTracking().Where(h => h.MeetingId == next.MeetingId).ToListAsync());
    }

    [Fact]
    public async Task System_end_yields_to_a_chair_who_already_ended_the_meeting()
    {
        var s = await SeedRunning(MeetingMode.InPerson, endedHoursAgo: 40, startedHoursAgo: 41);
        await Expect(await Act(s, "End"), 200, "MSG111");

        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IMeetingLifecycleService>();
        Assert.False(await lifecycle.EndOverrunAsync(s.MeetingId, fixture.Clock.Now.AddHours(-24), "x"));

        await using var check = fixture.Db();
        var history = await check.MeetingStatusHistory.Where(h => h.MeetingId == s.MeetingId).ToArrayAsync();
        Assert.Equal(MeetingHistoryAction.End, Assert.Single(history).Action);
    }
}
