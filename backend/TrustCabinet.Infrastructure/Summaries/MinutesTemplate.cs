using System.Text;
using System.Text.RegularExpressions;
using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Infrastructure.Summaries;

public sealed record MinutesPersonLine(string Name, string Role, bool Away);

public sealed record MinutesRecords(string Title, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, string Place,
    string? Chair, string? Secretary, IReadOnlyList<MinutesPersonLine> People, IReadOnlyList<AgendaFacts> Agenda,
    IReadOnlyList<string> Votes);

/// <summary>
/// The draft minutes (SCRUM-32): AI-eCabinet's one built-in template, "Biên bản cuộc họp"
/// (modules/minutes/internal/templates/bien-ban-cuoc-hop.html, commit bba2097f) — Decree 30/2020/NĐ-CP's Mẫu 1.4 —
/// filled the way eCab fills it when its model does not: the facts from the meeting's records, sections I–III from the
/// reviewed Dự thảo. There is one template, fixed here; TrustCabinet has no template management. Without a reviewed
/// Dự thảo, section II lists the agenda and section III the Chair's recorded conclusions and the votes, with "……" for the
/// Secretary to fill. The line marks are those of <see cref="MinutesPerson"/>'s file, MinutesContracts.cs.
/// </summary>
public static partial class MinutesTemplate
{
    public const string Blank = "……";

    public static string Build(MinutesRecords records, SummaryDraft? reviewed, TimeZoneInfo zone)
    {
        var start = TimeZoneInfo.ConvertTime(records.StartedAt, zone);
        var text = new StringBuilder()
            .AppendLine("@@ TÊN CƠ QUAN CHỦ QUẢN | **CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM**")
            .AppendLine("@@ **TÊN CƠ QUAN, ĐƠN VỊ** | **Độc lập - Tự do - Hạnh phúc**")
            .AppendLine($"@@ Số: {Blank}/BB-{Blank} | ")
            .AppendLine("# BIÊN BẢN")
            .AppendLine($"^ **Kết quả cuộc họp {Flat(records.Title)}**")
            .AppendLine($"**Thời gian bắt đầu:** {start:HH} giờ {start:mm} phút, ngày {start:dd}/{start:MM}/{start:yyyy}.")
            .AppendLine($"**Địa điểm:** {(string.IsNullOrWhiteSpace(records.Place) ? Blank : Flat(records.Place))}.");

        // As eCab (BR 19/10): everyone not marked away counts as there; the Secretary strikes whoever did not come.
        var here = records.People.Where(p => !p.Away).ToList();
        text.AppendLine($"**Thành phần tham dự:** {(here.Count > 0 ? here.Count.ToString("00") : Blank)} người có mặt, gồm:");
        if (here.Count == 0) text.AppendLine($"- {Blank}.");
        for (var i = 0; i < here.Count; i++)
            text.AppendLine($"- {Flat(here[i].Name)}, {here[i].Role}{(i == here.Count - 1 ? "." : ";")}");
        text.AppendLine($"**Chủ trì (chủ tọa):** {(records.Chair is null ? Blank : Flat(records.Chair))}.")
            .AppendLine($"**Thư ký (người ghi biên bản):** {(records.Secretary is null ? Blank : Flat(records.Secretary))}.");

        text.AppendLine($"## {SummaryWriter.ObjectivesHeading}")
            .AppendLine(Body(reviewed?.Objectives));

        text.AppendLine($"## {SummaryWriter.DiscussionHeading}");
        if (reviewed is not null) text.AppendLine(Body(reviewed.Discussion, ItemTitle()));
        else if (records.Agenda.Count == 0) text.AppendLine(Blank);
        else
            for (var i = 0; i < records.Agenda.Count; i++)
            {
                var item = records.Agenda[i];
                text.AppendLine($"### {i + 1}. {Flat(item.Title)}");
                text.AppendLine(item.Presenter is null ? Blank : $"{Flat(item.Presenter)} trình bày. {Blank}");
            }

        text.AppendLine($"## {SummaryWriter.ConclusionsHeading}");
        if (reviewed is not null) text.AppendLine(Body(reviewed.Conclusions, SubHeading()));
        else
        {
            text.AppendLine($"### {SummaryWriter.ChairHeading}");
            var recorded = records.Agenda.Where(a => !string.IsNullOrWhiteSpace(a.Conclusion)).ToList();
            if (recorded.Count == 0) text.AppendLine(Blank);
            foreach (var item in recorded) text.AppendLine($"- {Flat(item.Title)}: {Flat(item.Conclusion!)}");
            if (records.Votes.Count > 0)
            {
                text.AppendLine(SummaryWriter.BallotsHeading);
                foreach (var vote in records.Votes) text.AppendLine($"- {vote}");
            }
            text.AppendLine($"### {SummaryWriter.TasksHeading}").AppendLine(Blank);
        }

        var end = records.EndedAt is { } ended ? TimeZoneInfo.ConvertTime(ended, zone) : (DateTimeOffset?)null;
        text.AppendLine(end is { } e
                ? $"Cuộc họp kết thúc vào {e:HH} giờ {e:mm} phút, ngày {e:dd} tháng {e:MM} năm {e:yyyy}./."
                : $"Cuộc họp kết thúc vào {Blank} giờ {Blank} phút, ngày {Blank} tháng {Blank} năm {Blank}./.")
            .AppendLine("@@ **THƯ KÝ** | **CHỦ TỌA**")
            .AppendLine("@@ __(Chữ ký)__ | __(Chữ ký, dấu (nếu có))__")
            .AppendLine("@@ | ")
            .AppendLine("@@ | ")
            .AppendLine($"@@ **{Cell(records.Secretary)}** | **{Cell(records.Chair)}**")
            .AppendLine("~ **__Nơi nhận:__**")
            .AppendLine($"~ - {Blank};")
            .AppendLine($"~ - Lưu: VT, {Blank}.");
        return text.ToString().TrimEnd() + "\n";
    }

    /// <summary>
    /// The Dự thảo's text as minutes lines. A line that would read as one of the template's own marks is neutralised,
    /// so nothing the model wrote can turn into a heading or a signature block; the sub-headings section II and III are
    /// known to have ("1. &lt;nội dung&gt;", "1. Kết luận của chủ trì") are set bold.
    /// </summary>
    private static string Body(string? text, Regex? heading = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return Blank;
        var lines = text.Replace("\r", "", StringComparison.Ordinal).Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Select(Flat)
            .Select(l => heading is not null && heading.IsMatch(l) ? $"### {l}" : l);
        return string.Join('\n', lines);
    }

    /// <summary>Strips the marks from text that comes from people or the model, so it stays a plain line.</summary>
    private static string Flat(string text)
    {
        var line = text.Replace("\n", " ", StringComparison.Ordinal).Replace("**", "", StringComparison.Ordinal)
            .Replace("__", "", StringComparison.Ordinal).Trim();
        while (line.Length > 0 && (line[0] is '#' or '^' or '~' or '@')) line = line[1..].TrimStart();
        return line;
    }

    /// <summary>A name in the signature row: a "|" would split it into another column.</summary>
    private static string Cell(string? name)
        => name is null ? "Họ và tên" : Flat(name).Replace("|", "/", StringComparison.Ordinal);

    /// <summary>"1. Báo cáo tình hình quý 3": a numbered line short enough to be a title, not a sentence.</summary>
    [GeneratedRegex(@"^\d{1,2}\.\s+\S.{0,150}$")]
    private static partial Regex ItemTitle();

    [GeneratedRegex(@"^[12]\.\s+(Kết luận của chủ trì|Nhiệm vụ, sản phẩm và thời hạn)\s*$")]
    private static partial Regex SubHeading();
}
