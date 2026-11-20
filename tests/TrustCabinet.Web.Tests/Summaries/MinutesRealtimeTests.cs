using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>
/// minutesChanged on /hubs/meetings: the AI draft (SCRUM-36) and the minutes (SCRUM-32, SCRUM-33) tell the open Biên bản
/// tabs of the meeting's readers to re-read, and nobody else.
/// </summary>
public sealed class MinutesRealtimeTests(SummaryFixture f) : IClassFixture<SummaryFixture>
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private async Task<(HubConnection Hub, Channel<JsonElement> Events)> Listen(Guid actor, Guid meetingId)
    {
        var hub = f.Hub(actor);
        var events = Channel.CreateUnbounded<JsonElement>();
        hub.On<JsonElement>("minutesChanged", e => events.Writer.TryWrite(e));
        await hub.StartAsync();
        await hub.InvokeAsync("JoinMeeting", meetingId);
        return (hub, events);
    }

    private static async Task<JsonElement> Next(Channel<JsonElement> events, Guid meetingId)
    {
        var change = await events.Reader.ReadAsync().AsTask().WaitAsync(Wait);
        Assert.Equal(meetingId, change.GetProperty("meetingId").GetGuid());
        Assert.Equal(["eventId", "meetingId", "occurredAt"], change.EnumerateObject().Select(p => p.Name).Order());
        return change;
    }

    private static void Drain(Channel<JsonElement> events)
    {
        while (events.Reader.TryRead(out _)) { }
    }

    [Fact]
    public async Task Asking_for_a_draft_and_each_step_of_writing_it_tell_the_other_officer()
    {
        f.Model.Reset();
        var s = await f.Seed();
        var other = await f.Seed();
        var (chairHub, chair) = await Listen(s.Chair, s.MeetingId);
        var (outsiderHub, outsider) = await Listen(other.Member, other.MeetingId);
        await using var chairScope = chairHub;
        await using var outsiderScope = outsiderHub;
        using var secretary = f.Client(s.Secretary);

        await AssertCode(await secretary.PostAsync(s.Summary, null), 202, "MSG384");
        var queued = await Next(chair, s.MeetingId);
        // Running, then Ready: the tab starts and stops its progress view without waiting for its poll.
        await f.ProcessAll();
        var running = await Next(chair, s.MeetingId);
        var ready = await Next(chair, s.MeetingId);
        Assert.Equal(3, new[] { queued, running, ready }.Select(e => e.GetProperty("eventId").GetGuid()).Distinct().Count());

        await AssertCode(await secretary.PutAsJsonAsync(s.Summary,
            new { objectives = "Mục tiêu đã sửa.", discussion = "1. Báo cáo tiến độ\nĐã sửa.", conclusions = "1. Kết luận của chủ trì\nĐã sửa." }),
            200, "MSG388");
        await Next(chair, s.MeetingId);
        await AssertCode(await secretary.PostAsync($"{s.Summary}/review", null), 200, "MSG391");
        await Next(chair, s.MeetingId);
        // A refused request changes nothing, so it says nothing.
        await AssertCode(await f.Client(s.Member).PostAsync(s.Summary, null), 403, "MSG385");
        await Task.Delay(300);
        Assert.False(chair.Reader.TryRead(out var extra), extra.ToString());
        Assert.False(outsider.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Building_submitting_returning_and_issuing_the_minutes_tell_the_readers()
    {
        var s = await f.Seed(MeetingStatus.Ended);
        var other = await f.Seed(MeetingStatus.Ended);
        var (chairHub, chair) = await Listen(s.Chair, s.MeetingId);
        var (memberHub, member) = await Listen(s.Member, s.MeetingId);
        var (outsiderHub, outsider) = await Listen(other.Member, other.MeetingId);
        await using var chairScope = chairHub;
        await using var memberScope = memberHub;
        await using var outsiderScope = outsiderHub;
        using var secretary = f.Client(s.Secretary);
        using var chairClient = f.Client(s.Chair);

        var built = await AssertCode(await secretary.PostAsJsonAsync(s.Minutes, new { rebuild = false }), 201, "MSG395");
        await Next(chair, s.MeetingId);
        var id = built.GetProperty("minutes").GetProperty("id").GetGuid();
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var sent = await AssertCode(await secretary.PostAsJsonAsync($"/api/minutes/{id}/submit",
            new { updatedAt = built.GetProperty("minutes").GetProperty("updatedAt").GetString() }), 200, "MSG424");
        await Next(chair, s.MeetingId);
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var returned = await AssertCode(await chairClient.PostAsJsonAsync($"/api/minutes/{id}/reject",
            new { reason = "Bổ sung kết luận.", updatedAt = sent.GetProperty("minutes").GetProperty("updatedAt").GetString() }),
            200, "MSG430");
        await Next(chair, s.MeetingId);
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var resent = await AssertCode(await secretary.PostAsJsonAsync($"/api/minutes/{id}/submit",
            new { updatedAt = returned.GetProperty("minutes").GetProperty("updatedAt").GetString() }), 200, "MSG424");
        await Next(chair, s.MeetingId);
        Drain(member);
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await AssertCode(await chairClient.PostAsJsonAsync($"/api/minutes/{id}/approve",
            new { updatedAt = resent.GetProperty("minutes").GetProperty("updatedAt").GetString() }), 200, "MSG427");
        // Issued minutes are every reader's: the member's open tab re-reads too.
        await Next(chair, s.MeetingId);
        await Next(member, s.MeetingId);
        // A refused action changes nothing, so it says nothing.
        await AssertCode(await chairClient.PostAsJsonAsync($"/api/minutes/{id}/approve", new { updatedAt = (string?)null }),
            409, "MSG429");
        await Task.Delay(300);
        Assert.False(chair.Reader.TryRead(out _));
        Assert.False(outsider.Reader.TryRead(out _));
    }
}
