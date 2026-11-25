using System.Net;
using System.Net.Http.Json;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Entities;
using static TrustCabinet.Web.Tests.CalendarSharing.CalendarSharingFixture;

namespace TrustCabinet.Web.Tests.CalendarSharing;

/// <summary>
/// Round 1 gap coverage for SCRUM-137 (UC-85a, UC-85b): the Manual-only slot cases of
/// docs/08-reports/test-reports/cases/FE-16/api.json — the 10-person boundary, duration and
/// date-range limits, the one-working-session rule and the draft 401.
/// </summary>
public sealed class MeetingSlotRound1Tests(CalendarSharingFixture f) : IClassFixture<CalendarSharingFixture>
{
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private static object Search(Guid[] userIds, int duration = 60, DateOnly? from = null, DateOnly? to = null,
        string workStart = "08:00", string workEnd = "12:00", bool lunchBreak = false, int[]? weekdays = null)
        => new
        {
            userIds, includeMe = false, durationMinutes = duration, fromDate = Day(from ?? Monday),
            toDate = Day(to ?? from ?? Monday), workStart, workEnd, lunchBreak, bufferMinutes = 0, stepMinutes = 60,
            weekdays = weekdays ?? [1, 2, 3, 4, 5],
        };

    private async Task<Guid[]> TenActiveOthers(People p)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        await using var db = f.Db();
        var extra = Enumerable.Range(0, 8).Select(i => new User
        {
            FullName = $"Người Dùng {tag} {i}", Email = $"slot.{tag}.{i}@calendar.test",
            PasswordHash = "test-only-non-login-hash", RoleId = BuiltInRoles.Member,
        }).ToArray();
        db.Users.AddRange(extra);
        await db.SaveChangesAsync();
        return [p.Organizer2, p.Owner, p.Owner2, .. extra.Select(u => u.Id)];
    }

    [Fact]
    public async Task Ten_people_are_accepted_but_eleven_are_not()
    {
        var p = await f.Seed();
        var others = await TenActiveOthers(p);
        using var organizer = f.Client(p.Organizer);

        var ok = await organizer.PostAsJsonAsync("/api/calendar/slots", Search(others[..10]));
        Assert.True(ok.IsSuccessStatusCode, await ok.Content.ReadAsStringAsync());

        var body = await AssertCode(
            await organizer.PostAsJsonAsync("/api/calendar/slots", Search(others)), 400, "MSG444");
        Assert.True(body.GetProperty("errors").TryGetProperty("userIds", out _));
    }

    [Fact]
    public async Task Duration_and_date_range_limits_are_enforced()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        var invitees = new[] { p.Owner };

        // A full 8-hour session fits one working day without lunch; Saturday and Sunday may be
        // searched too (20/11/2026: the shared week is Monday to Sunday, so the search reaches it).
        var saturday = new DateOnly(2026, 11, 14);
        var fitting = await organizer.PostAsJsonAsync("/api/calendar/slots",
            Search(invitees, duration: 480, from: saturday, to: saturday.AddDays(1),
                workStart: "08:00", workEnd: "17:00", lunchBreak: false, weekdays: [6, 7]));
        Assert.True(fitting.IsSuccessStatusCode, await fitting.Content.ReadAsStringAsync());

        foreach (var duration in new[] { 485, 10 })
        {
            var body = await AssertCode(
                await organizer.PostAsJsonAsync("/api/calendar/slots", Search(invitees, duration: duration)),
                400, "MSG444");
            Assert.True(body.GetProperty("errors").TryGetProperty("durationMinutes", out _));
        }

        var range = await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots",
            Search(invitees, from: Monday, to: Monday.AddDays(31))), 400, "MSG444");
        Assert.True(range.GetProperty("errors").TryGetProperty("toDate", out _));

        var ahead = await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots",
            Search(invitees, from: Today.AddDays(366))), 400, "MSG444");
        Assert.True(ahead.GetProperty("errors").TryGetProperty("fromDate", out _));
    }

    [Fact]
    public async Task A_duration_longer_than_one_working_session_is_refused()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        var body = await AssertCode(
            await organizer.PostAsJsonAsync("/api/calendar/slots", Search([p.Owner], duration: 300)),
            400, "MSG444");
        Assert.Equal(MeetingSlotRules.DurationTooLongError,
            body.GetProperty("errors").GetProperty("durationMinutes").GetString());
    }

    [Fact]
    public async Task Without_a_token_a_draft_from_a_slot_is_refused()
    {
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/calendar/slots/draft", new { })).StatusCode);
    }
}
