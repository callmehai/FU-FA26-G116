namespace TrustCabinet.Domain.Summaries;

/// <summary>
/// SCRUM-36 — a language model run in-house (ADR-0054). It only ever drafts: what it writes is shown as AI-written
/// until the Secretary reviews it, and it never writes into the minutes on its own.
/// </summary>
public interface ILanguageModel
{
    bool IsConfigured { get; }

    /// <summary>The model's name, kept with what it wrote so a summary can be judged against how it was made.</summary>
    string Model { get; }

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
