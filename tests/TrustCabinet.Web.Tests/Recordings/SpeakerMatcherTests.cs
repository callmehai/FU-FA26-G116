using TrustCabinet.Domain.Transcription;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Web.Tests.Recordings;

public sealed class SpeakerMatcherTests
{
    private static readonly Guid Lan = Guid.NewGuid(), Minh = Guid.NewGuid();

    private static SpeakerMatcher.Candidate C(Guid user, float[] embedding) => new(user, embedding);

    private static IReadOnlyDictionary<string, SpeakerMatcher.Suggestion> Suggest(SpeechSpeaker[] speakers,
        params SpeakerMatcher.Candidate[] candidates) => SpeakerMatcher.Suggest(speakers, candidates, 0.5, 0.1, 0.7);

    [Fact]
    public void Cosine_ignores_length_and_refuses_mismatched_dimensions()
    {
        Assert.Equal(1, SpeakerMatcher.Cosine([1, 2, 3], [2, 4, 6]), 6);
        Assert.Equal(0, SpeakerMatcher.Cosine([1, 0], [0, 1]), 6);
        Assert.Equal(0, SpeakerMatcher.Cosine([1, 0], [1, 0, 0]));
        Assert.Equal(0, SpeakerMatcher.Cosine([0, 0], [1, 0]));
    }

    [Fact]
    public void Below_the_threshold_a_voice_stays_unnamed()
    {
        var result = Suggest([new("SPEAKER_00", 1000, [1f, 1f])], C(Lan, [1f, -0.6f]));
        Assert.Empty(result);
    }

    [Fact]
    public void A_near_tie_between_two_people_is_not_guessed()
    {
        var result = Suggest([new("SPEAKER_00", 1000, [1f, 1f])], C(Lan, [1f, 0.9f]), C(Minh, [0.9f, 1f]));
        Assert.Empty(result);
    }

    [Fact]
    public void The_best_sample_counts_and_one_person_split_in_two_is_suggested_for_both_voices()
    {
        var result = Suggest(
            [new("SPEAKER_00", 1000, [1f, 0.05f]), new("SPEAKER_01", 1000, [0.95f, 0.1f]), new("SPEAKER_02", 1000, [0f, 1f])],
            C(Lan, [0.6f, 0.8f]), C(Lan, [1f, 0f]), C(Minh, [0f, 1f]));
        // Lan's closer sample decides, not the average of both.
        Assert.Equal(Lan, result["SPEAKER_00"].UserId);
        Assert.Equal(Minh, result["SPEAKER_02"].UserId);
        // SPEAKER_01 sounds like Lan and like SPEAKER_00: the machine split Lan in two, so both are suggested as her.
        Assert.Equal(Lan, result["SPEAKER_01"].UserId);
    }

    [Fact]
    public void A_second_voice_that_does_not_sound_like_the_first_is_not_given_the_same_name()
    {
        // Both voices pass Lan's two samples, but they are far apart (cosine 0.6 < 0.7): two people, one of whom
        // never enrolled. Only the surer one is suggested as Lan.
        var result = Suggest(
            [new("SPEAKER_00", 1000, [1f, 0f]), new("SPEAKER_01", 1000, [0.6f, 0.8f])],
            C(Lan, [1f, 0f]), C(Lan, [0.6f, 0.8f]));
        Assert.Equal(Lan, result["SPEAKER_00"].UserId);
        Assert.False(result.ContainsKey("SPEAKER_01"));
    }

    [Fact]
    public void The_weaker_voice_of_a_person_points_at_the_surer_one_until_the_secretary_decides()
    {
        var same = SpeakerMatcher.SameAs([("SPEAKER_00", Lan, 0.70, null), ("SPEAKER_01", Lan, 0.82, null),
            ("SPEAKER_02", Minh, 0.90, null)]);
        Assert.Equal("SPEAKER_01", same["SPEAKER_00"]);
        Assert.Single(same);

        // Named as Lan, a voice leads whatever its score; named someone else, it is out of the group.
        Assert.Equal("SPEAKER_00", SpeakerMatcher.SameAs([("SPEAKER_00", Lan, 0.70, Lan), ("SPEAKER_01", Lan, 0.82, null)])["SPEAKER_01"]);
        Assert.Empty(SpeakerMatcher.SameAs([("SPEAKER_00", Lan, 0.70, Minh), ("SPEAKER_01", Lan, 0.82, null)]));
    }
}
