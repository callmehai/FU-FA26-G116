using TrustCabinet.Application.Summaries;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>A task's deadline stays in the draft only when the meeting record says it (Hải, 24/11/2026).</summary>
public sealed class DeadlineGroundingTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 20, 7, 0, 0, TimeSpan.Zero);

    private static MeetingFacts Said(params string[] lines)
        => new("Test thu hộ 20/11", [], [], [], [.. lines.Select((t, i) => new TranscriptLine($"c:{i}", Start.AddMinutes(i), "Người nói 1", t))]);

    [Fact]
    public void A_weekday_nobody_said_is_dropped_and_the_task_and_owner_stay()
    {
        var facts = Said("Hải kiểm tra lại hệ thống thu âm nhé.", "Huy xem camera với mở hai mic cùng lúc.");
        var tasks = "- Kiểm tra và cải thiện hệ thống thu âm — Trần Việt Hải — trước thứ Tư\n"
            + "- Kiểm tra camera và mở hai mic cùng lúc — Nguyễn Quang Huy — trước thứ Tư";
        Assert.Equal("- Kiểm tra và cải thiện hệ thống thu âm — Trần Việt Hải\n- Kiểm tra camera và mở hai mic cùng lúc — Nguyễn Quang Huy",
            DeadlineGrounding.Apply(tasks, facts));
    }

    [Fact]
    public void A_deadline_said_in_the_meeting_stays_whatever_its_spelling()
    {
        // Said as words, written as a digit, and the other way round.
        Assert.Equal("- Nộp báo cáo — Khánh — trước thứ 5",
            DeadlineGrounding.Apply("- Nộp báo cáo — Khánh — trước thứ 5", Said("tối thứ năm phải nộp báo cáo rồi")));
        Assert.Equal("- Nộp báo cáo — Khánh — trước thứ Năm",
            DeadlineGrounding.Apply("- Nộp báo cáo — Khánh — trước thứ Năm", Said("hạn là thứ 5 nhé")));
        // A spoken date reads the same as one in digits.
        Assert.Equal("- Gửi slide — Lộc — trước 03/12/2026",
            DeadlineGrounding.Apply("- Gửi slide — Lộc — trước 03/12/2026", Said("review ngày ba tháng mười hai")));
    }

    [Fact]
    public void Every_anchor_of_a_deadline_must_be_said()
    {
        var facts = Said("làm xong trong tuần này nhé");
        Assert.Equal("- Sửa lỗi — Hải — trong tuần", DeadlineGrounding.Apply("- Sửa lỗi — Hải — trong tuần", facts));
        // "tuần" was said, "thứ Sáu" was not.
        Assert.Equal("- Sửa lỗi — Hải", DeadlineGrounding.Apply("- Sửa lỗi — Hải — thứ Sáu tuần này", facts));
        Assert.Equal("- Sửa lỗi — Hải", DeadlineGrounding.Apply("- Sửa lỗi — Hải — 17 giờ ngày 25/11", facts));
    }

    [Fact]
    public void A_line_without_a_time_is_left_as_written()
    {
        var facts = Said("ok");
        Assert.Equal("- Rà soát tài liệu — Sở Nội vụ", DeadlineGrounding.Apply("- Rà soát tài liệu — Sở Nội vụ", facts));
        Assert.Equal("Không có.", DeadlineGrounding.Apply("Không có.", facts));
        Assert.Equal("- Rà soát tài liệu", DeadlineGrounding.Apply("- Rà soát tài liệu", facts));
    }

    [Fact]
    public void The_agenda_and_its_conclusions_count_as_the_record()
    {
        var facts = new MeetingFacts("Họp", [new AgendaFacts(Guid.NewGuid(), "Kế hoạch", null, "Hoàn thành trước thứ Sáu.", null, null)],
            [], [], []);
        Assert.Equal("- Hoàn thành kế hoạch — Phòng A — trước thứ Sáu",
            DeadlineGrounding.Apply("- Hoàn thành kế hoạch — Phòng A — trước thứ Sáu", facts));
    }
}
