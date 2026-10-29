using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Domain.VoiceId;

public enum VoiceFailure
{
    None, Inactive, NotFound, NoConsent, LimitReached, Rejected, Invalid, EngineUnavailable, ReadFailed, SaveFailed
}

public sealed record VoiceOutcome(VoiceFailure Failure = VoiceFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null, string? Reason = null);

public sealed record VoiceConsentView(DateTimeOffset GrantedAt, string NoticeVersion, string Purpose);
public sealed record VoiceSampleView(Guid Id, DateTimeOffset CreatedAt, long SpeechMs, bool Playable = false);

/// <summary>A sample's recording, decrypted for its owner to hear.</summary>
public sealed record VoiceSampleAudio(byte[] Bytes, string ContentType);
/// <param name="Controller">Who decides how the voice is processed (Luật 91/2025, Điều 9.2.b): part of the notice.</param>
public sealed record VoiceProfile(VoiceConsentView? Consent, IReadOnlyList<VoiceSampleView> Samples, int MaxSamples,
    string NoticeVersion, string Purpose, string Controller);

/// <summary>SCRUM-35 — a person's own Voice ID: consent, enrolment samples, withdrawal. Only ever about the caller.</summary>
public interface IVoiceIdService
{
    Task<VoiceOutcome> GetAsync(Guid actorId, CancellationToken ct = default);
    Task<VoiceOutcome> GrantAsync(Guid actorId, string? noticeVersion, CancellationToken ct = default);
    Task<VoiceOutcome> WithdrawAsync(Guid actorId, CancellationToken ct = default);
    Task<VoiceOutcome> AddSampleAsync(Guid actorId, Stream audio, string contentType, CancellationToken ct = default);
    Task<VoiceOutcome> DeleteSampleAsync(Guid actorId, Guid sampleId, CancellationToken ct = default);
    /// <summary>The caller's own sample recording; null when it is not theirs, does not exist or was not kept.</summary>
    Task<VoiceSampleAudio?> SampleAudioAsync(Guid actorId, Guid sampleId, CancellationToken ct = default);
}

public interface IVoiceIdRepository
{
    Task<VoiceConsent?> FindActiveConsentAsync(Guid userId, bool forUpdate, CancellationToken ct);
    Task<IReadOnlyList<VoiceSample>> ListSamplesAsync(Guid userId, CancellationToken ct);
    /// <summary>Samples of these people taken under a consent that still stands, for one embedding model.</summary>
    Task<IReadOnlyList<VoiceSample>> ListMatchableSamplesAsync(IReadOnlyCollection<Guid> userIds, string embeddingModel, CancellationToken ct);
    Task<int> DeleteSamplesAsync(Guid userId, CancellationToken ct);
    /// <summary>Clears machine suggestions that point at this person; names a secretary assigned stay.</summary>
    Task<int> ClearSuggestionsAsync(Guid userId, CancellationToken ct);
    void Add(VoiceConsent consent);
    void Add(VoiceSample sample);
    void Remove(VoiceSample sample);
    Task SaveChangesAsync(CancellationToken ct);
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct);
}

/// <summary>Encrypts an embedding at rest, bound to its owner, its sample id and its model; and the sample's audio.</summary>
public interface IVoiceprintProtector
{
    byte[] Protect(float[] embedding, Guid userId, Guid sampleId, string model);
    float[] Unprotect(byte[] ciphertext, Guid userId, Guid sampleId, string model);
    byte[] ProtectAudio(byte[] audio, Guid userId, Guid sampleId);
    byte[] UnprotectAudio(byte[] ciphertext, Guid userId, Guid sampleId);
}

/// <summary>The speech worker turned an enrolment sample down; <see cref="Reason"/> is its code (too_short, too_quiet…).</summary>
public sealed class VoiceSampleRejectedException(string reason) : Exception($"Voice sample rejected: {reason}")
{
    public string Reason { get; } = reason;
}

public sealed record SpeechEmbedding(float[] Embedding, string Model, long SpeechMs);

public interface IVoiceEmbedder
{
    Task<SpeechEmbedding> EmbedAsync(Stream audio, string contentType, CancellationToken ct);
}

public static class VoiceIdRules
{
    /// <summary>
    /// Bump when the wording below changes: consent is to the words a person actually read. 1.1 (ADR-0042): the sample
    /// recording is kept too, encrypted, for its owner to hear back. A sample's audio is kept only under a consent to
    /// 1.1 or later; a person who agreed to 1.0 renews to have it kept.
    /// </summary>
    public const string NoticeVersion = "1.1";

    /// <summary>The first notice under which a sample's recording is kept.</summary>
    public const string AudioNoticeVersion = "1.1";

    public const string Purpose =
        "Gợi ý tên người phát biểu trong bản gỡ băng của các cuộc họp tôi tham dự, để Thư ký lập biên bản.";

    public const int MaxSamples = 5;
    public const int MaxSampleBytes = 10 * 1024 * 1024;

    public static string RejectionMessage(string reason) => reason switch
    {
        "too_short" => "Giọng mẫu quá ngắn — cần ít nhất 6 giây có tiếng nói. Hãy đọc hết đoạn văn mẫu.",
        "too_quiet" => "Tiếng quá nhỏ — hãy nói to hơn hoặc ngồi gần micro hơn.",
        "clipped" => "Tiếng bị rè vì quá to — hãy để micro xa hơn một chút.",
        "multiple_speakers" => "Nghe như có hơn một người nói — hãy ghi ở nơi yên tĩnh, chỉ mình bạn nói.",
        "too_long" => "Giọng mẫu quá dài — tối đa 60 giây.",
        "unreadable_audio" => "Không đọc được tệp âm thanh.",
        _ => "Giọng mẫu chưa đạt. Hãy ghi lại.",
    };
}

/// <summary>
/// Who each diarized voice probably is. Only people offered as candidates — the meeting's own participants who
/// consented — can be suggested, and only when the best match is both close enough and clearly ahead of the
/// runner-up for that voice. Anything less stays "Người nói N".
/// <para>
/// One person may be suggested for several voices, as in AI-eCabinet: diarization splits a person in two, and the
/// Secretary then sees both as that person, the weaker marked "the same voice as" the stronger
/// (<see cref="SameAs"/>). A second voice must also sound like the first one (<c>sameVoice</c>): else it is
/// someone else who scores well against the same sample, such as a person who never enrolled.
/// </para>
/// </summary>
public static class SpeakerMatcher
{
    public sealed record Candidate(Guid UserId, float[] Embedding);
    public sealed record Suggestion(Guid UserId, double Score);

    public static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return na == 0 || nb == 0 ? 0 : dot / Math.Sqrt(na * nb);
    }

    public static IReadOnlyDictionary<string, Suggestion> Suggest(IReadOnlyList<SpeechSpeaker> speakers,
        IReadOnlyList<Candidate> candidates, double threshold, double margin, double sameVoice)
    {
        var voices = speakers.ToDictionary(s => s.Label, s => s.Embedding);
        // A person's score for a voice is their best sample against it: samples differ by mood, mic and room.
        var scores = speakers.ToDictionary(s => s.Label, s => candidates.GroupBy(c => c.UserId)
            .Select(g => (UserId: g.Key, Score: g.Max(c => Cosine(s.Embedding, c.Embedding))))
            .OrderByDescending(x => x.Score).ToArray());
        var result = new Dictionary<string, Suggestion>();
        // The voice each person was first suggested for: the surest, as pairs come best first.
        var first = new Dictionary<Guid, string>();
        foreach (var (label, userId, score) in scores
                     .SelectMany(kv => kv.Value.Select(x => (Label: kv.Key, x.UserId, x.Score)))
                     .OrderByDescending(x => x.Score))
        {
            if (result.ContainsKey(label) || score < threshold) continue;
            var runnerUp = scores[label].Where(x => x.UserId != userId).Select(x => x.Score).DefaultIfEmpty(0).Max();
            if (score - runnerUp < margin) continue;
            if (first.TryGetValue(userId, out var lead) && Cosine(voices[label], voices[lead]) < sameVoice) continue;
            result[label] = new(userId, Math.Round(score, 4));
            first.TryAdd(userId, label);
        }
        return result;
    }

    /// <summary>
    /// For each voice suggested as a person another voice is suggested as more surely, that other voice's label: one
    /// person the machine may have split in two. A named voice is the Secretary's decision, so it leads only when
    /// named as that same person; a voice named someone else drops out. Both keep the suggestion: forcing one onto
    /// the other could name a person wrongly, so the Secretary confirms both or one.
    /// </summary>
    public static IReadOnlyDictionary<string, string> SameAs(
        IReadOnlyList<(string Label, Guid? SuggestedUserId, double? Score, Guid? AssignedUserId)> voices)
    {
        var result = new Dictionary<string, string>();
        foreach (var group in voices.Where(v => v.SuggestedUserId is not null && (v.AssignedUserId is null
                     || v.AssignedUserId == v.SuggestedUserId)).GroupBy(v => v.SuggestedUserId))
        {
            var lead = group.OrderByDescending(v => v.AssignedUserId is not null).ThenByDescending(v => v.Score)
                .ThenBy(v => v.Label, StringComparer.Ordinal).First();
            foreach (var voice in group.Where(v => v.Label != lead.Label && v.AssignedUserId is null))
                result[voice.Label] = lead.Label;
        }
        return result;
    }
}
