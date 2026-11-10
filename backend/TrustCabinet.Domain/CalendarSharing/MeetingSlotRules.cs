using System.Globalization;
using System.Text.RegularExpressions;

namespace TrustCabinet.Domain.CalendarSharing;

/// <summary>
/// The limits and defaults of a meeting-time search (BR-421, BR-422, ADR-0058 §7) and of the slot sent back to create a
/// draft (UC-85b). No database: whether each invitee exists and is active is checked by the service.
/// </summary>
public static partial class MeetingSlotRules
{
    /// <summary>D14: at most 10 people besides the finder; "Tính cả tôi" does not take one of the places.</summary>
    public const int MaxPeople = 10;
    public const int MinDuration = 15;
    public const int MaxDuration = 480;
    public const int DefaultDuration = 120;
    public const int MaxRangeDays = 31;
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
    public const string FromDateError = "Ngày bắt đầu không được trước hôm nay.";
    public const string ToDateError = "Khoảng ngày tối đa 31 ngày, ngày kết thúc không trước ngày bắt đầu.";
    public const string WeekdaysError = "Chọn ít nhất một ngày từ thứ Hai đến thứ Sáu.";
    public const string WorkHoursError = "Giờ làm việc không hợp lệ.";
    public const string LunchError = "Giờ nghỉ trưa phải nằm trong giờ làm việc.";
    public const string BufferError = "Cách cuộc họp khác từ 0 đến 4 giờ, bội số của 5 phút.";
    public const string StepError = "Làm tròn giờ bắt đầu từ 5 phút đến 2 giờ, bội số của 5 phút.";
    public const string SlotTimeError = "Thời gian không hợp lệ.";
    public const string SlotEndError = "Thời gian kết thúc phải sau thời gian bắt đầu.";
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
        if (from < today) fieldErrors["fromDate"] = FromDateError;
        var to = input.ToDate ?? DefaultToDate(from);
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
        // time, the morning or the afternoon when there is a lunch break (a slot never overlaps lunch, BR-422).
        if (hoursOk && lunchOk && !fieldErrors.ContainsKey("durationMinutes")
            && LongestBlockMinutes(workStart, workEnd, lunchStart, lunchEnd) < duration)
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

    /// <summary>An ISO 8601 timestamp with an offset, the same shape UC-14 accepts for planned times.</summary>
    public static bool TryParseTimestamp(string? value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        return !string.IsNullOrEmpty(value)
            && IsoTimestampPattern().IsMatch(value)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);
    }

    private static int LongestBlockMinutes(TimeOnly workStart, TimeOnly workEnd, TimeOnly? lunchStart, TimeOnly? lunchEnd)
        => lunchStart is { } ls && lunchEnd is { } le
            ? Math.Max(Minutes(ls) - Minutes(workStart), Minutes(workEnd) - Minutes(le))
            : Minutes(workEnd) - Minutes(workStart);

    internal static int Minutes(TimeOnly time) => time.Hour * 60 + time.Minute;

    [GeneratedRegex(
        @"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex IsoTimestampPattern();
}
