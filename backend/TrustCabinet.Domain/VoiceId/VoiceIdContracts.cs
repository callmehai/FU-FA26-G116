using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Domain.VoiceId;

public enum VoiceFailure
{
    None, Inactive, NotFound, NoConsent, LimitReached, Rejected, Invalid, EngineUnavailable, ReadFailed, SaveFailed,
    /// <summary>ADR-0060: the meeting is not one whose Secretary may record samples now (state, mode or sensitivity).</summary>
    NotCollectable,
}

public sealed record VoiceOutcome(VoiceFailure Failure = VoiceFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null, string? Reason = null);

public sealed record VoiceConsentView(DateTimeOffset GrantedAt, string NoticeVersion, string Purpose);
public sealed record VoiceSampleView(Guid Id, DateTimeOffset CreatedAt, long SpeechMs, bool Playable = false);

/// <summary>A sample's recording, decrypted for its owner to hear.</summary>
public sealed record VoiceSampleAudio(byte[] Bytes, string ContentType);
/// <param name="Controller">Who decides how the voice is processed (Luật 91/2025, Điều 9.2.b): part of the notice.</param>
/// <param name="Proxy">ADR-0060: the second set, recorded for the person by a meeting's Secretary.</param>
public sealed record VoiceProfile(VoiceConsentView? Consent, IReadOnlyList<VoiceSampleView> Samples, int MaxSamples,
    string NoticeVersion, string Purpose, string Controller, ProxyVoiceProfile Proxy);

/// <summary>ADR-0060 — the samples meetings' Secretaries recorded for the person, as the person sees them.</summary>
public sealed record ProxyVoiceProfile(VoiceConsentView? Consent, IReadOnlyList<ProxySampleView> Samples, int MaxSamples,
    string NoticeVersion, string Purpose);

/// <summary>One sample a Secretary took for the person: where, by whom, so nothing is taken out of their sight.</summary>
public sealed record ProxySampleView(Guid Id, DateTimeOffset CreatedAt, long SpeechMs, bool Playable, Guid MeetingId,
    string MeetingTitle, string CollectedByName);

/// <summary>
/// SCRUM-35 — a person's own Voice ID: consent, enrolment samples, withdrawal. Only ever about the caller. Covers both
/// sets (ADR-0060): the caller enrols only their own, but sees, hears and deletes the ones Secretaries took for them, and
/// gives or withdraws the consent that lets Secretaries take them.
/// </summary>
public interface IVoiceIdService
{
    Task<VoiceOutcome> GetAsync(Guid actorId, CancellationToken ct = default);
    Task<VoiceOutcome> GrantAsync(Guid actorId, string? noticeVersion, CancellationToken ct = default);
    Task<VoiceOutcome> WithdrawAsync(Guid actorId, CancellationToken ct = default);
    /// <summary>ADR-0060: lets the Secretaries of the caller's meetings record samples for them.</summary>
    Task<VoiceOutcome> GrantProxyAsync(Guid actorId, string? noticeVersion, CancellationToken ct = default);
    /// <summary>ADR-0060: takes that back and erases every sample a Secretary took; the caller's own set stays.</summary>
    Task<VoiceOutcome> WithdrawProxyAsync(Guid actorId, CancellationToken ct = default);
    Task<VoiceOutcome> AddSampleAsync(Guid actorId, Stream audio, string contentType, CancellationToken ct = default);
    Task<VoiceOutcome> DeleteSampleAsync(Guid actorId, Guid sampleId, CancellationToken ct = default);
    /// <summary>The caller's own sample recording; null when it is not theirs, does not exist or was not kept.</summary>
    Task<VoiceSampleAudio?> SampleAudioAsync(Guid actorId, Guid sampleId, CancellationToken ct = default);
}

/// <summary>A participant of the meeting as its Secretary sees them for collecting samples (ADR-0060).</summary>
/// <param name="Consented">Whether they let Secretaries record for them; without it nothing can be taken.</param>
/// <param name="SampleCount">All the samples Secretaries took for them, in any meeting, against the limit.</param>
/// <param name="Samples">Only the ones taken in this meeting: the Secretary manages nothing taken elsewhere.</param>
/// <param name="Role">The meeting role's name (Chair, Secretary, Member), as every other endpoint sends it.</param>
public sealed record VoiceCollectionPerson(Guid UserId, string FullName, string Role, bool Consented, int SampleCount,
    IReadOnlyList<CollectedSampleView> Samples);

public sealed record CollectedSampleView(Guid Id, DateTimeOffset CreatedAt, long SpeechMs, string CollectedByName);

/// <param name="CanCollect">The meeting is Published or InProgress: samples can be added now, not only managed.</param>
public sealed record VoiceCollectionView(bool CanCollect, int MaxSamples, IReadOnlyList<VoiceCollectionPerson> People);

/// <summary>
/// ADR-0060 — the Secretary of an in-person or hybrid meeting records Voice ID samples for its people on the room's mic,
/// the mic the meeting will be transcribed from. Every call requires the caller to hold <see cref="Capability.VoiceCollector"/>
/// (checked by the endpoint) and to be the Secretary of that very meeting (checked here, on every call); anything else is
/// answered like a meeting that does not exist. A sample is the participant's own data and needs their own standing
/// <see cref="VoiceConsentScope.Proxy"/> consent: a Secretary can never consent for them.
/// </summary>
public interface IVoiceCollectionService
{
    Task<VoiceOutcome> ListAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    Task<VoiceOutcome> AddAsync(Guid meetingId, Guid actorId, Guid userId, Stream audio, string contentType,
        CancellationToken ct = default);
    /// <summary>A sample taken in this meeting, for its Secretary to check; every play is audited.</summary>
    Task<VoiceSampleAudio?> AudioAsync(Guid meetingId, Guid actorId, Guid sampleId, CancellationToken ct = default);
    Task<VoiceOutcome> DeleteAsync(Guid meetingId, Guid actorId, Guid sampleId, CancellationToken ct = default);
}

/// <summary>Why a person's Voice ID ended; written in the audit details.</summary>
public enum VoiceIdEndReason { Withdrawn, AccountDeactivated }

/// <summary>
/// Ends a person's Voice ID: erases every sample, clears the machine's suggestions and the names it gave from their
/// voice (names a Secretary gave stay), closes the consent and audits it. Used when the person withdraws, and when an
/// administrator deactivates the account — a deactivated person can no longer sign in to withdraw, and their voice
/// must not stay matchable for as long as the row exists (Luật 91/2025/QH15). Opens no transaction: the caller runs it
/// inside its own, so it commits or rolls back with the change that caused it.
/// </summary>
public interface IVoiceIdErasure
{
    /// <param name="scope">The one set to end; null ends both, as deactivating the account does.</param>
    /// <returns>False when no consent stood, so there was nothing to end.</returns>
    Task<bool> EndAsync(Guid userId, Guid actorId, VoiceIdEndReason reason, CancellationToken ct, VoiceConsentScope? scope = null);
}

public interface IVoiceIdRepository
{
    Task<VoiceConsent?> FindActiveConsentAsync(Guid userId, bool forUpdate, CancellationToken ct,
        VoiceConsentScope scope = VoiceConsentScope.Self);
    /// <summary>Every sample of the person, both sets, oldest first.</summary>
    Task<IReadOnlyList<VoiceSample>> ListSamplesAsync(Guid userId, CancellationToken ct);
    /// <summary>The person's samples Secretaries took, with the meeting and the Secretary for showing them.</summary>
    Task<IReadOnlyList<VoiceSample>> ListProxySamplesAsync(Guid userId, CancellationToken ct);
    /// <summary>Samples of these people taken under a consent that still stands, for one embedding model.</summary>
    Task<IReadOnlyList<VoiceSample>> ListMatchableSamplesAsync(IReadOnlyCollection<Guid> userIds, string embeddingModel, CancellationToken ct);
    /// <summary>
    /// Share-locks the standing consents of these people until the transaction ends, so a withdrawal (which locks the
    /// row for update) waits for a transcription matching against them, and the other way round.
    /// </summary>
    Task LockConsentsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
    /// <summary>Deletes the samples taken under this consent.</summary>
    Task<int> DeleteSamplesAsync(Guid consentId, CancellationToken ct);
    /// <summary>Clears machine suggestions that point at this person; names a secretary assigned stay.</summary>
    Task<int> ClearSuggestionsAsync(Guid userId, DateTimeOffset now, CancellationToken ct);
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
    /// <summary>ADR-0060: the notice for letting Secretaries record samples; consent is to these words.</summary>
    public const string ProxyNoticeVersion = "1.0";

    public const string ProxyPurpose =
        "Cho phép Thư ký của cuộc họp tôi tham dự (người được giao quyền Thu giọng mẫu hộ) ghi giọng mẫu của tôi bằng "
        + "micro phòng họp, để nhận ra và điền tên tôi trong bản gỡ băng các cuộc họp tôi tham dự. Thư ký đó chỉ nghe lại "
        + "và xoá được mẫu thu ở cuộc họp của họ; tôi xem, nghe, xoá mọi mẫu và rút đồng ý bất cứ lúc nào.";

    /// <summary>
    /// Bump when the wording below changes: consent is to the words a person actually read. 1.1 (ADR-0042): the sample
    /// recording is kept too, encrypted, for its owner to hear back. A sample's audio is kept only under a consent to
    /// 1.1 or later; a person who agreed to 1.0 renews to have it kept. 1.2 (ADR-0048): the machine fills in the name
    /// on a sure match (ADR-0045), only for a person whose consent is to 1.2 or later; for the others it still only
    /// suggests.
    /// </summary>
    public const string NoticeVersion = "1.2";

    /// <summary>The first notice under which a sample's recording is kept.</summary>
    public const string AudioNoticeVersion = "1.1";

    /// <summary>The first notice under which the machine names a sure match by itself.</summary>
    public const string AutoAssignNoticeVersion = "1.2";

    public const string Purpose =
        "Nhận ra người phát biểu trong bản gỡ băng của các cuộc họp tôi tham dự (gợi ý tên, tự điền tên khi giọng khớp "
        + "rõ), để Thư ký lập biên bản.";

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
/// someone else who scores well against the same sample, such as a person who never enrolled. It must also score at
/// least <c>secondVoice</c> against the person: two people recorded by one room mic can sound as alike as one person
/// split in two (0.745 against 0.777 on the test server, 31/10/2026), but the stranger scores lower against the sample.
/// </para>
/// <para>
/// A voice left unnamed that sounds nearly the same as a person's first voice (<c>inherit</c>) is that person: the
/// clustering split them, and the split-off part (singing, a changed tone) scores low against a sample read aloud. On
/// the test server one person singing came out as four voices at 0.81–0.85 to each other (03/11/2026), none of them
/// suggested; two different people never came closer than 0.745.
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
        IReadOnlyList<Candidate> candidates, double threshold, double margin, double sameVoice, double secondVoice,
        double inherit)
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
            if (first.TryGetValue(userId, out var lead) && (Cosine(voices[label], voices[lead]) < sameVoice || score < secondVoice))
                continue;
            result[label] = new(userId, Math.Round(score, 4));
            first.TryAdd(userId, label);
        }
        // Takes the first voice's suggestion, score included: it is the same voice, so it is named as surely.
        foreach (var label in voices.Keys.Where(l => !result.ContainsKey(l)).ToArray())
        {
            if (first.Count == 0) break;
            var closest = first.Values.MaxBy(lead => Cosine(voices[label], voices[lead]))!;
            if (Cosine(voices[label], voices[closest]) >= inherit) result[label] = result[closest];
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
