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
public sealed record VoiceSampleView(Guid Id, DateTimeOffset CreatedAt, long SpeechMs);
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

/// <summary>Encrypts an embedding at rest, bound to its owner, its sample id and its model.</summary>
public interface IVoiceprintProtector
{
    byte[] Protect(float[] embedding, Guid userId, Guid sampleId, string model);
    float[] Unprotect(byte[] ciphertext, Guid userId, Guid sampleId, string model);
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
    /// <summary>Bump when the wording below changes: consent is to the words a person actually read.</summary>
    public const string NoticeVersion = "1.0";

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
/// consented — can be suggested, each at most once per recording, and only when the best match is both close
/// enough and clearly ahead of the runner-up for that voice. Anything less stays "Người nói N".
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
        IReadOnlyList<Candidate> candidates, double threshold, double margin)
    {
        // A person's score for a voice is their best sample against it: samples differ by mood, mic and room.
        var scores = speakers.ToDictionary(s => s.Label, s => candidates.GroupBy(c => c.UserId)
            .Select(g => (UserId: g.Key, Score: g.Max(c => Cosine(s.Embedding, c.Embedding))))
            .OrderByDescending(x => x.Score).ToArray());
        var result = new Dictionary<string, Suggestion>();
        var taken = new HashSet<Guid>();
        foreach (var (label, userId, score) in scores
                     .SelectMany(kv => kv.Value.Select(x => (Label: kv.Key, x.UserId, x.Score)))
                     .OrderByDescending(x => x.Score))
        {
            if (result.ContainsKey(label) || taken.Contains(userId) || score < threshold) continue;
            var runnerUp = scores[label].Where(x => x.UserId != userId).Select(x => x.Score).DefaultIfEmpty(0).Max();
            if (score - runnerUp < margin) continue;
            result[label] = new(userId, Math.Round(score, 4));
            taken.Add(userId);
        }
        return result;
    }
}
