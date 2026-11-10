using System.Net.Http.Json;
using System.Text.Json;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.CalendarSharing.CalendarSharingFixture;

namespace TrustCabinet.Web.Tests.CalendarSharing;

/// <summary>
/// UC-85c — GET /api/calendar/shared-week: only busy blocks at the shared level (BR-414, BR-417), only meetings and
/// assignments that count (BR-419, BR-420), unknown days outside the range (BR-415), stopped people listed without hours
/// (BR-424), shares past their end date left out (BR-425), and nothing about any meeting (BR-427).
/// </summary>
public sealed class SharedWeekEndpointTests(CalendarSharingFixture f) : IClassFixture<CalendarSharingFixture>
{
    private const string SecretTitle = "Họp kín về nhân sự";
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private static DateTimeOffset At(DateOnly day, int hour, int minute = 0) => VietnamTime.At(day, new TimeOnly(hour, minute));

    private async Task Meeting(Guid creator, Guid person, DateOnly day, (int H, int M) from, (int H, int M) to,
        MeetingStatus status = MeetingStatus.Published, Sensitivity sensitivity = Sensitivity.Normal,
        MeetingRole role = MeetingRole.Member, InvitationResponse response = InvitationResponse.Pending,
        string title = "Họp giao ban")
    {
        await using var db = f.Db();
        var meeting = new Meeting
        {
            Title = title, Location = "Phòng 301", CreatedById = creator, Status = status, Sensitivity = sensitivity,
            Mode = MeetingMode.InPerson, PlannedStartTime = At(day, from.H, from.M).ToUniversalTime(),
            PlannedEndTime = At(day, to.H, to.M).ToUniversalTime(),
        };
        db.Meetings.Add(meeting);
        db.MeetingParticipants.Add(new MeetingParticipant
        {
            MeetingId = meeting.Id, UserId = person, Role = role, Response = response,
        });
        await db.SaveChangesAsync();
    }

    private static (DateTimeOffset Start, DateTimeOffset End)[] Busy(JsonElement day)
        => [.. day.GetProperty("busy").EnumerateArray()
            .Select(b => (b.GetProperty("start").GetDateTimeOffset(), b.GetProperty("end").GetDateTimeOffset()))];

    [Fact]
    public async Task The_week_shows_only_what_each_share_allows()
    {
        var p = await f.Seed();
        await f.AddShare(p.Owner, p.Organizer, level: CalendarShareLevel.Level1, start: Monday, end: Monday.AddDays(3));
        await f.AddShare(p.Owner2, p.Organizer, level: CalendarShareLevel.Level2, start: Today, end: Today.AddDays(30));
        await f.AddShare(p.Locked, p.Organizer, CalendarShareStatus.Stopped);
        // Someone the caller does not view: never listed.
        await f.AddShare(p.Owner, p.Organizer2, level: CalendarShareLevel.Level2, start: Monday, end: Monday.AddDays(4));

        var creator = p.Admin;
        await Meeting(creator, p.Owner, Monday, (9, 0), (10, 0));
        await Meeting(creator, p.Owner, Monday, (9, 30), (11, 0), role: MeetingRole.Chair, response: InvitationResponse.Declined);
        await Meeting(creator, p.Owner, Monday, (7, 0), (8, 30));
        await Meeting(creator, p.Owner, Monday, (13, 0), (14, 0), MeetingStatus.Approved, Sensitivity.Internal);
        await Meeting(creator, p.Owner, Monday, (14, 0), (15, 0), response: InvitationResponse.Declined);
        await Meeting(creator, p.Owner, Monday, (15, 0), (16, 0), sensitivity: Sensitivity.Secret, title: SecretTitle);
        await Meeting(creator, p.Owner, Monday, (16, 0), (16, 30), MeetingStatus.Draft);
        await Meeting(creator, p.Owner, Monday.AddDays(4), (9, 0), (10, 0)); // Friday: outside the share
        await Meeting(creator, p.Owner2, Monday.AddDays(1), (10, 0), (11, 0), MeetingStatus.WaitingApproval, Sensitivity.Internal);
        await Meeting(creator, p.Owner2, Monday.AddDays(1), (13, 0), (14, 0), sensitivity: Sensitivity.Secret, title: SecretTitle);
        await Meeting(creator, p.Owner2, Monday.AddDays(1), (15, 0), (16, 0), MeetingStatus.Cancelled);

        using var organizer = f.Client(p.Organizer);
        var response = await organizer.GetAsync($"/api/calendar/shared-week?week={Day(Monday.AddDays(2))}");
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, raw);
        // BR-427: no title, place or attendee of any meeting, ever.
        Assert.DoesNotContain(SecretTitle, raw);
        Assert.DoesNotContain("Họp giao ban", raw);
        Assert.DoesNotContain("Phòng 301", raw);
        Assert.DoesNotContain("sensitivity", raw, StringComparison.OrdinalIgnoreCase);

        var week = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(Day(Monday), week.GetProperty("weekStart").GetString());
        Assert.Equal(Day(Monday.AddDays(4)), week.GetProperty("weekEnd").GetString());
        var people = week.GetProperty("people").EnumerateArray()
            .ToDictionary(x => x.GetProperty("owner").GetProperty("id").GetGuid());
        Assert.Equal(3, people.Count);

        // Owner, Level 1: Normal only, merged, clipped to 08:00, declined/Draft/Internal/Secret left out.
        var owner = people[p.Owner];
        Assert.Equal("Effective", owner.GetProperty("status").GetString());
        var days = owner.GetProperty("days").EnumerateArray().ToList();
        Assert.Equal(5, days.Count);
        Assert.Equal([(At(Monday, 8), At(Monday, 8, 30)), (At(Monday, 9), At(Monday, 11))], Busy(days[0]));
        Assert.Equal(TimeSpan.FromHours(7), Busy(days[0])[0].Start.Offset);
        Assert.True(days[1].GetProperty("known").GetBoolean());
        Assert.Empty(Busy(days[1]));
        // Friday is outside the share: unknown, not free.
        Assert.False(days[4].GetProperty("known").GetBoolean());
        Assert.Empty(Busy(days[4]));

        // Owner2, Level 2: the Internal meeting shows; Secret and Cancelled never do.
        var owner2Days = people[p.Owner2].GetProperty("days").EnumerateArray().ToList();
        Assert.Equal([(At(Monday.AddDays(1), 10), At(Monday.AddDays(1), 11))], Busy(owner2Days[1]));

        // Stopped: listed with its state, no hours.
        var locked = people[p.Locked];
        Assert.Equal("Stopped", locked.GetProperty("status").GetString());
        Assert.Equal(0, locked.GetProperty("days").GetArrayLength());
    }

    [Fact]
    public async Task A_share_past_its_end_date_is_left_out()
    {
        var p = await f.Seed();
        await f.AddShare(p.Owner, p.Organizer, start: Today.AddDays(-10), end: Today.AddDays(-1));
        await Meeting(p.Admin, p.Owner, Monday, (9, 0), (10, 0));
        using var organizer = f.Client(p.Organizer);
        var week = await organizer.GetFromJsonAsync<JsonElement>($"/api/calendar/shared-week?week={Day(Monday)}");
        // PO 09/11/2026, Q-B: no Expired state to show — the person is simply not there.
        Assert.Equal(0, week.GetProperty("people").GetArrayLength());
    }

    [Fact]
    public async Task Nobody_shared_yet_is_an_empty_week_and_only_organizers_may_look()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        var week = await organizer.GetFromJsonAsync<JsonElement>("/api/calendar/shared-week");
        Assert.Equal(0, week.GetProperty("people").GetArrayLength());
        // Sunday 08/11 belongs to the week that began on Monday 02/11.
        Assert.Equal("2026-11-02", week.GetProperty("weekStart").GetString());

        using var member = f.Client(p.Owner);
        await AssertCode(await member.GetAsync("/api/calendar/shared-week"), 403, "MSG448");

        f.Failures.FailShareRead = true;
        try
        {
            await AssertCode(await organizer.GetAsync("/api/calendar/shared-week"), 500, "MSG453");
        }
        finally
        {
            f.Failures.FailShareRead = false;
        }
    }
}
