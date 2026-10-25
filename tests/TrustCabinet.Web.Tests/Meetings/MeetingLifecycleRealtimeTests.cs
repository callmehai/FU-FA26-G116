using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed partial class MeetingLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MeetingEnded_event_refreshes_committed_snapshot_and_reconnect_recovers(bool hasCurrent)
    {
        var s = await fixture.Seed(MeetingStatus.InProgress);
        await using var hub = fixture.Hub(s.Member);
        var received = Channel.CreateUnbounded<AgendaChange>();
        hub.On<AgendaChange>("agendaChanged", e => received.Writer.TryWrite(e));
        await hub.StartAsync(); await hub.InvokeAsync("JoinMeeting", s.MeetingId);
        if (hasCurrent)
        {
            await using var db = fixture.Db();
            var item = (await db.AgendaItems.FindAsync(s.Items[0]))!;
            item.Status = AgendaItemStatus.InProgress; item.ActualStartedAt = fixture.Clock.Now.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        await Expect(await Act(s, "End"), 200, "MSG111");
        var change = await received.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("MeetingEnded", change.ChangeType);
        Assert.Equal(hasCurrent ? s.Items[0] : (Guid?)null, change.AgendaItemId);
        Assert.Equal(fixture.Clock.Now, change.OccurredAt);
        var snapshot = await fixture.Client(s.Member).GetFromJsonAsync<JsonElement>(s.Url);
        Assert.Equal("Ended", snapshot.GetProperty("meetingStatus").GetString());
        Assert.All(snapshot.GetProperty("items").EnumerateArray(), item => Assert.False(item.GetProperty("actions").GetProperty("canSetCurrent").GetBoolean()));
        await hub.StopAsync(); await hub.StartAsync(); await hub.InvokeAsync("JoinMeeting", s.MeetingId);
        Assert.Equal("Ended", (await fixture.Client(s.Member).GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("meetingStatus").GetString());
        Assert.False(received.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("removed")]
    [InlineData("otherMeeting")]
    public async Task MeetingEnded_event_does_not_reach_stale_or_other_meeting_subscription(string reason)
    {
        var s = await fixture.Seed(MeetingStatus.InProgress);
        var other = await fixture.Seed(MeetingStatus.InProgress);
        await using var excluded = fixture.Hub(reason == "otherMeeting" ? other.Member : s.Member);
        await using var included = fixture.Hub(s.Secretary);
        var received = Channel.CreateUnbounded<AgendaChange>();
        var excludedEvents = Channel.CreateUnbounded<AgendaChange>();
        included.On<AgendaChange>("agendaChanged", e => received.Writer.TryWrite(e));
        excluded.On<AgendaChange>("agendaChanged", e => excludedEvents.Writer.TryWrite(e));
        await Task.WhenAll(included.StartAsync(), excluded.StartAsync());
        await included.InvokeAsync("JoinMeeting", s.MeetingId);
        await excluded.InvokeAsync("JoinMeeting", reason == "otherMeeting" ? other.MeetingId : s.MeetingId);
        await using (var db = fixture.Db())
        {
            if (reason == "inactive") (await db.Users.FindAsync(s.Member))!.IsActive = false;
            if (reason == "removed")
            {
                var member = await db.MeetingParticipants.SingleAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Member);
                member.AssignmentStatus = AssignmentStatus.Removed; member.RemovedAt = fixture.Clock.Now; member.RemovedById = s.Chair;
            }
            await db.SaveChangesAsync();
        }
        await Expect(await Act(s, "End"), 200, "MSG111");
        Assert.Equal("MeetingEnded", (await received.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5))).ChangeType);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => excludedEvents.Reader.ReadAsync(deadline.Token).AsTask());
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("removed")]
    [InlineData("delegated")]
    [InlineData("guest")]
    public async Task Start_recipient_snapshot_excludes_ineligible_assignments(string reason)
    {
        var s = await fixture.Seed();
        await using (var db = fixture.Db())
        {
            var member = await db.MeetingParticipants.Include(p => p.User).SingleAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Member);
            if (reason == "inactive") member.User.IsActive = false;
            if (reason == "removed") { member.AssignmentStatus = AssignmentStatus.Removed; member.RemovedAt = fixture.Clock.Now; member.RemovedById = s.Chair; }
            if (reason == "delegated") { member.Response = InvitationResponse.Delegated; member.DelegatedToUserId = s.Secretary; }
            if (reason == "guest") member.Role = MeetingRole.Guest;
            await db.SaveChangesAsync();
        }
        await Expect(await Act(s, "Start"), 200, "MSG110");
        await using var check = fixture.Db();
        var recipients = await check.Notifications.Where(n => n.MeetingId == s.MeetingId).Select(n => n.UserId).ToArrayAsync();
        Assert.Equal(new[] { s.Secretary }, recipients);
    }
}
