using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed partial class MeetingLifecycleTests(MeetingLifecycleFixture fixture) : IClassFixture<MeetingLifecycleFixture>
{
    private static string Url(Guid id) => $"/api/meetings/{id}/transitions";
    private Task<HttpResponseMessage> Act(MeetingLifecycleFixture.Scenario s, string action, Guid? actor = null)
        => fixture.Client(actor ?? s.Chair).PostAsJsonAsync(Url(s.MeetingId), new { action });

    private static async Task<JsonElement> Expect(HttpResponseMessage response, int status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {body}");
        var json = JsonDocument.Parse(body).RootElement.Clone();
        Assert.Equal(code, json.GetProperty("code").GetString());
        return json;
    }

    [Theory]
    [InlineData(MeetingMode.InPerson)]
    [InlineData(MeetingMode.Online)]
    [InlineData(MeetingMode.Hybrid)]
    public async Task Start_and_end_persist_server_time_history_and_notifications(MeetingMode mode)
    {
        var s = await fixture.Seed(count: 0, mode: mode);
        await using (var db = fixture.Db())
        {
            var secretary = await db.MeetingParticipants.SingleAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Secretary);
            secretary.Response = InvitationResponse.Declined;
            await db.SaveChangesAsync();
        }
        var start = await Expect(await Act(s, "Start"), 200, "MSG110");
        Assert.Equal("InProgress", start.GetProperty("meeting").GetProperty("status").GetString());
        await Expect(await Act(s, "Start"), 409, "MSG112");
        await Expect(await Act(s, "End"), 200, "MSG111");
        await Expect(await Act(s, "End"), 409, "MSG114");
        await using var check = fixture.Db();
        var meeting = await check.Meetings.SingleAsync(m => m.Id == s.MeetingId);
        Assert.Equal(MeetingStatus.Ended, meeting.Status);
        Assert.Equal(fixture.Clock.Now, meeting.ActualStartedAt);
        Assert.Equal(fixture.Clock.Now, meeting.ActualEndedAt);
        var history = await check.MeetingStatusHistory.Where(h => h.MeetingId == s.MeetingId).OrderBy(h => h.Id).ToArrayAsync();
        Assert.Equal(2, history.Length);
        Assert.Equal(new[] { MeetingHistoryAction.Start, MeetingHistoryAction.End }, history.Select(h => h.Action));
        Assert.All(history, h => { Assert.Equal(s.Chair, h.ActorId); Assert.Equal(fixture.Clock.Now, h.OccurredAt); Assert.Equal(h.OccurredAt, h.CreatedAt); Assert.Equal(h.CreatedAt, h.UpdatedAt); });
        var notifications = await check.Notifications.Where(n => n.MeetingId == s.MeetingId).ToArrayAsync();
        // The Chair who started it is not told of their own action; a Declined invitee still is.
        Assert.Equal(new[] { s.Secretary, s.Member }.Order(), notifications.Select(n => n.UserId).Order());
        Assert.All(notifications, n =>
        {
            Assert.Equal(NotificationType.MeetingStarted, n.Type);
            Assert.Equal(history[0].Id, n.OriginHistoryId);
            Assert.Equal($"meeting-started:{history[0].Id:D}:{n.UserId:D}", n.DeduplicationKey);
            Assert.Equal("Meeting", n.TargetType); Assert.Equal(s.MeetingId, n.TargetId);
            Assert.Equal("Cuộc họp đã bắt đầu", n.Title); Assert.Equal("Cuộc họp “Agenda test” đã bắt đầu.", n.Body);
            Assert.Null(n.ReadAt); Assert.Null(n.PushedAt); Assert.Equal(fixture.Clock.Now, n.CreatedAt);
        });
        Assert.Empty(await check.CallSessions.Where(c => c.MeetingId == s.MeetingId).ToArrayAsync());
        Assert.Equal(mode == MeetingMode.InPerson ? 0 : 1, fixture.ClosedRooms.Count(r => r == CallRoomNames.ForMeeting(s.MeetingId)));
        Assert.Single(fixture.Events, e => e.MeetingId == s.MeetingId);
        Assert.Null(fixture.Events.Single(e => e.MeetingId == s.MeetingId).AgendaItemId);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client(s.Chair).GetAsync($"/api/meetings/{s.MeetingId}")).StatusCode);
        var historyResponse = await fixture.Client(s.Chair).GetAsync($"/api/meetings/{s.MeetingId}/history");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        Assert.Contains("Start", await historyResponse.Content.ReadAsStringAsync());
    }

    public static IEnumerable<object[]> InvalidStates => Enum.GetValues<MeetingStatus>()
        .SelectMany(status => new[] { "Start", "End" }.Where(action => status != (action == "Start" ? MeetingStatus.Published : MeetingStatus.InProgress))
            .Select(action => new object[] { status, action }));

    [Theory, MemberData(nameof(InvalidStates))]
    public async Task Invalid_state_does_not_mutate(MeetingStatus status, string action)
    {
        var s = await fixture.Seed(status);
        await Expect(await Act(s, action), 409, action == "Start" ? "MSG112" : "MSG114");
        await AssertUnchanged(s, status);
    }

    [Theory]
    [InlineData("Start")]
    [InlineData("End")]
    public async Task Readable_non_chair_forbidden_and_hidden_resource_indistinguishable(string action)
    {
        var s = await fixture.Seed(action == "Start" ? MeetingStatus.Published : MeetingStatus.InProgress);
        foreach (var actor in new[] { s.Secretary, s.Member, s.Organizer })
            await Expect(await Act(s, action, actor), 403, action == "Start" ? "MSG112" : "MSG114");
        var hidden = await Act(s, action, s.Outsider);
        var missing = await fixture.Client(s.Outsider).PostAsJsonAsync(Url(Guid.NewGuid()), new { action });
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(hidden.StatusCode, missing.StatusCode);
        Assert.Equal(string.Empty, await hidden.Content.ReadAsStringAsync());
        Assert.Equal(string.Empty, await missing.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("removed")]
    [InlineData("delegated")]
    public async Task Start_requires_active_current_secretary_but_end_does_not(string reason)
    {
        var s = await fixture.Seed();
        await using (var db = fixture.Db())
        {
            var secretary = await db.MeetingParticipants.Include(p => p.User).SingleAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Secretary);
            if (reason == "inactive") secretary.User.IsActive = false;
            if (reason == "removed") { secretary.AssignmentStatus = AssignmentStatus.Removed; secretary.RemovedAt = fixture.Clock.Now; secretary.RemovedById = s.Chair; }
            if (reason == "delegated") { secretary.Response = InvitationResponse.Delegated; secretary.DelegatedToUserId = s.Member; }
            await db.SaveChangesAsync();
        }
        await Expect(await Act(s, "Start"), 409, "MSG112");
        await using (var db = fixture.Db())
        {
            var meeting = await db.Meetings.SingleAsync(m => m.Id == s.MeetingId);
            meeting.Status = MeetingStatus.InProgress;
            await db.SaveChangesAsync();
        }
        await Expect(await Act(s, "End"), 200, "MSG111");
    }

    [Fact]
    public async Task End_completes_only_current_agenda_and_preserves_open_votes_and_attendance()
    {
        var s = await fixture.Seed(MeetingStatus.InProgress);
        Guid voteId;
        string before;
        await using (var db = fixture.Db())
        {
            var items = await db.AgendaItems.Where(i => i.MeetingId == s.MeetingId).OrderBy(i => i.Position).ToArrayAsync();
            items[0].Status = AgendaItemStatus.InProgress; items[0].ActualStartedAt = fixture.Clock.Now.AddMinutes(-5);
            items[2].Status = AgendaItemStatus.Done; items[2].ActualStartedAt = fixture.Clock.Now.AddMinutes(-10); items[2].ActualEndedAt = fixture.Clock.Now.AddMinutes(-6);
            var vote = new VotingSession { MeetingId = s.MeetingId, Title = "Open vote", CreatedById = s.Chair, Status = VotingStatus.Open };
            var option = new VotingOption { VotingSessionId = vote.Id, Label = "Đồng ý" };
            db.VotingSessions.Add(vote); db.VotingOptions.Add(option);
            db.VoteReceipts.Add(new() { VotingSessionId = vote.Id, VoterId = s.Member });
            db.VoteChoices.Add(new() { VotingSessionId = vote.Id, VotingOptionId = option.Id, VoterId = s.Member });
            await db.SaveChangesAsync(); voteId = vote.Id;
            before = await PreservedSnapshot(db, s.MeetingId, s.Items[0]);
        }
        fixture.Queries.Clear();
        await Expect(await Act(s, "End"), 200, "MSG111");
        Assert.DoesNotContain(fixture.Queries, q => q.Contains("voting_", StringComparison.Ordinal) || q.Contains("vote_", StringComparison.Ordinal));
        await using var check = fixture.Db();
        Assert.Equal(before, await PreservedSnapshot(check, s.MeetingId, s.Items[0]));
        Assert.Equal(VotingStatus.Open, (await check.VotingSessions.FindAsync(voteId))!.Status);
        var current = await check.AgendaItems.FindAsync(s.Items[0]);
        Assert.Equal(AgendaItemStatus.Done, current!.Status); Assert.Equal(fixture.Clock.Now, current.ActualEndedAt);
        Assert.Equal(fixture.Clock.Now, current.UpdatedAt);
        Assert.Equal(fixture.Clock.Now.AddMinutes(-30), (await check.Meetings.FindAsync(s.MeetingId))!.ActualStartedAt);
        Assert.Equal(s.Items[0], fixture.Events.Single(e => e.MeetingId == s.MeetingId).AgendaItemId);
    }

    private static async Task<string> PreservedSnapshot(TrustCabinet.Infrastructure.TrustCabinetDbContext db, Guid id, Guid currentItemId)
        => JsonSerializer.Serialize(new
        {
            Participants = await db.MeetingParticipants.Where(p => p.MeetingId == id).OrderBy(p => p.Id)
                .Select(p => new { p.Id, p.Role, p.Response, p.Attendance, p.RespondedAt, p.AttendanceRecordedAt, p.UpdatedAt }).ToArrayAsync(),
            Items = await db.AgendaItems.Where(i => i.MeetingId == id && i.Id != currentItemId)
                .OrderBy(i => i.Id).Select(i => new { i.Id, i.Status, i.ActualStartedAt, i.ActualEndedAt, i.UpdatedAt }).ToArrayAsync(),
            Votes = await db.VotingSessions.Where(v => v.MeetingId == id).Select(v => new { v.Id, v.Status, v.UpdatedAt, v.ClosedAt }).ToArrayAsync(),
            Receipts = await db.VoteReceipts.Where(v => v.VotingSession.MeetingId == id).Select(v => new { v.Id, v.VoterId, v.CastAt, v.UpdatedAt }).ToArrayAsync(),
            Choices = await db.VoteChoices.Where(v => v.VotingSession.MeetingId == id).Select(v => new { v.Id, v.VoterId, v.VotingOptionId, v.CreatedAt, v.UpdatedAt }).ToArrayAsync(),
        });

    [Theory]
    [InlineData("Start", "save")]
    [InlineData("End", "save")]
    [InlineData("Start", "commit")]
    [InlineData("End", "commit")]
    [InlineData("Start", "read")]
    [InlineData("End", "read")]
    public async Task Persistence_failures_rollback_everything_and_skip_effects(string action, string failure)
    {
        var status = action == "Start" ? MeetingStatus.Published : MeetingStatus.InProgress;
        var s = await fixture.Seed(status);
        await using (var db = fixture.Db())
        {
            var item = (await db.AgendaItems.FindAsync(s.Items[0]))!;
            item.Status = AgendaItemStatus.InProgress; item.ActualStartedAt = fixture.Clock.Now.AddMinutes(-2);
            await db.SaveChangesAsync();
        }
        try
        {
            fixture.SaveFailure.Enabled = failure == "save"; fixture.FailCommit = failure == "commit"; fixture.FailWorkflowRead = failure == "read";
            await Expect(await Act(s, action), 503, action == "Start" ? "MSG115" : "MSG116");
        }
        finally { fixture.SaveFailure.Enabled = false; fixture.FailCommit = false; fixture.FailWorkflowRead = false; }
        await AssertUnchanged(s, status);
        await using var check = fixture.Db();
        var current = (await check.AgendaItems.FindAsync(s.Items[0]))!;
        Assert.Equal(AgendaItemStatus.InProgress, current.Status); Assert.Null(current.ActualEndedAt);
    }

    private async Task AssertUnchanged(MeetingLifecycleFixture.Scenario s, MeetingStatus status)
    {
        await using var check = fixture.Db();
        var m = (await check.Meetings.FindAsync(s.MeetingId))!;
        Assert.Equal(status, m.Status); Assert.Null(m.ActualEndedAt);
        Assert.Equal(status == MeetingStatus.InProgress ? fixture.Clock.Now.AddMinutes(-30) : (DateTimeOffset?)null, m.ActualStartedAt);
        Assert.Empty(await check.MeetingStatusHistory.Where(h => h.MeetingId == s.MeetingId).ToArrayAsync());
        Assert.Empty(await check.Notifications.Where(n => n.MeetingId == s.MeetingId).ToArrayAsync());
        Assert.DoesNotContain(fixture.Events, e => e.MeetingId == s.MeetingId);
        Assert.DoesNotContain(CallRoomNames.ForMeeting(s.MeetingId), fixture.ClosedRooms);
    }

    [Fact]
    public async Task Invalid_agenda_timestamps_roll_back_end()
    {
        var s = await fixture.Seed(MeetingStatus.InProgress);
        await using (var db = fixture.Db())
        {
            var item = (await db.AgendaItems.FindAsync(s.Items[0]))!;
            item.Status = AgendaItemStatus.InProgress; item.ActualStartedAt = fixture.Clock.Now.AddMinutes(1);
            await db.SaveChangesAsync();
        }
        await Expect(await Act(s, "End"), 503, "MSG116");
        await AssertUnchanged(s, MeetingStatus.InProgress);
    }

    [Fact]
    public async Task Notification_failure_does_not_rollback_start_or_replay_on_duplicate()
    {
        var s = await fixture.Seed();
        try { fixture.FailNotifications = true; await Expect(await Act(s, "Start"), 200, "MSG110"); }
        finally { fixture.FailNotifications = false; }
        await Expect(await Act(s, "Start"), 409, "MSG112");
        await using var db = fixture.Db();
        Assert.Equal(MeetingStatus.InProgress, (await db.Meetings.FindAsync(s.MeetingId))!.Status);
        Assert.Single(await db.MeetingStatusHistory.Where(h => h.MeetingId == s.MeetingId).ToArrayAsync());
        Assert.Empty(await db.Notifications.Where(n => n.MeetingId == s.MeetingId).ToArrayAsync());
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("push")]
    [InlineData("bookkeeping")]
    [InlineData("unconfigured")]
    public async Task End_effect_failure_preserves_commit_and_reconciliation_recovers(string failure)
    {
        var s = await fixture.Seed(MeetingStatus.InProgress);
        var room = CallRoomNames.ForMeeting(s.MeetingId);
        fixture.ProviderRooms[room] = 0;
        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ICallRepository>();
            var session = await repo.EnsureActiveRoomSessionAsync(s.MeetingId, s.Chair, fixture.Clock.Now.AddMinutes(-10));
            await repo.RecordJoinAsync(session.Id, s.Chair, fixture.Clock.Now.AddMinutes(-9));
        }
        try
        {
            fixture.ProviderUnavailable = failure == "provider"; fixture.FailPush = failure == "push";
            fixture.FailCalls = failure == "bookkeeping"; fixture.ProviderConfigured = failure != "unconfigured";
            await Expect(await Act(s, "End"), 200, "MSG111");
        }
        finally { fixture.ProviderUnavailable = false; fixture.FailPush = false; fixture.FailCalls = false; fixture.ProviderConfigured = true; }
        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ICallRoomService>();
            Assert.Equal(CallOutcome.RoomNotOpen, (await service.JoinAsync(s.MeetingId, s.Chair)).Outcome);
            await service.ReconcileAsync();
        }
        Assert.False(fixture.ProviderRooms.ContainsKey(room));
        await using var db = fixture.Db();
        Assert.Equal(MeetingStatus.Ended, (await db.Meetings.FindAsync(s.MeetingId))!.Status);
        var closed = await db.CallSessions.SingleAsync(c => c.MeetingId == s.MeetingId);
        Assert.Equal(CallStatus.Ended, closed.Status);
        Assert.All(await db.CallParticipants.Where(p => p.CallSessionId == closed.Id).ToArrayAsync(), p => Assert.NotNull(p.LeftAt));
    }

    [Fact]
    public async Task Orphan_room_recovered_without_active_session()
    {
        var s = await fixture.Seed(MeetingStatus.InProgress);
        var room = CallRoomNames.ForMeeting(s.MeetingId); fixture.ProviderRooms[room] = 0;
        try { fixture.ProviderUnavailable = true; await Expect(await Act(s, "End"), 200, "MSG111"); }
        finally { fixture.ProviderUnavailable = false; }
        Assert.True(fixture.ProviderRooms.ContainsKey(room));
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICallRoomService>().ReconcileAsync();
        Assert.False(fixture.ProviderRooms.ContainsKey(room));
    }

    [Theory]
    [InlineData("{\"action\":\"start\"}")]
    [InlineData("{\"action\":8}")]
    [InlineData("{\"action\":\"Start\",\"action\":\"End\"}")]
    [InlineData("{\"action\":\"Start\",\"reason\":null}")]
    [InlineData("{\"action\":\"End\",\"actorId\":\"forged\"}")]
    [InlineData("{\"action\":\"End\",\"actualEndedAt\":\"2026-01-01\"}")]
    [InlineData("{\"action\":\"Start\",\"assignedApproverId\":null}")]
    [InlineData("{\"action\":\"End\",\"organizerId\":null}")]
    [InlineData("{")]
    public async Task Strict_parser_rejects_forged_or_malformed_commands(string json)
    {
        var s = await fixture.Seed();
        await Expect(await fixture.Client(s.Chair).PostAsync(Url(s.MeetingId), new StringContent(json, Encoding.UTF8, "application/json")), 400, "MSG80");
    }

    [Fact]
    public async Task Authentication_active_gate_content_type_and_size_contracts_are_preserved()
    {
        var s = await fixture.Seed();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client().PostAsJsonAsync(Url(s.MeetingId), new { action = "Start" })).StatusCode);
        using var invalid = fixture.Client(); invalid.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await invalid.PostAsJsonAsync(Url(s.MeetingId), new { action = "Start" })).StatusCode);
        await Expect(await fixture.Client(s.Chair).PostAsync(Url(s.MeetingId), new StringContent("Start")), 415, "MSG34");
        await Expect(await fixture.Client(s.Chair).PostAsync(Url(s.MeetingId), new StringContent(new string(' ', 65537), Encoding.UTF8, "application/json")), 413, "MSG87");
        try { fixture.FailGate = true; await Expect(await Act(s, "Start"), 503, "MSG131"); }
        finally { fixture.FailGate = false; }
        await using var db = fixture.Db();
        (await db.Users.FindAsync(s.Chair))!.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await Act(s, "Start")).StatusCode);
    }

    [Fact]
    public async Task Old_workflow_service_rejects_lifecycle_actions_and_new_service_rejects_other_commands()
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var old = scope.ServiceProvider.GetRequiredService<IMeetingService>();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IMeetingLifecycleService>();
        foreach (var action in new[] { MeetingTransitionAction.Start, MeetingTransitionAction.End })
            Assert.Equal(MeetingWorkflowOutcome.InvalidRequest, (await old.TransitionAsync(Guid.NewGuid(), new(action), Guid.NewGuid())).Outcome);
        foreach (var command in new[] { new MeetingTransitionCommand(MeetingTransitionAction.Publish), new(MeetingTransitionAction.Start, Reason: "x"), new(MeetingTransitionAction.End, OrganizerId: Guid.NewGuid()) })
            Assert.Equal(MeetingWorkflowOutcome.InvalidRequest, (await lifecycle.TransitionAsync(Guid.NewGuid(), command, Guid.NewGuid())).Outcome);
    }
}
