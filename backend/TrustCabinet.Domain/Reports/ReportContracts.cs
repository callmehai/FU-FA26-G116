using System.Globalization;

namespace TrustCabinet.Domain.Reports;

/// <summary>
/// SCRUM-38 — a report period (BR-433): whole calendar months in Vietnam time, the start not after the end, at most
/// <see cref="MaxMonths"/> months. Months are "yyyy-MM".
/// </summary>
public readonly record struct ReportPeriod(int FromYear, int FromMonth, int ToYear, int ToMonth)
{
    public const int MaxMonths = 24;
    public const int DefaultMonths = 12;

    /// <summary>Vietnam keeps +07:00 all year, with no daylight saving, so a fixed offset is exact.</summary>
    public static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    public string From => Month(FromYear, FromMonth);
    public string To => Month(ToYear, ToMonth);
    public int Months => (ToYear * 12 + ToMonth) - (FromYear * 12 + FromMonth) + 1;

    /// <summary>The first instant of the start month in Vietnam time.</summary>
    public DateTimeOffset Start => new(FromYear, FromMonth, 1, 0, 0, 0, Vietnam);

    /// <summary>The first instant after the end month in Vietnam time (exclusive).</summary>
    public DateTimeOffset End => new DateTimeOffset(ToYear, ToMonth, 1, 0, 0, 0, Vietnam).AddMonths(1);

    /// <summary>Every month of the period, in order.</summary>
    public IEnumerable<(int Year, int Month)> EachMonth()
    {
        for (var at = new DateTime(FromYear, FromMonth, 1); at <= new DateTime(ToYear, ToMonth, 1); at = at.AddMonths(1))
            yield return (at.Year, at.Month);
    }

    public static string Month(int year, int month) => $"{year:D4}-{month:D2}";

    /// <summary>
    /// Reads <paramref name="from"/> and <paramref name="to"/>. Both missing: the 12 months ending with the current
    /// month. Only one given, a month that does not parse, or a period that breaks BR-433: null.
    /// </summary>
    public static ReportPeriod? Parse(string? from, string? to, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(from) && string.IsNullOrEmpty(to))
        {
            var today = now.ToOffset(Vietnam);
            var start = new DateTime(today.Year, today.Month, 1).AddMonths(-(DefaultMonths - 1));
            return new ReportPeriod(start.Year, start.Month, today.Year, today.Month);
        }
        if (!TryMonth(from, out var f) || !TryMonth(to, out var t)) return null;
        var period = new ReportPeriod(f.Year, f.Month, t.Year, t.Month);
        return period.Months is >= 1 and <= MaxMonths ? period : null;
    }

    private static bool TryMonth(string? value, out DateTime month)
    {
        month = default;
        return value is { Length: 7 }
            && DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out month)
            && month.Year >= 2000;
    }
}

public sealed record MonthCount(string Month, int Count);
public sealed record StatusCount(string Status, int Count);
public sealed record SensitivityCount(string Sensitivity, int Count);

/// <summary>
/// BR-435. <see cref="AverageRate"/> is a fraction (0.87 is 87 %), null when no Ended meeting had attendance taken.
/// <see cref="Meetings"/> is how many meetings it is based on.
/// </summary>
public sealed record AttendanceFigure(double? AverageRate, int Meetings, int MeetingsWithoutAttendance);

/// <summary>
/// PO 09/11/2026 (Q2). Minutes from the actual start to the actual end of an Ended meeting; null when none has both.
/// </summary>
public sealed record DurationFigure(double? AverageMinutes, int Meetings, int MeetingsWithoutTimes);

/// <summary>BR-436. Hours from the actual end to the first issue of the minutes; null when none is issued yet.</summary>
public sealed record TurnaroundFigure(double? AverageHours, int Meetings, int MeetingsWithoutIssuedMinutes);

/// <summary>
/// UC-70a — counts, rates and averages only (BR-437): never a meeting's id, title, participants or content.
/// <see cref="ByMonth"/> has every month of the period and the other two every enum value, zeros included.
/// </summary>
public sealed record MeetingReport(string From, string To, DateTimeOffset GeneratedAt, int Total,
    IReadOnlyList<MonthCount> ByMonth, IReadOnlyList<StatusCount> ByStatus, IReadOnlyList<SensitivityCount> BySensitivity,
    AttendanceFigure Attendance, DurationFigure Duration, TurnaroundFigure MinutesTurnaround);

public sealed record ReportFile(string FileName, byte[] Bytes, string ContentType);

public enum ReportFailure
{
    None,
    /// <summary>BR-433 — the period does not parse, runs backwards or is longer than 24 months.</summary>
    InvalidPeriod,
    ReadFailed,
    ExportFailed,
}

public sealed record ReportOutcome(ReportFailure Failure = ReportFailure.None, MeetingReport? Report = null, ReportFile? File = null);

/// <summary>The <c>:Report Service</c> of UC-70a and UC-70b.</summary>
public interface IReportService
{
    /// <summary>UC-70a — the figures of the meetings <paramref name="actorId"/> may read whose planned start is in the period.</summary>
    Task<ReportOutcome> GetMeetingReportAsync(Guid actorId, string? from, string? to, CancellationToken ct = default);

    /// <summary>UC-70b — the same figures, computed again, as an Excel file; the export is audited (BR-438).</summary>
    Task<ReportOutcome> ExportMeetingReportAsync(Guid actorId, string? from, string? to, CancellationToken ct = default);
}
