using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Web.Tests.Reports;

/// <summary>Round 1 gap tests for FE-15 (SCRUM-38, UC-70a–UC-70b).</summary>
public sealed class ReportRound1Tests(ReportFixture f) : IClassFixture<ReportFixture>
{
    private const string Report = "/api/reports/meetings";
    private const string Export = "/api/reports/meetings/export";
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    private static DateTimeOffset Local(int year, int month, int day, int hour = 9)
        => new DateTimeOffset(year, month, day, hour, 0, 0, Vietnam).ToUniversalTime();

    private static int Count(JsonElement report, string table, string key, string value)
        => report.GetProperty(table).EnumerateArray().Single(e => e.GetProperty(key).GetString() == value).GetProperty("count").GetInt32();

    [Fact]
    public async Task Report_CancelledMeetingIsCounted()
    {
        var me = (await f.Users(1))[0];
        await f.Meeting(me, Local(2026, 1, 10), MeetingStatus.Cancelled);
        await f.Meeting(me, Local(2026, 1, 12), MeetingStatus.Draft);

        using var client = f.Client(me);
        var response = await client.GetAsync($"{Report}?from=2026-01&to=2026-01");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, report.GetProperty("total").GetInt32());
        Assert.Equal(1, Count(report, "byStatus", "status", "Cancelled"));
        Assert.Equal(1, Count(report, "byStatus", "status", "Draft"));
    }

    [Fact]
    public async Task Report_AdminCountsOnlyReadableMeetings()
    {
        var (admin, organizer) = (await f.Users(2)) is [var a, var b] ? (a, b) : default;
        await using (var db = f.Db())
        {
            var user = await db.Users.SingleAsync(u => u.Id == admin);
            user.RoleId = BuiltInRoles.Admin;
            await db.SaveChangesAsync();
        }

        await f.Meeting(organizer, Local(2026, 2, 5), MeetingStatus.Draft);
        await f.Meeting(organizer, Local(2026, 2, 6), MeetingStatus.Published, Sensitivity.Secret);

        using var asAdmin = f.Client(admin);
        var adminReport = await asAdmin.GetFromJsonAsync<JsonElement>($"{Report}?from=2026-01&to=2026-12");
        Assert.Equal(0, adminReport.GetProperty("total").GetInt32());

        using var asOrganizer = f.Client(organizer);
        var organizerReport = await asOrganizer.GetFromJsonAsync<JsonElement>($"{Report}?from=2026-01&to=2026-12");
        Assert.Equal(2, organizerReport.GetProperty("total").GetInt32());
        Assert.Equal(1, Count(organizerReport, "byStatus", "status", "Draft"));
    }

    [Fact]
    public async Task Export_WithoutPeriodExportsTwelveMonthsEndingThisMonth()
    {
        var me = (await f.Users(1))[0];
        using var client = f.Client(me);

        var response = await client.GetAsync(Export);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("bao-cao-cuoc-hop_2025-12_2026-11.xlsx", response.Content.Headers.ContentDisposition!.FileName);
    }
}
