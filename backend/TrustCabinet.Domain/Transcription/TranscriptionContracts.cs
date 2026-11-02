using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Domain.Transcription;

/// <summary>
/// The self-hosted speech worker (workers/speech, ADR-0026): speech to text, who-spoke-when, and the voice
/// embedding of each speaker. Audio goes to it over the internal network and nothing leaves the system.
/// </summary>
public interface ISpeechEngine
{
    bool IsConfigured { get; }
    /// <param name="clip">One sentence from one person's own mic (SCRUM-109): the worker lifts quiet stretches far less,
    /// since those are mostly a neighbour overheard.</param>
    Task<SpeechTranscription> TranscribeAsync(Stream audio, string contentType, bool diarize, bool clip, CancellationToken ct);
}

public sealed record SpeechTranscription(long DurationMs, string Language, string AsrModel, string EmbeddingModel,
    IReadOnlyList<SpeechSegment> Segments, IReadOnlyList<SpeechSpeaker> Speakers);

/// <param name="LevelDbfs">How loud the segment reached the mic, before the worker levelled it.</param>
public sealed record SpeechSegment(long StartMs, long EndMs, string? Speaker, string Text, double? LevelDbfs = null);

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
    SecretMeeting, NotReady
}

public sealed record TranscriptOutcome(TranscriptFailure Failure = TranscriptFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed record TranscriptSegmentView(Guid Id, int Position, long StartMs, long EndMs, string Text,
    string SpeakerLabel, DateTimeOffset? EditedAt, string? EditedByName);

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
    /// <summary>Takes the oldest waiting transcript and runs it; false when there was nothing to do.</summary>
    Task<bool> ProcessNextAsync(CancellationToken ct = default);
    /// <summary>Puts back transcripts a previous process was running when it stopped.</summary>
    Task<int> RequeueInterruptedAsync(CancellationToken ct = default);
}

public interface ITranscriptRepository
{
    Task<Transcript?> FindByRecordingAsync(Guid recordingId, bool tracked, CancellationToken ct);
    Task<TranscriptSegment?> FindSegmentAsync(Guid segmentId, CancellationToken ct);
    /// <summary>Atomically moves one Pending transcript to Processing (FOR UPDATE SKIP LOCKED) and returns its id.</summary>
    Task<Guid?> ClaimNextAsync(DateTimeOffset now, CancellationToken ct);
    Task<int> RequeueProcessingAsync(DateTimeOffset now, CancellationToken ct);
    Task<Transcript?> FindForProcessingAsync(Guid transcriptId, CancellationToken ct);
    /// <summary>Reads the transcript's own columns again, as they are once its recording's row is locked.</summary>
    Task ReloadAsync(Transcript transcript, CancellationToken ct);
    Task DeleteSegmentsAsync(Guid transcriptId, CancellationToken ct);
    void Add(Transcript transcript);
    Task DeleteSpeakersAsync(Guid transcriptId, CancellationToken ct);
    void AddSpeakers(IEnumerable<TranscriptSpeaker> speakers);
    void AddSegments(IEnumerable<TranscriptSegment> segments);
    /// <summary>The guests invited to the meeting and not revoked, by the name they gave or were invited by.</summary>
    Task<IReadOnlyList<TranscriptPerson>> ListGuestsAsync(Guid meetingId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public static class TranscriptRules
{
    public const int MaxSegmentText = 8000;

    /// <summary>Gipformer, run in-house; the version pins which model wrote a given transcript.</summary>
    public static string Provider(string asrModel) => $"self-hosted:{asrModel}";

    /// <summary>
    /// A segment of a room recording (ADR-0041) placed in the take that holds most of it, and cut to that take. The
    /// worker lays segments end to end, each starting where the one before ended, so a segment can start in the take
    /// before the one its words were said in; read by its start, the words of one take showed under the previous one.
    /// </summary>
    public static (long StartMs, long EndMs) FitToTake(long startMs, long endMs, IReadOnlyList<RecordingTake>? takes)
    {
        if (takes is not { Count: > 1 }) return (startMs, endMs);
        RecordingTake? best = null;
        var most = 0L;
        foreach (var take in takes)
        {
            var overlap = Math.Min(endMs, take.OffsetMs + take.DurationMs) - Math.Max(startMs, take.OffsetMs);
            // A tie goes to the later take: the worker stretches a segment back, never forward.
            if (overlap > 0 && overlap >= most) (best, most) = (take, overlap);
        }
        return best is null ? (startMs, endMs)
            : (Math.Max(startMs, best.OffsetMs), Math.Min(endMs, best.OffsetMs + best.DurationMs));
    }
}
