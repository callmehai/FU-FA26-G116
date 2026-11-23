using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Application.Transcription;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Application.VoiceId;

public enum VoiceFailure
{
    None, Inactive, NotFound, NoConsent, LimitReached, Rejected, Invalid, EngineUnavailable, ReadFailed, SaveFailed,
    /// <summary>A proxy sample sent without the collector confirming that the person agreed to this take, there and then.</summary>
    NotConfirmed,
}

public sealed record VoiceOutcome(VoiceFailure Failure = VoiceFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null, string? Reason = null);

public sealed record VoiceConsentView(DateTimeOffset GrantedAt, string NoticeVersion, string Purpose);
public sealed record VoiceSampleView(Guid Id, DateTimeOffset CreatedAt, long SpeechMs, bool Playable = false);

/// <summary>A sample's recording, decrypted for its owner to hear.</summary>
public sealed record VoiceSampleAudio(byte[] Bytes, string ContentType);
/// <param name="Controller">Who decides how the voice is processed (Luật 91/2025, Điều 9.2.b): part of the notice.</param>
/// <param name="Proxy">ADR-0060: the second set, recorded for the person by a meeting's Secretary, under the same consent
/// (ADR-0060).</param>
public sealed record VoiceProfile(VoiceConsentView? Consent, IReadOnlyList<VoiceSampleView> Samples, int MaxSamples,
    string NoticeVersion, string Purpose, string Controller, ProxyVoiceProfile Proxy);

/// <summary>ADR-0060 — the samples meetings' Secretaries recorded for the person, as the person sees them.</summary>
/// <param name="Allowed">The person's consent is to a notice that names others recording them
/// (<see cref="VoiceIdRules.ProxyNoticeVersion"/>): without it nobody can record them.</param>
public sealed record ProxyVoiceProfile(bool Allowed, IReadOnlyList<ProxySampleView> Samples, int MaxSamples);

/// <summary>One sample someone took for the person: where, by whom, so nothing is taken out of their sight.</summary>
/// <param name="MeetingId">Null for a sample taken outside any meeting, by a holder of "Thu giọng mẫu hộ" (ADR-0071).</param>
public sealed record ProxySampleView(Guid Id, DateTimeOffset CreatedAt, long SpeechMs, bool Playable, Guid? MeetingId,
    string? MeetingTitle, string CollectedByName);

/// <summary>
/// SCRUM-35 — a person's own Voice ID: consent, enrolment samples, withdrawal. Only ever about the caller. Covers both
/// sets (ADR-0060): the caller enrols only their own, but sees, hears and deletes the ones Secretaries took for them.
/// One consent covers both, and withdrawing it erases both (ADR-0060).
/// </summary>
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

/// <summary>ADR-0071 — one person as a holder of "Thu giọng mẫu hộ" sees them, outside any meeting.</summary>
/// <param name="Consented">Their consent is to notice 1.4 or later, which names recording outside a meeting.</param>
/// <param name="CanCollect">Consented, and the person's limit of proxy samples is not reached.</param>
/// <param name="Samples">Every sample anyone took for them, in a meeting or not, with who took it.</param>
public sealed record PersonVoiceCollectionView(Guid UserId, string FullName, bool Consented, bool CanCollect, int MaxSamples,
    IReadOnlyList<ProxySampleView> Samples);

/// <summary>
/// ADR-0071, ADR-0075 — a holder of <see cref="Capability.VoiceCollector"/> (checked by the endpoint) records one person at
/// any time, with no meeting: only an active account whose own consent is to notice 1.4 or later, and only a take the
/// person agreed to there and then (<c>personAgreed</c>, ADR-0073). Anything else is a person who does not exist. A
/// collector can never consent for them.
/// </summary>
public interface IVoiceCollectionService
{
    Task<VoiceOutcome> GetPersonAsync(Guid actorId, Guid userId, CancellationToken ct = default);
    Task<VoiceOutcome> AddForPersonAsync(Guid actorId, Guid userId, Stream audio, string contentType,
        bool personAgreed, CancellationToken ct = default);
    /// <summary>Any sample someone took for the person; every play is audited.</summary>
    Task<VoiceSampleAudio?> PersonAudioAsync(Guid actorId, Guid userId, Guid sampleId, CancellationToken ct = default);
    Task<VoiceOutcome> DeletePersonSampleAsync(Guid actorId, Guid userId, Guid sampleId, CancellationToken ct = default);
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
    /// <returns>False when no consent stood, so there was nothing to end.</returns>
    Task<bool> EndAsync(Guid userId, Guid actorId, VoiceIdEndReason reason, CancellationToken ct);
}

public interface IVoiceIdRepository
{
    Task<VoiceConsent?> FindActiveConsentAsync(Guid userId, bool forUpdate, CancellationToken ct);
    /// <summary>Every sample of the person, both sets, oldest first.</summary>
    Task<IReadOnlyList<VoiceSample>> ListSamplesAsync(Guid userId, CancellationToken ct);
    /// <summary>The person's samples someone else took, with the meeting (samples from before ADR-0075) and the collector.</summary>
    Task<IReadOnlyList<VoiceSample>> ListProxySamplesAsync(Guid userId, CancellationToken ct);
    /// <summary>Samples of these people taken under a consent that still stands, for one embedding model.</summary>
    Task<IReadOnlyList<VoiceSample>> ListMatchableSamplesAsync(IReadOnlyCollection<Guid> userIds, string embeddingModel, CancellationToken ct);
    /// <summary>
    /// Share-locks the standing consents of these people until the transaction ends, so a withdrawal (which locks the
    /// row for update) waits for a transcription matching against them, and the other way round.
    /// </summary>
    Task LockConsentsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
    /// <summary>Deletes the samples taken under this consent, both sets.</summary>
    Task<int> DeleteSamplesAsync(Guid consentId, CancellationToken ct);
    /// <summary>Clears machine suggestions that point at this person; names a secretary assigned stay.</summary>
    Task<int> ClearSuggestionsAsync(Guid userId, DateTimeOffset now, CancellationToken ct);
    /// <summary>ADR-0071: a sample someone took for this person, in a meeting or not; null for their own or another's.</summary>
    Task<VoiceSample?> FindProxySampleAsync(Guid userId, Guid sampleId, bool tracked, CancellationToken ct);
    void Add(VoiceConsent consent);
    void Add(VoiceSample sample);
    void Remove(VoiceSample sample);
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

/// <summary>
/// Who each diarized voice probably is. Only people offered as candidates — the meeting's own participants who
/// consented — can be suggested, and only when the best match is both close enough and clearly ahead of the
/// runner-up for that voice. Anything less stays "Người nói N". A runner-up already suggested, more surely, for another
/// voice that sounds different is not a runner-up: one person is not two voices that differ.
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

    /// <summary>
    /// Each voice's score against every candidate, best first. A person's score for a voice is their best sample against
    /// it: samples differ by mood, mic and room.
    /// </summary>
    public static Dictionary<string, (Guid UserId, double Score)[]> Scores(IReadOnlyList<SpeechSpeaker> speakers,
        IReadOnlyList<Candidate> candidates)
        => speakers.ToDictionary(s => s.Label, s => candidates.GroupBy(c => c.UserId)
            .Select(g => (UserId: g.Key, Score: g.Max(c => Cosine(s.Embedding, c.Embedding))))
            .OrderByDescending(x => x.Score).ToArray());

    public static IReadOnlyDictionary<string, Suggestion> Suggest(IReadOnlyList<SpeechSpeaker> speakers,
        IReadOnlyList<Candidate> candidates, double threshold, double margin, double sameVoice, double secondVoice,
        double inherit)
    {
        var voices = speakers.ToDictionary(s => s.Label, s => s.Embedding);
        var scores = Scores(speakers, candidates);
        var result = new Dictionary<string, Suggestion>();
        // The voice each person was first suggested for: the surest, as pairs come best first.
        var first = new Dictionary<Guid, string>();
        foreach (var (label, userId, score) in scores
                     .SelectMany(kv => kv.Value.Select(x => (Label: kv.Key, x.UserId, x.Score)))
                     .OrderByDescending(x => x.Score))
        {
            if (result.ContainsKey(label) || score < threshold) continue;
            // A runner-up already suggested, more surely, for another voice that does not sound like this one is not
            // in the running: one person is not two voices that differ. Two people's samples cut from one room
            // recording sound alike (Hải 0.670 against Khánh 0.635 for Hải's voice, 23/11/2026, Khánh already 0.735
            // as his own), and the margin alone left Hải unnamed.
            var runnerUp = scores[label]
                .Where(x => x.UserId != userId
                    && !(first.TryGetValue(x.UserId, out var own) && Cosine(voices[label], voices[own]) < sameVoice))
                .Select(x => x.Score).DefaultIfEmpty(0).Max();
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
