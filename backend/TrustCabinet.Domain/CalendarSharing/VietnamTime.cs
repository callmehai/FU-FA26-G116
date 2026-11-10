namespace TrustCabinet.Domain.CalendarSharing;

/// <summary>
/// The calendar the people of the organisation live in. "Today" for a calendar share is Vietnam's date, never the
/// server's (the test server runs in UTC, where midnight is 07:00 in Hanoi).
/// </summary>
public static class VietnamTime
{
    public static readonly TimeZoneInfo Zone = FindZone();

    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);

    /// <summary>The instant <paramref name="time"/> on <paramref name="date"/> happens in Vietnam.</summary>
    public static DateTimeOffset At(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local));
    }

    public static DateTimeOffset ToLocal(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone);

    /// <summary>The Monday of the week <paramref name="date"/> falls in.</summary>
    public static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static TimeZoneInfo FindZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Try the next name: Linux knows the IANA id, older Windows only the Windows one.
            }
        }

        // Vietnam has kept UTC+7 without daylight saving since 1975.
        return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT");
    }
}
