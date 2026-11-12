using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>ADR-0061 — the room mic's copy of an online attendee's words, heard through the room's speakers.</summary>
public sealed class RoomEchoTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 12, 9, 0, 0, TimeSpan.FromHours(7));

    private static RoomEcho.Span At(int fromSeconds, int toSeconds, string text)
        => new(T0.AddSeconds(fromSeconds), T0.AddSeconds(toSeconds), text);

    [Fact]
    public void The_room_repeating_an_online_sentence_at_the_same_time_is_an_echo()
    {
        var online = new[] { At(10, 16, "Đề nghị phòng Tài chính gửi báo cáo quý ba trước thứ sáu.") };
        // A second transcription of the same voice loses tones and a word or two.
        Assert.True(RoomEcho.IsEcho(At(11, 17, "de nghi phong tai chinh gui bao cao quy ba truoc thu"), online));
    }

    [Fact]
    public void The_same_words_said_later_in_the_room_are_not_an_echo()
    {
        var online = new[] { At(10, 16, "Đề nghị phòng Tài chính gửi báo cáo quý ba trước thứ sáu.") };
        Assert.False(RoomEcho.IsEcho(At(40, 46, "Đề nghị phòng Tài chính gửi báo cáo quý ba trước thứ sáu."), online));
    }

    [Fact]
    public void Someone_in_the_room_speaking_over_an_online_attendee_is_kept()
    {
        var online = new[] { At(10, 16, "Đề nghị phòng Tài chính gửi báo cáo quý ba trước thứ sáu.") };
        Assert.False(RoomEcho.IsEcho(At(11, 15, "Tôi đồng ý, nhưng cần thêm số liệu của chi nhánh miền Nam."), online));
    }

    [Fact]
    public void Timing_may_drift_by_the_slack_but_no_more()
    {
        var online = new[] { At(10, 12, "Chúng ta biểu quyết mục này") };
        Assert.True(RoomEcho.IsEcho(At(13, 15, "chúng ta biểu quyết mục này"), online));
        Assert.False(RoomEcho.IsEcho(At(15, 17, "chúng ta biểu quyết mục này"), online));
    }

    [Fact]
    public void A_one_word_segment_needs_that_word_and_an_empty_one_is_never_an_echo()
    {
        var online = new[] { At(10, 11, "Đồng ý.") };
        Assert.True(RoomEcho.IsEcho(At(10, 11, "Ý"), online));
        Assert.False(RoomEcho.IsEcho(At(10, 11, "  ...  "), online));
        Assert.False(RoomEcho.IsEcho(At(10, 11, "Không"), online));
    }

    [Fact]
    public void Words_ignore_tones_case_and_punctuation()
        => Assert.Equal(new HashSet<string> { "dong", "y", "quy", "3" }, RoomEcho.Words("Đồng ý — QUÝ 3!"));
}
