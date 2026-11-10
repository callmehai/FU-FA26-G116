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
                var (busy, unknown) = Check(start, end, search.BufferMinutes, persons);
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
            else if (person.Busy.Any(b => b.Start < to && b.End > from))
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
