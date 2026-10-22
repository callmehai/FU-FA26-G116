using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed partial class MeetingLifecycleTests
{
    [Theory]
    [InlineData("Start", 2)]
    [InlineData("End", 2)]
    [InlineData("Start", 20)]
    [InlineData("End", 20)]
    public async Task Concurrent_lifecycle_requests_have_exactly_one_winner(string action, int count)
    {
        var s = await fixture.Seed(action == "Start" ? MeetingStatus.Published : MeetingStatus.InProgress);
        await using var locked = fixture.Db();
        await using var transaction = await locked.Database.BeginTransactionAsync();
        await locked.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {s.MeetingId} FOR UPDATE").ToArrayAsync();
        if (action == "End")
        {
            var item = (await locked.AgendaItems.FindAsync(s.Items[0]))!;
            item.Status = AgendaItemStatus.InProgress; item.ActualStartedAt = fixture.Clock.Now.AddMinutes(-1);
            await locked.SaveChangesAsync();
        }
        fixture.ObserveLocks(2);
        var requests = Enumerable.Range(0, count).Select(_ => Act(s, action)).ToArray();
        await fixture.LockAttempt!.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await transaction.CommitAsync();
        var responses = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(30));
        await Expect(Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK), 200, action == "Start" ? "MSG110" : "MSG111");
        Assert.Equal(count - 1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var check = fixture.Db();
        var history = Assert.Single(await check.MeetingStatusHistory.Where(h => h.MeetingId == s.MeetingId).ToArrayAsync());
        Assert.Equal(fixture.Clock.Now, history.OccurredAt);
        Assert.Equal(action == "Start" ? 3 : 0, await check.Notifications.CountAsync(n => n.MeetingId == s.MeetingId));
        Assert.Equal(action == "Start" ? 0 : 1, fixture.ClosedRooms.Count(r => r == CallRoomNames.ForMeeting(s.MeetingId)));
        Assert.Equal(action == "Start" ? 0 : 1, fixture.Events.Count(e => e.MeetingId == s.MeetingId));
        if (action == "End") Assert.Empty(await check.AgendaItems.Where(i => i.MeetingId == s.MeetingId && i.Status == AgendaItemStatus.InProgress).ToArrayAsync());
    }

    [Theory]
    [InlineData("Start")]
    [InlineData("End")]
    public async Task Chair_handover_while_lifecycle_waits_uses_current_assignment(string action)
    {
        var s = await fixture.Seed(action == "Start" ? MeetingStatus.Published : MeetingStatus.InProgress);
        await using var locked = fixture.Db();
        await using var transaction = await locked.Database.BeginTransactionAsync();
        await locked.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {s.MeetingId} FOR UPDATE").ToArrayAsync();
        fixture.ObserveLocks();
        var request = Act(s, action);
        await fixture.LockAttempt!.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var old = await locked.MeetingParticipants.SingleAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Chair);
        old.Role = MeetingRole.Member; await locked.SaveChangesAsync();
        var next = await locked.MeetingParticipants.SingleAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Member);
        next.Role = MeetingRole.Chair; await locked.SaveChangesAsync();
        await transaction.CommitAsync();
        await Expect(await request.WaitAsync(TimeSpan.FromSeconds(10)), 403, action == "Start" ? "MSG112" : "MSG114");
        await Expect(await Act(s, action, s.Member), 200, action == "Start" ? "MSG110" : "MSG111");
        await using var check = fixture.Db();
        Assert.Equal(s.Member, (await check.MeetingStatusHistory.SingleAsync(h => h.MeetingId == s.MeetingId)).ActorId);
    }

    [Fact]
    public async Task Account_deactivation_after_gate_is_rechecked_under_user_lock()
    {
        var s = await fixture.Seed();
        await using var locked = fixture.Db();
        await using var transaction = await locked.Database.BeginTransactionAsync();
        var user = (await locked.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {s.Chair} FOR UPDATE").ToArrayAsync()).Single();
        fixture.ObserveAccountLock = true; fixture.ObserveLocks();
        try
        {
            var request = Act(s, "Start");
            await fixture.LockAttempt!.Task.WaitAsync(TimeSpan.FromSeconds(10));
            user.IsActive = false; await locked.SaveChangesAsync(); await transaction.CommitAsync();
            await Expect(await request.WaitAsync(TimeSpan.FromSeconds(10)), 403, "MSG112");
        }
        finally { fixture.ObserveAccountLock = false; }
        await AssertUnchanged(s, MeetingStatus.Published);
    }

    [Fact]
    public async Task Start_waiting_for_reschedule_preserves_new_schedule()
    {
        var s = await fixture.Seed();
        await using var locked = fixture.Db();
        await using var transaction = await locked.Database.BeginTransactionAsync();
        var meeting = (await locked.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {s.MeetingId} FOR UPDATE").ToArrayAsync()).Single();
        fixture.ObserveLocks();
        var request = Act(s, "Start");
        await fixture.LockAttempt!.Task.WaitAsync(TimeSpan.FromSeconds(10));
        meeting.PlannedStartTime = fixture.Clock.Now.AddDays(1); meeting.PlannedEndTime = fixture.Clock.Now.AddDays(1).AddHours(1);
        locked.MeetingStatusHistory.Add(new() { MeetingId = s.MeetingId, ActorId = s.Organizer, Action = MeetingHistoryAction.Reschedule,
            FromStatus = MeetingStatus.Published, ToStatus = MeetingStatus.Published, OccurredAt = fixture.Clock.Now });
        await locked.SaveChangesAsync(); await transaction.CommitAsync();
        await Expect(await request.WaitAsync(TimeSpan.FromSeconds(10)), 200, "MSG110");
        await using var check = fixture.Db();
        Assert.Equal(fixture.Clock.Now.AddDays(1), (await check.Meetings.FindAsync(s.MeetingId))!.PlannedStartTime);
        Assert.Equal(2, await check.MeetingStatusHistory.CountAsync(h => h.MeetingId == s.MeetingId));
    }

    [Theory]
    [InlineData("current")]
    [InlineData("complete")]
    public async Task Concurrent_agenda_execution_and_end_leave_no_current_item(string operation)
    {
        var s = await fixture.Seed(MeetingStatus.InProgress);
        if (operation == "complete")
            Assert.Equal(HttpStatusCode.OK, (await fixture.Client(s.Chair).PostAsync(s.Url + $"/{s.Items[0]}/current", null)).StatusCode);
        await using var locked = fixture.Db();
        await using var transaction = await locked.Database.BeginTransactionAsync();
        await locked.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {s.MeetingId} FOR UPDATE").ToArrayAsync();
        fixture.ObserveLocks(2);
        var end = Act(s, "End");
        var agenda = fixture.Client(s.Chair).PostAsync(s.Url + $"/{s.Items[0]}/{operation}", null);
        await fixture.LockAttempt!.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await transaction.CommitAsync();
        await Expect(await end.WaitAsync(TimeSpan.FromSeconds(10)), 200, "MSG111");
        Assert.Contains((await agenda.WaitAsync(TimeSpan.FromSeconds(10))).StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        await using var check = fixture.Db();
        Assert.Empty(await check.AgendaItems.Where(i => i.MeetingId == s.MeetingId && i.Status == AgendaItemStatus.InProgress).ToArrayAsync());
    }

    [Theory]
    [InlineData("Start")]
    [InlineData("End")]
    public async Task Client_cancellation_after_commit_does_not_cancel_effects(string action)
    {
        var s = await fixture.Seed(action == "Start" ? MeetingStatus.Published : MeetingStatus.InProgress);
        using var abort = new CancellationTokenSource();
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        try
        {
            fixture.OnCommitted = abort.Cancel;
            var result = await scope.ServiceProvider.GetRequiredService<IMeetingLifecycleService>()
                .TransitionAsync(s.MeetingId, new(Enum.Parse<MeetingTransitionAction>(action)), s.Chair, abort.Token);
            Assert.True(result.Succeeded); Assert.True(abort.IsCancellationRequested);
        }
        finally { fixture.OnCommitted = null; }
        await using var check = fixture.Db();
        if (action == "Start") Assert.Equal(3, await check.Notifications.CountAsync(n => n.MeetingId == s.MeetingId));
        else Assert.Contains(CallRoomNames.ForMeeting(s.MeetingId), fixture.ClosedRooms);
    }
}
