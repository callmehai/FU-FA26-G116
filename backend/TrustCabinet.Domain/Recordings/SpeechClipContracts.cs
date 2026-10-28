using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Domain.Recordings;

public enum SpeechClipFailure
{
    None, NotFound, Inactive, Forbidden, NotCapturing, Invalid, TooLarge, EngineUnavailable, ReadFailed,
    SaveFailed, ExportForbidden, SecretMeeting
}

public sealed record SpeechClipOutcome(SpeechClipFailure Failure = SpeechClipFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

/// <summary>
/// One sentence as everyone in the meeting reads it. An excluded sentence still comes, so the line does not vanish
/// without a trace, but only the Chair and the Secretary get its words. <c>Leak</c> says it was probably overheard.
/// <c>HasAudio</c> says the caller may hear it (<see cref="ISpeechClipService.PlaybackAsync"/>); <c>Peaks</c> draws
/// its waveform, one byte per 100 ms.
/// </summary>
public sealed record SpeechClipView(Guid Id, Guid SpeakerId, string SpeakerName, DateTimeOffset StartedAt, long DurationMs,
    string Text, DateTimeOffset UpdatedAt, DateTimeOffset? EditedAt, string? EditedByName, bool CanDelete,
    bool Excluded = false, string? ExclusionReason = null, string? ExcludedByName = null, SpeechClipLeak? Leak = null,
    bool HasAudio = false, byte[]? Peaks = null);

/// <summary>
/// SpeechClipFold's verdict: the sentence, or the words <c>[from, to)</c> of it, probably are <c>SpeakerName</c>'s,
/// overheard from their sentence <c>ClipId</c>. <c>Whole</c> when all of it is.
/// </summary>
public sealed record SpeechClipLeak(Guid ClipId, Guid SpeakerId, string SpeakerName, bool Whole, IReadOnlyList<int[]> Words);

/// <summary>
/// The meeting's per-mic transcript, or what changed in it since <paramref name="ReadAt"/> of the previous read.
/// <c>CanCapture</c> says whether the caller's own mic may send sentences now; nobody can switch on anyone else's.
/// <c>RetentionDays</c> is how long the words and audio are kept, said to the speaker before they switch on.
/// </summary>
public sealed record SpeechClipList(Guid MeetingId, bool CanCapture, bool CanEdit, DateTimeOffset ReadAt, IReadOnlyList<SpeechClipView> Items, bool CanExclude = false, bool CanExport = false,
    int RetentionDays = 0);

/// <summary>One sentence's audio as the browser sent it; written to the recordings store once it holds words.</summary>
public sealed record SpeechClipUpload(Guid ClientClipId, DateTimeOffset StartedAt, long DurationMs, string ContentType, byte[] Audio);

/// <summary>Told to everyone in the meeting so their transcript re-reads; it carries no words of its own.</summary>
public sealed record SpeechChange(Guid EventId, Guid MeetingId, string ChangeType, DateTimeOffset OccurredAt);

public interface ISpeechChangePublisher
{
    Task PublishAsync(SpeechChange change, CancellationToken ct);
}

public interface ISpeechClipService
{
    Task<SpeechClipOutcome> ListAsync(Guid meetingId, Guid actorId, DateTimeOffset? since, CancellationToken ct = default);
    /// <summary>Checked before the endpoint reads the body, so a refused sentence costs nothing.</summary>
    Task<SpeechClipOutcome> CanCaptureAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    Task<SpeechClipOutcome> AddAsync(Guid meetingId, Guid actorId, SpeechClipUpload upload, CancellationToken ct = default);
    Task<SpeechClipOutcome> EditAsync(Guid clipId, Guid actorId, string? text, CancellationToken ct = default);
    Task<SpeechClipOutcome> DeleteAsync(Guid clipId, Guid actorId, CancellationToken ct = default);
    /// <summary>The Chair or a Secretary takes a sentence out of the record (<c>reason</c> Leak or Other), or puts it back.</summary>
    Task<SpeechClipOutcome> SetExclusionAsync(Guid clipId, Guid actorId, bool excluded, string? reason, CancellationToken ct = default);
    /// <summary>The per-mic transcript as a text file, without the sentences taken out of the record.</summary>
    Task<SpeechClipOutcome> ExportAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    /// <summary>Deletes the sentences, and their audio, whose retention period has run out; returns how many.</summary>
    Task<int> EraseExpiredAsync(CancellationToken ct = default);
    /// <summary>
    /// A signed link to hear one sentence, for whoever may read the meeting's transcript; a sentence taken out of the
    /// record is heard only by the Chair and the Secretary, like its words.
    /// </summary>
    Task<SpeechClipOutcome> PlaybackAsync(Guid clipId, Guid actorId, CancellationToken ct = default);
    /// <summary>The stored audio a verified link points at, or null once the sentence is gone.</summary>
    Task<SpeechClipAudio?> FindAudioAsync(Guid clipId, CancellationToken ct = default);
}

public sealed record SpeechClipAudio(string StoragePath, string ContentType);

/// <summary>Signs and checks the short-lived link an &lt;audio&gt; element plays one sentence from.</summary>
public interface ISpeechClipAudioSigner
{
    RecordingPlayback Sign(Guid clipId, DateTimeOffset now);
    bool Verify(Guid clipId, long expires, string signature, DateTimeOffset now);
}

public static class SpeechClipRules
{
    /// <summary>A minute of 16 kHz mono 16-bit WAV is under 2 MB; compressed formats are far smaller.</summary>
    public const int MaxClipBytes = 3 * 1024 * 1024;
    public const long MaxDurationMs = 60_000;
    public const int MaxTextLength = 8000;

    /// <summary>One waveform byte per this many milliseconds of a sentence.</summary>
    public const int PeakMs = 100;

    /// <summary>What a browser's own capture produces — WAV it encoded itself, or MediaRecorder's containers — and the
    /// extension its audio is stored under.</summary>
    public static readonly IReadOnlyDictionary<string, string> Extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/wav"] = ".wav", ["audio/x-wav"] = ".wav", ["audio/wave"] = ".wav", ["audio/webm"] = ".webm", ["audio/ogg"] = ".ogg",
    };

    /// <summary>A browser's clock may be off; further than this and the sentence's time is the server's.</summary>
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A sentence said during the meeting may still arrive this long after it ended: the browser keeps what it could
    /// not send (offline, tab closed, transcriber down) and sends it on the next visit, as AI-eCabinet does.
    /// </summary>
    public static readonly TimeSpan LateUpload = TimeSpan.FromDays(7);

    /// <summary>
    /// ADR-0040: how a meeting's speech is captured follows from how it is held. Whoever joins from afar speaks into
    /// their own mic, so an online meeting and the remote half of a hybrid one are captured a sentence per mic.
    /// </summary>
    public static bool PerMic(MeetingMode mode) => mode is MeetingMode.Online or MeetingMode.Hybrid;

    /// <summary>Mics are on only while the meeting runs, and only when someone joins it from afar.</summary>
    public static bool Capturing(Meeting meeting) => meeting.Status == MeetingStatus.InProgress && PerMic(meeting.Mode);

    /// <summary>Sentences are taken while the meeting runs, and for <see cref="LateUpload"/> after it ended.</summary>
    public static bool Accepts(Meeting meeting, DateTimeOffset now)
        => PerMic(meeting.Mode) && (meeting.Status == MeetingStatus.InProgress
            || (meeting.Status == MeetingStatus.Ended && meeting.ActualEndedAt is { } ended && now - ended <= LateUpload));

    /// <summary>Who runs the meeting's record: its current Chair and Secretaries.</summary>
    public static bool RunsRecord(Meeting meeting, Guid actor) => meeting.Participants.Any(p => p.MeetingId == meeting.Id
        && p.UserId == actor && p.Role is MeetingRole.Chair or MeetingRole.Secretary && ParticipantRules.Active(p));
}
