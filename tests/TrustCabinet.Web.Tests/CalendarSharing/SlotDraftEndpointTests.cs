using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.CalendarSharing.CalendarSharingFixture;

namespace TrustCabinet.Web.Tests.CalendarSharing;

/// <summary>
/// UC-85b — POST /api/calendar/slots/draft: the slot is rechecked against the same shared data as the search (BR-428), a
/// Secret meeting never blocks it, and the meeting is created in Draft through UC-14 with the first invitee as Chair, the
/// rest as Members and the finder as Secretary (BR-429).
/// </summary>
public sealed class SlotDraftEndpointTests(CalendarSharingFixture f) : IClassFixture<CalendarSharingFixture>
{
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private static DateTimeOffset At(DateOnly day, int hour, int minute = 0) => VietnamTime.At(day, new TimeOnly(hour, minute));

    private static string Iso(DateTimeOffset instant) => VietnamTime.ToLocal(instant).ToString("yyyy-MM-ddTHH:mm:sszzz");

    private async Task Meeting(Guid creator, Guid person, DateOnly day, (int H, int M) from, (int H, int M) to,
        Sensitivity sensitivity = Sensitivity.Normal)
    {
        await using var db = f.Db();
        var meeting = new Meeting
        {
            Title = "Họp khác", Location = "Phòng 302", CreatedById = creator, Status = MeetingStatus.Published,
            Sensitivity = sensitivity, Mode = MeetingMode.InPerson, PlannedStartTime = At(day, from.H, from.M).ToUniversalTime(),
            PlannedEndTime = At(day, to.H, to.M).ToUniversalTime(),
        };
        db.Meetings.Add(meeting);
        db.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = person, Role = MeetingRole.Member });
        await db.SaveChangesAsync();
    }

    private static object Draft(Guid[] userIds, int hour = 9, Guid[]? accepted = null, bool includeMe = false,
        string? title = "Họp triển khai quý IV", DateOnly? day = null, int buffer = 30)
        => new
        {
            title, plannedStartTime = Iso(At(day ?? Monday, hour)), plannedEndTime = Iso(At(day ?? Monday, hour + 1)),
            mode = "InPerson", location = "Phòng 301", sensitivity = "Normal", description = "",
            userIds, includeMe, bufferMinutes = buffer, acceptedBusyUserIds = accepted ?? [],
        };

    private async Task<int> MeetingsCreatedBy(Guid userId)
    {
        await using var db = f.Db();
        return await db.Meetings.CountAsync(m => m.CreatedById == userId);
    }

    [Fact]
    public async Task A_free_slot_becomes_a_Draft_with_the_default_roles()
    {
        var p = await f.Seed();
        await f.AddShare(p.Owner, p.Organizer);
        using var organizer = f.Client(p.Organizer);

        var response = await organizer.PostAsJsonAsync("/api/calendar/slots/draft", Draft([p.Owner, p.Owner2, p.Organizer2]));
        var body = await AssertCode(response, 201, "MSG29");
        var meeting = body.GetProperty("meeting");
        var id = meeting.GetProperty("id").GetGuid();
        Assert.Equal($"/api/meetings/{id}", response.Headers.Location?.ToString());
        Assert.Equal("Draft", meeting.GetProperty("status").GetString());
        Assert.Equal(At(Monday, 9), meeting.GetProperty("plannedStartTime").GetDateTimeOffset());

        await using var db = f.Db();
        var roster = await db.MeetingParticipants.AsNoTracking().Where(x => x.MeetingId == id)
            .ToDictionaryAsync(x => x.UserId, x => x.Role);
        Assert.Equal(4, roster.Count);
        Assert.Equal(MeetingRole.Chair, roster[p.Owner]);
        Assert.Equal(MeetingRole.Member, roster[p.Owner2]);
        Assert.Equal(MeetingRole.Member, roster[p.Organizer2]);
        Assert.Equal(MeetingRole.Secretary, roster[p.Organizer]);
        Assert.Equal(p.Organizer, (await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == id)).CreatedById);
    }

    [Fact]
    public async Task Someone_who_became_busy_makes_the_slot_taken()
    {
        var p = await f.Seed();
        await f.AddShare(p.Owner, p.Organizer);
        await f.AddShare(p.Owner2, p.Organizer);
        await Meeting(p.Admin, p.Owner2, Monday, (9, 30), (10, 0));
        using var organizer = f.Client(p.Organizer);

        await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft", Draft([p.Owner, p.Owner2])), 409, "MSG446");
        Assert.Equal(0, await MeetingsCreatedBy(p.Organizer));

        // The buffer counts too: a meeting ending 08:45 is within 30 minutes of 09:00.
        var q = await f.Seed();
        await f.AddShare(q.Owner, q.Organizer);
        await Meeting(q.Admin, q.Owner, Monday, (8, 0), (8, 45));
        using var organizer2 = f.Client(q.Organizer);
        await AssertCode(await organizer2.PostAsJsonAsync("/api/calendar/slots/draft", Draft([q.Owner])), 409, "MSG446");
        await AssertCode(await organizer2.PostAsJsonAsync("/api/calendar/slots/draft", Draft([q.Owner], buffer: 0)), 201, "MSG29");
    }

    [Fact]
    public async Task A_most_free_slot_is_kept_when_those_busy_then_are_still_the_only_ones()
    {
        var p = await f.Seed();
        await f.AddShare(p.Owner, p.Organizer);
        await f.AddShare(p.Owner2, p.Organizer, start: Monday.AddDays(1), end: Monday.AddDays(5));
        await Meeting(p.Admin, p.Owner, Monday, (9, 0), (10, 0));
        using var organizer = f.Client(p.Organizer);

        // Owner is busy and Owner2's Monday is unknown — both were shown so when the slot was found (UC-85a AF-1).
        await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft",
            Draft([p.Owner, p.Owner2], accepted: [p.Owner, p.Owner2])), 201, "MSG29");
        // Without saying so, the same slot is taken.
        await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft",
            Draft([p.Owner, p.Owner2], accepted: [p.Owner])), 409, "MSG446");
    }

    [Fact]
    public async Task A_Secret_meeting_never_blocks_a_draft()
    {
        var p = await f.Seed();
        await f.AddShare(p.Owner, p.Organizer, level: CalendarShareLevel.Level2);
        await Meeting(p.Admin, p.Owner, Monday, (9, 0), (10, 0), Sensitivity.Secret);
        using var organizer = f.Client(p.Organizer);
        // BR-428: the recheck reads only shared data, so it answers exactly as if nothing were there.
        await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft", Draft([p.Owner])), 201, "MSG29");
    }

    [Fact]
    public async Task A_meeting_above_the_shared_level_never_blocks_a_draft()
    {
        var p = await f.Seed();
        await f.AddShare(p.Owner, p.Organizer, level: CalendarShareLevel.Level1);
        await Meeting(p.Admin, p.Owner, Monday, (9, 0), (10, 0), Sensitivity.Internal);
        using var organizer = f.Client(p.Organizer);
        // BR-417: at Level 1 an Internal meeting is free for the finder, at the search and at the recheck alike.
        await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft", Draft([p.Owner])), 201, "MSG29");
    }

    [Fact]
    public async Task People_without_a_share_are_not_rechecked()
    {
        var p = await f.Seed();
        await Meeting(p.Admin, p.Owner, Monday, (9, 0), (10, 0));
        // Owner shares only with someone else: the finder knows nothing about Owner's hours.
        await f.AddShare(p.Owner2, p.Organizer2);
        await f.AddShare(p.Owner, p.Organizer2, level: CalendarShareLevel.Level2);
        using var organizer = f.Client(p.Organizer);
        await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft", Draft([p.Owner])), 201, "MSG29");
    }

    [Fact]
    public async Task The_finders_own_calendar_is_rechecked_only_when_ticked()
    {
        var p = await f.Seed();
        await Meeting(p.Admin, p.Organizer, Monday, (9, 0), (10, 0), Sensitivity.Secret);
        using var organizer = f.Client(p.Organizer);
        await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft",
            Draft([p.Owner], includeMe: true)), 409, "MSG446");
        await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft",
            Draft([p.Owner], includeMe: true, accepted: [p.Organizer])), 201, "MSG29");
    }

    [Fact]
    public async Task Only_a_Meeting_Organizer_may_create_a_draft_from_a_slot()
    {
        var p = await f.Seed();
        using var member = f.Client(p.Owner);
        await AssertCode(await member.PostAsJsonAsync("/api/calendar/slots/draft", Draft([p.Owner2])), 403, "MSG448");
        Assert.Equal(0, await MeetingsCreatedBy(p.Owner));
    }

    [Fact]
    public async Task An_invalid_invitee_list_answers_MSG277()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        foreach (var userIds in new[]
                 {
                     Array.Empty<Guid>(),
                     [.. Enumerable.Range(0, 11).Select(_ => Guid.NewGuid())],
                     [p.Owner, p.Owner],
                     [p.Organizer],
                 })
        {
            var body = await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft", Draft(userIds)), 400, "MSG277");
            Assert.True(body.GetProperty("errors").TryGetProperty("userIds", out _));
        }

        // A deactivated account is refused by UC-14's own roster check, named after the invitee the page sent.
        var locked = await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft",
            Draft([p.Owner, p.Locked])), 400, "MSG277");
        var errors = locked.GetProperty("errors");
        Assert.Equal(MeetingSlotRules.UserUnavailableError, errors.GetProperty("userIds[1]").GetString());
        Assert.DoesNotContain(errors.EnumerateObject(), e => e.Name.StartsWith("participants", StringComparison.Ordinal));
        Assert.Equal(0, await MeetingsCreatedBy(p.Organizer));
    }

    [Fact]
    public async Task Invalid_meeting_values_answer_MSG31()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);

        var noTitle = await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft",
            Draft([p.Owner], title: "")), 400, "MSG31");
        Assert.True(noTitle.GetProperty("errors").TryGetProperty("title", out _));

        // The slot's start has passed since the search (UC-14 EX-1).
        var past = await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft",
            Draft([p.Owner], hour: 8, day: Today)), 400, "MSG31");
        Assert.True(past.GetProperty("errors").TryGetProperty("plannedStartTime", out _));

        var stranger = await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft",
            Draft([p.Owner], accepted: [Guid.NewGuid()])), 400, "MSG31");
        Assert.Equal(MeetingSlotRules.AcceptedBusyError, stranger.GetProperty("errors").GetProperty("acceptedBusyUserIds").GetString());

        var badTime = await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft", new
        {
            title = "Họp", plannedStartTime = "2026-11-09 09:00", plannedEndTime = Iso(At(Monday, 10)), userIds = new[] { p.Owner },
        }), 400, "MSG31");
        Assert.True(badTime.GetProperty("errors").TryGetProperty("plannedStartTime", out _));

        var badBuffer = await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft",
            Draft([p.Owner], buffer: 7)), 400, "MSG31");
        Assert.True(badBuffer.GetProperty("errors").TryGetProperty("bufferMinutes", out _));
        Assert.Equal(0, await MeetingsCreatedBy(p.Organizer));
    }

    [Fact]
    public async Task A_read_failure_answers_MSG453_and_a_save_failure_MSG35()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        f.Failures.FailShareRead = true;
        try
        {
            await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft", Draft([p.Owner])), 500, "MSG453");
        }
        finally
        {
            f.Failures.FailShareRead = false;
        }

        f.Failures.FailSave = true;
        try
        {
            await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/slots/draft", Draft([p.Owner])), 500, "MSG35");
        }
        finally
        {
            f.Failures.FailSave = false;
        }

        Assert.Equal(0, await MeetingsCreatedBy(p.Organizer));
    }
}
