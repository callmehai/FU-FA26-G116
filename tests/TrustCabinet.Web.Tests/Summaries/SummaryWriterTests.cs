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
        Assert.Equal($"1. Mục có thảo luận\nTóm tắt\nĐoạn.\n\n2. Mục im lặng\nLê Văn A trình bày nội dung. {SummaryWriter.NoDiscussion}",
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
        Assert.Equal("1. Nội dung thảo luận\nBản gộp.", draft.Discussion);
        // No agenda: the model is told to split by issue.
        Assert.Contains(model.Prompts, p => p.Contains("Cuộc họp không có chương trình"));
        Assert.Equal(30, Assert.Single(draft.Sources).Lines);
    }
}
