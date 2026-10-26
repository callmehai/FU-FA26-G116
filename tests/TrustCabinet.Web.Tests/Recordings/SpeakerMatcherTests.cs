using TrustCabinet.Domain.Transcription;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Web.Tests.Recordings;

public sealed class SpeakerMatcherTests
{
    private static readonly Guid Lan = Guid.NewGuid(), Minh = Guid.NewGuid();

    private static SpeakerMatcher.Candidate C(Guid user, float[] embedding) => new(user, embedding);

    private static IReadOnlyDictionary<string, SpeakerMatcher.Suggestion> Suggest(SpeechSpeaker[] speakers,
        params SpeakerMatcher.Candidate[] candidates) => SpeakerMatcher.Suggest(speakers, candidates, 0.5, 0.1);

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
    public void The_best_sample_counts_and_a_person_is_suggested_for_one_voice_only()
    {
        var result = Suggest(
            [new("SPEAKER_00", 1000, [1f, 0.05f]), new("SPEAKER_01", 1000, [0.95f, 0.1f]), new("SPEAKER_02", 1000, [0f, 1f])],
            C(Lan, [0.6f, 0.8f]), C(Lan, [1f, 0f]), C(Minh, [0f, 1f]));
        // Lan's closer sample decides, not the average of both.
        Assert.Equal(Lan, result["SPEAKER_00"].UserId);
        Assert.Equal(Minh, result["SPEAKER_02"].UserId);
        // SPEAKER_01 also sounds like Lan, who is already taken: it stays unnamed rather than become a second Lan.
        Assert.False(result.ContainsKey("SPEAKER_01"));
    }
}
