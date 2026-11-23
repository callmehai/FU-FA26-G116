using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Application.Transcription;

/// <summary>
/// The self-hosted speech worker (workers/speech, ADR-0026): speech to text, who-spoke-when, and the voice
/// embedding of each speaker. Audio goes to it over the internal network and nothing leaves the system.
/// </summary>
public interface ISpeechEngine
{
    bool IsConfigured { get; }
    /// <param name="clip">One sentence from one person's own mic (SCRUM-109): the worker lifts quiet stretches far less,
    /// since those are mostly a neighbour overheard.</param>
    /// <param name="context">What the same speaker said just before (their previous sentence): read ahead of this one so
    /// a sentence cut at a breath is punctuated as one; the worker sends it back punctuated again.</param>
    Task<SpeechTranscription> TranscribeAsync(Stream audio, string contentType, bool diarize, bool clip, CancellationToken ct,
        string? context = null);
}

/// <param name="Context">The <c>context</c> sent, punctuated again with this audio's words after it; null when none was
/// sent, or the worker could not use it.</param>
public sealed record SpeechTranscription(long DurationMs, string Language, string AsrModel, string EmbeddingModel,
    IReadOnlyList<SpeechSegment> Segments, IReadOnlyList<SpeechSpeaker> Speakers, string? Context = null);

/// <param name="LevelDbfs">How loud the segment reached the mic, before the worker levelled it.</param>
/// <param name="WordStartsMs">When each word of <c>Text</c> begins, in ms into the audio, one per word; null when the worker
/// could not tell (or is older than word timings).</param>
/// <param name="WordConfidence">How sure the recogniser was of each word of <c>Text</c>, 0–100 per cent, one per word; null when the
/// worker could not tell (or is older than word scores).</param>
public sealed record SpeechSegment(long StartMs, long EndMs, string? Speaker, string Text, double? LevelDbfs = null,
    IReadOnlyList<long>? WordStartsMs = null, IReadOnlyList<int>? WordConfidence = null);

/// <summary>
/// One diarized voice. The embedding is used once, to suggest who the voice is (SCRUM-35), and is never stored:
/// a vector computed from a meeting is biometric data of people who did not enrol.
/// </summary>
public sealed record SpeechSpeaker(string Label, long SpeechMs, float[] Embedding);

/// <summary>What went wrong talking to the worker, already worded for the secretary who will read it.</summary>
public sealed class SpeechEngineException(string userMessage, Exception? inner = null) : Exception(userMessage, inner)
{
    public string UserMessage { get; } = userMessage;
}

public enum TranscriptFailure
{
    None, NotFound, Inactive, Forbidden, NotRetryable, Invalid, InvalidSpeaker, ReadFailed, SaveFailed, ExportForbidden,
    SecretMeeting, NotReady,
    /// <summary>ADR-0061: a combined recording is only for listening; its words are already in its sources.</summary>
    Combined,
}

public sealed record TranscriptOutcome(TranscriptFailure Failure = TranscriptFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

/// <summary>
/// One line of a transcript. <c>WordStartsMs</c>: when each word of <c>Text</c> begins, in ms into the recording, so the
/// line can follow the voice word by word; null for a line the Secretary corrected or one transcribed before word
/// timings were kept. <c>WordConfidence</c>: how sure the recogniser was of each word, 0–100 per cent, so a doubtful word can be
/// marked for the Secretary to check; null on the same terms.
/// </summary>
public sealed record TranscriptSegmentView(Guid Id, int Position, long StartMs, long EndMs, string Text,
    string SpeakerLabel, DateTimeOffset? EditedAt, string? EditedByName, IReadOnlyList<long>? WordStartsMs = null,
    IReadOnlyList<int>? WordConfidence = null);

/// <summary>
/// SCRUM-35 — one voice of the recording. <c>Suggested*</c> is the machine's guess from enrolled voices;
/// <c>Assigned*</c> is the secretary's decision, and only that one names the speaker. <c>SameAsLabel</c> is another
/// voice suggested as the same person more surely: perhaps one person the machine split in two (Secretary only).
/// A voice is named as a user or as a guest of the meeting (<c>AssignedGuestId</c>), never both. <c>AutoAssigned</c>:
/// the machine named it because the match was sure enough, and no Secretary has changed it since.
/// </summary>
public sealed record TranscriptSpeakerView(string Label, long SpeechMs, Guid? SuggestedUserId, string? SuggestedName,
    double? SuggestionScore, Guid? AssignedUserId, string? AssignedName, string? SameAsLabel = null,
    Guid? AssignedGuestId = null, bool AutoAssigned = false);

/// <summary>Someone a voice can be named as: a person of the meeting, or a guest invited to it (<c>Guest</c>).</summary>
public sealed record TranscriptPerson(Guid Id, string FullName, bool Guest = false);

public sealed record TranscriptView(Guid RecordingId, string Status, string Provider, string Language,
    string ErrorMessage, DateTimeOffset? CompletedAt, long DurationMs, bool CanEdit,
    IReadOnlyList<TranscriptSegmentView> Segments, IReadOnlyList<TranscriptSpeakerView> Speakers,
    IReadOnlyList<TranscriptPerson> People, bool CanExport = false);

public interface ITranscriptionService
{
    Task<TranscriptOutcome> GetAsync(Guid recordingId, Guid actorId, CancellationToken ct = default);
    /// <summary>Queues the recording again: after a failure, or to redo a finished transcript (a newer model, new voices).</summary>
    Task<TranscriptOutcome> RetryAsync(Guid recordingId, Guid actorId, CancellationToken ct = default);
    /// <summary>The finished transcript as a text file (<see cref="ExportedText"/>).</summary>
    Task<TranscriptOutcome> ExportAsync(Guid recordingId, Guid actorId, CancellationToken ct = default);
    Task<TranscriptOutcome> EditSegmentAsync(Guid segmentId, Guid actorId, string? text, CancellationToken ct = default);
    /// <summary>SCRUM-35 — the secretary says who a voice is (a user or a guest of the meeting), or clears it with both null.</summary>
    Task<TranscriptOutcome> AssignSpeakerAsync(Guid recordingId, string label, Guid? userId, Guid? guestId, Guid actorId,
        CancellationToken ct = default);
    /// <summary>
    /// The secretary says who said one line, as AI-eCabinet's "just this line": the line moves to the voice already
    /// named as that user or guest, or to a new voice named so; or, with <paramref name="label"/>, back to a voice of
    /// the recording. Every other line keeps its voice.
    /// </summary>
    Task<TranscriptOutcome> AssignSegmentSpeakerAsync(Guid segmentId, Guid? userId, Guid? guestId, string? label,
        Guid actorId, CancellationToken ct = default);
    /// <summary>Takes the oldest waiting transcript and runs it; false when there was nothing to do.</summary>
    Task<bool> ProcessNextAsync(CancellationToken ct = default);
    /// <summary>Puts back transcripts a previous process was running when it stopped.</summary>
    Task<int> RequeueInterruptedAsync(CancellationToken ct = default);
}

public interface ITranscriptRepository
{
    Task<Transcript?> FindByRecordingAsync(Guid recordingId, bool tracked, CancellationToken ct);
    Task<TranscriptSegment?> FindSegmentAsync(Guid segmentId, CancellationToken ct);
    /// <summary>The recording a segment belongs to, read without tracking anything; null when there is no such segment.</summary>
    Task<Guid?> FindSegmentRecordingAsync(Guid segmentId, CancellationToken ct);
    /// <summary>Atomically moves one Pending transcript to Processing (FOR UPDATE SKIP LOCKED) and returns its id.</summary>
    Task<Guid?> ClaimNextAsync(DateTimeOffset now, CancellationToken ct);
    Task<int> RequeueProcessingAsync(DateTimeOffset now, CancellationToken ct);
    Task<Transcript?> FindForProcessingAsync(Guid transcriptId, CancellationToken ct);
    /// <summary>Whether the transcript is still Processing, read without tracking anything.</summary>
    Task<bool> IsProcessingAsync(Guid transcriptId, CancellationToken ct);
    /// <summary>Reads the transcript's own columns again, as they are once its recording's row is locked.</summary>
    Task ReloadAsync(Transcript transcript, CancellationToken ct);
    /// <summary>
    /// The lines and voices a transcript has now, read without tracking anything, so a new run can carry over what the
    /// Secretary corrected by hand before they are replaced (ADR-0077). Empty for a transcript that never completed.
    /// </summary>
    Task<TranscriptCorrections> ReadCorrectionsAsync(Guid transcriptId, CancellationToken ct);
    Task DeleteSegmentsAsync(Guid transcriptId, CancellationToken ct);
    void Add(Transcript transcript);
    Task DeleteSpeakersAsync(Guid transcriptId, CancellationToken ct);
    void AddSpeakers(IEnumerable<TranscriptSpeaker> speakers);
    void AddSegments(IEnumerable<TranscriptSegment> segments);
    /// <summary>The guests invited to the meeting and not revoked, by the name they gave or were invited by.</summary>
    Task<IReadOnlyList<TranscriptPerson>> ListGuestsAsync(Guid meetingId, CancellationToken ct);
}

/// <summary>A transcript as it stood before a new run replaced it: what <see cref="TranscriptionService"/> carries over.</summary>
public sealed record TranscriptCorrections(IReadOnlyList<PreviousLine> Lines, IReadOnlyList<PreviousVoice> Voices);

/// <param name="Text">Read only for a line the Secretary edited: the others are the machine's, made again by the new run.</param>
public sealed record PreviousLine(long StartMs, long EndMs, string SpeakerLabel, string? Text, Guid? EditedById,
    DateTimeOffset? EditedAt, Guid? SpeakerAssignedById, DateTimeOffset? SpeakerAssignedAt);

public sealed record PreviousVoice(string Label, Guid? AssignedUserId, Guid? AssignedGuestId, Guid? AssignedById,
    DateTimeOffset? AssignedAt);
