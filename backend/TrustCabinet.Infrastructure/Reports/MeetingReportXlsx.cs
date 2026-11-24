using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using TrustCabinet.Application.Reports;

namespace TrustCabinet.Infrastructure.Reports;

/// <summary>
/// UC-79b — the meeting report as an Excel file, written by hand as the parts a.xlsx needs (as <c>MinutesDocx</c> does
/// for Word), so no library comes in for one sheet. One sheet: the period and the time of export at the top, then the
/// tables by month, by status and by sensitivity, then the three averages (BR-010). Text is written inline, so there is no
/// shared-strings part; numbers stay numbers so the file can be worked on.
/// </summary>
public static class MeetingReportXlsx
{
    private const int Plain = 0;
    private const int Bold = 1;
    private const int Percent = 2;
    private const int Decimal = 3;

    private const string NoData = "Chưa có dữ liệu";

    public static byte[] Build(MeetingReport report) => Workbook("Báo cáo cuộc họp", Sheet(report));

    /// <summary>
    /// The parts of a one-sheet workbook around <paramref name="sheet"/>. Shared with <see cref="ReportTableXlsx"/>, so
    /// both files carry the same styles: plain, bold, a percentage with one decimal and a number with one.
    /// </summary>
    internal static byte[] Workbook(string sheetName, string sheet)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                <Default Extension="xml" ContentType="application/xml"/>
                <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                </Types>
                """);
            Add(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            Add(zip, "xl/workbook.xml", $$"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                <sheets><sheet name="{{SecurityElement.Escape(sheetName)}}" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            Add(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """);
            // Four cell formats: plain, bold, a percentage with one decimal (built-in 10 has two), and a number with one.
            Add(zip, "xl/styles.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                <numFmts count="2"><numFmt numFmtId="164" formatCode="0.0%"/><numFmt numFmtId="165" formatCode="0.0"/></numFmts>
                <fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>
                <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
                <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
                <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
                <cellXfs count="4">
                <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
                <xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>
                <xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
                <xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
                </cellXfs>
                </styleSheet>
                """);
            Add(zip, "xl/worksheets/sheet1.xml", sheet);
        }
        return buffer.ToArray();
    }

    private static string Sheet(MeetingReport report)
    {
        var rows = new List<object?[]>
        {
            new object?[] { new Cell("Báo cáo cuộc họp", Bold) },
            new object?[] { "Khoảng thời gian", $"{Display(report.From)} – {Display(report.To)}" },
            new object?[] { "Thời điểm xuất", report.GeneratedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " (GMT+7)" },
            new object?[] { "Tổng số cuộc họp", report.Total },
            Array.Empty<object?>(),
            new object?[] { new Cell("Tháng", Bold), new Cell("Số cuộc họp", Bold) },
        };
        rows.AddRange(report.ByMonth.Select(m => new object?[] { Display(m.Month), m.Count }));
        rows.Add([]);
        rows.Add([new Cell("Trạng thái", Bold), new Cell("Số cuộc họp", Bold)]);
        rows.AddRange(report.ByStatus.Select(s => new object?[] { ReportLabels.MeetingStatus.GetValueOrDefault(s.Status, s.Status), s.Count }));
        rows.Add([]);
        rows.Add([new Cell("Mức độ mật", Bold), new Cell("Số cuộc họp", Bold)]);
        rows.AddRange(report.BySensitivity.Select(s => new object?[]
            { ReportLabels.Sensitivity.GetValueOrDefault(s.Sensitivity, s.Sensitivity), s.Count }));
        rows.Add([]);
        rows.Add([new Cell("Chỉ số", Bold), new Cell("Giá trị", Bold), new Cell("Số cuộc họp được tính", Bold),
            new Cell("Số cuộc họp chưa có dữ liệu", Bold)]);
        rows.Add(["Tỷ lệ tham dự trung bình",
            report.Attendance.AverageRate is { } rate ? new Cell(rate, Percent) : NoData,
            report.Attendance.Meetings, report.Attendance.MeetingsWithoutAttendance]);
        rows.Add(["Thời lượng họp trung bình (phút)",
            report.Duration.AverageMinutes is { } minutes ? new Cell(minutes, Decimal) : NoData,
            report.Duration.Meetings, report.Duration.MeetingsWithoutTimes]);
        rows.Add(["Thời gian trung bình từ khi kết thúc họp tới khi ban hành biên bản (giờ)",
            report.MinutesTurnaround.AverageHours is { } hours ? new Cell(hours, Decimal) : NoData,
            report.MinutesTurnaround.Meetings, report.MinutesTurnaround.MeetingsWithoutIssuedMinutes]);
        rows.Add([]);
        rows.Add([new Cell("Số liệu chính", Bold), new Cell("Giá trị", Bold)]);
        rows.Add(["Biểu quyết đã đóng", report.Votes.Closed]);
        rows.Add(["Biểu quyết thông qua", report.Votes.Passed]);
        rows.Add(["Biểu quyết không thông qua", report.Votes.NotPassed]);
        rows.Add(["Biểu quyết hoãn", report.Votes.Deferred]);
        rows.Add(["Cuộc họp đã ban hành biên bản", report.Minutes.Issued]);
        rows.Add(["Biên bản chờ duyệt", report.Minutes.WaitingApproval]);
        rows.Add(["Số bản ghi", report.Recordings.Count]);
        rows.Add(["Giờ ghi âm", new Cell(report.Recordings.RecordedHours, Decimal)]);
        rows.Add(["Giờ đã gỡ băng", new Cell(report.Recordings.TranscribedHours, Decimal)]);

        return SheetXml(rows, """<col min="1" max="1" width="62" customWidth="1"/><col min="2" max="2" width="22" customWidth="1"/><col min="3" max="4" width="28" customWidth="1"/>""");
    }

    /// <summary>A worksheet of <paramref name="rows"/> (an empty row is left blank) with the given column widths.</summary>
    internal static string SheetXml(List<object?[]> rows, string cols)
    {
        var data = new StringBuilder();
        for (var r = 0; r < rows.Count; r++)
        {
            if (rows[r].Length == 0) continue;
            data.Append(CultureInfo.InvariantCulture, $"""<row r="{r + 1}">""");
            for (var c = 0; c < rows[r].Length; c++) data.Append(CellXml(rows[r][c], $"{Column(c)}{r + 1}"));
            data.Append("</row>");
        }
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
            <cols>{cols}</cols>
            <sheetData>{data}</sheetData>
            </worksheet>
            """;
    }

    /// <summary>A, B, … Z, then AA: a report table has at most a handful of columns, but the name never breaks.</summary>
    private static string Column(int index) => index < 26 ? $"{(char)('A' + index)}" : $"{Column(index / 26 - 1)}{(char)('A' + index % 26)}";

    internal sealed record Cell(object Value, int Style);

    internal const int PlainStyle = Plain;
    internal const int BoldStyle = Bold;
    internal const int PercentStyle = Percent;
    internal const int DecimalStyle = Decimal;

    private static string CellXml(object? value, string reference)
    {
        var (content, style) = value is Cell cell ? (cell.Value, cell.Style) : (value, Plain);
        var s = style == Plain ? "" : $" s=\"{style}\"";
        return content switch
        {
            null => "",
            int n => $"""<c r="{reference}"{s}><v>{n.ToString(CultureInfo.InvariantCulture)}</v></c>""",
            double d => $"""<c r="{reference}"{s}><v>{d.ToString("R", CultureInfo.InvariantCulture)}</v></c>""",
            _ => $"""<c r="{reference}" t="inlineStr"{s}><is><t>{SecurityElement.Escape(content.ToString())}</t></is></c>""",
        };
    }

    /// <summary>"2026-01" as people read it: 01/2026.</summary>
    private static string Display(string month) => $"{month[5..]}/{month[..4]}";

    private static void Add(ZipArchive zip, string path, string xml)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(xml.TrimStart());
    }
}
