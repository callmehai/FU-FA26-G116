using TrustCabinet.Domain.Summaries;
using TrustCabinet.Infrastructure.Summaries;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>SCRUM-36 — the pure parts of the writer, no database.</summary>
public sealed class SummaryWriterTests
{
    [Fact]
    public void Overview_is_cut_at_its_three_labels()
    {
        var (objectives, conclusions, tasks) = SummaryWriter.ParseOverview(
            "MỤC TIÊU:\nRà soát tiến độ.\nKẾT LUẬN:\n- Báo cáo: Thống nhất.\nNHIỆM VỤ:\n- Hoàn thiện báo cáo — Sở Nội vụ — trước thứ Tư");
        Assert.Equal("Rà soát tiến độ.", objectives);
        Assert.Equal("- Báo cáo: Thống nhất.", conclusions);
        Assert.Equal("- Hoàn thiện báo cáo — Sở Nội vụ — trước thứ Tư", tasks);
    }

    [Fact]
    public void Overview_labels_match_in_any_case_and_missing_sections_read_none()
    {
        Assert.Equal(("Mục tiêu.", "Kết luận.", "Không có."), SummaryWriter.ParseOverview("mục tiêu: Mục tiêu.\nkết luận: Kết luận."));
        Assert.Equal(("Mục tiêu.", "Không có.", "Không có."), SummaryWriter.ParseOverview("MỤC TIÊU:\nMục tiêu.\nKẾT LUẬN:\n\nNHIỆM VỤ:\n"));
        // No "MỤC TIÊU:" label: what comes before the conclusions is the objectives.
        Assert.Equal(("Đoạn mở đầu.", "Có.", "Không có."), SummaryWriter.ParseOverview("Đoạn mở đầu.\nKẾT LUẬN:\nCó."));
    }

    [Fact]
    public void An_overview_without_labels_is_kept_whole_as_the_objectives()
        => Assert.Equal(("Một đoạn văn không nhãn.", "Không có.", "Không có."),
            SummaryWriter.ParseOverview("  Một đoạn văn không nhãn.  "));

    [Fact]
    public void Clean_reduces_markdown_to_plain_text()
    {
        Assert.Equal("Tiêu đề\nĐoạn đậm và nghiêng.\n- Ý một\n- Ý hai\n- Ý ba",
            SummaryWriter.Clean("## Tiêu đề\r\n**Đoạn đậm** và __nghiêng__.   \n* Ý một\n• Ý hai\n- Ý ba\n\n"));
        Assert.Equal("Không có ký hiệu.", SummaryWriter.Clean("### Không có ký hiệu."));
    }

    [Fact]
    public void Tidy_drops_the_blank_lines_a_section_would_show_as_gaps()
        => Assert.Equal("1. Mục một\nĐoạn một.\n- Ý một\n- Ý hai",
            SummaryWriter.Tidy("\n1. Mục một\r\nĐoạn một.  \n\n- Ý một\n \t\n- Ý hai\n\n\n"));

    private sealed class ScriptedModel(Func<string, string> answer) : ILanguageModel
    {
        internal readonly List<string> Prompts = [];
        public bool IsConfigured => true;
        public string Model => "scripted";
        public Task<string> CompleteAsync(string system, string user, CancellationToken ct)
        {
            Prompts.Add(user);
            return Task.FromResult(answer(user));
        }
    }

    private static readonly DateTimeOffset Start = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task An_item_with_nothing_said_is_not_sent_to_the_model_and_votes_are_appended_verbatim()
    {
        var model = new ScriptedModel(p => p.Contains("MỤC TIÊU:") ? "MỤC TIÊU:\nM.\nKẾT LUẬN:\n**K.**\nNHIỆM VỤ:\nKhông có." : "# Tóm tắt\nĐoạn.");
        var said = new AgendaFacts(Guid.NewGuid(), "Mục có thảo luận", null, null, Start, Start.AddMinutes(10));
        var silent = new AgendaFacts(Guid.NewGuid(), "Mục im lặng", "Lê Văn A", null, Start.AddMinutes(10), Start.AddMinutes(20));
        var facts = new MeetingFacts("Họp thử", [said, silent], ["Biểu quyết A: Đồng ý 2 phiếu. Kết luận: Thông qua."], [],
            [new TranscriptLine("c:1", Start.AddMinutes(1), "Người nói 1", "Nội dung.")]);

        var draft = await new SummaryWriter(model).WriteAsync(facts, CancellationToken.None);

        Assert.Equal(2, model.Prompts.Count); // one part, one overview
        Assert.Equal($"1. Mục có thảo luận\nTóm tắt\nĐoạn.\n2. Mục im lặng\nLê Văn A trình bày nội dung. {SummaryWriter.NoDiscussion}",
            draft.Discussion);
        Assert.Equal("M.", draft.Objectives);
        Assert.Equal(string.Join('\n', SummaryWriter.ChairHeading, "K.", SummaryWriter.BallotsHeading,
            "- Biểu quyết A: Đồng ý 2 phiếu. Kết luận: Thông qua.", SummaryWriter.TasksHeading, "Không có."), draft.Conclusions);
        Assert.Equal([1, 0], draft.Sources.Select(x => x.Lines));
        Assert.Null(draft.Sources[1].From);
    }

    [Fact]
    public async Task A_part_too_long_for_one_call_is_written_in_pieces_and_merged()
    {
        var model = new ScriptedModel(p => p.Contains("MỤC TIÊU:") ? "KẾT LUẬN:\nKhông có." :
            p.Contains("Gộp các đoạn") ? "Bản gộp." : "Một đoạn tóm tắt đủ dài để không bị coi là không có thảo luận gì cả trong phần này.");
        var lines = Enumerable.Range(0, 30).Select(i => new TranscriptLine($"c:{i}", Start.AddSeconds(i), "Người nói 1",
            new string('a', 500))).ToList();
        var draft = await new SummaryWriter(model).WriteAsync(new MeetingFacts("Họp", [], [], [], lines), CancellationToken.None);

        Assert.Contains(model.Prompts, p => p.Contains("(Đây là phần 1/2 của lời thoại.)"));
        Assert.Contains(model.Prompts, p => p.Contains("Gộp các đoạn"));
        // No agenda: the model numbers the issues itself, in each piece and in the merge, and no generic heading wraps them.
        Assert.Equal("Bản gộp.", draft.Discussion);
        Assert.All(model.Prompts.Where(p => !p.Contains("MỤC TIÊU:")), p => Assert.Contains("đánh số từ 1", p));
        Assert.Equal(30, Assert.Single(draft.Sources).Lines);
    }

    [Fact]
    public async Task Without_an_agenda_the_issues_are_the_numbered_items_of_section_two()
    {
        var model = new ScriptedModel(p => p.Contains("MỤC TIÊU:") ? "KẾT LUẬN:\nKhông có."
            : "1. kế hoạch quý tới:\n- Đề nghị tăng ngân sách 10%.\n\n2. Lịch trực Tết :\nChốt trước ngày 20/1.");
        var facts = new MeetingFacts("Họp", [], [], [], [new TranscriptLine("c:1", Start, "Người nói 1", "Nội dung.")]);

        var draft = await new SummaryWriter(model).WriteAsync(facts, CancellationToken.None);

        // No generic "1. Nội dung thảo luận" over the issues; their trailing colons go and they start with a capital.
        Assert.Equal("1. Kế hoạch quý tới\n- Đề nghị tăng ngân sách 10%.\n2. Lịch trực Tết\nChốt trước ngày 20/1.", draft.Discussion);
    }

    [Fact]
    public async Task A_part_with_real_talk_is_not_offered_the_nothing_said_answer_and_a_tacked_on_one_is_dropped()
    {
        var model = new ScriptedModel(p => p.Contains("MỤC TIÊU:") ? "KẾT LUẬN:\nKhông có."
            : $"Trần Văn B trình bày dự thảo kế hoạch năm 2027 gồm ba trọng tâm.\n- Kinh phí 4,8 tỷ đồng.\n\n{SummaryWriter.NoDiscussion}");
        var item = new AgendaFacts(Guid.NewGuid(), "Kế hoạch năm 2027", "Trần Văn B", null, Start, Start.AddMinutes(20));
        var talk = Enumerable.Range(0, 7).Select(i => new TranscriptLine($"c:{i}", Start.AddMinutes(i + 1), "Người nói 1",
            "Dự thảo kế hoạch năm 2027 gồm ba trọng tâm, tổng kinh phí dự kiến là 4,8 tỷ đồng.")).ToList();

        var draft = await new SummaryWriter(model).WriteAsync(new MeetingFacts("Họp", [item], [], [], talk), CancellationToken.None);

        Assert.DoesNotContain(SummaryWriter.NoDiscussion, model.Prompts[0]);
        Assert.Equal("1. Kế hoạch năm 2027\nTrần Văn B trình bày dự thảo kế hoạch năm 2027 gồm ba trọng tâm.\n- Kinh phí 4,8 tỷ đồng.",
            draft.Discussion);
    }

    [Fact]
    public async Task A_few_words_of_talk_may_still_be_answered_as_nothing_said()
    {
        var model = new ScriptedModel(p => p.Contains("MỤC TIÊU:") ? "KẾT LUẬN:\nKhông có." : SummaryWriter.NoDiscussion);
        var item = new AgendaFacts(Guid.NewGuid(), "Mục ngắn", null, null, Start, Start.AddMinutes(5));
        var draft = await new SummaryWriter(model).WriteAsync(new MeetingFacts("Họp", [item], [], [],
            [new TranscriptLine("c:1", Start.AddMinutes(1), "Người nói 1", "Xin chào mọi người.")]), CancellationToken.None);

        Assert.Contains(SummaryWriter.NoDiscussion, model.Prompts[0]);
        Assert.Equal($"1. Mục ngắn\n{SummaryWriter.NoDiscussion}", draft.Discussion);
    }

    /// <summary>Answers after a short wait and records how many calls were in flight at once.</summary>
    private sealed class SlowModel(Func<string, string> answer) : ILanguageModel
    {
        private int running;
        internal int MostAtOnce;
        internal readonly System.Collections.Concurrent.ConcurrentQueue<string> Prompts = new();
        public bool IsConfigured => true;
        public string Model => "slow";
        public async Task<string> CompleteAsync(string system, string user, CancellationToken ct)
        {
            Prompts.Enqueue(user);
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref MostAtOnce, now);
            await Task.Delay(50, ct);
            Interlocked.Decrement(ref running);
            return answer(user);
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int seen;
            while ((seen = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, seen) != seen) { }
        }
    }

    private static AgendaFacts Item(int i, string? presenter = null)
        => new(Guid.NewGuid(), $"Mục {i}", presenter, null, Start.AddMinutes(10 * i), Start.AddMinutes(10 * i + 10));

    private static TranscriptLine Said(int item, int n, string text = "Nội dung thảo luận.")
        => new($"c:{item}-{n}", Start.AddMinutes(10 * item + 1).AddSeconds(n), "Người nói 1", text);

    [Fact]
    public async Task Through_an_api_the_items_are_written_side_by_side_in_agenda_order()
    {
        var model = new SlowModel(p => p.Contains("MỤC TIÊU:") ? "MỤC TIÊU:\nM.\nKẾT LUẬN:\nK.\nNHIỆM VỤ:\nKhông có."
            : "Tóm tắt " + System.Text.RegularExpressions.Regex.Match(p, @"NỘI DUNG: (Mục \d)").Groups[1].Value + " đủ dài để là thảo luận thật.");
        var agenda = Enumerable.Range(0, 5).Select(i => Item(i)).ToList();
        var facts = new MeetingFacts("Họp", agenda, [], [], [.. Enumerable.Range(0, 5).Select(i => Said(i, 0))]);

        var draft = await new SummaryWriter(model, SummaryWriter.ApiChunkChars).WritePartsAsync(facts, 3, CancellationToken.None);

        Assert.Equal(3, model.MostAtOnce);
        Assert.Equal(6, model.Prompts.Count); // five items, one overview
        Assert.Equal(string.Join('\n', Enumerable.Range(0, 5).Select(i => $"{i + 1}. Mục {i}\nTóm tắt Mục {i} đủ dài để là thảo luận thật.")),
            draft.Discussion);
    }

    [Fact]
    public async Task Through_an_api_a_long_item_is_one_call_where_the_in_house_model_needs_pieces()
    {
        var model = new ScriptedModel(p => p.Contains("MỤC TIÊU:") ? "KẾT LUẬN:\nKhông có." : "Một đoạn tóm tắt đủ dài cho phần này.");
        var item = new AgendaFacts(Guid.NewGuid(), "Mục dài", null, null, Start, Start.AddHours(3));
        var talk = Enumerable.Range(0, 60).Select(i => new TranscriptLine($"c:{i}", Start.AddMinutes(i + 1), "Người nói 1", new string('a', 500))).ToList();

        await new SummaryWriter(model, SummaryWriter.ApiChunkChars).WritePartsAsync(new MeetingFacts("Họp", [item], [], [], talk), 3, CancellationToken.None);

        Assert.Equal(2, model.Prompts.Count); // the whole item, then the overview: no pieces, no merge
        Assert.DoesNotContain(model.Prompts, p => p.Contains("Gộp các đoạn"));
    }

    [Fact]
    public async Task The_one_call_answer_is_cut_back_into_the_agenda_with_votes_copied_from_the_data()
    {
        var answer = string.Join('\n', "MỤC TIÊU:", "Rà soát quý 3.", "THẢO LUẬN:", "[PHẦN 1]", "Lê An trình bày báo cáo.",
            "- Kết luận: câu trong phần thảo luận, không phải nhãn.", "[PHẦN 3] Mục 2", "Thảo luận mục 2.",
            "KẾT LUẬN:", "- Mục 0: Thống nhất.", "NHIỆM VỤ:", "- Làm tờ trình — Phòng Hành chính — trước 15/11");
        var model = new ScriptedModel(_ => answer);
        var agenda = new[] { Item(0, "Lê An"), Item(1, "Trần B"), Item(2) };
        var facts = new MeetingFacts("Họp", agenda, ["Biểu quyết A: Đồng ý 3 phiếu. Kết luận: Thông qua."], [], [Said(0, 0), Said(2, 0)]);

        var draft = await new SummaryWriter(model, SummaryWriter.ApiChunkChars).WriteOneShotAsync(facts, 3, CancellationToken.None);

        var prompt = Assert.Single(model.Prompts);
        Assert.Contains("[PHẦN 2] Mục 1 — người trình bày: Trần B\n(không có lời thoại)", prompt);
        Assert.Equal("Rà soát quý 3.", draft.Objectives);
        Assert.Equal(string.Join('\n', "1. Mục 0", "Lê An trình bày báo cáo.", "- Kết luận: câu trong phần thảo luận, không phải nhãn.",
            "2. Mục 1", $"Trần B trình bày nội dung. {SummaryWriter.NoDiscussion}", "3. Mục 2", "Thảo luận mục 2."), draft.Discussion);
        Assert.Equal(string.Join('\n', SummaryWriter.ChairHeading, "- Mục 0: Thống nhất.", SummaryWriter.BallotsHeading,
            "- Biểu quyết A: Đồng ý 3 phiếu. Kết luận: Thông qua.", SummaryWriter.TasksHeading,
            "- Làm tờ trình — Phòng Hành chính — trước 15/11"), draft.Conclusions);
    }

    [Fact]
    public async Task Without_an_agenda_the_issues_marked_with_the_same_part_are_all_kept()
    {
        var model = new ScriptedModel(_ => "MỤC TIÊU:\nM.\nTHẢO LUẬN:\n[PHẦN 1]\n1. Quý 3\nĐạt 18/20.\n[PHẦN 1]\n2. Kế hoạch 2027\nTổng 4,8 tỷ.\nKẾT LUẬN:\nKhông có.");
        var draft = await new SummaryWriter(model, SummaryWriter.ApiChunkChars).WriteOneShotAsync(
            new MeetingFacts("Họp", [], [], [], [Said(0, 0)]), 3, CancellationToken.None);

        Assert.Equal("1. Quý 3\nĐạt 18/20.\n2. Kế hoạch 2027\nTổng 4,8 tỷ.", draft.Discussion);
    }

    [Fact]
    public async Task A_one_call_answer_that_loses_an_item_falls_back_to_writing_item_by_item()
    {
        var model = new ScriptedModel(p => p.Contains("BẢN GHI LỜI THOẠI THEO TỪNG PHẦN") ? "MỤC TIÊU:\nM.\nTHẢO LUẬN:\n[PHẦN 1]\nChỉ có mục đầu.\nKẾT LUẬN:\nKhông có."
            : p.Contains("MỤC TIÊU:") ? "MỤC TIÊU:\nM.\nKẾT LUẬN:\nKhông có.\nNHIỆM VỤ:\nKhông có." : "Viết riêng từng mục, đủ dài để là thảo luận.");
        var facts = new MeetingFacts("Họp", [Item(0), Item(1)], [], [], [Said(0, 0), Said(1, 0)]);

        var draft = await new SummaryWriter(model, SummaryWriter.ApiChunkChars).WriteOneShotAsync(facts, 3, CancellationToken.None);

        Assert.Equal(4, model.Prompts.Count); // the one call, then two items and the overview
        Assert.Equal("1. Mục 0\nViết riêng từng mục, đủ dài để là thảo luận.\n2. Mục 1\nViết riêng từng mục, đủ dài để là thảo luận.", draft.Discussion);
    }
}
