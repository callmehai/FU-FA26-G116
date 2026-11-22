using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Transcription;
using static TrustCabinet.Domain.Transcription.TranscriptCarry;

namespace TrustCabinet.Application.Tests.Transcription;

/// <summary>ADR-0077 — matching a new run's voices and lines to the old run's, by time only.</summary>
public sealed class TranscriptCarryTests
{
    [Fact]
    public void A_new_voice_is_the_old_voice_that_holds_most_of_its_speech_in_the_old_audio()
    {
        Span[] before = [new(0, 10_000, "SPEAKER_00"), new(10_000, 20_000, "SPEAKER_01")];
        // Numbered the other way round this time, and still speaking in the take the old run never heard.
        Span[] after = [new(0, 3_000, "SPEAKER_00"), new(3_000, 10_000, "SPEAKER_01"), new(10_000, 20_000, "SPEAKER_00"),
            new(20_000, 60_000, "SPEAKER_00")];

        var voices = Voices(before, after);

        Assert.Equal("SPEAKER_01", voices["SPEAKER_00"]);
        Assert.Equal("SPEAKER_00", voices["SPEAKER_01"]);
    }

    [Fact]
    public void A_voice_split_between_two_old_ones_is_neither_and_a_short_agreement_is_none()
    {
        Span[] before = [new(0, 5_000, "SPEAKER_00"), new(5_000, 10_000, "SPEAKER_01")];

        // Half one, half the other: below the 60 % that makes it the same voice; 5 s of 8 s is above it.
        Assert.Empty(Voices(before, [new(0, 10_000, "SPEAKER_00")]));
        Assert.Equal("SPEAKER_00", Voices(before, [new(0, 8_000, "SPEAKER_00")])["SPEAKER_00"]);
        // All of it, but only a second and a half: a coincidence.
        Assert.Empty(Voices([new(0, 1_500, "SPEAKER_00")], [new(0, 1_500, "SPEAKER_00")]));
    }

    [Fact]
    public void A_line_the_Secretary_moved_counts_for_no_voice()
    {
        // The middle line was moved by hand: without it, the new voice is all SPEAKER_01's.
        Span[] before = [new(0, 4_000, "SPEAKER_00"), new(4_000, 10_000, null), new(10_000, 13_000, "SPEAKER_01")];

        Assert.Equal("SPEAKER_01", Voices(before, [new(4_000, 13_000, "SPEAKER_00")])["SPEAKER_00"]);
    }

    [Fact]
    public void Lines_match_one_to_one_within_the_tolerance_or_by_overlap_and_never_across_takes()
    {
        RecordingTake[] takes = [new(0, 10_000, DateTimeOffset.UnixEpoch), new(10_000, 10_000, DateTimeOffset.UnixEpoch)];
        Span[] before = [new(1_000, 3_000, null), new(4_000, 9_000, null), new(9_800, 10_000, null), new(12_000, 18_000, null)];
        Span[] after = [new(1_400, 3_300, null), new(4_200, 8_600, null), new(10_000, 10_400, null), new(13_000, 16_000, null)];

        var lines = Lines(before, after, takes);

        Assert.Equal(0, lines[0]);
        Assert.Equal(1, lines[1]);
        // In the first take before, in the second now: not the same line.
        Assert.False(lines.ContainsKey(2));
        // 3 s of 6 s: not 80 %.
        Assert.False(lines.ContainsKey(3));
    }
}
