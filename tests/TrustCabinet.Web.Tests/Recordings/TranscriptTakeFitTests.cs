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
