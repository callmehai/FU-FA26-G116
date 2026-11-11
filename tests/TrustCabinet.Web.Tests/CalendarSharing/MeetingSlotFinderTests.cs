using TrustCabinet.Domain.CalendarSharing;

namespace TrustCabinet.Web.Tests.CalendarSharing;

/// <summary>
/// UC-85a steps 3–4 — the slots a search proposes: working hours, lunch, past starts and the step (BR-422, D15), unknown
/// days (BR-415), common slots versus the most people free (AF-1), and the 200-slot cap.
/// </summary>
public sealed class MeetingSlotFinderTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly DateOnly Monday = new(2026, 11, 9);
    /// <summary>Sunday 08/11/2026 10:00 in Hanoi: nothing on Monday is in the past.</summary>
    private static readonly DateTimeOffset SundayMorning = VietnamTime.At(new DateOnly(2026, 11, 8), new TimeOnly(10, 0));

    private static DateTimeOffset At(DateOnly day, int hour, int minute = 0) => VietnamTime.At(day, new TimeOnly(hour, minute));

    private static SlotSearch Search(int duration = 60, DateOnly? from = null, DateOnly? to = null, int buffer = 30,
        int step = 30, string workStart = "08:00", string workEnd = "17:00", bool lunch = true,
        DayOfWeek[]? weekdays = null)
        => new([A], false, duration, from ?? Monday, to ?? from ?? Monday,
            weekdays ?? [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            TimeOnly.Parse(workStart), TimeOnly.Parse(workEnd), lunch ? new TimeOnly(12, 0) : null,
            lunch ? new TimeOnly(13, 0) : null, buffer, step);

    private static SlotPerson Person(Guid id, params BusyInterval[] busy)
        => new(id, new KnownDays(Monday.AddDays(-30), Monday.AddDays(30)), busy);

    private static SlotPerson Windowed(Guid id, KnownDays window, params BusyInterval[] busy) => new(id, window, busy);

    /// <summary>The finder's own calendar: known on every day (BR-418).</summary>
    private static SlotPerson Own(params BusyInterval[] busy) => new(Me, null, busy);

    private static BusyInterval Busy(DateOnly day, (int H, int M) from, (int H, int M) to)
        => new(At(day, from.H, from.M), At(day, to.H, to.M));

    private static TimeOnly[] Starts(SlotFinding finding)
        => [.. finding.Slots.Select(s => TimeOnly.FromDateTime(VietnamTime.ToLocal(s.Start).DateTime))];

    [Fact]
    public void The_BR_422_example_gives_10_00()
    {
        // A meeting ending 09:10, a 30-minute gap and a 30-minute step: the earliest start after it is 10:00.
        var finding = MeetingSlotFinder.Find(Search(), SundayMorning,
            [Person(A, Busy(Monday, (8, 0), (9, 10)))]);
        Assert.Equal(SlotResultKind.AllFree, finding.Kind);
        Assert.Equal(new TimeOnly(10, 0), Starts(finding)[0]);
    }

    [Fact]
    public void The_gap_applies_before_a_busy_period_too()
    {
        // Busy 11:00–11:30: a one-hour slot must end by 10:30 or start at 12:00 (lunch) — so 09:30 is the last morning start.
        var finding = MeetingSlotFinder.Find(Search(), SundayMorning, [Person(A, Busy(Monday, (11, 0), (11, 30)))]);
        var starts = Starts(finding);
        Assert.Contains(new TimeOnly(9, 30), starts);
        Assert.DoesNotContain(new TimeOnly(10, 0), starts);
        Assert.DoesNotContain(new TimeOnly(11, 30), starts);
    }

    [Fact]
    public void Busy_periods_may_arrive_unsorted_and_overlapping()
    {
        var finding = MeetingSlotFinder.Find(Search(buffer: 0), SundayMorning,
        [
            Person(A, Busy(Monday, (14, 0), (15, 0)), Busy(Monday, (8, 0), (9, 30)), Busy(Monday, (9, 0), (10, 0))),
        ]);
        Assert.Equal([new TimeOnly(10, 0), new TimeOnly(10, 30), new TimeOnly(11, 0), new TimeOnly(13, 0),
            new TimeOnly(15, 0), new TimeOnly(15, 30), new TimeOnly(16, 0)], Starts(finding));
    }

    [Fact]
    public void The_binary_search_agrees_with_a_plain_scan()
    {
        // Many random busy periods over a month: every slot's verdict must match checking each period one by one.
        var random = new Random(137);
        var busy = Enumerable.Range(0, 400).Select(_ =>
        {
            var day = Monday.AddDays(random.Next(0, 31));
            var start = random.Next(7 * 60, 18 * 60);
            var startAt = At(day, start / 60, start % 60);
            return new BusyInterval(startAt, startAt.AddMinutes(random.Next(5, 180)));
        }).ToArray();
        SlotPerson[] persons = [Windowed(A, new KnownDays(Monday, Monday.AddDays(30)), busy)];
        var verdicts = new HashSet<bool>();
        for (var day = Monday; day <= Monday.AddDays(30); day = day.AddDays(1))
        {
            for (var minute = 8 * 60; minute <= 17 * 60; minute += 5)
            {
                var start = At(day, minute / 60, minute % 60);
                var end = start.AddMinutes(30);
                var scan = busy.Any(b => b.Start < end.AddMinutes(15) && b.End > start.AddMinutes(-15));
                var (found, _) = MeetingSlotFinder.Check(start, end, 15, persons);
                Assert.Equal(scan, found.Contains(A));
                verdicts.Add(scan);
            }
        }

        Assert.Equal(2, verdicts.Count); // both busy and free slots were exercised
    }

    [Fact]
    public void Starts_follow_the_step_from_midnight_inside_working_hours()
    {
        var finding = MeetingSlotFinder.Find(Search(workStart: "08:10", workEnd: "10:30", step: 30, lunch: false),
            SundayMorning, [Person(A)]);
        Assert.Equal([new TimeOnly(8, 30), new TimeOnly(9, 0), new TimeOnly(9, 30)], Starts(finding));
    }

    [Fact]
    public void A_slot_never_overlaps_lunch_unless_lunch_is_off()
    {
        var withLunch = Starts(MeetingSlotFinder.Find(Search(duration: 120), SundayMorning, [Person(A)]));
        Assert.Equal([new TimeOnly(8, 0), new TimeOnly(8, 30), new TimeOnly(9, 0), new TimeOnly(9, 30), new TimeOnly(10, 0),
            new TimeOnly(13, 0), new TimeOnly(13, 30), new TimeOnly(14, 0), new TimeOnly(14, 30), new TimeOnly(15, 0)],
            withLunch);

        var allDay = MeetingSlotFinder.Find(Search(duration: 480, lunch: false, step: 60), SundayMorning, [Person(A)]);
        Assert.Equal([new TimeOnly(8, 0), new TimeOnly(9, 0)], Starts(allDay));
    }

    [Fact]
    public void Past_starts_are_never_proposed()
    {
        var now = At(Monday, 10, 0);
        var starts = Starts(MeetingSlotFinder.Find(Search(), now, [Person(A)]));
        Assert.DoesNotContain(new TimeOnly(10, 0), starts);
        Assert.Equal(new TimeOnly(10, 30), starts[0]);
    }

    [Fact]
    public void Only_the_chosen_weekdays_are_searched()
    {
        var finding = MeetingSlotFinder.Find(Search(from: Monday, to: Monday.AddDays(6), weekdays: [DayOfWeek.Wednesday]),
            SundayMorning, [Person(A)]);
        Assert.All(finding.Slots, s => Assert.Equal(Monday.AddDays(2), s.Date));
        Assert.NotEmpty(finding.Slots);
    }

    [Fact]
    public void A_day_outside_a_share_is_unknown_never_a_common_slot()
    {
        // A shares Monday only; B shares the whole week. On Tuesday A is unknown, so Tuesday has no common slot.
        var persons = new[]
        {
            Windowed(A, new KnownDays(Monday, Monday)),
            Person(B),
        };
        var finding = MeetingSlotFinder.Find(Search(from: Monday, to: Monday.AddDays(1)), SundayMorning, persons);
        Assert.Equal(SlotResultKind.AllFree, finding.Kind);
        Assert.All(finding.Slots, s => Assert.Equal(Monday, s.Date));

        // With A busy all Monday there is nothing common: AF-1 shows Tuesday with A unknown, never counted as free.
        persons[0] = Windowed(A, new KnownDays(Monday, Monday), Busy(Monday, (7, 0), (18, 0)));
        var partial = MeetingSlotFinder.Find(Search(from: Monday, to: Monday.AddDays(1)), SundayMorning, persons);
        Assert.Equal(SlotResultKind.MostFree, partial.Kind);
        Assert.Contains(partial.Slots, s => s.Date == Monday.AddDays(1) && s.UnknownUserIds.SequenceEqual([A]) && s.FreeCount == 1);
        Assert.Contains(partial.Slots, s => s.Date == Monday && s.BusyUserIds.SequenceEqual([A]) && s.FreeCount == 1);
    }

    [Fact]
    public void Without_a_common_slot_the_most_people_free_are_shown()
    {
        var persons = new[]
        {
            Own(Busy(Monday, (8, 0), (12, 0))),
            Person(A, Busy(Monday, (13, 0), (17, 0))),
            Person(B, Busy(Monday, (8, 0), (10, 0))),
        };
        var finding = MeetingSlotFinder.Find(Search(buffer: 0), SundayMorning, persons);
        Assert.Equal(SlotResultKind.MostFree, finding.Kind);
        Assert.All(finding.Slots, s => Assert.Equal(2, s.FreeCount));
        Assert.All(finding.Slots, s => Assert.Equal(3, s.CountedCount));
        // 10:00–11:00: only Me is busy; 13:00–14:00: only A is busy.
        Assert.Contains(finding.Slots, s => s.Start == At(Monday, 10) && s.BusyUserIds.SequenceEqual([Me]));
        Assert.Contains(finding.Slots, s => s.Start == At(Monday, 13) && s.BusyUserIds.SequenceEqual([A]));
        Assert.DoesNotContain(finding.Slots, s => s.Start == At(Monday, 8));
    }

    [Fact]
    public void Nobody_free_or_nobody_counted_gives_nothing()
    {
        var allBusy = MeetingSlotFinder.Find(Search(), SundayMorning, [Person(A, Busy(Monday, (7, 0), (18, 0)))]);
        Assert.Equal(SlotResultKind.None, allBusy.Kind);
        Assert.Empty(allBusy.Slots);

        var nobody = MeetingSlotFinder.Find(Search(), SundayMorning, []);
        Assert.Equal(SlotResultKind.None, nobody.Kind);
        Assert.Empty(nobody.Slots);
    }

    [Fact]
    public void At_most_200_slots_in_time_order()
    {
        // 23 weekdays × 108 five-minute starts: far more than 200.
        var finding = MeetingSlotFinder.Find(
            Search(duration: 15, from: Monday, to: Monday.AddDays(30), step: 5, buffer: 0, lunch: false),
            SundayMorning, [Windowed(A, new KnownDays(Monday, Monday.AddDays(30)))]);
        Assert.True(finding.Truncated);
        Assert.Equal(MeetingSlotRules.MaxSlots, finding.Slots.Count);
        Assert.Equal(finding.Slots.OrderBy(s => s.Start), finding.Slots);
        Assert.Equal(At(Monday, 8), finding.Slots[0].Start);

        var few = MeetingSlotFinder.Find(Search(), SundayMorning, [Person(A)]);
        Assert.False(few.Truncated);
    }

    [Fact]
    public void Check_reports_busy_and_unknown_people_for_one_slot()
    {
        var persons = new[]
        {
            Own(Busy(Monday, (9, 0), (9, 50))),
            Windowed(A, new KnownDays(Monday.AddDays(1), Monday.AddDays(5))),
            Person(B),
        };
        var (busy, unknown) = MeetingSlotFinder.Check(At(Monday, 10), At(Monday, 11), 15, persons);
        Assert.Equal([Me], busy);
        Assert.Equal([A], unknown);

        var (none, _) = MeetingSlotFinder.Check(At(Monday, 10), At(Monday, 11), 10, persons);
        Assert.Empty(none);
    }

    [Fact]
    public void A_gap_reaching_into_a_day_outside_the_share_makes_the_person_unknown()
    {
        // Shared Monday only; a meeting on Sunday evening. A 4-hour gap around 00:00 Monday reaches back into Sunday:
        // the answer must not depend on Sunday at all (BR-415, review of PR #319).
        var window = new KnownDays(Monday, Monday);
        var sunday = Monday.AddDays(-1);
        SlotPerson[] withSunday = [Windowed(A, window, Busy(sunday, (22, 0), (23, 0)))];
        SlotPerson[] withoutSunday = [Windowed(A, window)];
        foreach (var persons in new[] { withSunday, withoutSunday })
        {
            var (busy, unknown) = MeetingSlotFinder.Check(At(Monday, 0), At(Monday, 1), 240, persons);
            Assert.Empty(busy);
            Assert.Equal([A], unknown);
        }

        // Once the gap stays inside Monday the person is known (and free).
        var (lateBusy, lateUnknown) = MeetingSlotFinder.Check(At(Monday, 4), At(Monday, 5), 240, withSunday);
        Assert.Empty(lateBusy);
        Assert.Empty(lateUnknown);

        // The other edge: a gap reaching past midnight into Tuesday is unknown; one ending exactly at midnight is not.
        Assert.Equal([A], MeetingSlotFinder.Check(At(Monday, 22), At(Monday, 23), 240, withoutSunday).Unknown);
        Assert.Empty(MeetingSlotFinder.Check(At(Monday, 19), At(Monday, 20), 240, withoutSunday).Unknown);
    }
}
