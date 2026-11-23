using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Reports;

/// <summary>
/// The report query (supervisor review 22/11/2026): GET /api/reports/query and GET /api/reports/query/export, and the
/// manager figures GET /api/reports/meetings now carries. Same gate as the meeting report: any active internal user,
/// counting only what they may see.
/// </summary>
public sealed class ReportQueryEndpointTests(ReportFixture f) : IClassFixture<ReportFixture>
{
    private const string Query = "/api/reports/query";
    private const string Export = "/api/reports/query/export";
    private const string Report = "/api/reports/meetings";
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    private static DateTimeOffset Local(int year, int month, int day, int hour = 9)
        => new DateTimeOffset(year, month, day, hour, 0, 0, Vietnam).ToUniversalTime();

    private static JsonElement[] Rows(JsonElement table) => table.GetProperty("rows").EnumerateArray().ToArray();

    private static double? Value(JsonElement row, string key)
        => row.GetProperty("values").GetProperty(key) is { ValueKind: JsonValueKind.Number } v ? v.GetDouble() : null;

    private async Task<Guid> Participant(Guid meeting, Guid user, MeetingRole role, AttendanceStatus attendance = AttendanceStatus.Unknown)
    {
        await using var db = f.Db();
        var row = new MeetingParticipant { MeetingId = meeting, UserId = user, Role = role, Attendance = attendance };
        db.MeetingParticipants.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    [Fact]
    public async Task Groups_only_the_meetings_the_caller_may_read_and_names_them_when_grouped_by_meeting()
    {
        var (me, chair, other) = (await f.Users(3)) is [var a, var b, var c] ? (a, b, c) : default;
        var held = await f.Meeting(me, Local(2026, 4, 2), MeetingStatus.Ended, endedAt: Local(2026, 4, 2, 11), title: "Họp được xem 1");
        var second = await f.Meeting(me, Local(2026, 4, 9), MeetingStatus.Ended, endedAt: Local(2026, 4, 9, 10), title: "Họp được xem 2");
        await f.Meeting(me, Local(2026, 4, 12), MeetingStatus.Cancelled, title: "Họp đã huỷ");
        await f.Meeting(other, Local(2026, 4, 5), MeetingStatus.Published, title: "Họp người khác");
        await Participant(held, chair, MeetingRole.Chair);
        await Participant(second, chair, MeetingRole.Chair);

        using var client = f.Client(me);
        var response = await client.GetAsync($"{Query}?entity=meetings&from=2026-04&to=2026-04&groupBy=chair");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var table = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, table.GetProperty("total").GetProperty("count").GetDouble());
        var rows = Rows(table);
        Assert.Equal(chair.ToString(), rows[0].GetProperty("key").GetString());
        Assert.Equal((2d, 2d, 3d), (Value(rows[0], "count")!.Value, Value(rows[0], "ended")!.Value, Value(rows[0], "hours")!.Value));
        Assert.Equal(("None", "Chưa có Chủ toạ"), (rows[1].GetProperty("key").GetString(), rows[1].GetProperty("label").GetString()));

        var byMeeting = await client.GetFromJsonAsync<JsonElement>(
            $"{Query}?entity=meetings&from=2026-04&to=2026-04&groupBy=meeting&sort=hours&top=1");
        var top = Assert.Single(Rows(byMeeting));
        Assert.Equal((held.ToString(), "Họp được xem 1"), (top.GetProperty("key").GetString(), top.GetProperty("label").GetString()));
        Assert.Equal(3, byMeeting.GetProperty("groups").GetInt32());
        Assert.DoesNotContain("Họp người khác", byMeeting.GetRawText());
    }

    [Fact]
    public async Task A_member_sees_no_draft_vote_unissued_minutes_and_an_approver_off_the_roster_no_recording()
    {
        var (organizer, secretary, member, approver) = (await f.Users(4)) is [var a, var b, var c, var d] ? (a, b, c, d) : default;
        var meeting = await f.Meeting(organizer, Local(2026, 5, 6), MeetingStatus.Ended, endedAt: Local(2026, 5, 6, 11));
        await Participant(meeting, secretary, MeetingRole.Secretary, AttendanceStatus.Present);
        await Participant(meeting, member, MeetingRole.Member, AttendanceStatus.Absent);
        await using (var db = f.Db())
        {
            var m = await db.Meetings.SingleAsync(x => x.Id == meeting);
            m.AssignedApproverId = approver;
            db.VotingSessions.AddRange(
                new VotingSession { MeetingId = meeting, Title = "Nháp", CreatedById = secretary, Status = VotingStatus.Draft },
                new VotingSession { MeetingId = meeting, Title = "Đã đóng", CreatedById = secretary, Status = VotingStatus.Closed,
                    Conclusion = VoteConclusion.Passed, EligibleCount = 2 });
            db.MeetingMinutes.AddRange(
                new MeetingMinutes { MeetingId = meeting, RevisionNumber = 1, CreatedById = secretary, Status = MinutesStatus.WaitingApproval });
            db.Recordings.Add(new Recording { MeetingId = meeting, Source = RecordingSource.Upload, Status = RecordingStatus.Available,
                FileName = "a.webm", StoragePath = $"test/{Guid.NewGuid()}", ContentType = "audio/webm", DurationMs = 3_600_000,
                CreatedById = secretary });
            await db.SaveChangesAsync();
        }

        const string period = "from=2026-05&to=2026-05";
        using var asSecretary = f.Client(secretary);
        using var asMember = f.Client(member);
        using var asApprover = f.Client(approver);
        double Total(JsonElement t, string key) => t.GetProperty("total").GetProperty(key).GetDouble();

        Assert.Equal(2, Total(await asSecretary.GetFromJsonAsync<JsonElement>($"{Query}?entity=votes&{period}"), "count"));
        var memberVotes = await asMember.GetFromJsonAsync<JsonElement>($"{Query}?entity=votes&{period}");
        Assert.Equal((1d, 1d), (Total(memberVotes, "count"), Total(memberVotes, "passed")));
        Assert.Equal(1, Total(await asSecretary.GetFromJsonAsync<JsonElement>($"{Query}?entity=minutes&{period}"), "waiting"));
        Assert.Equal(0, Total(await asMember.GetFromJsonAsync<JsonElement>($"{Query}?entity=minutes&{period}"), "count"));
        Assert.Equal(1, Total(await asMember.GetFromJsonAsync<JsonElement>($"{Query}?entity=recordings&{period}"), "hours"));
        Assert.Equal(0, Total(await asApprover.GetFromJsonAsync<JsonElement>($"{Query}?entity=recordings&{period}"), "count"));

        var absent = await asMember.GetFromJsonAsync<JsonElement>(
            $"{Query}?entity=participants&{period}&groupBy=person&sort=absent&top=1&attendance=Absent");
        var row = Assert.Single(Rows(absent));
        Assert.Equal(member.ToString(), row.GetProperty("key").GetString());
        Assert.Equal("attendance", absent.GetProperty("filters")[0].GetProperty("key").GetString());

        // The manager figures on the meeting report follow the same rules.
        var report = await asMember.GetFromJsonAsync<JsonElement>($"{Report}?{period}");
        Assert.Equal(1, report.GetProperty("votes").GetProperty("closed").GetInt32());
        Assert.Equal(1, report.GetProperty("votes").GetProperty("passed").GetInt32());
        Assert.Equal(0, report.GetProperty("minutes").GetProperty("waitingApproval").GetInt32());
        Assert.Equal(1, report.GetProperty("recordings").GetProperty("recordedHours").GetDouble());
        var secretaryReport = await asSecretary.GetFromJsonAsync<JsonElement>($"{Report}?{period}");
        Assert.Equal(1, secretaryReport.GetProperty("minutes").GetProperty("waitingApproval").GetInt32());
    }

    [Theory]
    [InlineData("entity=tasks")]
    [InlineData("entity=meetings&groupBy=person")]
    [InlineData("entity=meetings&sort=turnout")]
    [InlineData("entity=meetings&top=0")]
    [InlineData("entity=votes&attendance=Present")]
    [InlineData("entity=votes&conclusion=Maybe")]
    public async Task A_parameter_the_table_does_not_have_is_refused_with_msg484(string query)
    {
        var me = (await f.Users(1))[0];
        using var client = f.Client(me);
        await AssertCode(await client.GetAsync($"{Query}?{query}"), 400, "MSG484");
        await AssertCode(await client.GetAsync($"{Export}?{query}"), 400, "MSG484");
        await using var db = f.Db();
        Assert.False(await db.AuditLog.AnyAsync(a => a.ActorId == me));
    }

    [Fact]
    public async Task An_invalid_period_is_msg463_before_any_parameter()
    {
        var me = (await f.Users(1))[0];
        using var client = f.Client(me);
        await AssertCode(await client.GetAsync($"{Query}?entity=tasks&from=2026-05&to=2026-01"), 400, "MSG463");
        await AssertCode(await client.GetAsync($"{Export}?from=2026-05"), 400, "MSG463");
    }

    [Fact]
    public async Task Export_writes_the_table_with_what_was_asked_and_records_it()
    {
        var me = (await f.Users(1))[0];
        await f.Meeting(me, Local(2026, 7, 1), MeetingStatus.Ended, Sensitivity.Secret, endedAt: Local(2026, 7, 1, 10));
        await f.Meeting(me, Local(2026, 8, 1), MeetingStatus.Cancelled, Sensitivity.Secret);
        await f.Meeting(me, Local(2026, 8, 2), MeetingStatus.Draft);

        using var client = f.Client(me);
        var response = await client.GetAsync($"{Export}?entity=meetings&from=2026-07&to=2026-08&groupBy=month&sensitivity=Secret");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("bao-cao-cuoc-hop_2026-07_2026-08.xlsx", response.Content.Headers.ContentDisposition!.FileName);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());

        using var zip = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XDocument sheet;
        using (var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()) sheet = XDocument.Load(stream);
        var rows = sheet.Descendants(ns + "row").Select(r => r.Elements(ns + "c")
            .Select(c => c.Attribute("t")?.Value == "inlineStr" ? c.Descendants(ns + "t").Single().Value : c.Element(ns + "v")?.Value ?? "")
            .ToArray()).ToArray();
        Assert.Contains(rows, r => r is ["Nhóm theo", "Tháng"]);
        Assert.Contains(rows, r => r is ["Bộ lọc", "Mức độ: Mật"]);
        Assert.Contains(rows, r => r.Length > 3 && r[0] == "#" && r[1] == "Tháng" && r[2] == "Số cuộc họp");
        Assert.Contains(rows, r => r.Length > 4 && r[0] == "1" && r[1] == "07/2026" && r[2] == "1" && r[3] == "1");
        Assert.Contains(rows, r => r.Length > 4 && r[0] == "2" && r[1] == "08/2026" && r[2] == "1" && r[4] == "1");
        Assert.Contains(rows, r => r.Length > 2 && r[0] == "Tổng" && r[1] == "2");

        await using var db = f.Db();
        var entry = await db.AuditLog.SingleAsync(a => a.ActorId == me);
        Assert.Equal(AuditActions.ReportExported, entry.Action);
        using var details = JsonDocument.Parse(entry.Details);
        Assert.Equal("meetings", details.RootElement.GetProperty("report").GetString());
        Assert.Equal("month", details.RootElement.GetProperty("groupBy").GetString());
        Assert.Equal("sensitivity=Secret", details.RootElement.GetProperty("filters").GetString());
    }

    [Fact]
    public async Task Viewing_records_nothing_and_failures_answer_msg464_and_msg465()
    {
        var me = (await f.Users(1))[0];
        using var client = f.Client(me);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Query}?entity=documents")).StatusCode);
        await using (var db = f.Db()) Assert.False(await db.AuditLog.AnyAsync(a => a.ActorId == me));

        f.Failures.FailMeetingsRead = true;
        try
        {
            await AssertCode(await client.GetAsync($"{Query}?entity=votes"), 500, "MSG464");
            await AssertCode(await client.GetAsync($"{Export}?entity=votes"), 500, "MSG465");
        }
        finally
        {
            f.Failures.FailMeetingsRead = false;
        }

        f.Failures.FailSave = true;
        try
        {
            await AssertCode(await client.GetAsync(Export), 500, "MSG465");
        }
        finally
        {
            f.Failures.FailSave = false;
        }
    }

    [Fact]
    public async Task Anonymous_callers_guests_and_deactivated_accounts_are_refused()
    {
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Query)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Export)).StatusCode);
        using var guest = f.GuestClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync(Query)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync(Export)).StatusCode);
        var gone = (await f.Users(1, active: false))[0];
        using var deactivated = f.Client(gone);
        Assert.Equal(HttpStatusCode.Forbidden, (await deactivated.GetAsync(Query)).StatusCode);
    }
}
