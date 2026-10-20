using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Attendance;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Web.Tests.Participants;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Attendance;

public sealed class AttendanceEndpointTests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    [Fact]
    public async Task Database_failure_at_commit_rolls_back_updates_versions_and_audit_together()
    {
        var s = await f.Seed(MeetingStatus.InProgress); var g = await Guest(f, s); using var client = f.Client(s.Secretary);
        await using var db = f.Db();
        // A deferred constraint fails after SaveChanges has executed all UPDATE/INSERT statements.
        // This trigger belongs only to this fixture's unique disposable database.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION attendance_test_reject_commit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF NEW.action = 'meeting.attendance.recorded' THEN
                RAISE EXCEPTION 'Injected attendance commit failure' USING ERRCODE = '23514';
              END IF;
              RETURN NEW;
            END $$;
            CREATE CONSTRAINT TRIGGER attendance_test_reject_commit AFTER INSERT ON audit_log
              DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION attendance_test_reject_commit();
            """);
        try { await Code(await Save(client, s, Entry(s.MeetingId, s.MemberAssignment), Entry(s.MeetingId, g.Id, "Guest")), 503, "MSG150"); }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER attendance_test_reject_commit ON audit_log; DROP FUNCTION attendance_test_reject_commit();");
        }
        Assert.Equal(0, (await db.MeetingParticipants.FindAsync(s.MemberAssignment))!.AttendanceVersion);
        Assert.Equal(0, (await db.MeetingGuestInvitations.FindAsync(g.Id))!.AttendanceVersion);
        Assert.Empty(await db.AuditLog.Where(a => a.ObjectId == s.MeetingId).ToArrayAsync());
    }

    [Theory]
    [InlineData("Corrupt")]
    [InlineData("999")]
    [InlineData("present")]
    public async Task Corrupt_database_attendance_is_not_silently_mapped_to_unmarked(string value)
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var client = f.Client(s.Secretary);
        await using var db = f.Db();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE meeting_participants SET attendance = {value} WHERE id = {s.MemberAssignment}");
        await Code(await client.GetAsync(s.Url + "/attendance"), 500, "MSG277");
        Assert.Equal(value, await db.Database.SqlQuery<string>($"SELECT attendance AS \"Value\" FROM meeting_participants WHERE id = {s.MemberAssignment}").SingleAsync());
    }

    [Fact]
    public async Task Chunked_requests_cannot_bypass_body_size_limit()
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var client = f.Client(s.Secretary);
        using var content = new UnknownLengthContent();
        content.Headers.ContentType = new("application/json");
        await Code(await client.PostAsync(s.Url + "/attendance", content), 413, "MSG87");
    }
    private sealed class UnknownLengthContent : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(new byte[65537]).AsTask();
    }

    internal static object Entry(Guid meeting, Guid id, string kind = "Internal", string status = "Present", long version = 0)
        => new { id, kind, attendance = status, expectedVersion = AttendanceRules.Token(meeting, kind, id, version) };
    internal static Task<HttpResponseMessage> Save(HttpClient client, ParticipantFixture.Scenario s, params object[] entries)
        => client.PostAsJsonAsync(s.Url + "/attendance", new { entries });
    internal static async Task<MeetingGuestInvitation> Guest(ParticipantFixture f, ParticipantFixture.Scenario s, bool grant = true)
    {
        await using var db = f.Db();
        var guest = new MeetingGuestInvitation { MeetingId = s.MeetingId, InvitedById = s.Secretary, InvitedName = "Invited guest",
            Email = $"{Guid.CreateVersion7():N}@guest.test", SensitivityAtInvitation = Sensitivity.Internal };
        db.MeetingGuestInvitations.Add(guest);
        if (grant) db.MeetingParticipantPermissions.Add(new() { GuestInvitationId = guest.Id,
            PermissionKey = MeetingPermissionCatalog.ViewParticipants, GrantedById = s.Secretary, GrantedAt = f.Clock.Now });
        await db.SaveChangesAsync(); return guest;
    }

    [Fact]
    public async Task Mixed_batch_is_partial_audited_versioned_and_noop_preserves_metadata()
    {
        var s = await f.Seed(MeetingStatus.InProgress); var guest = await Guest(f, s);
        using var client = f.Client(s.Secretary);
        var before = await client.GetFromJsonAsync<JsonElement>(s.Url + "/attendance");
        Assert.Equal(4, before.GetProperty("totals").GetProperty("unmarked").GetInt32());
        var result = await Code(await Save(client, s, Entry(s.MeetingId, s.MemberAssignment), Entry(s.MeetingId, guest.Id, "Guest", "Absent")), 200, "MSG147");
        Assert.Equal(2, result.GetProperty("changedCount").GetInt32());
        await using var db = f.Db();
        var member = await db.MeetingParticipants.SingleAsync(p => p.Id == s.MemberAssignment);
        Assert.Equal(1, member.AttendanceVersion); Assert.Equal(s.Secretary, member.AttendanceRecordedById); Assert.Equal(f.Clock.Now, member.AttendanceRecordedAt);
        Assert.Equal(InvitationResponse.Pending, member.Response);
        Assert.Equal(0, (await db.Meetings.FindAsync(s.MeetingId))!.ParticipantsRevision);
        var untouched = await db.MeetingParticipants.SingleAsync(p => p.Id == s.ChairAssignment);
        Assert.Equal(AttendanceStatus.Unknown, untouched.Attendance); Assert.Equal(0, untouched.AttendanceVersion); Assert.Null(untouched.AttendanceRecordedById);
        Assert.Single(await db.AuditLog.Where(a => a.ObjectId == s.MeetingId && a.Action == "meeting.attendance.recorded").ToArrayAsync());
        var after = await client.GetFromJsonAsync<JsonElement>(s.Url + "/attendance");
        foreach (var entry in result.GetProperty("entries").EnumerateArray())
            Assert.Equal(entry.GetProperty("rowVersion").GetString(), after.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == entry.GetProperty("id").GetGuid()).GetProperty("rowVersion").GetString());
        var noop = await Code(await Save(client, s, Entry(s.MeetingId, s.MemberAssignment, version: 1)), 200, "MSG147");
        Assert.Equal(0, noop.GetProperty("changedCount").GetInt32());
        await Code(await Save(client, s, Entry(s.MeetingId, s.MemberAssignment)), 409, "MSG151");
        await Code(await Save(client, s, Entry(s.MeetingId, s.ChairAssignment), Entry(s.MeetingId, s.MemberAssignment)), 409, "MSG151");
        await db.Entry(untouched).ReloadAsync(); Assert.Equal(AttendanceStatus.Unknown, untouched.Attendance);
        Assert.Equal(1, await db.AuditLog.CountAsync(a => a.ObjectId == s.MeetingId && a.Action == "meeting.attendance.recorded"));
    }

    [Fact]
    public async Task Response_and_role_updates_preserve_attendance_token_and_ABA_does_not()
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var client = f.Client(s.Secretary); using var member = f.Client(s.Member);
        await Code(await Save(client, s, Entry(s.MeetingId, s.MemberAssignment)), 200, "MSG147");
        await Code(await Write(member, s.Url + "/invitation-response", HttpMethod.Put, new { response = "Accepted" }), 200, "MSG105");
        await Code(await Write(client, s.Url + "/participants/assignments", HttpMethod.Put,
            new { changes = new[] { new { participantId = s.MemberAssignment, role = "Secretary" } } }), 200, "MSG100");
        await Code(await Save(client, s, Entry(s.MeetingId, s.MemberAssignment, status: "Absent", version: 1)), 200, "MSG147");
        await Code(await Save(client, s, Entry(s.MeetingId, s.MemberAssignment, version: 2)), 200, "MSG147");
        await Code(await Save(client, s, Entry(s.MeetingId, s.MemberAssignment, version: 1)), 409, "MSG151");
        await using var db = f.Db(); var row = await db.MeetingParticipants.FindAsync(s.MemberAssignment);
        Assert.Equal(3, row!.AttendanceVersion); Assert.Equal(InvitationResponse.Accepted, row.Response);
        Assert.Equal(f.Clock.Now, row.AttendanceRecordedAt);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"entries\":[]}")]
    [InlineData("{\"entries\":[null]}")]
    [InlineData("{\"entries\":null}")]
    [InlineData("{\"entries\":[],\"entries\":[]}")]
    [InlineData("{\"entries\":[],\"actorId\":\"ignored\"}")]
    [InlineData("[]")]
    [InlineData("{\"entries\":[{\"id\":\"invalid\"}]}")]
    public async Task Strict_body_validation(string body)
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var client = f.Client(s.Secretary);
        await Code(await client.PostAsync(s.Url + "/attendance", new StringContent(body, Encoding.UTF8, "application/json")), 400, "MSG148");
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("status")]
    [InlineData("emptyId")]
    [InlineData("duplicate")]
    [InlineData("token")]
    [InlineData("numericStatus")]
    public async Task Invalid_entries_are_rejected(string reason)
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var client = f.Client(s.Secretary);
        var entry = new Dictionary<string, object?> { ["id"] = reason == "emptyId" ? Guid.Empty : s.MemberAssignment,
            ["kind"] = reason == "kind" ? "internal" : "Internal", ["attendance"] = reason == "numericStatus" ? 1 : reason == "status" ? "Unknown" : "Present",
            ["expectedVersion"] = reason == "token" ? null : AttendanceRules.Token(s.MeetingId, "Internal", s.MemberAssignment, 0) };
        await Code(await Save(client, s, reason == "duplicate" ? [entry, entry] : [entry]), 400, "MSG148");
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.WaitingApproval)]
    [InlineData(MeetingStatus.Rejected)]
    [InlineData(MeetingStatus.Approved)]
    [InlineData(MeetingStatus.Published)]
    [InlineData(MeetingStatus.Ended)]
    public async Task Only_in_progress_can_write(MeetingStatus status)
    {
        var s = await f.Seed(status); using var client = f.Client(s.Secretary);
        await Code(await Save(client, s, Entry(s.MeetingId, s.MemberAssignment)), 409, "MSG149");
        var page = await client.GetFromJsonAsync<JsonElement>(s.Url + "/attendance");
        Assert.False(page.GetProperty("permissions").GetProperty("canRecordAttendance").GetBoolean());
    }

    [Fact]
    public async Task Authorization_and_field_privacy_are_server_side()
    {
        var s = await f.Seed(MeetingStatus.InProgress); var g = await Guest(f, s);
        using var anon = f.Client(); using var member = f.Client(s.Member); using var outsider = f.Client(s.Outsider);
        using var chair = f.Client(s.Chair); using var secretary = f.Client(s.Secretary);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(s.Url + "/attendance")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(s.Url + "/attendance?page=-1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync(s.Url + "/attendance")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Save(chair, s, Entry(s.MeetingId, s.MemberAssignment))).StatusCode);
        await using var db = f.Db(); db.MeetingParticipantPermissions.Add(new() { MeetingParticipantId = s.MemberAssignment,
            PermissionKey = MeetingPermissionCatalog.ViewParticipants, GrantedById = s.Secretary, GrantedAt = f.Clock.Now }); await db.SaveChangesAsync();
        var page = await member.GetFromJsonAsync<JsonElement>(s.Url + "/attendance");
        Assert.All(page.GetProperty("items").EnumerateArray(), row => { Assert.Equal(JsonValueKind.Null, row.GetProperty("invitationResponse").ValueKind); Assert.False(row.TryGetProperty("email", out _)); });
        using var guest = f.Guest(s.MeetingId, g.Id); using var unverified = f.Guest(s.MeetingId, g.Id, verified: false);
        using var expired = f.Guest(s.MeetingId, g.Id, expired: true); using var mixed = f.Guest(s.MeetingId, g.Id, user: s.Secretary);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync(s.Url + "/attendance")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Save(guest, s, Entry(s.MeetingId, g.Id, "Guest"))).StatusCode);
        foreach (var rejected in new[] { unverified, expired, mixed }) Assert.Equal(HttpStatusCode.NotFound, (await rejected.GetAsync(s.Url + "/attendance")).StatusCode);
        (await db.Users.FindAsync(s.Secretary))!.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await Save(secretary, s, Entry(s.MeetingId, s.MemberAssignment))).StatusCode);
    }

    [Fact]
    public async Task Legacy_values_are_separate_and_can_be_corrected_without_rewriting_others()
    {
        var s = await f.Seed(MeetingStatus.InProgress); var g = await Guest(f, s); using var client = f.Client(s.Secretary);
        await using var db = f.Db(); var p = await db.MeetingParticipants.FindAsync(s.MemberAssignment);
        p!.Attendance = AttendanceStatus.Excused; (await db.MeetingGuestInvitations.FindAsync(g.Id))!.Attendance = AttendanceStatus.Delegated;
        (await db.Users.FindAsync(s.Member))!.IsActive = false; await db.SaveChangesAsync();
        var page = await client.GetFromJsonAsync<JsonElement>(s.Url + "/attendance?pageSize=1");
        Assert.Equal(2, page.GetProperty("totals").GetProperty("legacy").GetInt32()); Assert.Equal(2, page.GetProperty("totals").GetProperty("unmarked").GetInt32());
        await Code(await Save(client, s, Entry(s.MeetingId, s.MemberAssignment)), 200, "MSG147");
        await db.Entry(p).ReloadAsync(); Assert.Equal(AttendanceStatus.Present, p.Attendance); Assert.Equal(1, p.AttendanceVersion);
        Assert.Contains("Excused", (await db.AuditLog.SingleAsync(a => a.ObjectId == s.MeetingId)).Details);
    }

    [Fact]
    public async Task Persistence_failure_rolls_back_batch_and_transport_failure_keeps_commit()
    {
        var s = await f.Seed(MeetingStatus.InProgress); var g = await Guest(f, s); using var client = f.Client(s.Secretary);
        f.Failure.MeetingId = s.MeetingId;
        try { await Code(await Save(client, s, Entry(s.MeetingId, s.MemberAssignment), Entry(s.MeetingId, g.Id, "Guest")), 503, "MSG150"); }
        finally { f.Failure.Reset(); }
        await using var db = f.Db();
        Assert.Equal(0, (await db.MeetingParticipants.FindAsync(s.MemberAssignment))!.AttendanceVersion);
        Assert.Equal(0, (await db.MeetingGuestInvitations.FindAsync(g.Id))!.AttendanceVersion);
        Assert.Empty(await db.AuditLog.Where(a => a.ObjectId == s.MeetingId).ToArrayAsync());
        f.FailAttendancePublication = true;
        try { await Code(await Save(client, s, Entry(s.MeetingId, s.MemberAssignment)), 200, "MSG147"); }
        finally { f.FailAttendancePublication = false; }
        db.ChangeTracker.Clear(); Assert.Equal(1, (await db.MeetingParticipants.FindAsync(s.MemberAssignment))!.AttendanceVersion);
    }

    [Fact]
    public async Task Read_failure_query_media_size_and_wrong_identifiers_have_stable_codes()
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var client = f.Client(s.Secretary);
        f.Guests.FailProfiles = true;
        try { await Code(await client.GetAsync(s.Url + "/attendance"), 500, "MSG277"); }
        finally { f.Guests.FailProfiles = false; }
        foreach (var q in new[] { "page=0", "page=1&page=2", "pageSize=101", "q=x", "page=-1", "page=2147483648" })
            await Code(await client.GetAsync(s.Url + "/attendance?" + q), 400, "MSG278");
        await Code(await client.PostAsync(s.Url + "/attendance", new StringContent("{}")), 415, "MSG34");
        await Code(await client.PostAsync(s.Url + "/attendance", new StringContent(new string(' ', 65537), Encoding.UTF8, "application/json")), 413, "MSG87");
        await Code(await Save(client, s, Entry(s.MeetingId, s.Member)), 409, "MSG151");
        var other = await f.Seed(MeetingStatus.InProgress);
        await Code(await Save(client, s, Entry(other.MeetingId, other.MemberAssignment)), 409, "MSG151");
        using var response = await client.GetAsync(s.Url + "/attendance?page=2147483647");
        Assert.True(response.Headers.CacheControl!.NoStore); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
