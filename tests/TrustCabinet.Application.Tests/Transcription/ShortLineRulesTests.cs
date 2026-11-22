using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Application.Tests.Transcription;

/// <summary>Team test 21/11/2026: very short lines nobody can be named for are left out; long lines always stay.</summary>
public sealed class ShortLineRulesTests
{
    [Theory]
    [InlineData("Ừ.", 900, false, false)]
    [InlineData("Nó có còn phải test không?", 2200, false, false)]
    [InlineData("Đồng ý.", 4000, false, false)]
    [InlineData("Đồng ý.", 1500, true, true)]
    [InlineData("À. Ừ.", 1500, true, false)]
    [InlineData("Kính thưa các đồng chí, hôm nay chúng ta họp.", 4500, false, true)]
    [InlineData("Ừ ừ ừ ừ ừ ừ ừ ờ ờ ờ ờ ờ.", 4500, false, true)]
    public void A_room_line_is_left_out_only_when_short_and_unmatched_or_only_interjections(string text, long ms, bool attributed,
        bool kept)
        => Assert.Equal(kept, ShortLineRules.KeepsRoomLine(text, ms, attributed));

    [Theory]
    [InlineData("Ừ.", false)]
    [InlineData("ừm", false)]
    [InlineData("Ờ. Ừm.", false)]
    [InlineData("Ừ à.", false)]
    [InlineData("OK.", true)]
    [InlineData("Vâng.", true)]
    [InlineData("Khó nghe.", true)]
    [InlineData("Ừ là sao? Ừ. Ừ.", true)]
    public void A_sentence_from_ones_own_mic_is_left_out_only_when_it_is_interjections(string text, bool kept)
        => Assert.Equal(kept, ShortLineRules.KeepsClip(text));

    [Fact]
    public void The_voices_left_are_numbered_in_the_order_they_first_speak()
        => Assert.Equal(new Dictionary<string, string> { ["SPEAKER_03"] = "SPEAKER_00", ["SPEAKER_01"] = "SPEAKER_01" },
            ShortLineRules.Renumber(["SPEAKER_03", null, "SPEAKER_01", "SPEAKER_03", ""]));
}
