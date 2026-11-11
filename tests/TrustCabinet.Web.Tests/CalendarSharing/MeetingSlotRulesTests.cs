using TrustCabinet.Domain.CalendarSharing;

namespace TrustCabinet.Web.Tests.CalendarSharing;

/// <summary>UC-85a step 2 — the defaults and limits of a meeting-time search (BR-421, BR-422, ADR-0058 §7).</summary>
public sealed class MeetingSlotRulesTests
{
    private static readonly Guid Finder = Guid.NewGuid();
    private static readonly Guid A = Guid.NewGuid();
    private static readonly DateOnly Wednesday = new(2026, 11, 11);

    private static SlotSearchInput Input(
        Guid[]? userIds = null, int? duration = null, DateOnly? from = null, DateOnly? to = null, int[]? weekdays = null,
        string? workStart = null, string? workEnd = null, bool? lunchBreak = null, string? lunchStart = null,
        string? lunchEnd = null, int? buffer = null, int? step = null, bool? includeMe = null)
        => new(userIds ?? [A], includeMe, duration, from, to, weekdays, workStart, workEnd, lunchBreak, lunchStart, lunchEnd,
            buffer, step);

    private static IReadOnlyDictionary<string, string> Errors(SlotSearchInput input, DateOnly? today = null)
    {
        Assert.False(MeetingSlotRules.TryValidate(input, Finder, today ?? Wednesday, out var search, out var errors));
        Assert.Null(search);
        return errors;
    }

    private static SlotSearch Valid(SlotSearchInput input, DateOnly? today = null)
    {
        Assert.True(MeetingSlotRules.TryValidate(input, Finder, today ?? Wednesday, out var search, out var errors),
            string.Join("; ", errors.Select(e => $"{e.Key}: {e.Value}")));
        return search!;
    }

    [Fact]
    public void An_empty_search_takes_the_eCab_defaults()
    {
        var search = Valid(Input());
        Assert.Equal([A], search.UserIds);
        Assert.False(search.IncludeMe);
        Assert.Equal(120, search.DurationMinutes);
        Assert.Equal(Wednesday, search.FromDate);
        Assert.Equal(new DateOnly(2026, 11, 13), search.ToDate); // the Friday of the week
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            search.Weekdays);
        Assert.Equal(new TimeOnly(8, 0), search.WorkStart);
        Assert.Equal(new TimeOnly(17, 0), search.WorkEnd);
        Assert.Equal(new TimeOnly(12, 0), search.LunchStart);
        Assert.Equal(new TimeOnly(13, 0), search.LunchEnd);
        Assert.Equal(30, search.BufferMinutes);
        Assert.Equal(30, search.StepMinutes);
    }

    [Theory]
    [InlineData("2026-11-09", "2026-11-13")] // Monday → that Friday
    [InlineData("2026-11-13", "2026-11-13")] // Friday → the same day
    [InlineData("2026-11-14", "2026-11-20")] // Saturday → the next Friday
    [InlineData("2026-11-15", "2026-11-20")] // Sunday → the next Friday
    public void The_default_end_is_the_coming_Friday(string from, string expected)
        => Assert.Equal(DateOnly.Parse(expected), MeetingSlotRules.DefaultToDate(DateOnly.Parse(from)));

    [Fact]
    public void Lunch_can_be_switched_off()
    {
        var search = Valid(Input(lunchBreak: false, duration: 480));
        Assert.Null(search.LunchStart);
        Assert.Null(search.LunchEnd);
    }

    [Theory]
    [InlineData(15, true)]
    [InlineData(240, true)]
    [InlineData(14, false)]
    [InlineData(16, false)] // not a multiple of 5
    [InlineData(481, false)]
    [InlineData(0, false)]
    public void Duration_is_15_minutes_to_8_hours(int minutes, bool valid)
    {
        var input = Input(duration: minutes, lunchBreak: false);
        if (valid) Valid(input);
        else Assert.Equal(MeetingSlotRules.DurationError, Errors(input)["durationMinutes"]);
    }

    [Fact]
    public void Eight_hours_fits_only_without_lunch_and_with_a_long_enough_day()
    {
        Valid(Input(duration: 480, lunchBreak: false, workStart: "08:00", workEnd: "17:00"));
        // With a lunch break neither half of the day holds 5 hours.
        Assert.Equal(MeetingSlotRules.DurationTooLongError, Errors(Input(duration: 300))["durationMinutes"]);
        Valid(Input(duration: 240)); // 08:00–12:00 or 13:00–17:00
        Assert.Equal(MeetingSlotRules.DurationTooLongError,
            Errors(Input(duration: 120, lunchBreak: false, workStart: "08:00", workEnd: "09:00"))["durationMinutes"]);
    }

    [Theory]
    [InlineData("2026-11-11", "2026-12-11", true)] // 31 days counting both ends
    [InlineData("2026-11-11", "2026-12-12", false)] // 32 days
    [InlineData("2026-11-11", "2026-11-10", false)] // end before start
    public void The_range_is_at_most_31_days(string from, string to, bool valid)
    {
        var input = Input(from: DateOnly.Parse(from), to: DateOnly.Parse(to));
        if (valid) Valid(input);
        else Assert.Equal(MeetingSlotRules.ToDateError, Errors(input)["toDate"]);
    }

    [Fact]
    public void The_range_never_starts_before_today()
        => Assert.Equal(MeetingSlotRules.FromDateError, Errors(Input(from: Wednesday.AddDays(-1)))["fromDate"]);

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 6 })]
    [InlineData(new[] { 7 })]
    [InlineData(new[] { 0 })]
    [InlineData(new[] { 1, 1 })]
    public void Weekdays_are_Monday_to_Friday(int[] weekdays)
        => Assert.Equal(MeetingSlotRules.WeekdaysError, Errors(Input(weekdays: weekdays))["weekdays"]);

    [Fact]
    public void Weekdays_are_kept_in_week_order()
        => Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday], Valid(Input(weekdays: [5, 1])).Weekdays);

    [Theory]
    [InlineData("8:00", null)]
    [InlineData("08:00:00", null)]
    [InlineData("25:00", null)]
    [InlineData(null, "abc")]
    public void Working_hours_are_HH_mm(string? workStart, string? workEnd)
    {
        var errors = Errors(Input(workStart: workStart, workEnd: workEnd, lunchBreak: false));
        Assert.Contains(errors.Keys, k => k is "workStart" or "workEnd");
    }

    [Fact]
    public void The_working_day_ends_after_it_starts()
        => Assert.Equal(MeetingSlotRules.WorkHoursError,
            Errors(Input(workStart: "17:00", workEnd: "08:00", lunchBreak: false))["workEnd"]);

    [Theory]
    [InlineData("07:30", "13:00", "lunchStart")] // before the day starts
    [InlineData("12:00", "17:30", "lunchEnd")] // after the day ends
    [InlineData("13:00", "12:00", "lunchEnd")] // ends before it starts
    [InlineData("12:00", "12:00", "lunchEnd")]
    public void Lunch_sits_inside_working_hours(string lunchStart, string lunchEnd, string key)
        => Assert.Equal(MeetingSlotRules.LunchError,
            Errors(Input(lunchStart: lunchStart, lunchEnd: lunchEnd, duration: 60))[key]);

    [Theory]
    [InlineData(0, true)]
    [InlineData(240, true)]
    [InlineData(241, false)]
    [InlineData(-5, false)]
    [InlineData(7, false)]
    public void Buffer_is_0_to_4_hours(int minutes, bool valid)
    {
        var input = Input(buffer: minutes);
        if (valid) Valid(input);
        else Assert.Equal(MeetingSlotRules.BufferError, Errors(input)["bufferMinutes"]);
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(120, true)]
    [InlineData(4, false)]
    [InlineData(121, false)]
    [InlineData(125, false)]
    [InlineData(12, false)]
    public void Step_is_5_minutes_to_2_hours(int minutes, bool valid)
    {
        var input = Input(step: minutes);
        if (valid) Valid(input);
        else Assert.Equal(MeetingSlotRules.StepError, Errors(input)["stepMinutes"]);
    }

    [Fact]
    public void Invitees_are_1_to_10_distinct_people_other_than_the_finder()
    {
        Valid(Input(userIds: [.. Enumerable.Range(0, 10).Select(_ => Guid.NewGuid())]));
        Assert.Equal(MeetingSlotRules.UserIdsError, Errors(Input(userIds: []))["userIds"]);
        Assert.Equal(MeetingSlotRules.UserIdsError,
            Errors(Input(userIds: [.. Enumerable.Range(0, 11).Select(_ => Guid.NewGuid())]))["userIds"]);
        Assert.Equal(MeetingSlotRules.UserIdsError, Errors(Input(userIds: [A, A]))["userIds"]);
        Assert.Equal(MeetingSlotRules.UserIdsError, Errors(Input(userIds: [A, Finder]))["userIds"]);
        Assert.Equal(MeetingSlotRules.UserIdsError, Errors(Input(userIds: [Guid.Empty]))["userIds"]);
        // "Tính cả tôi" does not take one of the 10 places (D14).
        Valid(Input(userIds: [.. Enumerable.Range(0, 10).Select(_ => Guid.NewGuid())], includeMe: true));
    }

    [Fact]
    public void Every_broken_field_is_reported_at_once()
    {
        var errors = Errors(new SlotSearchInput(null, null, 7, Wednesday.AddDays(-1), null, [6], "x", null, null, null, null,
            999, 1));
        Assert.Equal(["bufferMinutes", "durationMinutes", "fromDate", "stepMinutes", "userIds", "weekdays", "workStart"],
            errors.Keys.Order());
    }

    [Theory]
    [InlineData("2026-11-10T08:00:00+07:00", true)]
    [InlineData("2026-11-10T01:00:00Z", true)]
    [InlineData("2026-11-10T08:00:00", false)] // no offset
    [InlineData("2026-11-10 08:00", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Slot_times_carry_an_offset(string? value, bool valid)
        => Assert.Equal(valid, MeetingSlotRules.TryParseTimestamp(value, out _));

    [Fact]
    public void A_search_starts_at_most_365_days_ahead_and_never_overflows()
    {
        Valid(Input(from: Wednesday.AddDays(365), to: Wednesday.AddDays(365)));
        Assert.Equal(MeetingSlotRules.FromDateError, Errors(Input(from: Wednesday.AddDays(366)))["fromDate"]);
        // The ends of the calendar are refused, not thrown on (review of PR #319).
        Assert.Equal(MeetingSlotRules.FromDateError,
            Errors(Input(from: DateOnly.MaxValue, to: DateOnly.MaxValue))["fromDate"]);
        Assert.Equal(MeetingSlotRules.FromDateError, Errors(Input(from: DateOnly.MaxValue))["fromDate"]);
        Assert.Equal(MeetingSlotRules.FromDateError, Errors(Input(from: DateOnly.MinValue))["fromDate"]);
        Assert.Equal(MeetingSlotRules.ToDateError, Errors(Input(to: DateOnly.MaxValue))["toDate"]);
    }

    [Fact]
    public void The_duration_must_fit_a_block_from_a_start_on_the_step_grid()
    {
        // 08:15–12:00 holds 225 minutes, but with a 30-minute step the first start is 08:30, which ends at 12:15.
        Assert.Equal(MeetingSlotRules.DurationTooLongError,
            Errors(Input(duration: 225, workStart: "08:15", workEnd: "12:00", lunchBreak: false, step: 30))["durationMinutes"]);
        Valid(Input(duration: 225, workStart: "08:15", workEnd: "12:00", lunchBreak: false, step: 15));
        // The afternoon block counts too: 13:00 is on the grid and 13:00 + 225 minutes ends before 17:00.
        Valid(Input(duration: 225, workStart: "08:15", step: 30));
    }

    [Fact]
    public void A_slot_starts_after_now_within_reach_of_a_search_and_lasts_at_most_8_hours()
    {
        var now = VietnamTime.At(Wednesday, new TimeOnly(10, 0));
        IDictionary<string, string> Check(DateTimeOffset start, DateTimeOffset end)
        {
            var errors = new Dictionary<string, string>();
            MeetingSlotRules.CheckSlotTimes(start, end, now, errors);
            return errors;
        }

        Assert.Empty(Check(now.AddMinutes(30), now.AddHours(8).AddMinutes(30)));
        Assert.Equal(MeetingSlotRules.SlotPastError, Check(now, now.AddHours(1))["plannedStartTime"]);
        Assert.Equal(MeetingSlotRules.SlotPastError, Check(DateTimeOffset.MinValue, now)["plannedStartTime"]);
        var lastDay = VietnamTime.At(Wednesday.AddDays(MeetingSlotRules.MaxDaysAhead + MeetingSlotRules.MaxRangeDays - 1),
            new TimeOnly(9, 0));
        Assert.Empty(Check(lastDay, lastDay.AddHours(1)));
        Assert.Equal(MeetingSlotRules.SlotTooFarError, Check(lastDay.AddDays(1), lastDay.AddDays(1).AddHours(1))["plannedStartTime"]);
        Assert.Equal(MeetingSlotRules.SlotTooFarError,
            Check(DateTimeOffset.MaxValue.AddHours(-1), DateTimeOffset.MaxValue)["plannedStartTime"]);
        Assert.Equal(MeetingSlotRules.SlotEndError, Check(now.AddHours(1), now.AddHours(9).AddMinutes(1))["plannedEndTime"]);
        Assert.Equal(MeetingSlotRules.SlotEndError, Check(now.AddHours(2), now.AddHours(1))["plannedEndTime"]);
    }
}
