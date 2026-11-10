using System.Net.Http.Json;
using System.Text.Json;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.CalendarSharing.CalendarSharingFixture;

namespace TrustCabinet.Web.Tests.CalendarSharing;

/// <summary>
/// UC-85a — POST /api/calendar/slots against a real PostgreSQL: only calendars shared with the finder count, each at its
/// level (BR-414, BR-417); the finder's own calendar only when ticked and then at every level (BR-416, BR-418); only
/// meetings and assignments that count (BR-419, BR-420); unknown days and people without a share are never free (BR-415,
/// BR-416); the most people free when nothing fits everyone (AF-1, MSG445); nothing about any meeting (BR-427).
/// </summary>
public sealed class MeetingSlotEndpointTests(CalendarSharingFixture f) : IClassFixture<CalendarSharingFixture>
{
    private const string Title = "Họp kín về nhân sự";
    private const string Place = "Phòng 301";
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private static DateTimeOffset At(DateOnly day, int hour, int minute = 0) => VietnamTime.At(day, new TimeOnly(hour, minute));

    private async Task Meeting(Guid creator, Guid person, DateOnly day, (int H, int M) from, (int H, int M) to,
        MeetingStatus status = MeetingStatus.Published, Sensitivity sensitivity = Sensitivity.Normal,
        MeetingRole role = MeetingRole.Member, InvitationResponse response = InvitationResponse.Pending)
    {
        await using var db = f.Db();
        var meeting = new Meeting
        {
            Title = Title, Location = Place, CreatedById = creator, Status = status, Sensitivity = sensitivity,
            Mode = MeetingMode.InPerson, PlannedStartTime = At(day, from.H, from.M).ToUniversalTime(),
            PlannedEndTime = At(day, to.H, to.M).ToUniversalTime(),
        };
        db.Meetings.Add(meeting);
        db.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = person, Role = role, Response = response });
        await db.SaveChangesAsync();
    }

    /// <summary>One Monday, 08:00–12:00, one-hour slots on the hour, no buffer: four candidates, easy to reason about.</summary>
    private static object Search(Guid[] userIds, bool includeMe = false, DateOnly? from = null, DateOnly? to = null,
        int buffer = 0)
        => new
        {
            userIds, includeMe, durationMinutes = 60, fromDate = Day(from ?? Monday), toDate = Day(to ?? from ?? Monday),
            workStart = "08:00", workEnd = "12:00", lunchBreak = false, bufferMinutes = buffer, stepMinutes = 60,
        };

    private static async Task<JsonElement> Ok(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, raw);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        // BR-427: no title, place or level of any meeting, ever.
        Assert.DoesNotContain(Title, raw);
        Assert.DoesNotContain(Place, raw);
        Assert.DoesNotContain("sensitivity", raw, StringComparison.OrdinalIgnoreCase);
        return JsonDocument.Parse(raw).RootElement;
    }

    private static List<JsonElement> Slots(JsonElement result)
        => [.. result.GetProperty("days").EnumerateArray().SelectMany(d => d.GetProperty("slots").EnumerateArray())];

    private static int[] StartHours(JsonElement result)
        => [.. Slots(result).Select(s => VietnamTime.ToLocal(s.GetProperty("start").GetDateTimeOffset()).Hour)];

    private static Guid[] Ids(JsonElement slot, string property)
        => [.. slot.GetProperty(property).EnumerateArray().Select(x => x.GetGuid())];

    [Fact]
    public async Task Without_a_token_the_search_is_refused()
    {
        using var anonymous = f.Client();
        var response = await anonymous.PostAsJsonAsync("/api/calendar/slots", Search([Guid.NewGuid()]));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Only_a_Meeting_Organizer_may_search()
    {
        var p = await f.Seed();
        using var member = f.Client(p.Owner);
        // MSG448 comes before any parameter check (sequence UC-85a), so even an empty body gets it.
        await AssertCode(await member.PostAsJsonAsync("/api/calendar/slots", new { }), 403, "MSG448");
    }

    [Fact]
    public async Task Broken_parameters_are_reported_by_field()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        var body = await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots",
            new { userIds = new[] { p.Owner }, durationMinutes = 7, stepMinutes = 1, weekdays = new[] { 6 } }), 400, "MSG444");
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("durationMinutes", out _));
        Assert.True(errors.TryGetProperty("stepMinutes", out _));
        Assert.True(errors.TryGetProperty("weekdays", out _));

        // A deactivated or unknown invitee is pointed at by position.
        body = await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots",
            Search([p.Owner, p.Locked, Guid.NewGuid()])), 400, "MSG444");
        errors = body.GetProperty("errors");
        Assert.False(errors.TryGetProperty("userIds[0]", out _));
        Assert.Equal(MeetingSlotRules.UserUnavailableError, errors.GetProperty("userIds[1]").GetString());
        Assert.Equal(MeetingSlotRules.UserUnavailableError, errors.GetProperty("userIds[2]").GetString());

        await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots", Search([p.Organizer])), 400, "MSG444");
    }

    [Fact]
    public async Task Common_slots_respect_shared_levels_statuses_and_roles()
    {
        var p = await f.Seed();
        await f.AddShare(p.Owner, p.Organizer, level: CalendarShareLevel.Level1);
        await f.AddShare(p.Owner2, p.Organizer, level: CalendarShareLevel.Level2);
        var creator = p.Admin;
        // Owner (Level 1): only the Normal 08:00 meeting counts.
        await Meeting(creator, p.Owner, Monday, (8, 0), (9, 0));
        await Meeting(creator, p.Owner, Monday, (9, 0), (10, 0), sensitivity: Sensitivity.Internal);
        await Meeting(creator, p.Owner, Monday, (10, 0), (11, 0), sensitivity: Sensitivity.Secret);
        await Meeting(creator, p.Owner, Monday, (11, 0), (12, 0), MeetingStatus.Draft);
        // Owner2 (Level 2): the Internal 09:00 meeting counts; declined, cancelled and Secret never do.
        await Meeting(creator, p.Owner2, Monday, (9, 0), (10, 0), MeetingStatus.WaitingApproval, Sensitivity.Internal);
        await Meeting(creator, p.Owner2, Monday, (10, 0), (11, 0), response: InvitationResponse.Declined);
        await Meeting(creator, p.Owner2, Monday, (11, 0), (12, 0), MeetingStatus.Cancelled);
        await Meeting(creator, p.Owner2, Monday, (11, 0), (12, 0), sensitivity: Sensitivity.Secret);

        using var organizer = f.Client(p.Organizer);
        var result = await Ok(await organizer.PostAsJsonAsync("/api/calendar/slots", Search([p.Owner, p.Owner2])));
        Assert.True(result.GetProperty("allFree").GetBoolean());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("code").ValueKind);
        Assert.False(result.GetProperty("truncated").GetBoolean());
        // 10:00 and 11:00 overlap only Secret, Draft, declined or cancelled meetings — free as far as the finder may know.
        Assert.Equal([10, 11], StartHours(result));
        Assert.All(Slots(result), s => Assert.Equal(2, s.GetProperty("freeCount").GetInt32()));

        var people = result.GetProperty("people").EnumerateArray().ToList();
        Assert.Equal([p.Owner, p.Owner2], people.Select(x => x.GetProperty("id").GetGuid()));
        Assert.Equal(["Shared", "Shared"], people.Select(x => x.GetProperty("status").GetString()));
        Assert.Equal(["Level1", "Level2"], people.Select(x => x.GetProperty("level").GetString()));
    }

    [Fact]
    public async Task The_finders_own_calendar_counts_only_when_ticked_and_at_every_level()
    {
        var p = await f.Seed();
        await f.AddShare(p.Owner, p.Organizer);
        await Meeting(p.Admin, p.Organizer, Monday, (8, 0), (9, 0), sensitivity: Sensitivity.Secret, role: MeetingRole.Secretary);

        using var organizer = f.Client(p.Organizer);
        var without = await Ok(await organizer.PostAsJsonAsync("/api/calendar/slots", Search([p.Owner])));
        Assert.Equal([8, 9, 10, 11], StartHours(without));
        Assert.DoesNotContain(without.GetProperty("people").EnumerateArray(), x => x.GetProperty("id").GetGuid() == p.Organizer);

        var with = await Ok(await organizer.PostAsJsonAsync("/api/calendar/slots", Search([p.Owner], includeMe: true)));
        Assert.Equal([9, 10, 11], StartHours(with));
        var me = with.GetProperty("people")[0];
        Assert.Equal(p.Organizer, me.GetProperty("id").GetGuid());
        Assert.Equal("Own", me.GetProperty("status").GetString());
    }

    [Fact]
    public async Task People_without_a_live_share_are_not_shared_and_never_free()
    {
        var p = await f.Seed();
        await f.AddShare(p.Owner, p.Organizer);
        // Owner2's shares are stopped, pending, past their end date, or to someone else: none counts.
        await f.AddShare(p.Owner2, p.Organizer, CalendarShareStatus.Stopped);
        await f.AddShare(p.Owner2, p.Organizer, CalendarShareStatus.Effective, start: Today.AddDays(-20), end: Today.AddDays(-1));
        await f.AddShare(p.Owner2, p.Organizer2);
        await Meeting(p.Admin, p.Owner2, Monday, (8, 0), (12, 0));

        using var organizer = f.Client(p.Organizer);
        var result = await Ok(await organizer.PostAsJsonAsync("/api/calendar/slots", Search([p.Owner, p.Owner2])));
        var owner2 = result.GetProperty("people")[1];
        Assert.Equal("NotShared", owner2.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, owner2.GetProperty("level").ValueKind);
        // Owner2's busy morning does not show: they are simply not counted (BR-416).
        Assert.True(result.GetProperty("allFree").GetBoolean());
        Assert.All(Slots(result), s => Assert.Equal(1, s.GetProperty("countedCount").GetInt32()));

        // Nobody counted at all: nothing to propose, and never "everyone is free".
        var nobody = await Ok(await organizer.PostAsJsonAsync("/api/calendar/slots", Search([p.Owner2])));
        Assert.False(nobody.GetProperty("allFree").GetBoolean());
        Assert.Equal(JsonValueKind.Null, nobody.GetProperty("code").ValueKind);
        Assert.Empty(Slots(nobody));
    }

    [Fact]
    public async Task Without_a_common_slot_the_most_people_free_are_shown_with_who_is_busy()
    {
        var p = await f.Seed();
        await f.AddShare(p.Owner, p.Organizer);
        await f.AddShare(p.Owner2, p.Organizer, start: Monday.AddDays(1), end: Monday.AddDays(10));
        await Meeting(p.Admin, p.Owner, Monday, (8, 0), (10, 0));
        await Meeting(p.Admin, p.Owner, Monday, (10, 0), (12, 0), MeetingStatus.Approved);

        using var organizer = f.Client(p.Organizer);
        var response = await organizer.PostAsJsonAsync("/api/calendar/slots", Search([p.Owner, p.Owner2], includeMe: true));
        var result = await Ok(response);
        Assert.Equal("MSG445", result.GetProperty("code").GetString());
        Assert.False(result.GetProperty("allFree").GetBoolean());
        // Monday: Owner is busy all morning, Owner2's share starts on Tuesday (unknown, not free). Only the finder is free.
        var slots = Slots(result);
        Assert.Equal(4, slots.Count);
        Assert.All(slots, s =>
        {
            Assert.Equal(1, s.GetProperty("freeCount").GetInt32());
            Assert.Equal(3, s.GetProperty("countedCount").GetInt32());
            Assert.Equal([p.Owner], Ids(s, "busyUserIds"));
            Assert.Equal([p.Owner2], Ids(s, "unknownUserIds"));
        });
    }

    [Fact]
    public async Task A_gap_keeps_slots_away_from_busy_periods()
    {
        var p = await f.Seed();
        await f.AddShare(p.Owner, p.Organizer);
        await Meeting(p.Admin, p.Owner, Monday, (9, 0), (9, 10));
        using var organizer = f.Client(p.Organizer);
        var result = await Ok(await organizer.PostAsJsonAsync("/api/calendar/slots", Search([p.Owner], buffer: 30)));
        // 08:00–09:00 ends right as the meeting starts and 09:00 overlaps it; 10:00 starts 50 minutes after 09:10.
        Assert.Equal([10, 11], StartHours(result));
    }

    [Fact]
    public async Task A_database_failure_answers_MSG453()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        f.Failures.FailShareRead = true;
        try
        {
            await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots", Search([p.Owner])), 500, "MSG453");
        }
        finally
        {
            f.Failures.FailShareRead = false;
        }
    }
}
