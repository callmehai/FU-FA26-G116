using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Reports;

/// <summary>SCRUM-38 — GET /api/reports/meetings (UC-70a) and GET /api/reports/meetings/export (UC-70b).</summary>
public sealed class ReportEndpointTests(ReportFixture f) : IClassFixture<ReportFixture>
{
    private const string Report = "/api/reports/meetings";
    private const string Export = "/api/reports/meetings/export";
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    /// <summary>A time in Vietnam, as the UTC value Npgsql writes.</summary>
    private static DateTimeOffset Local(int year, int month, int day, int hour = 9)
        => new DateTimeOffset(year, month, day, hour, 0, 0, Vietnam).ToUniversalTime();

    private static int Count(JsonElement report, string table, string key, string value)
        => report.GetProperty(table).EnumerateArray().Single(e => e.GetProperty(key).GetString() == value).GetProperty("count").GetInt32();

    [Fact]
    public async Task Counts_the_readable_meetings_per_month_status_and_sensitivity_in_vietnam_time()
    {
        var (me, other) = (await f.Users(2)) is [var a, var b] ? (a, b) : default;
        await f.Meeting(me, Local(2026, 1, 10), MeetingStatus.Draft, Sensitivity.Normal);
        await f.Meeting(me, Local(2026, 1, 20), MeetingStatus.Published, Sensitivity.Secret);
        // 31/01 18:00 UTC is 01/02 01:00 in Vietnam: a February meeting (BR-433).
        await f.Meeting(me, new DateTimeOffset(2026, 1, 31, 18, 0, 0, TimeSpan.Zero), MeetingStatus.Cancelled, Sensitivity.Internal);
        await f.Meeting(me, Local(2026, 3, 1, 0), MeetingStatus.Ended, Sensitivity.Secret, Local(2026, 3, 1, 2));
        // Outside the period on either side, by an hour in Vietnam time.
        await f.Meeting(me, new DateTimeOffset(2025, 12, 31, 16, 0, 0, TimeSpan.Zero));
        await f.Meeting(me, new DateTimeOffset(2026, 3, 31, 17, 0, 0, TimeSpan.Zero));
        // Someone else's meeting the caller may not read (BR-432).
        await f.Meeting(other, Local(2026, 2, 5), MeetingStatus.Published, Sensitivity.Secret);

        using var client = f.Client(me);
        var response = await client.GetAsync($"{Report}?from=2026-01&to=2026-03");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("2026-01", report.GetProperty("from").GetString());
        Assert.Equal("2026-03", report.GetProperty("to").GetString());
        Assert.Equal(TimeSpan.FromHours(7), report.GetProperty("generatedAt").GetDateTimeOffset().Offset);
        Assert.Equal(4, report.GetProperty("total").GetInt32());
        Assert.Equal(["2026-01", "2026-02", "2026-03"],
            report.GetProperty("byMonth").EnumerateArray().Select(m => m.GetProperty("month").GetString()!));
        Assert.Equal([2, 1, 1], report.GetProperty("byMonth").EnumerateArray().Select(m => m.GetProperty("count").GetInt32()));
        // Every status and sensitivity, zeros included, in the enum's order (BR-434).
        Assert.Equal(Enum.GetNames<MeetingStatus>(),
            report.GetProperty("byStatus").EnumerateArray().Select(s => s.GetProperty("status").GetString()!));
        Assert.Equal(1, Count(report, "byStatus", "status", "Draft"));
        Assert.Equal(1, Count(report, "byStatus", "status", "Published"));
        Assert.Equal(1, Count(report, "byStatus", "status", "Cancelled"));
        Assert.Equal(1, Count(report, "byStatus", "status", "Ended"));
        Assert.Equal(0, Count(report, "byStatus", "status", "Approved"));
        Assert.Equal(Enum.GetNames<Sensitivity>(),
            report.GetProperty("bySensitivity").EnumerateArray().Select(s => s.GetProperty("sensitivity").GetString()!));
        Assert.Equal(1, Count(report, "bySensitivity", "sensitivity", "Normal"));
        Assert.Equal(1, Count(report, "bySensitivity", "sensitivity", "Internal"));
        Assert.Equal(2, Count(report, "bySensitivity", "sensitivity", "Secret"));
        // BR-437: figures only — no id or title anywhere in the body.
        var raw = report.GetRawText();
        Assert.DoesNotContain("Cuộc họp trong báo cáo", raw);
        Assert.DoesNotContain("\"id\"", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Averages_attendance_and_minutes_turnaround_over_ended_meetings()
    {
        var users = await f.Users(6);
        var (organizer, chair, secretary, member, absent, removed) = (users[0], users[1], users[2], users[3], users[4], users[5]);
        var endedAt = Local(2026, 6, 10, 11);
        var taken = await f.Meeting(organizer, Local(2026, 6, 10), MeetingStatus.Ended, endedAt: endedAt);
        var untaken = await f.Meeting(organizer, Local(2026, 6, 12), MeetingStatus.Ended, endedAt: Local(2026, 6, 12, 11));
        // Not Ended: never in either average.
        await f.Meeting(organizer, Local(2026, 6, 15), MeetingStatus.Published);
        await using (var db = f.Db())
        {
            db.MeetingParticipants.AddRange(
                new MeetingParticipant { MeetingId = taken, UserId = chair, Role = MeetingRole.Chair, Attendance = AttendanceStatus.Present },
                new MeetingParticipant { MeetingId = taken, UserId = secretary, Role = MeetingRole.Secretary, Attendance = AttendanceStatus.Present },
                new MeetingParticipant { MeetingId = taken, UserId = member, Role = MeetingRole.Member },
                new MeetingParticipant { MeetingId = taken, UserId = absent, Role = MeetingRole.Member, Attendance = AttendanceStatus.Absent },
                // Removed before the meeting: not an invitee any more.
                new MeetingParticipant { MeetingId = taken, UserId = removed, Role = MeetingRole.Member, Attendance = AttendanceStatus.Present,
                    AssignmentStatus = AssignmentStatus.Removed, RemovedAt = Local(2026, 6, 1), RemovedById = organizer },
                new MeetingParticipant { MeetingId = untaken, UserId = chair, Role = MeetingRole.Chair });
            db.MeetingGuestInvitations.AddRange(
                new MeetingGuestInvitation { MeetingId = taken, InvitedName = "Khách có mặt", Email = $"{Guid.NewGuid():N}@guest.test",
                    InvitedById = organizer, Attendance = AttendanceStatus.Present, InitialInvitationDeliveryKey = Guid.NewGuid().ToString("N") },
                // Revoked: not counted, present or not.
                new MeetingGuestInvitation { MeetingId = taken, InvitedName = "Khách bị thu hồi", Email = $"{Guid.NewGuid():N}@guest.test",
                    InvitedById = organizer, Attendance = AttendanceStatus.Absent, RevokedAt = Local(2026, 6, 2), RevokedById = organizer,
                    InitialInvitationDeliveryKey = Guid.NewGuid().ToString("N") });
            // The first issue counts; the correction issued later does not (BR-436).
            var first = new MeetingMinutes { MeetingId = taken, RevisionNumber = 1, CreatedById = secretary,
                Status = MinutesStatus.Issued, IssuedAt = endedAt.AddHours(10) };
            db.MeetingMinutes.Add(first);
            db.MeetingMinutes.Add(new MeetingMinutes { MeetingId = taken, RevisionNumber = 2, SupersedesId = first.Id,
                CreatedById = secretary, Status = MinutesStatus.Issued, IssuedAt = endedAt.AddHours(50) });
            // A draft has no issue time: the meeting stays out of the turnaround.
            db.MeetingMinutes.Add(new MeetingMinutes { MeetingId = untaken, RevisionNumber = 1, CreatedById = secretary });
            await db.SaveChangesAsync();
        }

        using var client = f.Client(organizer);
        var report = await client.GetFromJsonAsync<JsonElement>($"{Report}?from=2026-06&to=2026-06");
        Assert.Equal(3, report.GetProperty("total").GetInt32());
        // Present: Chair, Secretary, the guest — of four current participants and one guest (BR-435).
        var attendance = report.GetProperty("attendance");
        Assert.Equal(0.6, attendance.GetProperty("averageRate").GetDouble());
        Assert.Equal(1, attendance.GetProperty("meetings").GetInt32());
        Assert.Equal(1, attendance.GetProperty("meetingsWithoutAttendance").GetInt32());
        var turnaround = report.GetProperty("minutesTurnaround");
        Assert.Equal(10, turnaround.GetProperty("averageHours").GetDouble());
        Assert.Equal(1, turnaround.GetProperty("meetings").GetInt32());
        Assert.Equal(1, turnaround.GetProperty("meetingsWithoutIssuedMinutes").GetInt32());

        // The Chair reads both meetings as a participant (Ended); the removed member reads neither.
        using var asChair = f.Client(chair);
        Assert.Equal(2, (await asChair.GetFromJsonAsync<JsonElement>($"{Report}?from=2026-06&to=2026-06")).GetProperty("total").GetInt32());
        using var asRemoved = f.Client(removed);
        Assert.Equal(0, (await asRemoved.GetFromJsonAsync<JsonElement>($"{Report}?from=2026-06&to=2026-06")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task A_viewer_with_nothing_readable_gets_zeros_and_no_averages()
    {
        // An administrator is counted like anyone else: no meeting of theirs, nothing to count (BR-432).
        var (admin, organizer) = (await f.Users(2)) is [var a, var b] ? (a, b) : default;
        await using (var db = f.Db())
        {
            var user = await db.Users.SingleAsync(u => u.Id == admin);
            user.RoleId = TrustCabinet.Domain.Access.BuiltInRoles.Admin;
            await db.SaveChangesAsync();
        }
        await f.Meeting(organizer, Local(2026, 5, 5), MeetingStatus.Ended, Sensitivity.Secret, Local(2026, 5, 5, 11));

        using var client = f.Client(admin);
        var report = await client.GetFromJsonAsync<JsonElement>($"{Report}?from=2026-05&to=2026-05");
        Assert.Equal(0, report.GetProperty("total").GetInt32());
        Assert.All(report.GetProperty("byStatus").EnumerateArray(), s => Assert.Equal(0, s.GetProperty("count").GetInt32()));
        Assert.Equal(JsonValueKind.Null, report.GetProperty("attendance").GetProperty("averageRate").ValueKind);
        Assert.Equal(0, report.GetProperty("attendance").GetProperty("meetings").GetInt32());
        Assert.Equal(JsonValueKind.Null, report.GetProperty("minutesTurnaround").GetProperty("averageHours").ValueKind);
    }

    [Fact]
    public async Task Without_a_period_the_report_covers_the_twelve_months_ending_this_month()
    {
        var me = (await f.Users(1))[0];
        using var client = f.Client(me);
        // The clock reads 04/11/2026 in Vietnam.
        var report = await client.GetFromJsonAsync<JsonElement>(Report);
        Assert.Equal("2025-12", report.GetProperty("from").GetString());
        Assert.Equal("2026-11", report.GetProperty("to").GetString());
        Assert.Equal(12, report.GetProperty("byMonth").GetArrayLength());

        // 31/12 20:00 UTC is already January in Vietnam.
        var saved = f.Clock.Now;
        try
        {
            f.Clock.Now = new DateTimeOffset(2026, 12, 31, 20, 0, 0, TimeSpan.Zero);
            var january = await client.GetFromJsonAsync<JsonElement>(Report);
            Assert.Equal("2026-02", january.GetProperty("from").GetString());
            Assert.Equal("2027-01", january.GetProperty("to").GetString());
        }
        finally
        {
            f.Clock.Now = saved;
        }
    }

    [Theory]
    [InlineData("from=2026-05&to=2026-04")]
    [InlineData("from=2025-01&to=2027-01")]
    [InlineData("from=2026-01")]
    [InlineData("to=2026-01")]
    [InlineData("from=2026-13&to=2026-12")]
    [InlineData("from=2026-1&to=2026-12")]
    [InlineData("from=abc&to=2026-12")]
    public async Task An_invalid_period_is_refused_by_both_endpoints(string query)
    {
        var me = (await f.Users(1))[0];
        using var client = f.Client(me);
        await AssertCode(await client.GetAsync($"{Report}?{query}"), 400, "MSG463");
        await AssertCode(await client.GetAsync($"{Export}?{query}"), 400, "MSG463");
        await using var db = f.Db();
        Assert.False(await db.AuditLog.AnyAsync(a => a.ActorId == me));
    }

    [Fact]
    public async Task Twenty_four_months_is_the_longest_period()
    {
        var me = (await f.Users(1))[0];
        using var client = f.Client(me);
        var report = await client.GetFromJsonAsync<JsonElement>($"{Report}?from=2025-01&to=2026-12");
        Assert.Equal(24, report.GetProperty("byMonth").GetArrayLength());
    }

    [Fact]
    public async Task Export_returns_the_same_figures_as_an_excel_file_and_records_the_export()
    {
        var me = (await f.Users(1))[0];
        await f.Meeting(me, Local(2026, 2, 3), MeetingStatus.Published, Sensitivity.Secret, title: "Tiêu đề không được lộ");
        await f.Meeting(me, Local(2026, 3, 3), MeetingStatus.Draft);

        using var client = f.Client(me);
        var response = await client.GetAsync($"{Export}?from=2026-02&to=2026-03");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("bao-cao-cuoc-hop_2026-02_2026-03.xlsx", response.Content.Headers.ContentDisposition!.FileName);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());

        using var zip = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
        Assert.NotNull(zip.GetEntry("xl/styles.xml"));
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XDocument sheet;
        using (var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()) sheet = XDocument.Load(stream);
        var rows = sheet.Descendants(ns + "row").Select(r => r.Elements(ns + "c")
            .Select(c => c.Attribute("t")?.Value == "inlineStr" ? c.Descendants(ns + "t").Single().Value : c.Element(ns + "v")!.Value)
            .ToArray()).ToArray();
        Assert.Contains(rows, r => r is ["Khoảng thời gian", "02/2026 – 03/2026"]);
        Assert.Contains(rows, r => r is ["Tổng số cuộc họp", "2"]);
        Assert.Contains(rows, r => r is ["02/2026", "1"]);
        Assert.Contains(rows, r => r is ["03/2026", "1"]);
        Assert.Contains(rows, r => r is ["Đã công bố", "1"]);
        Assert.Contains(rows, r => r is ["Nháp", "1"]);
        Assert.Contains(rows, r => r is ["Mật", "1"]);
        Assert.Contains(rows, r => r is ["Tỷ lệ tham dự trung bình", "Chưa có dữ liệu", "0", "0"]);
        // BR-437: no title in the file.
        Assert.DoesNotContain("Tiêu đề không được lộ", sheet.ToString());

        await using var db = f.Db();
        var entry = await db.AuditLog.SingleAsync(a => a.ActorId == me);
        Assert.Equal(AuditActions.ReportExported, entry.Action);
        Assert.Equal(AuditActions.ReportObjectType, entry.ObjectType);
        Assert.Null(entry.ObjectId);
        using var details = JsonDocument.Parse(entry.Details);
        Assert.Equal("2026-02", details.RootElement.GetProperty("from").GetString());
        Assert.Equal("2026-03", details.RootElement.GetProperty("to").GetString());
    }

    [Fact]
    public async Task Viewing_the_report_records_nothing()
    {
        var me = (await f.Users(1))[0];
        using var client = f.Client(me);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Report)).StatusCode);
        await using var db = f.Db();
        Assert.False(await db.AuditLog.AnyAsync(a => a.ActorId == me));
    }

    [Fact]
    public async Task A_read_failure_answers_msg464_and_msg465()
    {
        var me = (await f.Users(1))[0];
        using var client = f.Client(me);
        f.Failures.FailMeetingsRead = true;
        try
        {
            await AssertCode(await client.GetAsync(Report), 500, "MSG464");
            await AssertCode(await client.GetAsync(Export), 500, "MSG465");
        }
        finally
        {
            f.Failures.FailMeetingsRead = false;
        }
        await using var db = f.Db();
        Assert.False(await db.AuditLog.AnyAsync(a => a.ActorId == me));
    }

    [Fact]
    public async Task No_file_leaves_when_the_export_cannot_be_recorded()
    {
        var me = (await f.Users(1))[0];
        using var client = f.Client(me);
        f.Failures.FailSave = true;
        try
        {
            var response = await client.GetAsync(Export);
            await AssertCode(response, 500, "MSG465");
            Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        }
        finally
        {
            f.Failures.FailSave = false;
        }
    }

    [Fact]
    public async Task Anonymous_callers_and_deactivated_accounts_are_refused()
    {
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Report)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Export)).StatusCode);

        var gone = (await f.Users(1, active: false))[0];
        using var deactivated = f.Client(gone);
        Assert.Equal(HttpStatusCode.Forbidden, (await deactivated.GetAsync(Report)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await deactivated.GetAsync(Export)).StatusCode);
    }
}
