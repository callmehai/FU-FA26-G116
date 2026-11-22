using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>
/// The room recording of "[Test FE] Mic phòng – Hải" on the test server, 01/11/2026: the worker laid its segments end to
/// end, so each started where the one before ended, inside the take before its own.
/// </summary>
public sealed class TranscriptTakeFitTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 1, 15, 0, 0, TimeSpan.Zero);

    private static readonly RecordingTake[] Takes =
    [
        new(0, 8021, At), new(8021, 7338, At), new(15359, 19114, At), new(34473, 16896, At), new(51369, 9984, At),
        new(61353, 37674, At),
    ];

    [Theory]
    // "Hello… mình đang test mic sau khi deploy" was said in take 4 (34473–51369) but started 247 ms into take 3.
    [InlineData(34226, 50278, 34473, 50278)]
    // Mostly in take 2: 1.2 s of take 1 is the gap before the words.
    [InlineData(6784, 14284, 8021, 14284)]
    // Mostly in take 6, 2.9 s early.
    [InlineData(58454, 75206, 61353, 75206)]
    // Inside one take: unchanged.
    [InlineData(24476, 34226, 24476, 34226)]
    public void A_segment_moves_to_the_take_holding_most_of_it(long start, long end, long fitStart, long fitEnd)
    {
        Assert.Equal((fitStart, fitEnd), TranscriptRules.FitToTake(start, end, Takes));
    }

    [Fact]
    public void A_segment_running_past_its_take_is_cut_at_the_take_end()
    {
        Assert.Equal((15359L, 34473L), TranscriptRules.FitToTake(15359, 35000, Takes));
    }

    [Fact]
    public void An_upload_or_a_single_take_is_left_as_the_worker_wrote_it()
    {
        Assert.Equal((6784L, 14284L), TranscriptRules.FitToTake(6784, 14284, null));
        Assert.Equal((6784L, 14284L), TranscriptRules.FitToTake(6784, 14284, [new(0, 8021, At)]));
    }
}

/// <summary>
/// The meeting of 22/11/2026: a line cut to its take kept the times of the words said across the cut, so 6 of 664 lines
/// had words starting before the line or after it ended.
/// </summary>
public sealed class TranscriptWordsInLineTests
{
    [Fact]
    public void Words_across_the_cut_start_at_the_line_edge_and_stay_in_order()
    {
        Assert.Equal([1000L, 1000, 1500, 2000, 2000], WordTimings.Within([400, 900, 1500, 2600, 3100], 1000, 2000)!);
    }

    [Fact]
    public void Words_inside_the_line_are_unchanged_and_no_times_stay_none()
    {
        Assert.Equal([1000L, 1200, 1900], WordTimings.Within([1000, 1200, 1900], 1000, 2000)!);
        Assert.Null(WordTimings.Within(null, 1000, 2000));
    }

    [Fact]
    public void A_line_as_read_never_shows_a_word_outside_it()
    {
        Assert.Equal([2250L, 9000, 16042], WordTimings.ForLine("một hai ba", [2250, 9000, 18690], false, 2250, 16042));
        Assert.Null(WordTimings.ForLine("một hai ba", [2250, 9000, 18690], true, 2250, 16042));
    }
}
