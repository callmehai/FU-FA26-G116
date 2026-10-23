using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Attendance;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Web.Tests.Participants;
using static TrustCabinet.Web.Tests.Attendance.AttendanceEndpointTests;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Attendance;

public sealed class AttendanceConcurrencyTests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    private async Task Waiters(int count)
    {
        await using var observer = f.Db(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            var waiting = await observer.Database.SqlQueryRaw<int>("""
                SELECT count(*)::int AS "Value" FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock'
                  AND query LIKE '%FOR UPDATE%' AND pid <> pg_backend_pid()
                """).SingleAsync(timeout.Token);
            if (waiting >= count) return;
            await Task.Delay(10, timeout.Token);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_writers_compare_versions_after_waiting(bool differentTargets)
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var one = f.Client(s.Secretary); using var two = f.Client(s.Secretary);
        await using var db = f.Db(); await using var tx = await db.Database.BeginTransactionAsync();
        await db.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {s.MeetingId} FOR UPDATE").ToArrayAsync();
        var a = Save(one, s, Entry(s.MeetingId, s.MemberAssignment));
        var b = Save(two, s, Entry(s.MeetingId, differentTargets ? s.ChairAssignment : s.MemberAssignment, status: "Absent"));
        await Waiters(2); await tx.CommitAsync();
        var results = await Task.WhenAll(a, b).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(differentTargets ? 2 : 1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        if (!differentTargets) await Code(Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict), 409, "MSG151");
        Assert.Equal(differentTargets ? 2 : 1, await db.AuditLog.CountAsync(audit => audit.ObjectId == s.MeetingId));
    }

    [Theory]
    [InlineData("ended", 409)]
    [InlineData("secretary", 403)]
    [InlineData("inactive", 403)]
    [InlineData("removed", 409)]
    [InlineData("revoked", 409)]
    [InlineData("newParticipant", 200)]
    [InlineData("response", 200)]
    [InlineData("targetRole", 200)]
    public async Task Waiting_writer_rechecks_state_authority_membership_but_not_unrelated_changes(string change, int expected)
    {
        var s = await f.Seed(MeetingStatus.InProgress); var guest = await Guest(f, s); using var client = f.Client(s.Secretary);
        await using var db = f.Db(); await using var tx = await db.Database.BeginTransactionAsync();
        var meeting = (await db.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {s.MeetingId} FOR UPDATE").ToArrayAsync()).Single();
        var request = Save(client, s, Entry(s.MeetingId, s.MemberAssignment), Entry(s.MeetingId, guest.Id, "Guest"));
        await Waiters(1);
        var member = await db.MeetingParticipants.SingleAsync(p => p.Id == s.MemberAssignment);
        switch (change)
        {
            case "ended": meeting.Status = MeetingStatus.Ended; break;
            case "secretary": (await db.MeetingParticipants.FindAsync(s.SecretaryAssignment))!.Role = MeetingRole.Member; break;
            case "inactive": (await db.Users.FindAsync(s.Secretary))!.IsActive = false; break;
            case "removed": member.AssignmentStatus = AssignmentStatus.Removed; member.RemovedAt = f.Clock.Now; member.RemovedById = s.Secretary; break;
            case "revoked": var g = (await db.MeetingGuestInvitations.FindAsync(guest.Id))!; g.RevokedAt = f.Clock.Now; g.RevokedById = s.Secretary; break;
            case "newParticipant": db.MeetingParticipants.Add(new() { MeetingId = s.MeetingId, UserId = s.Outsider }); break;
            case "response": member.Response = InvitationResponse.Declined; member.RespondedAt = f.Clock.Now; break;
            case "targetRole": member.Role = MeetingRole.Secretary; break;
        }
        await db.SaveChangesAsync(); await tx.CommitAsync();
        var response = await request.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(expected, (int)response.StatusCode);
        db.ChangeTracker.Clear(); Assert.Equal(expected == 200 ? 1 : 0, (await db.MeetingParticipants.FindAsync(s.MemberAssignment))!.AttendanceVersion);
        Assert.Equal(expected == 200 ? 1 : 0, (await db.MeetingGuestInvitations.FindAsync(guest.Id))!.AttendanceVersion);
    }

    [Fact]
    public async Task Target_row_is_loaded_after_its_lock_not_from_an_earlier_roster_snapshot()
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var client = f.Client(s.Secretary);
        await using var db = f.Db(); await using var tx = await db.Database.BeginTransactionAsync();
        var p = (await db.MeetingParticipants.FromSqlInterpolated($"SELECT * FROM meeting_participants WHERE id = {s.MemberAssignment} FOR UPDATE").ToArrayAsync()).Single();
        var request = Save(client, s, Entry(s.MeetingId, s.MemberAssignment)); await Waiters(1);
        p.Attendance = AttendanceStatus.Absent; p.AttendanceVersion = 1; p.AttendanceRecordedById = s.Secretary; p.AttendanceRecordedAt = f.Clock.Now;
        await db.SaveChangesAsync(); await tx.CommitAsync();
        await Code(await request, 409, "MSG151"); await db.Entry(p).ReloadAsync(); Assert.Equal(AttendanceStatus.Absent, p.Attendance);
    }

    [Fact]
    public async Task Reverse_order_batches_do_not_deadlock_and_stale_batch_is_atomic()
    {
        var s = await f.Seed(MeetingStatus.InProgress); var g = await Guest(f, s); using var client = f.Client(s.Secretary);
        await using var db = f.Db(); await using var tx = await db.Database.BeginTransactionAsync();
        await db.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {s.MeetingId} FOR UPDATE").ToArrayAsync();
        var a = Save(client, s, Entry(s.MeetingId, s.MemberAssignment), Entry(s.MeetingId, g.Id, "Guest"));
        var b = Save(client, s, Entry(s.MeetingId, g.Id, "Guest", "Absent"), Entry(s.MeetingId, s.MemberAssignment, status: "Absent"));
        await Waiters(2); await tx.CommitAsync();
        var results = await Task.WhenAll(a, b).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        var member = (await db.MeetingParticipants.FindAsync(s.MemberAssignment))!; var guest = (await db.MeetingGuestInvitations.FindAsync(g.Id))!;
        Assert.Equal(member.Attendance, guest.Attendance); Assert.Equal(1, member.AttendanceVersion); Assert.Equal(1, guest.AttendanceVersion);
    }

    [Fact]
    public async Task Remove_then_reinvite_retains_old_evidence_and_new_assignment_starts_at_zero()
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var client = f.Client(s.Secretary);
        await Code(await Save(client, s, Entry(s.MeetingId, s.MemberAssignment)), 200, "MSG147");
        await Code(await Write(client, s.Url + $"/participants/{s.MemberAssignment}", HttpMethod.Delete), 200, "MSG103");
        await Code(await Write(client, s.Url + "/participants", HttpMethod.Post,
            new { userIds = new[] { s.Member }, role = "Member" }, etag: await Tag(client, s.Url)), 201, "MSG96");
        await Code(await Save(client, s, Entry(s.MeetingId, s.MemberAssignment, version: 1)), 409, "MSG151");
        await using var db = f.Db(); var rows = await db.MeetingParticipants.Where(p => p.MeetingId == s.MeetingId && p.UserId == s.Member).ToArrayAsync();
        var old = Assert.Single(rows, p => p.AssignmentStatus == AssignmentStatus.Removed);
        var current = Assert.Single(rows, p => p.AssignmentStatus == AssignmentStatus.Current);
        Assert.Equal(AttendanceStatus.Present, old.Attendance); Assert.Equal(1, old.AttendanceVersion);
        Assert.Equal(AttendanceStatus.Unknown, current.Attendance); Assert.Equal(0, current.AttendanceVersion);
    }

    [Fact]
    public async Task Overflow_cannot_partially_update_other_entries()
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var client = f.Client(s.Secretary);
        await using var db = f.Db(); (await db.MeetingParticipants.FindAsync(s.MemberAssignment))!.AttendanceVersion = long.MaxValue;
        await db.SaveChangesAsync();
        await Code(await Save(client, s, Entry(s.MeetingId, s.ChairAssignment), Entry(s.MeetingId, s.MemberAssignment, version: long.MaxValue)), 503, "MSG150");
        db.ChangeTracker.Clear(); Assert.Equal(0, (await db.MeetingParticipants.FindAsync(s.ChairAssignment))!.AttendanceVersion);
    }
    [Fact]
    public async Task Chair_handover_and_attendance_serialize_without_overwriting_each_other()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair); using var secretary = f.Client(s.Secretary);
        await using var db = f.Db(); await using var tx = await db.Database.BeginTransactionAsync();
        await db.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {s.MeetingId} FOR UPDATE").ToArrayAsync();
        var attendance = Save(secretary, s, Entry(s.MeetingId, s.MemberAssignment));
        var handover = Write(chair, s.Url + "/participants/chair-handover", HttpMethod.Post,
            new { newChairParticipantId = s.MemberAssignment, confirmed = true });
        await Waiters(2); await tx.CommitAsync();
        await Code(await attendance.WaitAsync(TimeSpan.FromSeconds(15)), 200, "MSG147");
        await Code(await handover.WaitAsync(TimeSpan.FromSeconds(15)), 200, "MSG257");
        db.ChangeTracker.Clear();
        var incoming = (await db.MeetingParticipants.FindAsync(s.MemberAssignment))!;
        Assert.Equal(MeetingRole.Chair, incoming.Role);
        Assert.Equal(AttendanceStatus.Present, incoming.Attendance);
        Assert.Equal(1, incoming.AttendanceVersion);
        Assert.Equal(s.Secretary, incoming.AttendanceRecordedById);
        Assert.Equal(1, (await db.Meetings.FindAsync(s.MeetingId))!.ParticipantsRevision);
        await Code(await Save(secretary, s, Entry(s.MeetingId, s.MemberAssignment, status: "Absent", version: 1)), 200, "MSG147");
    }

}
