using System.Globalization;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Domain.CalendarSharing;

/// <summary>
/// The limits and defaults of a meeting-time search (BR-421, BR-422, ADR-0058 §7) and of the slot sent back to create a
/// draft (UC-85b). No database: whether each invitee exists and is active is checked by the service.
/// </summary>
public static class MeetingSlotRules
{
    /// <summary>D14: at most 10 people besides the finder; "Tính cả tôi" does not take one of the places.</summary>
    public const int MaxPeople = 10;
    public const int MinDuration = 15;
    public const int MaxDuration = 480;
    public const int DefaultDuration = 120;
    public const int MaxRangeDays = 31;
    /// <summary>A search starts at most this many days ahead, so dates near the end of the calendar never reach the
    /// arithmetic (review of PR #319). A share covers at most 365 days (BR-431), so nothing later could be known anyway.</summary>
    public const int MaxDaysAhead = 365;
    public const int MaxBuffer = 240;
    public const int DefaultBuffer = 30;
    public const int MinStep = 5;
    public const int MaxStep = 120;
    public const int DefaultStep = 30;
    /// <summary>Durations, buffers and steps are whole multiples of the smallest step, so slots line up.</summary>
    public const int Grain = 5;
    /// <summary>P5: a search returns at most this many slots, in time order.</summary>
    public const int MaxSlots = 200;

    public static readonly TimeOnly DefaultWorkStart = new(8, 0);
    public static readonly TimeOnly DefaultWorkEnd = new(17, 0);
    public static readonly TimeOnly DefaultLunchStart = new(12, 0);
    public static readonly TimeOnly DefaultLunchEnd = new(13, 0);

    public const string UserIdsError = "Chọn từ 1 đến 10 người, không trùng và không chọn chính mình.";
    public const string UserUnavailableError = "Người dùng không tồn tại hoặc đã ngừng hoạt động.";
    public const string DurationError = "Thời lượng từ 15 phút đến 8 giờ, bội số của 5 phút.";
    public const string DurationTooLongError = "Thời lượng dài hơn một buổi làm việc. Rút ngắn hoặc bỏ nghỉ trưa.";
    public const string FromDateError = "Ngày bắt đầu từ hôm nay đến tối đa 365 ngày sau.";
    public const string ToDateError = "Khoảng ngày tối đa 31 ngày, ngày kết thúc không trước ngày bắt đầu.";
    public const string WeekdaysError = "Chọn ít nhất một ngày từ thứ Hai đến thứ Sáu.";
    public const string WorkHoursError = "Giờ làm việc không hợp lệ.";
    public const string LunchError = "Giờ nghỉ trưa phải nằm trong giờ làm việc.";
    public const string BufferError = "Cách cuộc họp khác từ 0 đến 4 giờ, bội số của 5 phút.";
    public const string StepError = "Làm tròn giờ bắt đầu từ 5 phút đến 2 giờ, bội số của 5 phút.";
    public const string SlotTimeError = "Thời gian không hợp lệ.";
    public const string SlotPastError = "Thời gian bắt đầu phải sau thời điểm hiện tại.";
    public const string SlotTooFarError = "Thời gian bắt đầu nằm ngoài khoảng tìm giờ họp.";
    public const string SlotEndError = "Thời gian kết thúc phải sau thời gian bắt đầu, dài tối đa 8 giờ.";
    public const string AcceptedBusyError = "Danh sách người bận chỉ gồm người trong khung đã chọn.";

    /// <summary>
    /// UC-85a step 2: applies the defaults and checks every field (BR-421, BR-422). Errors are keyed by the request's
    /// field name so the page can highlight them (MSG444).
    /// </summary>
    public static bool TryValidate(SlotSearchInput input, Guid finderId, DateOnly today, out SlotSearch? search,
        out IReadOnlyDictionary<string, string> errors)
    {
        var fieldErrors = new Dictionary<string, string>(StringComparer.Ordinal);
        search = null;

        var userIds = input.UserIds ?? [];
        if (!ValidInvitees(userIds, finderId)) fieldErrors["userIds"] = UserIdsError;

        var duration = input.DurationMinutes ?? DefaultDuration;
        if (duration is < MinDuration or > MaxDuration || duration % Grain != 0) fieldErrors["durationMinutes"] = DurationError;

        var from = input.FromDate ?? today;
        var fromOk = from >= today && from <= today.AddDays(MaxDaysAhead);
        if (!fromOk) fieldErrors["fromDate"] = FromDateError;
        var to = input.ToDate ?? (fromOk ? DefaultToDate(from) : from);
        if (to < from || CalendarShareRules.Length(from, to) > MaxRangeDays) fieldErrors["toDate"] = ToDateError;

        var weekdays = input.Weekdays ?? [1, 2, 3, 4, 5];
        if (weekdays.Count == 0 || weekdays.Distinct().Count() != weekdays.Count || weekdays.Any(d => d is < 1 or > 5))
            fieldErrors["weekdays"] = WeekdaysError;

        var workStartOk = TryParseClock(input.WorkStart, DefaultWorkStart, out var workStart);
        var workEndOk = TryParseClock(input.WorkEnd, DefaultWorkEnd, out var workEnd);
        if (!workStartOk) fieldErrors["workStart"] = WorkHoursError;
        if (!workEndOk || (workStartOk && workEnd <= workStart)) fieldErrors["workEnd"] = WorkHoursError;
        var hoursOk = !fieldErrors.ContainsKey("workStart") && !fieldErrors.ContainsKey("workEnd");

        TimeOnly? lunchStart = null, lunchEnd = null;
        var lunchOk = true;
        if (input.LunchBreak ?? true)
        {
            var lunchStartOk = TryParseClock(input.LunchStart, DefaultLunchStart, out var parsedLunchStart);
            var lunchEndOk = TryParseClock(input.LunchEnd, DefaultLunchEnd, out var parsedLunchEnd);
            if (!lunchStartOk || (hoursOk && parsedLunchStart < workStart))
                fieldErrors["lunchStart"] = LunchError;
            if (!lunchEndOk || (lunchStartOk && parsedLunchEnd <= parsedLunchStart) || (hoursOk && parsedLunchEnd > workEnd))
                fieldErrors["lunchEnd"] = LunchError;
            lunchOk = !fieldErrors.ContainsKey("lunchStart") && !fieldErrors.ContainsKey("lunchEnd");
            lunchStart = parsedLunchStart;
            lunchEnd = parsedLunchEnd;
        }

        var buffer = input.BufferMinutes ?? DefaultBuffer;
        if (!ValidBuffer(buffer)) fieldErrors["bufferMinutes"] = BufferError;

        var step = input.StepMinutes ?? DefaultStep;
        if (step is < MinStep or > MaxStep || step % Grain != 0) fieldErrors["stepMinutes"] = StepError;

        // The ticket's "end of the working day after start + duration": the meeting must fit in one block of working
        // time, the morning or the afternoon when there is a lunch break (a slot never overlaps lunch, BR-422), starting on
        // the step grid — otherwise the search could only ever come back empty.
        if (hoursOk && lunchOk && !fieldErrors.ContainsKey("durationMinutes") && !fieldErrors.ContainsKey("stepMinutes")
            && !FitsOneBlock(workStart, workEnd, lunchStart, lunchEnd, duration, step))
            fieldErrors["durationMinutes"] = DurationTooLongError;

        errors = fieldErrors;
        if (fieldErrors.Count > 0) return false;

        search = new SlotSearch(
            [.. userIds], input.IncludeMe ?? false, duration, from, to,
            [.. weekdays.Order().Select(d => (DayOfWeek)(d % 7))],
            workStart, workEnd, lunchStart, lunchEnd, buffer, step);
        return true;
    }

    /// <summary>1 to 10 distinct people, none empty and never the finder (BR-421, D14).</summary>
    public static bool ValidInvitees(IReadOnlyList<Guid> userIds, Guid finderId)
        => userIds.Count is >= 1 and <= MaxPeople
            && userIds.Distinct().Count() == userIds.Count
            && !userIds.Contains(Guid.Empty)
            && !userIds.Contains(finderId);

    /// <summary>"Cách cuộc họp khác" (BR-422): 0 to 4 hours in steps of 5 minutes.</summary>
    public static bool ValidBuffer(int minutes) => minutes is >= 0 and <= MaxBuffer && minutes % Grain == 0;

    /// <summary>The Friday of the week <paramref name="from"/> falls in; from a Saturday or Sunday, the next Friday.</summary>
    public static DateOnly DefaultToDate(DateOnly from)
    {
        var friday = VietnamTime.Monday(from).AddDays(4);
        return from > friday ? friday.AddDays(7) : friday;
    }

    /// <summary>"HH:mm", 24-hour. A missing value takes <paramref name="fallback"/>.</summary>
    public static bool TryParseClock(string? value, TimeOnly fallback, out TimeOnly time)
    {
        if (value is null)
        {
            time = fallback;
            return true;
        }

        return TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }

    /// <summary>An ISO 8601 timestamp with an offset — exactly what UC-14 accepts for planned times (one parser for both).</summary>
    public static bool TryParseTimestamp(string? value, out DateTimeOffset timestamp)
        => MeetingRules.TryParseTimestamp(value, out timestamp);

    /// <summary>
    /// UC-85b, before any arithmetic on the slot: it starts after now, on or before the last day a search can reach, and
    /// lasts at most 8 hours (BR-421). Keeps dates near either end of the calendar away from start − buffer / end + buffer,
    /// and keeps the recheck from being asked about past days. Errors are keyed like UC-14's (MSG31).
    /// </summary>
    public static void CheckSlotTimes(DateTimeOffset start, DateTimeOffset end, DateTimeOffset now,
        IDictionary<string, string> errors)
    {
        // Compared as instants: converting a time near the end of the calendar to Vietnam time would itself overflow.
        var pastLastSearchableDay = VietnamTime.At(VietnamTime.Today(now).AddDays(MaxDaysAhead + MaxRangeDays), TimeOnly.MinValue);
        if (start <= now) errors["plannedStartTime"] = SlotPastError;
        else if (start >= pastLastSearchableDay) errors["plannedStartTime"] = SlotTooFarError;
        if (end <= start || end - start > TimeSpan.FromMinutes(MaxDuration)) errors["plannedEndTime"] = SlotEndError;
    }

    /// <summary>Whether some start on the step grid (counted from midnight) leaves room for the meeting in one block.</summary>
    private static bool FitsOneBlock(TimeOnly workStart, TimeOnly workEnd, TimeOnly? lunchStart, TimeOnly? lunchEnd,
        int duration, int step)
    {
        bool Fits(int blockStart, int blockEnd) => (blockStart + step - 1) / step * step + duration <= blockEnd;
        return lunchStart is { } ls && lunchEnd is { } le
            ? Fits(Minutes(workStart), Minutes(ls)) || Fits(Minutes(le), Minutes(workEnd))
            : Fits(Minutes(workStart), Minutes(workEnd));
    }

    internal static int Minutes(TimeOnly time) => time.Hour * 60 + time.Minute;

}
