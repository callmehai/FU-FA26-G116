using System.Text;
using System.Text.RegularExpressions;
using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Infrastructure.Summaries;

/// <summary>
/// ADR-0056 — the writer's harness for a hosted model (DeepSeek V4.1 Flash reads a million tokens). The in-house path,
/// <see cref="WriteAsync"/>, is untouched: these methods only run when the draft goes to an API. Two shapes:
/// <list type="bullet">
/// <item><see cref="WritePartsAsync"/>: the same prompts as the in-house path, each agenda item's talk in one call (no
/// 9,000-character pieces to merge) and up to <c>parallelism</c> items at once.</item>
/// <item><see cref="WriteOneShotAsync"/>: AI-eCabinet's shape, the whole meeting in one call; an answer that cannot be cut
/// back into the agenda's parts falls back to <see cref="WritePartsAsync"/>.</item>
/// </list>
/// </summary>
public sealed partial class SummaryWriter
{
    /// <summary>About 80,000 tokens of Vietnamese: three hours of talk is one call, far inside a hosted model's context.</summary>
    internal const int ApiChunkChars = 200_000;

    public async Task<SummaryDraft> WritePartsAsync(MeetingFacts facts, int parallelism, CancellationToken ct)
    {
        var parts = Split(facts);
        var texts = new string[parts.Count];
        using var gate = new SemaphoreSlim(Math.Max(1, parallelism));
        await Task.WhenAll(parts.Select(async (part, i) =>
        {
            if (part.Lines.Count == 0)
            {
                texts[i] = NoDiscussion;
                return;
            }
            await gate.WaitAsync(ct);
            try
            {
                texts[i] = await SummarisePartAsync(facts, part, ct);
            }
            finally
            {
                gate.Release();
            }
        }));
        var written = parts.Select((p, i) => (p.Title, Presented(p, texts[i]))).ToList();
        var (objectives, conclusions, tasks) = await OverviewAsync(facts, written, ct);
        return Compose(facts, parts, written, objectives, conclusions, tasks);
    }

    public async Task<SummaryDraft> WriteOneShotAsync(MeetingFacts facts, int parallelism, CancellationToken ct)
    {
        var parts = Split(facts);
        var spoken = parts.Select((p, i) => (Part: p, Number: i + 1)).Where(x => x.Part.Lines.Count > 0).ToList();
        var answer = Clean(await model.CompleteAsync(Rules, OneShotPrompt(facts, parts), ct));

        // Each label is looked for after the one before it, in capitals as asked and without a bullet: "- Kết luận: …"
        // inside the discussion is a sentence, not the start of section III.
        var goal = Label(answer, "MỤC TIÊU", 0);
        var talk = Label(answer, "THẢO LUẬN", Math.Max(goal.After, 0));
        var end = Label(answer, "KẾT LUẬN", Math.Max(talk.After, 0));
        var work = Label(answer, "NHIỆM VỤ", Math.Max(end.After, 0));
        if (goal.At < 0 || talk.At < 0 || end.At < 0)
            return await WritePartsAsync(facts, parallelism, ct);
        var discussion = answer[talk.After..end.At];
        var texts = new string[parts.Count];
        var found = PartMark().Matches(discussion).Cast<Match>().ToList();
        for (var m = 0; m < found.Count; m++)
        {
            var number = int.Parse(found[m].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var stop = m + 1 < found.Count ? found[m + 1].Index : discussion.Length;
            if (number < 1 || number > parts.Count) continue;
            // A meeting without an agenda is one part split by issue, and the model may open each issue with the same
            // "[PHẦN 1]": what follows each mark is kept, in order, never the last one alone.
            var text = discussion[(found[m].Index + found[m].Length)..stop].Trim();
            texts[number - 1] = string.IsNullOrEmpty(texts[number - 1]) ? text : texts[number - 1] + "\n" + text;
        }
        // A single part with talk needs no mark: a short meeting's answer often leaves "[PHẦN 1]" out.
        if (found.Count == 0 && spoken.Count == 1 && discussion.Trim().Length > 0)
            texts[spoken[0].Number - 1] = discussion.Trim();
        // Every part that has talk must come back, or the draft would silently lose an agenda item.
        if (spoken.Any(x => string.IsNullOrWhiteSpace(texts[x.Number - 1])))
            return await WritePartsAsync(facts, parallelism, ct);
        for (var i = 0; i < parts.Count; i++) texts[i] = parts[i].Lines.Count == 0 ? NoDiscussion : texts[i];

        var written = parts.Select((p, i) => (p.Title, Presented(p, texts[i]))).ToList();
        static string OrNothing(string s) => s.Trim() is { Length: > 0 } t ? t : Nothing;
        var objectives = OrNothing(answer[goal.After..talk.At]);
        var conclusions = OrNothing(work.At > end.At ? answer[end.After..work.At] : answer[end.After..]);
        var tasks = work.At > end.At ? OrNothing(answer[work.After..]) : Nothing;
        return Compose(facts, parts, written, objectives, conclusions, tasks);
    }

    /// <summary>The in-house path's rule for an item its presenter introduced and nobody discussed.</summary>
    private static string Presented(Part part, string text)
        => part.Presenter is not null && part.Lines.Count == 0 ? $"{part.Presenter} trình bày nội dung. {NoDiscussion}" : text;

    /// <summary>Sections I–III from the parts' texts, as <see cref="WriteAsync"/> lays them out.</summary>
    private static SummaryDraft Compose(MeetingFacts facts, IReadOnlyList<Part> parts,
        IReadOnlyList<(string Title, string Text)> written, string objectives, string conclusions, string tasks)
    {
        var discussion = new StringBuilder();
        for (var i = 0; i < parts.Count; i++)
        {
            if (i > 0) discussion.Append('\n');
            discussion.Append(NoAgenda(facts) && parts[i].Lines.Count > 0
                ? Issues(written[i].Text) : $"{i + 1}. {written[i].Title}\n{written[i].Text}");
        }
        var third = new StringBuilder().AppendLine(ChairHeading).AppendLine(conclusions);
        if (facts.Votes.Count > 0)
        {
            third.AppendLine(BallotsHeading);
            foreach (var vote in facts.Votes) third.AppendLine($"- {vote}");
        }
        third.AppendLine(TasksHeading).Append(tasks);
        var sources = parts.Select(p => new SummarySource(p.AgendaItemId, p.Title,
            p.Lines.Count > 0 ? p.Lines[0].At : null, p.Lines.Count > 0 ? p.Lines[^1].At : null, p.Lines.Count)).ToList();
        return new(Tidy(objectives), Tidy(discussion.ToString()), Tidy(third.ToString()), sources);
    }

    /// <summary>
    /// The whole meeting: the agenda with its presenters and the Chair's conclusions, then the talk already placed under
    /// each part (the code cuts it along the agenda's times, as the in-house path does), then the four answers asked for
    /// with the in-house path's own instructions.
    /// </summary>
    private static string OneShotPrompt(MeetingFacts facts, IReadOnlyList<Part> parts)
    {
        var prompt = new StringBuilder().AppendLine($"TÊN CUỘC HỌP: {facts.Title}");
        if (facts.Documents.Count > 0)
            prompt.AppendLine($"TÀI LIỆU (chỉ có tên, không có nội dung): {string.Join("; ", facts.Documents)}");
        prompt.AppendLine().AppendLine("CHƯƠNG TRÌNH:");
        for (var i = 0; i < facts.Agenda.Count; i++)
        {
            var item = facts.Agenda[i];
            prompt.AppendLine($"{i + 1}. {item.Title}");
            if (item.Presenter is not null) prompt.AppendLine($"   Người trình bày: {item.Presenter}");
            if (!string.IsNullOrWhiteSpace(item.Conclusion)) prompt.AppendLine($"   Kết luận của chủ trì: {item.Conclusion.Trim()}");
        }
        if (facts.Agenda.Count == 0) prompt.AppendLine("Không có.");

        prompt.AppendLine().AppendLine("BẢN GHI LỜI THOẠI THEO TỪNG PHẦN (lời nói, số đọc thành chữ):");
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            prompt.AppendLine($"[PHẦN {i + 1}] {part.Title}" + (part.Presenter is null ? "" : $" — người trình bày: {part.Presenter}"));
            if (part.Lines.Count == 0) prompt.AppendLine("(không có lời thoại)");
            foreach (var line in part.Lines) prompt.AppendLine($"[{Clock(line.At)}] {line.Speaker}: {line.Text}");
            prompt.AppendLine();
        }

        prompt.AppendLine("Viết đúng bốn phần, mỗi phần mở đầu bằng nhãn của nó trên một dòng riêng, không thêm ký hiệu:")
            .AppendLine("MỤC TIÊU:")
            .AppendLine("Một đến hai đoạn ngắn (khoảng 100 chữ) nêu mục tiêu và phạm vi cuộc họp, suy ra từ tên cuộc họp và chương trình.")
            .AppendLine("THẢO LUẬN:")
            .AppendLine("Với MỖI phần có lời thoại, mở bằng đúng dòng \"[PHẦN n]\" (n là số của phần, không viết tên phần), rồi ở các dòng sau viết phần tóm tắt: "
                + "phần có người trình bày thì câu đầu nêu người đó trình bày (lấy từ người trình bày của phần, KHÔNG lấy người nói trong bản ghi); "
                + "rồi tóm tắt DIỄN BIẾN thảo luận lấy từ bản ghi của phần đó: ý kiến nêu ra, số liệu, ý kiến khác nhau, ghi đúng tên hoặc nhãn người nói. "
                + "KHÔNG chép lại kết luận ở phần này. Tối đa 250 chữ mỗi phần. Mỗi đoạn văn viết liền trên một dòng, không để dòng trống; liệt kê ngắn thì mỗi ý một dòng bắt đầu bằng \"- \". "
                + "Phần ghi \"(không có lời thoại)\" thì không viết. Phần có rất ít lời và không nói gì về nội dung đó thì sau dòng \"[PHẦN n]\" chỉ viết đúng câu: " + NoDiscussion)
            .AppendLine(NoAgenda(facts) ? IssueRule : "")
            .AppendLine("KẾT LUẬN:")
            .AppendLine("Kết luận của chủ trì, mỗi nội dung một dòng \"- <tên nội dung>: <kết luận>\": theo dòng \"Kết luận của chủ trì\" của nội dung đó; nội dung không có dòng kết luận thì lấy từ lời kết luận, lời giao việc của chủ trì trong bản ghi; không có thì bỏ nội dung đó. Không có kết luận nào thì ghi: Không có.")
            .AppendLine("NHIỆM VỤ:")
            .AppendLine("Mỗi nhiệm vụ được giao trong cuộc họp một dòng \"- <nhiệm vụ> — <người hoặc đơn vị phụ trách> — <hạn>\". Chỉ ghi người hoặc đơn vị mà hồ sơ nói rõ được giao việc đó; người trình bày một nội dung KHÔNG phải là người phụ trách; hồ sơ không nói ai phụ trách thì bỏ phần đó. Hạn chép ĐÚNG như hồ sơ nói, kể cả hạn tương đối (\"trước thứ Tư\"), không tự tính ra ngày khác; không nói hạn thì bỏ phần hạn. Không có nhiệm vụ nào thì ghi: Không có.")
            .AppendLine("KHÔNG viết kết quả biểu quyết: hệ thống tự chép từ dữ liệu.");
        return prompt.ToString();
    }

    private static (int At, int After) Label(string text, string label, int from)
        => new Regex($@"^[ \t]*(?:[IVX]+\.)?[ \t]*{label}[ \t]*:", RegexOptions.Multiline).Match(text, from) is { Success: true } m
            ? (m.Index, m.Index + m.Length) : (-1, -1);

    [GeneratedRegex(@"^[ \t]*\[PHẦN[ \t]+(\d{1,3})\][^\n]*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex PartMark();
}
