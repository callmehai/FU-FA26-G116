using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Attendance;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Infrastructure.Participants;
using TrustCabinet.Web.Tests.Participants;
using static TrustCabinet.Web.Tests.Attendance.AttendanceEndpointTests;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Attendance;

public sealed class AttendanceHubTests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    private static async Task<AttendanceChange> Next(ChannelReader<AttendanceChange> events)
        => await events.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    private static async Task None(ChannelReader<AttendanceChange> events)
    {
        using var timeout = new CancellationTokenSource(250);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await events.ReadAsync(timeout.Token));
    }

    [Fact]
    public async Task Joined_roster_readers_receive_committed_events_and_reconnect_reads_current_state()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var secretary = f.Client(s.Secretary); using var member = f.Client(s.Member);
        await using var allowed = f.Hub(secretary); await using var excluded = f.Hub(member);
        var received = Channel.CreateUnbounded<AttendanceChange>(); var denied = Channel.CreateUnbounded<AttendanceChange>();
        allowed.On<AttendanceChange>("attendanceChanged", e => received.Writer.TryWrite(e));
        excluded.On<AttendanceChange>("attendanceChanged", e => denied.Writer.TryWrite(e));
        await Task.WhenAll(allowed.StartAsync(), excluded.StartAsync());
        await allowed.InvokeAsync("JoinMeeting", s.MeetingId);
        await Code(await Save(secretary, s, Entry(s.MeetingId, s.MemberAssignment)), 200, "MSG147");
        var change = await Next(received.Reader); Assert.Equal(s.MeetingId, change.MeetingId); Assert.NotEqual(Guid.Empty, change.EventId);
        var page = await secretary.GetFromJsonAsync<JsonElement>(s.Url + "/attendance");
        Assert.Equal(1, page.GetProperty("totals").GetProperty("present").GetInt32()); await None(denied.Reader);
        await Code(await Save(secretary, s, Entry(s.MeetingId, s.MemberAssignment, version: 1)), 200, "MSG147"); await None(received.Reader);
        f.Failure.MeetingId = s.MeetingId;
        try { await Code(await Save(secretary, s, Entry(s.MeetingId, s.MemberAssignment, status: "Absent", version: 1)), 503, "MSG150"); }
        finally { f.Failure.Reset(); }
        await None(received.Reader);
        await allowed.StopAsync(); await allowed.StartAsync(); await allowed.InvokeAsync("JoinMeeting", s.MeetingId);
        Assert.Equal(HttpStatusCode.OK, (await secretary.GetAsync(s.Url + "/attendance")).StatusCode);
        await Code(await Save(secretary, s, Entry(s.MeetingId, s.MemberAssignment, status: "Absent", version: 1)), 200, "MSG147");
        Assert.NotEqual(change.EventId, (await Next(received.Reader)).EventId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Owner_and_member_readers_receive_events_without_grants(bool owner)
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var secretary = f.Client(s.Secretary);
        using var reader = f.Client(owner ? s.Organizer : s.Member);
        await using var hub = f.Hub(reader);
        var events = Channel.CreateUnbounded<AttendanceChange>();
        hub.On<AttendanceChange>("attendanceChanged", e => events.Writer.TryWrite(e));
        await hub.StartAsync(); await hub.InvokeAsync("JoinMeeting", s.MeetingId);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync(s.Url + "/attendance")).StatusCode);
        await Code(await Save(secretary, s, Entry(s.MeetingId, s.MemberAssignment)), 200, "MSG147");
        Assert.Equal(s.MeetingId, (await Next(events.Reader)).MeetingId);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("removed")]
    public async Task Existing_subscriber_losing_permission_receives_no_further_event(string reason)
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var secretary = f.Client(s.Secretary); using var member = f.Client(s.Member);
        await using var db = f.Db();
        await using var hub = f.Hub(member); var events = Channel.CreateUnbounded<AttendanceChange>();
        hub.On<AttendanceChange>("attendanceChanged", e => events.Writer.TryWrite(e)); await hub.StartAsync(); await hub.InvokeAsync("JoinMeeting", s.MeetingId);
        await Save(secretary, s, Entry(s.MeetingId, s.ChairAssignment)); await Next(events.Reader);
        if (reason == "inactive") (await db.Users.FindAsync(s.Member))!.IsActive = false;
        else { var p = (await db.MeetingParticipants.FindAsync(s.MemberAssignment))!; p.AssignmentStatus = AssignmentStatus.Removed; p.RemovedAt = f.Clock.Now; p.RemovedById = s.Secretary; }
        await db.SaveChangesAsync();
        await Code(await Save(secretary, s, Entry(s.MeetingId, s.ChairAssignment, status: "Absent", version: 1)), 200, "MSG147");
        await None(events.Reader);
    }

    [Theory]
    [InlineData("sessionRevoked")]
    [InlineData("expired")]
    [InlineData("unverified")]
    [InlineData("invitationRevoked")]
    public async Task Guest_sessions_are_isolated_and_revalidated_after_join(string change)
    {
        var s = await f.Seed(MeetingStatus.InProgress); var g = await Guest(f, s); using var secretary = f.Client(s.Secretary);
        using var guestA = f.Guest(s.MeetingId, g.Id); using var guestB = f.Guest(s.MeetingId, g.Id);
        var sessionA = f.Guests.Sessions.Values.First(v => v.Identity.InvitationId == g.Id).Identity;
        await using var a = f.Hub(guestA); await using var b = f.Hub(guestB);
        var eventsA = Channel.CreateUnbounded<AttendanceChange>(); var eventsB = Channel.CreateUnbounded<AttendanceChange>();
        a.On<AttendanceChange>("attendanceChanged", e => eventsA.Writer.TryWrite(e)); b.On<AttendanceChange>("attendanceChanged", e => eventsB.Writer.TryWrite(e));
        await Task.WhenAll(a.StartAsync(), b.StartAsync()); await a.InvokeAsync("JoinMeeting", s.MeetingId); await b.InvokeAsync("JoinMeeting", s.MeetingId);
        await Save(secretary, s, Entry(s.MeetingId, s.MemberAssignment)); await Next(eventsA.Reader); await Next(eventsB.Reader);
        if (change is "sessionRevoked" or "expired" or "unverified")
        {
            // Resolve the first client's exact session rather than relying on dictionary enumeration order.
            var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(guestA.DefaultRequestHeaders.Authorization!.Parameter);
            sessionA = f.Guests.Sessions[Guid.Parse(jwt.Claims.Single(c => c.Type == "test_session").Value)].Identity;
            var state = f.Guests.Sessions[sessionA.SessionId];
            f.Guests.Sessions[sessionA.SessionId] = change switch
            {
                "sessionRevoked" => state with { Revoked = true }, "expired" => state with { ExpiresAt = f.Clock.Now }, _ => state with { Verified = false }
            };
        }
        else
        {
            await using var db = f.Db();
            { var row = (await db.MeetingGuestInvitations.FindAsync(g.Id))!; row.RevokedAt = f.Clock.Now; row.RevokedById = s.Secretary; }
            await db.SaveChangesAsync();
        }
        await Code(await Save(secretary, s, Entry(s.MeetingId, s.MemberAssignment, status: "Absent", version: 1)), 200, "MSG147");
        await None(eventsA.Reader);
        if (change is "invitationRevoked") await None(eventsB.Reader); else await Next(eventsB.Reader);
    }

    [Fact]
    public async Task Production_default_guest_adapter_denies_and_lists_no_sessions()
    {
        IParticipantGuestAccess access = new UnavailableParticipantGuestAccess();
        Assert.False(await access.CanAccessAsync(new(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()), default));
        Assert.Empty(await access.ListReadersAsync(Guid.CreateVersion7(), default));
    }
}
