using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>ADR-0044 — telling whether a voice of the latest takes is one heard before, from the worker's own answer.</summary>
public sealed class TranscriptVoiceMatchTests
{
    [Fact]
    public void Each_voice_is_sampled_from_the_ends_of_its_longest_lines_before_the_new_takes()
    {
        var samples = TranscriptRules.PickSamples([
            ("SPEAKER_00", 0, 4000), ("SPEAKER_00", 4000, 9000), ("SPEAKER_00", 9000, 9500),
            ("SPEAKER_01", 9500, 10500),
            ("SPEAKER_02", 10500, 13000),
            // Said in the takes being added: not a sample.
            ("SPEAKER_03", 20000, 25000),
            ("", 13000, 18000),
        ], beforeMs: 20000);

        Assert.Equal([
            // 5 s of the longest line, then the last second of the next: 6 s, in the order they were said.
            ("SPEAKER_00", 3000L, 4000L), ("SPEAKER_00", 4000L, 9000L),
            // SPEAKER_01 spoke one second: too little to tell apart.
            ("SPEAKER_02", 10500L, 13000L),
        ], samples);
    }

    [Fact]
    public void A_voice_of_the_run_is_an_earlier_voice_when_it_holds_most_of_its_samples()
    {
        (string, long, long)[] placed = [("SPEAKER_00", 0, 2000), ("SPEAKER_01", 3500, 5500), ("SPEAKER_02", 7000, 9000)];
        SpeechSegment Line(long start, long end, string voice) => new(start, end, voice, "…");

        var same = TranscriptRules.SameVoices([
            // The worker numbers its own voices: its SPEAKER_00 is the earlier SPEAKER_01.
            Line(0, 2000, "SPEAKER_05"), Line(2000, 5500, "SPEAKER_00"),
            // Most of SPEAKER_02's sample, and the gap before it: the same voice. The rest of it alone is not.
            Line(5500, 8200, "SPEAKER_01"), Line(8200, 9000, "SPEAKER_04"),
            Line(10500, 12000, "SPEAKER_00"), Line(12000, 14000, "SPEAKER_03"),
        ], placed);

        Assert.Equal(new Dictionary<string, string>
        {
            ["SPEAKER_05"] = "SPEAKER_00", ["SPEAKER_00"] = "SPEAKER_01", ["SPEAKER_01"] = "SPEAKER_02",
        }, same);
    }

    [Fact]
    public void A_voice_holding_under_half_of_any_sample_is_a_new_voice()
    {
        var same = TranscriptRules.SameVoices([new(0, 900, "SPEAKER_00", "…"), new(900, 2000, "SPEAKER_01", "…")],
            [("SPEAKER_00", 0, 2000)]);

        Assert.Equal(new Dictionary<string, string> { ["SPEAKER_01"] = "SPEAKER_00" }, same);
    }
}
