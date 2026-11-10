namespace TrustCabinet.Domain.CalendarSharing;

/// <summary>
/// UC-85a step 3–4 without a database: the candidate slots of a search and, for each, who among the counted people is
/// free, busy or unknown. Only busy periods come in — nothing about the meetings behind them (BR-427).
/// </summary>
public static class MeetingSlotFinder
{
    /// <summary>
    /// Every start on the step grid counted from midnight (D15) inside working hours, never overlapping lunch, never in
    /// the past (BR-422), on the chosen weekdays. Common slots when some fit everyone counted; otherwise the slots with
    /// the most people free (UC-85a AF-1); nothing when nobody is counted or nobody is ever free.
    /// </summary>
    public static SlotFinding Find(SlotSearch search, DateTimeOffset now, IReadOnlyList<SlotPerson> persons)
    {
        if (persons.Count == 0) return new(SlotResultKind.None, [], false);
        persons = Sorted(persons);

        var duration = TimeSpan.FromMinutes(search.DurationMinutes);
        var workStart = MeetingSlotRules.Minutes(search.WorkStart);
        var workEnd = MeetingSlotRules.Minutes(search.WorkEnd);
        int? lunchStart = search.LunchStart is { } ls ? MeetingSlotRules.Minutes(ls) : null;
        int? lunchEnd = search.LunchEnd is { } le ? MeetingSlotRules.Minutes(le) : null;
        var firstMinute = (workStart + search.StepMinutes - 1) / search.StepMinutes * search.StepMinutes;

        var candidates = new List<FoundSlot>();
        for (var date = search.FromDate; date <= search.ToDate; date = date.AddDays(1))
        {
            if (!search.Weekdays.Contains(date.DayOfWeek)) continue;
            for (var minute = firstMinute; minute + search.DurationMinutes <= workEnd; minute += search.StepMinutes)
            {
                if (lunchStart is { } lunchFrom && lunchEnd is { } lunchTo
                    && minute < lunchTo && minute + search.DurationMinutes > lunchFrom)
                    continue;
                var start = VietnamTime.At(date, new TimeOnly(minute / 60, minute % 60));
                if (start <= now) continue;
                var end = start + duration;
                var (busy, unknown) = CheckSorted(start, end, search.BufferMinutes, persons);
                candidates.Add(new FoundSlot(date, start, end, persons.Count - busy.Count - unknown.Count, persons.Count,
                    busy, unknown));
            }
        }

        var common = candidates.Where(s => s.FreeCount == s.CountedCount).ToList();
        if (common.Count > 0) return Capped(SlotResultKind.AllFree, common);

        var most = candidates.Count == 0 ? 0 : candidates.Max(s => s.FreeCount);
        return most == 0
            ? new(SlotResultKind.None, [], false)
            : Capped(SlotResultKind.MostFree, [.. candidates.Where(s => s.FreeCount == most)]);
    }

    /// <summary>
    /// Who is busy and whose day is unknown for [<paramref name="start"/>, <paramref name="end"/>). A day outside a
    /// person's known days is unknown, never free (BR-415); otherwise a busy period closer than the buffer on either side
    /// makes them busy (BR-422, D15). Used by the search and by the recheck before a draft (UC-85b, BR-428).
    /// </summary>
    public static (IReadOnlyList<Guid> Busy, IReadOnlyList<Guid> Unknown) Check(DateTimeOffset start, DateTimeOffset end,
        int bufferMinutes, IReadOnlyList<SlotPerson> persons)
        => CheckSorted(start, end, bufferMinutes, Sorted(persons));

    /// <summary>Each person's busy periods sorted and joined, so one binary search answers "busy near this slot?".</summary>
    private static IReadOnlyList<SlotPerson> Sorted(IReadOnlyList<SlotPerson> persons)
        => [.. persons.Select(p => p with { Busy = CalendarBusyRules.Merge(p.Busy) })];

    /// <summary>
    /// Whether a sorted, non-overlapping list of busy periods has one inside [<paramref name="from"/>, <paramref name="to"/>):
    /// the first period ending after <paramref name="from"/> (ends are increasing) must start before <paramref name="to"/>.
    /// O(log n) per slot instead of scanning every period (review of SCRUM-137, worst case ~1 s per search).
    /// </summary>
    private static bool AnyOverlap(IReadOnlyList<BusyInterval> busy, DateTimeOffset from, DateTimeOffset to)
    {
        int low = 0, high = busy.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (busy[middle].End > from) high = middle;
            else low = middle + 1;
        }

        return low < busy.Count && busy[low].Start < to;
    }

    private static (IReadOnlyList<Guid> Busy, IReadOnlyList<Guid> Unknown) CheckSorted(DateTimeOffset start,
        DateTimeOffset end, int bufferMinutes, IReadOnlyList<SlotPerson> persons)
    {
        var buffer = TimeSpan.FromMinutes(bufferMinutes);
        var firstDay = DateOnly.FromDateTime(VietnamTime.ToLocal(start).DateTime);
        var lastDay = DateOnly.FromDateTime(VietnamTime.ToLocal(end.AddTicks(-1)).DateTime);
        var from = start - buffer;
        var to = end + buffer;
        var busy = new List<Guid>();
        var unknown = new List<Guid>();
        foreach (var person in persons)
        {
            if (person.Window is { } window && !(window.Contains(firstDay) && window.Contains(lastDay)))
                unknown.Add(person.Id);
            else if (AnyOverlap(person.Busy, from, to))
                busy.Add(person.Id);
        }

        return (busy, unknown);
    }

    private static SlotFinding Capped(SlotResultKind kind, List<FoundSlot> slots)
    {
        var ordered = slots.OrderBy(s => s.Start).ToList();
        return ordered.Count > MeetingSlotRules.MaxSlots
            ? new(kind, ordered.Take(MeetingSlotRules.MaxSlots).ToList(), true)
            : new(kind, ordered, false);
    }
}
