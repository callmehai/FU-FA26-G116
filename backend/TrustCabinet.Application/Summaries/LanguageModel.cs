using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Summaries;

/// <summary>
/// SCRUM-36 — a language model run in-house (ADR-0054). It only ever drafts: what it writes is shown as AI-written
/// until the Secretary reviews it, and it never writes into the minutes on its own.
/// </summary>
public interface ILanguageModel
{
    bool IsConfigured { get; }

    /// <summary>The model's name, kept with what it wrote so a summary can be judged against how it was made.</summary>
    string Model { get; }

    /// <summary>A hosted API (ADR-0056) rather than the in-house model: the draft then uses the hosted harness.</summary>
    bool Remote => false;

    /// <summary>One chat turn; the answer is plain text, as AI-eCabinet asks of its model (JSON makes small models worse).</summary>
    Task<string> CompleteAsync(string system, string user, CancellationToken ct);
}

/// <summary>The model could not be reached or gave no usable answer; the message is for the Secretary.</summary>
public sealed class LanguageModelException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// One thing said, on the meeting's clock. <see cref="Ref"/> points back at its source so a summary can cite it:
/// <c>c:&lt;id&gt;</c> for a sentence of someone's mic, <c>s:&lt;id&gt;</c> for a segment of a recording.
/// </summary>
public sealed record TranscriptLine(string Ref, DateTimeOffset At, string Speaker, string Text)
{
    public static string ClipRef(Guid id) => $"c:{id}";
    public static string SegmentRef(Guid id) => $"s:{id}";
}

/// <summary>A sentence of someone's mic, untracked, with the name of who said it when known.</summary>
public sealed record SpokenClip(Guid Id, DateTimeOffset StartedAt, long DurationMs, string Text, string? Speaker);

/// <summary>An available recording whose transcript is completed.</summary>
public sealed record TranscribedRecording(Guid Id, List<RecordingTake>? Takes, DateTimeOffset CreatedAt, Guid TranscriptId);

/// <summary>A non-empty segment of a transcript.</summary>
public sealed record SpokenSegment(Guid Id, long StartMs, long EndMs, string Text, string SpeakerLabel);

/// <summary>What <see cref="MeetingTranscriptReader"/> reads, untracked.</summary>
public interface IMeetingTranscriptQuery
{
    Task<bool> IsHybridAsync(Guid meetingId, CancellationToken ct);

    /// <summary>The meeting's sentences that are not excluded and have text, in no order.</summary>
    Task<IReadOnlyList<SpokenClip>> ListClipsAsync(Guid meetingId, CancellationToken ct);

    /// <summary>The meeting's available, not deleted recordings with a completed transcript.</summary>
    Task<IReadOnlyList<TranscribedRecording>> ListRecordingsAsync(Guid meetingId, CancellationToken ct);

    /// <summary>Each voice label of a transcript and the name of the person or Guest it was assigned to, if any.</summary>
    Task<IReadOnlyDictionary<string, string?>> ListSpeakerNamesAsync(Guid transcriptId, CancellationToken ct);

    /// <summary>The transcript's segments with text, in order.</summary>
    Task<IReadOnlyList<SpokenSegment>> ListSegmentsAsync(Guid transcriptId, CancellationToken ct);
}
