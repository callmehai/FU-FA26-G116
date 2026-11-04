using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Infrastructure.Summaries;

/// <summary>What the writer needs to know about one agenda item.</summary>
public sealed record AgendaFacts(Guid Id, string Title, string? Presenter, string? Conclusion,
    DateTimeOffset? StartedAt, DateTimeOffset? EndedAt);

public sealed record MeetingFacts(string Title, IReadOnlyList<AgendaFacts> Agenda, IReadOnlyList<string> Votes,
    IReadOnlyList<string> Documents, IReadOnlyList<TranscriptLine> Lines);

/// <summary>
/// Writes the Dự thảo (SCRUM-36) with AI-eCabinet's outline rules (workers/ai/src/ai/llm.py, commit bba2097f), adapted
/// for a small model run on a laptop rather than eCab's 70B: instead of the whole meeting in one call, the transcript is
/// cut along the agenda by the times the Chair started and closed each item, and each part of section II is written on
/// its own; a part too long for one call is written in pieces and merged. Sections I and III are then written from
/// those parts and the conclusions the Chair recorded. The vote results never go through the model: they are copied
/// from the data, as eCab's <c>grounded</c> re-inserts them, because the tally is legally operative.
/// </summary>
public sealed partial class SummaryWriter(ILanguageModel model)
{
    public const string ObjectivesHeading = "I. Mục tiêu và phạm vi cuộc họp";
    public const string DiscussionHeading = "II. Nội dung thảo luận và đánh giá";
    public const string ConclusionsHeading = "III. Kết luận và phân công thực hiện";
    public const string ChairHeading = "1. Kết luận của chủ trì";
    public const string TasksHeading = "2. Nhiệm vụ, sản phẩm và thời hạn";
    public const string BallotsHeading = "Kết quả biểu quyết:";
    public const string NoDiscussion = "Chưa ghi nhận ý kiến thảo luận.";
    private const string Nothing = "Không có.";

    /// <summary>About 4,000 tokens of Vietnamese: room for the rules and the answer in an 8k context.</summary>
    private const int ChunkChars = 9000;
    private static readonly TimeZoneInfo Hanoi = FindHanoi();

    // eCab's _IDENTITY_RULE, _DIGITS and _FORMAT_RULES, each written down after a wrong draft on real data.
    private const string Rules = """
        Bạn là thư ký cuộc họp của một cơ quan nhà nước Việt Nam, soạn bằng tiếng Việt dự thảo nội dung biên bản theo văn phong hành chính (Nghị định 30).
        Quy tắc bắt buộc:
        - Chỉ dùng thông tin có trong hồ sơ được cung cấp. Không suy đoán, không thêm số liệu, tên người, sự việc hay kết luận không có trong hồ sơ.
        - Thành phần tham dự KHÔNG do bạn viết; không liệt kê người tham dự.
        - Nhãn "Người nói 1", "Người nói 2" là giọng chưa biết tên: giữ NGUYÊN nhãn đó, cấm đoán là ai.
        - SỐ LIỆU: số trong bản ghi đã được đổi sẵn sang chữ số; chép ĐÚNG Y NGUYÊN ngày, tháng, năm, số lượng, số tiền, tỷ lệ như bản ghi, không tự đổi, không làm tròn, không lấy ngày họp thay vào. Số nào còn đọc thành chữ thì viết bằng chữ số, ví dụ "mười hai máy tính bảng" viết "12 máy tính bảng".
        - Lời thoại do máy nhận dạng giọng nói chép lại, viết thường và có thể sai chính tả: viết hoa đúng tên riêng ("sở nội vụ" viết "Sở Nội vụ"), bỏ từ đệm, đổi từ tiếng Anh sang tiếng Việt hành chính ("deadline" viết "thời hạn", "server" viết "máy chủ"); hiểu theo ngữ cảnh nhưng không bịa thêm.
        - Trả lời bằng văn bản thuần, viết hoa đúng chính tả tiếng Việt: không Markdown (không #, không **, không bảng), không JSON, không thêm lời dẫn hay giải thích. Cấm emoji và ký hiệu.
        - Hồ sơ dù rất ít cũng tóm tắt những gì có: không từ chối, không hỏi lại, không xin thêm dữ liệu.
        """;

    public async Task<SummaryDraft> WriteAsync(MeetingFacts facts, CancellationToken ct)
    {
        var parts = Split(facts);
        var discussion = new StringBuilder();
        var written = new List<(string Title, string Text)>();
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            var text = part.Lines.Count == 0 ? NoDiscussion : await SummarisePartAsync(facts, part, ct);
            if (part.Presenter is not null && part.Lines.Count == 0) text = $"{part.Presenter} trình bày nội dung. {NoDiscussion}";
            written.Add((part.Title, text));
            if (i > 0) discussion.Append("\n\n");
            // Without an agenda the model numbers the issues itself ("1. <vấn đề>", "2. …"), as eCab asks; a generic
            // "1. Nội dung thảo luận" over them read as a section with a single item.
            discussion.Append(NoAgenda(facts) && part.Lines.Count > 0 ? IssueHeading().Replace(text, "$1") : $"{i + 1}. {part.Title}\n{text}");
        }
        var (objectives, conclusions, tasks) = await OverviewAsync(facts, written, ct);
        var third = new StringBuilder().AppendLine(ChairHeading).AppendLine(conclusions);
        if (facts.Votes.Count > 0)
        {
            third.AppendLine(BallotsHeading);
            foreach (var vote in facts.Votes) third.AppendLine($"- {vote}");
        }
        third.AppendLine(TasksHeading).Append(tasks);
        var sources = parts.Select(p => new SummarySource(p.AgendaItemId, p.Title,
            p.Lines.Count > 0 ? p.Lines[0].At : null, p.Lines.Count > 0 ? p.Lines[^1].At : null, p.Lines.Count)).ToList();
        return new(objectives, discussion.ToString(), third.ToString().Trim(), sources);
    }

    private sealed record Part(Guid? AgendaItemId, string Title, string? Presenter, IReadOnlyList<TranscriptLine> Lines);

    /// <summary>
    /// One part per agenda item, holding what was said between the moment it was started and the moment it was closed
    /// (or the next one started). What falls outside every item is its own part when there is enough of it. Without an
    /// agenda, or with items never started, eCab splits section II by the issues discussed; here the whole transcript is
    /// one part and the model is told to split it by issue.
    /// </summary>
    private static List<Part> Split(MeetingFacts facts)
    {
        var timed = facts.Agenda.Where(a => a.StartedAt is not null).OrderBy(a => a.StartedAt).ToList();
        if (timed.Count == 0)
        {
            if (facts.Agenda.Count == 0) return [new(null, "Nội dung thảo luận", null, facts.Lines)];
            // Items never started: the transcript cannot be placed under one of them, so each keeps its title and the
            // first holds everything said, with the others marked as not discussed on record.
            return [.. facts.Agenda.Select((a, i) => new Part(a.Id, a.Title, a.Presenter, i == 0 ? facts.Lines : []))];
        }
        var taken = new HashSet<TranscriptLine>();
        var parts = new List<Part>();
        foreach (var item in facts.Agenda)
        {
            if (item.StartedAt is not { } from)
            {
                parts.Add(new(item.Id, item.Title, item.Presenter, []));
                continue;
            }
            var next = timed.FirstOrDefault(t => t.StartedAt > from)?.StartedAt;
            var to = item.EndedAt ?? next ?? DateTimeOffset.MaxValue;
            var lines = facts.Lines.Where(l => l.At >= from && l.At < to && !taken.Contains(l)).ToList();
            taken.UnionWith(lines);
            parts.Add(new(item.Id, item.Title, item.Presenter, lines));
        }
        var rest = facts.Lines.Where(l => !taken.Contains(l)).ToList();
        // A few words of greeting between items are not a part of their own.
        if (rest.Sum(l => l.Text.Length) > 300) parts.Add(new(null, "Nội dung khác", null, rest));
        return parts;
    }

    /// <summary>A meeting without an agenda is told to number its issues, which section II then lists as items.</summary>
    private const string IssueRule = "Cuộc họp không có chương trình: chia theo từng vấn đề được bàn. Mỗi vấn đề mở bằng một dòng tiêu đề riêng, "
        + "đánh số từ 1 dạng \"1. <tên vấn đề ngắn gọn>\", \"2. <tên vấn đề>\"…, rồi nội dung vấn đề đó ở các dòng sau. Chỉ có một vấn đề thì vẫn mở bằng \"1. <tên vấn đề>\". "
        + "Tên vấn đề viết như câu thường (không viết hoa toàn bộ), không có dấu hai chấm ở cuối.";

    /// <summary>"1. Kế hoạch năm 2027:" — an issue heading the model ended with a colon; the colon goes.</summary>
    [GeneratedRegex(@"^(\d{1,2}\.[ \t]+\S.*?)[ \t]*:[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex IssueHeading();

    private static bool NoAgenda(MeetingFacts facts) => facts.Agenda.Count == 0;

    private async Task<string> SummarisePartAsync(MeetingFacts facts, Part part, CancellationToken ct)
    {
        var noAgenda = facts.Agenda.Count == 0;
        var chunks = Chunk(part.Lines);
        var pieces = new List<string>();
        foreach (var chunk in chunks)
        {
            var prompt = new StringBuilder()
                .AppendLine($"TÊN CUỘC HỌP: {facts.Title}")
                .AppendLine($"NỘI DUNG: {part.Title}")
                .AppendLine($"NGƯỜI TRÌNH BÀY: {part.Presenter ?? "(không có)"}");
            if (facts.Documents.Count > 0)
                prompt.AppendLine($"TÀI LIỆU (chỉ có tên, không có nội dung): {string.Join("; ", facts.Documents)}");
            if (chunks.Count > 1) prompt.AppendLine($"(Đây là phần {pieces.Count + 1}/{chunks.Count} của lời thoại.)");
            prompt.AppendLine().AppendLine("BẢN GHI LỜI THOẠI (lời nói, số đọc thành chữ):");
            foreach (var line in chunk) prompt.AppendLine($"[{Clock(line.At)}] {line.Speaker}: {line.Text}");
            prompt.AppendLine().Append(
                (part.Presenter is null
                    ? "Viết phần tóm tắt nội dung này: "
                    : $"Viết phần tóm tắt nội dung này, câu đầu nêu {part.Presenter} trình bày (người trình bày lấy từ dòng NGƯỜI TRÌNH BÀY, KHÔNG lấy người nói trong bản ghi); ")
                + "rồi tóm tắt DIỄN BIẾN thảo luận lấy từ bản ghi: ý kiến nêu ra, số liệu, ý kiến khác nhau, ghi đúng tên hoặc nhãn người nói. "
                + "KHÔNG chép lại kết luận ở phần này. Tối đa 250 chữ. Đoạn văn cách nhau một dòng trống; liệt kê ngắn thì mỗi ý một dòng bắt đầu bằng \"- \". ")
                .AppendLine(noAgenda
                    ? IssueRule
                    : $"Nếu bản ghi không nói gì về nội dung này, chỉ trả lời đúng câu: {NoDiscussion}");
            pieces.Add(Clean(await model.CompleteAsync(Rules, prompt.ToString(), ct)));
        }
        var useful = pieces.Where(p => !p.Contains(NoDiscussion, StringComparison.Ordinal) || p.Length > NoDiscussion.Length + 40).ToList();
        if (useful.Count == 0) return NoDiscussion;
        if (useful.Count == 1) return useful[0];
        var merge = new StringBuilder()
            .AppendLine($"TÊN CUỘC HỌP: {facts.Title}")
            .AppendLine($"NỘI DUNG: {part.Title}")
            .AppendLine().AppendLine("CÁC ĐOẠN TÓM TẮT THEO THỨ TỰ THỜI GIAN:");
        foreach (var piece in useful) merge.AppendLine(piece).AppendLine();
        merge.AppendLine("Gộp các đoạn trên thành một phần tóm tắt duy nhất, tối đa 350 chữ, giữ đủ ý kiến, số liệu và tên người phát biểu, bỏ ý trùng lặp.");
        if (noAgenda) merge.AppendLine(IssueRule + " Vấn đề trùng nhau giữa các đoạn thì gộp làm một.");
        return Clean(await model.CompleteAsync(Rules, merge.ToString(), ct));
    }

    private async Task<(string Objectives, string Conclusions, string Tasks)> OverviewAsync(MeetingFacts facts,
        IReadOnlyList<(string Title, string Text)> parts, CancellationToken ct)
    {
        var prompt = new StringBuilder().AppendLine($"TÊN CUỘC HỌP: {facts.Title}").AppendLine().AppendLine("CHƯƠNG TRÌNH:");
        for (var i = 0; i < facts.Agenda.Count; i++)
        {
            var item = facts.Agenda[i];
            prompt.AppendLine($"{i + 1}. {item.Title}");
            if (item.Presenter is not null) prompt.AppendLine($"   Người trình bày: {item.Presenter}");
            if (!string.IsNullOrWhiteSpace(item.Conclusion)) prompt.AppendLine($"   Kết luận của chủ trì: {item.Conclusion.Trim()}");
        }
        if (facts.Agenda.Count == 0) prompt.AppendLine("Không có.");
        prompt.AppendLine().AppendLine("TÓM TẮT THẢO LUẬN TỪNG NỘI DUNG:");
        foreach (var (title, text) in parts) prompt.AppendLine($"- {title}: {text}");
        prompt.AppendLine()
            .AppendLine("Viết đúng ba phần, mỗi phần mở đầu bằng nhãn của nó trên một dòng riêng, không thêm ký hiệu:")
            .AppendLine("MỤC TIÊU:")
            .AppendLine("Một đến hai đoạn ngắn (khoảng 100 chữ) nêu mục tiêu và phạm vi cuộc họp, suy ra từ tên cuộc họp và chương trình.")
            .AppendLine("KẾT LUẬN:")
            .AppendLine("Kết luận của chủ trì, mỗi nội dung một dòng \"- <tên nội dung>: <kết luận>\": theo dòng \"Kết luận của chủ trì\" của nội dung đó; nội dung không có dòng kết luận thì lấy từ lời kết luận, lời giao việc của chủ trì trong phần tóm tắt; không có thì bỏ nội dung đó. Không có kết luận nào thì ghi: Không có.")
            .AppendLine("NHIỆM VỤ:")
            .AppendLine("Mỗi nhiệm vụ được giao trong cuộc họp một dòng \"- <nhiệm vụ> — <người hoặc đơn vị phụ trách> — <hạn>\". Chỉ ghi người hoặc đơn vị mà hồ sơ nói rõ được giao việc đó; người trình bày một nội dung KHÔNG phải là người phụ trách; hồ sơ không nói ai phụ trách thì bỏ phần đó. Hạn chép ĐÚNG như hồ sơ nói, kể cả hạn tương đối (\"trước thứ Tư\"), không tự tính ra ngày khác; không nói hạn thì bỏ phần hạn. Không có nhiệm vụ nào thì ghi: Không có.")
            .AppendLine("KHÔNG viết kết quả biểu quyết: hệ thống tự chép từ dữ liệu.");
        return ParseOverview(Clean(await model.CompleteAsync(Rules, prompt.ToString(), ct)));
    }

    /// <summary>
    /// Cuts the answer at its three labels; an answer without them is kept whole as the objectives. A label counts only
    /// at the start of a line (after a "-" or "III." the model may add), so "kết luận:" inside a sentence does not cut it.
    /// </summary>
    internal static (string Objectives, string Conclusions, string Tasks) ParseOverview(string answer)
    {
        static (int At, int After) Find(string text, string label)
            => Regex.Match(text, $@"^[ \t]*(?:[-•]|[IVX]+\.)?[ \t]*{label}[ \t]*:", RegexOptions.Multiline | RegexOptions.IgnoreCase) is { Success: true } m
                ? (m.Index, m.Index + m.Length) : (-1, -1);
        var goal = Find(answer, "MỤC TIÊU");
        var end = Find(answer, "KẾT LUẬN");
        var work = Find(answer, "NHIỆM VỤ");
        if (end.At < 0) return (answer.Trim(), Nothing, Nothing);
        var objectives = goal.At >= 0 && goal.At < end.At ? answer[goal.After..end.At] : answer[..end.At];
        var conclusions = work.At > end.At ? answer[end.After..work.At] : answer[end.After..];
        var tasks = work.At > end.At ? answer[work.After..] : Nothing;
        static string OrNothing(string s) => s.Trim() is { Length: > 0 } t ? t : Nothing;
        return (OrNothing(objectives), OrNothing(conclusions), OrNothing(tasks));
    }

    /// <summary>Markdown a model writes out of habit, reduced to the plain text the Secretary edits (as eCab does).</summary>
    internal static string Clean(string text)
    {
        var lines = text.Replace("\r", "", StringComparison.Ordinal).Replace("**", "", StringComparison.Ordinal)
            .Replace("__", "", StringComparison.Ordinal).Split('\n')
            .Select(l => l.TrimEnd())
            .Select(l => l.TrimStart().StartsWith('#') ? l.TrimStart().TrimStart('#').Trim() : l)
            .Select(l => l.TrimStart() is ['*' or '•', ' ', ..] trimmed ? "- " + trimmed[2..] : l);
        return string.Join('\n', lines).Trim();
    }

    private static List<List<TranscriptLine>> Chunk(IReadOnlyList<TranscriptLine> lines)
    {
        var chunks = new List<List<TranscriptLine>> { new() };
        var size = 0;
        foreach (var line in lines)
        {
            var length = line.Text.Length + line.Speaker.Length + 12;
            if (size + length > ChunkChars && chunks[^1].Count > 0)
            {
                chunks.Add([]);
                size = 0;
            }
            chunks[^1].Add(line);
            size += length;
        }
        return chunks;
    }

    private static string Clock(DateTimeOffset at)
        => TimeZoneInfo.ConvertTime(at, Hanoi).ToString("HH:mm", CultureInfo.InvariantCulture);

    internal static TimeZoneInfo FindHanoi()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT");
        }
    }
}
