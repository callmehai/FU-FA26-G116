using System.Globalization;
using TrustCabinet.Application.Reports;
using static TrustCabinet.Infrastructure.Reports.MeetingReportXlsx;

namespace TrustCabinet.Infrastructure.Reports;

/// <summary>
/// One report query table as an Excel file, in the same hand-written workbook as <see cref="MeetingReportXlsx"/>: what
/// was asked (table, period, grouping, filters, time of export) at the top, then one row per group with its rank, and
/// the total over every row that passed the filters. Numbers stay numbers; a percentage keeps its percent format.
/// </summary>
public static class ReportTableXlsx
{
    private const string NoData = "Chưa có dữ liệu";

    public static byte[] Build(ReportTable table)
    {
        var rows = new List<object?[]>
        {
            new object?[] { new Cell($"Báo cáo {table.EntityLabel.ToLowerInvariant()}", BoldStyle) },
            new object?[] { "Khoảng thời gian", $"{ReportLabels.Month(table.From)} – {ReportLabels.Month(table.To)}" },
            new object?[] { "Nhóm theo", table.GroupLabel },
            new object?[]
            {
                "Bộ lọc",
                table.Filters.Count == 0 ? "Không" : string.Join("; ", table.Filters.Select(f => $"{f.Label}: {f.ValueLabel}")),
            },
            new object?[] { "Thời điểm xuất", table.GeneratedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " (GMT+7)" },
            Array.Empty<object?>(),
        };
        rows.Add([new Cell("#", BoldStyle), new Cell(table.GroupLabel, BoldStyle),
            .. table.Columns.Select(c => (object?)new Cell(c.Label, BoldStyle))]);
        rows.AddRange(table.Rows.Select(r => (object?[])[r.Rank, r.Label, .. table.Columns.Select(c => Value(c, r.Values))]));
        rows.Add([null, new Cell("Tổng", BoldStyle), .. table.Columns.Select(c => Value(c, table.Total))]);

        return Workbook(SheetName(table.EntityLabel), SheetXml(rows,
            """<col min="1" max="1" width="6" customWidth="1"/><col min="2" max="2" width="48" customWidth="1"/><col min="3" max="8" width="20" customWidth="1"/>"""));
    }

    private static object? Value(ReportColumn column, IReadOnlyDictionary<string, double?> values)
        => values.GetValueOrDefault(column.Key) switch
        {
            null => NoData,
            { } v when column.Format == "percent" => new Cell(v, PercentStyle),
            { } v when column.Format is "hours" or "megabytes" => new Cell(v, DecimalStyle),
            { } v => (int)Math.Round(v),
        };

    /// <summary>Excel takes at most 31 characters and none of <c>: \ / ? * [ ]</c> in a sheet name.</summary>
    private static string SheetName(string label)
    {
        var name = new string(label.Where(c => !":\\/?*[]".Contains(c)).ToArray());
        return name.Length <= 31 ? name : name[..31];
    }
}
