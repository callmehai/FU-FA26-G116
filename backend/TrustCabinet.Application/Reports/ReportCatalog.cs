using System.Globalization;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Reports;

/// <summary>A row of one table joined to the meeting it belongs to.</summary>
internal sealed record ReportFact<TRow>(ReportMeetingRow Meeting, TRow Row);

/// <summary>What a row is grouped under: the value, its words, and where it sorts (<see cref="Last"/>: after the rest).</summary>
internal sealed record ReportGroupKey(string Key, string Label, string Order, bool Last = false);

/// <summary>
/// A column rows are grouped by. <see cref="Values"/> is set when the column has a fixed list of values: it can then
/// filter as well. <see cref="Every"/> lists groups shown even when empty (every month of the period).
/// </summary>
internal sealed record ReportDimension<T>(string Key, string Label, Func<T, ReportGroupKey> Of,
    IReadOnlyDictionary<string, string>? Values = null, Func<ReportPeriod, IEnumerable<ReportGroupKey>>? Every = null);

/// <summary>A figure computed over the rows of a group; null when there is nothing to average.</summary>
internal sealed record ReportMeasure<T>(string Key, string Label, string Format, Func<IReadOnlyCollection<T>, double?> Of);

/// <summary>A query that passed <see cref="IReportDefinition.Parse"/>.</summary>
internal sealed record ParsedReportQuery(string GroupBy, string Sort, int? Top, IReadOnlyList<KeyValuePair<string, string>> Filters);

internal interface IReportDefinition
{
    string Entity { get; }
    string Label { get; }

    /// <summary>The Excel file's name: <c>bao-cao-{FileSlug}_{from}_{to}.xlsx</c>.</summary>
    string FileSlug { get; }

    /// <summary>The query, or null when a group, sort, top or filter is not one this table has (MSG484).</summary>
    ParsedReportQuery? Parse(ReportQueryInput input);
}

/// <summary>
/// One table the report can be read from: its columns to group and filter by and the figures it counts. Rows are grouped,
/// sorted and cut here, over what <see cref="IReportFactQuery"/> read and <see cref="ReportService"/> let the caller see.
/// </summary>
internal sealed class ReportDefinition<TRow>(string entity, string label, string fileSlug, string defaultGroup,
    IReadOnlyList<ReportDimension<ReportFact<TRow>>> dimensions, IReadOnlyList<ReportMeasure<ReportFact<TRow>>> measures)
    : IReportDefinition
{
    public string Entity => entity;
    public string Label => label;
    public string FileSlug => fileSlug;

    public ParsedReportQuery? Parse(ReportQueryInput input)
    {
        var groupBy = string.IsNullOrEmpty(input.GroupBy) ? defaultGroup : input.GroupBy;
        if (dimensions.All(d => d.Key != groupBy)) return null;
        // By default a timeline reads in time order, anything else biggest first.
        var sort = string.IsNullOrEmpty(input.Sort) ? (groupBy == ReportCatalog.MonthKey ? ReportCatalog.GroupSort : measures[0].Key) : input.Sort;
        if (sort != ReportCatalog.GroupSort && measures.All(m => m.Key != sort)) return null;
        int? top = null;
        if (!string.IsNullOrEmpty(input.Top))
        {
            if (!int.TryParse(input.Top, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n is < 1 or > ReportCatalog.MaxTop)
                return null;
            top = n;
        }
        var filters = new List<KeyValuePair<string, string>>();
        foreach (var (key, value) in input.Filters)
        {
            if (string.IsNullOrEmpty(value)) continue;
            var dimension = dimensions.FirstOrDefault(d => d.Key == key);
            if (dimension?.Values is not { } values || !values.ContainsKey(value)) return null;
            filters.Add(new(key, value));
        }
        return new(groupBy, sort, top, filters);
    }

    public ReportTable Run(IReadOnlyList<ReportFact<TRow>> facts, ParsedReportQuery query, ReportPeriod period, DateTimeOffset now)
    {
        var byKey = dimensions.ToDictionary(d => d.Key);
        var kept = facts.Where(f => query.Filters.All(x => byKey[x.Key].Of(f).Key == x.Value)).ToList();
        var dimension = byKey[query.GroupBy];

        var groups = kept.GroupBy(f => dimension.Of(f).Key)
            .Select(g => (Key: dimension.Of(g.First()), Values: Values(g.ToList())))
            .ToList();
        foreach (var key in dimension.Every?.Invoke(period) ?? [])
            if (groups.All(g => g.Key.Key != key.Key)) groups.Add((key, Values([])));

        var natural = groups.OrderBy(g => g.Key.Last).ThenBy(g => g.Key.Order, StringComparer.InvariantCulture);
        var ordered = query.Sort == ReportCatalog.GroupSort ? natural
            : groups.OrderByDescending(g => g.Values[query.Sort] ?? double.NegativeInfinity)
                .ThenBy(g => g.Key.Last).ThenBy(g => g.Key.Order, StringComparer.InvariantCulture);
        var rows = ordered.Take(query.Top ?? int.MaxValue)
            .Select((g, i) => new ReportRow(i + 1, g.Key.Key, g.Key.Label, g.Values)).ToArray();

        return new ReportTable(entity, label, dimension.Key, dimension.Label, query.Sort, query.Top, period.From, period.To,
            now.ToOffset(ReportPeriod.Vietnam),
            query.Filters.Select(x => new ReportFilter(x.Key, byKey[x.Key].Label, x.Value, byKey[x.Key].Values![x.Value])).ToArray(),
            measures.Select(m => new ReportColumn(m.Key, m.Label, m.Format)).ToArray(),
            groups.Count, Values(kept), rows);
    }

    private Dictionary<string, double?> Values(IReadOnlyCollection<ReportFact<TRow>> facts)
        => measures.ToDictionary(m => m.Key, m => m.Of(facts));
}

/// <summary>
/// The tables of the report query (supervisor review 22/11/2026): one per main table of the model — meetings, their
/// participants and attendance, votes, documents, minutes and recordings — each with the columns a manager groups and
/// filters by. Every table also groups by month, meeting status, mode, sensitivity and meeting.
/// </summary>
internal static class ReportCatalog
{
    public const string MonthKey = "month";
    public const string GroupSort = "group";
    public const int MaxTop = 500;

    private const string Count = "count";
    private const string Percent = "percent";
    private const string Hours = "hours";
    private const string Megabytes = "megabytes";

    public static readonly ReportDefinition<ReportMeetingRow> Meetings = new("meetings", "Cuộc họp", "cuoc-hop", MonthKey,
        [
            .. Common<ReportMeetingRow>(),
            Person<ReportMeetingRow>("chair", "Chủ toạ", f => f.Meeting.ChairId, f => f.Meeting.ChairName, "Chưa có Chủ toạ"),
            Person<ReportMeetingRow>("creator", "Người tạo", f => f.Meeting.CreatedById, f => f.Meeting.CreatedByName, ""),
        ],
        [
            new("count", "Số cuộc họp", Count, f => f.Count),
            new("ended", "Đã họp", Count, f => f.Count(x => x.Meeting.Status == MeetingStatus.Ended)),
            new("cancelled", "Đã huỷ", Count, f => f.Count(x => x.Meeting.Status == MeetingStatus.Cancelled)),
            new("hours", "Giờ họp", Hours, f => Round(f.Where(x => Held(x.Meeting))
                .Sum(x => (x.Meeting.ActualEndedAt!.Value - x.Meeting.ActualStartedAt!.Value).TotalHours), 1)),
            // BR-151: present over invitees, averaged over Ended meetings that had attendance taken.
            new("attendance", "Tỷ lệ có mặt", Percent, f => Average(f
                .Where(x => x.Meeting.Status == MeetingStatus.Ended && x.Meeting.Marked > 0 && x.Meeting.Invitees > 0)
                .Select(x => (double)x.Meeting.Present / x.Meeting.Invitees), 4)),
        ]);

    public static readonly ReportDefinition<ReportParticipantRow> Participants = new("participants", "Người dự họp",
        "thanh-phan", "person",
        [
            .. Common<ReportParticipantRow>(),
            new("person", "Người dự", f => new(f.Row.PersonKey, f.Row.PersonName, f.Row.PersonName)),
            Enum<ReportParticipantRow>("role", "Vai trò", ReportLabels.Role, f => f.Row.Role.ToString()),
            Enum<ReportParticipantRow>("attendance", "Điểm danh", ReportLabels.Attendance, f => f.Row.Attendance.ToString()),
            Enum<ReportParticipantRow>("response", "Trả lời mời", ReportLabels.Response, f => f.Row.Response.ToString()),
        ],
        [
            new("count", "Lượt mời", Count, f => f.Count),
            new("present", "Có mặt", Count, f => f.Count(x => x.Row.Attendance == AttendanceStatus.Present)),
            new("absent", "Vắng", Count, f => f.Count(x => x.Row.Attendance is AttendanceStatus.Absent or AttendanceStatus.Excused)),
            // Of the times the person was marked at all: a meeting nobody took attendance of says nothing about them.
            new("rate", "Tỷ lệ có mặt", Percent, f => Ratio(f.Count(x => x.Row.Attendance == AttendanceStatus.Present),
                f.Count(x => x.Row.Attendance != AttendanceStatus.Unknown))),
        ]);

    public static readonly ReportDefinition<ReportVoteRow> Votes = new("votes", "Biểu quyết", "bieu-quyet", "conclusion",
        [
            .. Common<ReportVoteRow>(),
            Enum<ReportVoteRow>("conclusion", "Kết luận", ReportLabels.Conclusion, f => f.Row.Conclusion?.ToString() ?? ReportLabels.None),
            Enum<ReportVoteRow>("voteStatus", "Trạng thái biểu quyết", ReportLabels.VoteStatus, f => f.Row.Status.ToString()),
            Enum<ReportVoteRow>("ballot", "Hình thức bỏ phiếu", ReportLabels.Ballot, f => f.Row.IsAnonymous ? "anonymous" : "public"),
        ],
        [
            new("count", "Số biểu quyết", Count, f => f.Count),
            new("passed", "Thông qua", Count, f => f.Count(x => x.Row.Conclusion == VoteConclusion.Passed)),
            new("notPassed", "Không thông qua", Count, f => f.Count(x => x.Row.Conclusion == VoteConclusion.NotPassed)),
            new("turnout", "Tỷ lệ bỏ phiếu", Percent, f => Ratio(
                f.Where(x => x.Row.Status != VotingStatus.Draft).Sum(x => x.Row.Ballots),
                f.Where(x => x.Row.Status != VotingStatus.Draft).Sum(x => x.Row.EligibleCount))),
        ]);

    public static readonly ReportDefinition<ReportDocumentRow> Documents = new("documents", "Tài liệu", "tai-lieu", "uploader",
        [
            .. Common<ReportDocumentRow>(),
            Person<ReportDocumentRow>("uploader", "Người đưa lên", f => f.Row.AddedById, f => f.Row.AddedByName, ""),
            Enum<ReportDocumentRow>("fileType", "Loại tệp", ReportLabels.FileType, f => FileType(f.Row.FileName)),
        ],
        [
            new("count", "Số tài liệu", Count, f => f.Count),
            new("pages", "Số trang", Count, f => f.Sum(x => x.Row.PageCount ?? 0)),
            new("size", "Dung lượng (MB)", Megabytes, f => Round(f.Sum(x => x.Row.ByteSize) / 1_000_000d, 1)),
            new("comments", "Góp ý", Count, f => f.Sum(x => x.Row.Comments)),
            new("reads", "Xác nhận đã đọc", Count, f => f.Sum(x => x.Row.ReadConfirmations)),
        ]);

    public static readonly ReportDefinition<ReportMinutesRow> Minutes = new("minutes", "Biên bản", "bien-ban", "minutesStatus",
        [
            .. Common<ReportMinutesRow>(),
            Enum<ReportMinutesRow>("minutesStatus", "Trạng thái biên bản", ReportLabels.MinutesStatus, f => f.Row.Status.ToString()),
            Person<ReportMinutesRow>("author", "Người lập", f => f.Row.CreatedById, f => f.Row.CreatedByName, ""),
        ],
        [
            new("count", "Số bản", Count, f => f.Count),
            new("issued", "Đã ban hành", Count, f => f.Count(x => x.Row.IssuedAt is not null)),
            new("waiting", "Chờ duyệt", Count, f => f.Count(x => x.Row.Status == MinutesStatus.WaitingApproval)),
            new("returned", "Bị trả lại", Count, f => f.Count(x => x.Row.Status == MinutesStatus.Rejected)),
            // BR-151: from the end of the meeting to the first issue of its minutes; a correction does not restart it.
            new("turnaround", "Giờ tới ban hành (TB)", Hours, f => Average(f
                .Where(x => x.Row.IssuedAt is not null && x.Meeting.ActualEndedAt is not null)
                .GroupBy(x => x.Meeting.Id)
                .Select(g => (g.Min(x => x.Row.IssuedAt!.Value) - g.First().Meeting.ActualEndedAt!.Value).TotalHours), 1)),
        ]);

    public static readonly ReportDefinition<ReportRecordingRow> Recordings = new("recordings", "Bản ghi", "ban-ghi", MonthKey,
        [
            .. Common<ReportRecordingRow>(),
            Enum<ReportRecordingRow>("source", "Nguồn ghi", ReportLabels.Source, f => f.Row.Source.ToString()),
            Enum<ReportRecordingRow>("transcript", "Gỡ băng", ReportLabels.Transcript, f => f.Row.Transcript?.ToString() ?? ReportLabels.None),
        ],
        [
            new("count", "Số bản ghi", Count, f => f.Count),
            new("hours", "Giờ ghi âm", Hours, f => RecordedHours(f.Select(x => x.Row))),
            new("transcribed", "Giờ đã gỡ băng", Hours, f => TranscribedHours(f.Select(x => x.Row))),
        ]);

    public static readonly IReadOnlyDictionary<string, IReportDefinition> All = new IReportDefinition[]
        { Meetings, Participants, Votes, Documents, Minutes, Recordings }.ToDictionary(d => d.Entity);

    /// <summary>
    /// Hours of audio that can be played. A combined recording is a mix of the others (ADR-0061), so it would count the
    /// same speech twice.
    /// </summary>
    public static double RecordedHours(IEnumerable<ReportRecordingRow> rows) => Round(rows
        .Where(r => r.Status == RecordingStatus.Available && r.Source != RecordingSource.Combined)
        .Sum(r => r.DurationMs) / 3_600_000d, 1);

    public static double TranscribedHours(IEnumerable<ReportRecordingRow> rows) => Round(rows
        .Where(r => r.Status == RecordingStatus.Available && r.Transcript == TranscriptStatus.Completed)
        .Sum(r => r.DurationMs) / 3_600_000d, 1);

    /// <summary>Month, meeting status, mode, sensitivity and the meeting itself: every table has them.</summary>
    private static ReportDimension<ReportFact<T>>[] Common<T>() =>
    [
        new(MonthKey, "Tháng", f => MonthOf(f.Meeting.PlannedStart),
            Every: period => period.EachMonth().Select(m => MonthKeyOf(ReportPeriod.Month(m.Year, m.Month)))),
        Enum<T>("status", "Trạng thái cuộc họp", ReportLabels.MeetingStatus, f => f.Meeting.Status.ToString()),
        Enum<T>("mode", "Hình thức", ReportLabels.Mode, f => f.Meeting.Mode.ToString()),
        Enum<T>("sensitivity", "Mức độ", ReportLabels.Sensitivity, f => f.Meeting.Sensitivity.ToString()),
        // Names a meeting the caller may read anyway, ordered by when it was planned.
        new("meeting", "Cuộc họp", f => new(f.Meeting.Id.ToString(), f.Meeting.Title,
            f.Meeting.PlannedStart.UtcDateTime.ToString("o", CultureInfo.InvariantCulture) + f.Meeting.Id)),
    ];

    private static ReportDimension<ReportFact<T>> Enum<T>(string key, string label, IReadOnlyDictionary<string, string> values,
        Func<ReportFact<T>, string> of)
    {
        var position = values.Keys.Select((k, i) => (k, i)).ToDictionary(p => p.k, p => p.i.ToString("D3", CultureInfo.InvariantCulture));
        return new(key, label, f =>
        {
            var value = of(f);
            return new(value, values.GetValueOrDefault(value, value), position.GetValueOrDefault(value, "999"));
        }, values);
    }

    private static ReportDimension<ReportFact<T>> Person<T>(string key, string label, Func<ReportFact<T>, Guid?> id,
        Func<ReportFact<T>, string?> name, string nobody)
        => new(key, label, f => id(f) is { } person
            ? new(person.ToString(), name(f) ?? "", name(f) ?? "")
            : new(ReportLabels.None, nobody, "", Last: true));

    private static ReportGroupKey MonthOf(DateTimeOffset plannedStart)
    {
        var local = plannedStart.ToOffset(ReportPeriod.Vietnam);
        return MonthKeyOf(ReportPeriod.Month(local.Year, local.Month));
    }

    private static ReportGroupKey MonthKeyOf(string month) => new(month, ReportLabels.Month(month), month);

    private static bool Held(ReportMeetingRow m)
        => m.Status == MeetingStatus.Ended && m.ActualStartedAt is not null && m.ActualEndedAt is not null;

    /// <summary>The kind of file people know it by, from its extension.</summary>
    internal static string FileType(string fileName)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        return extension switch
        {
            "pdf" => "pdf",
            "doc" or "docx" or "odt" or "rtf" => "word",
            "xls" or "xlsx" or "ods" or "csv" => "excel",
            "ppt" or "pptx" or "odp" => "powerpoint",
            "png" or "jpg" or "jpeg" or "gif" or "webp" or "bmp" => "image",
            _ => "other",
        };
    }

    private static double? Ratio(double part, double whole) => whole <= 0 ? null : Math.Round(part / whole, 4);

    private static double? Average(IEnumerable<double> values, int digits)
    {
        var list = values.ToArray();
        return list.Length == 0 ? null : Math.Round(list.Average(), digits);
    }

    private static double Round(double value, int digits) => Math.Round(value, digits);
}
