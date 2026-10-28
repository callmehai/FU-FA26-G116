using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Domain.Recordings;

public enum SpeechClipFailure
{
    None, NotFound, Inactive, Forbidden, NotCapturing, Locked, Cancelled, Invalid, TooLarge, EngineUnavailable, ReadFailed,
    SaveFailed
}

public sealed record SpeechClipOutcome(SpeechClipFailure Failure = SpeechClipFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

/// <summary>
/// One sentence as everyone in the meeting reads it. An excluded sentence still comes, so the line does not vanish
/// without a trace, but only the Chair and the Secretary get its words. <c>Leak</c> says it was probably overheard.
/// </summary>
public sealed record SpeechClipView(Guid Id, Guid SpeakerId, string SpeakerName, DateTimeOffset StartedAt, long DurationMs,
    string Text, DateTimeOffset UpdatedAt, DateTimeOffset? EditedAt, string? EditedByName, bool CanDelete,
    bool Excluded = false, string? ExclusionReason = null, string? ExcludedByName = null, SpeechClipLeak? Leak = null);

/// <summary>
/// SpeechClipFold's verdict: the sentence, or the words <c>[from, to)</c> of it, probably are <c>SpeakerName</c>'s,
/// overheard from their sentence <c>ClipId</c>. <c>Whole</c> when all of it is.
/// </summary>
public sealed record SpeechClipLeak(Guid ClipId, Guid SpeakerId, string SpeakerName, bool Whole, IReadOnlyList<int[]> Words);

/// <summary>
/// The meeting's per-mic transcript, or what changed in it since <paramref name="ReadAt"/> of the previous read.
/// <c>CanCapture</c> says whether the caller's own mic may send sentences now; nobody can switch on anyone else's.
/// </summary>
public sealed record SpeechClipList(Guid MeetingId, string CaptureMode, bool CanCapture, bool CanEdit, bool CanChangeMode,
    DateTimeOffset ReadAt, IReadOnlyList<SpeechClipView> Items, bool CanExclude = false);

/// <summary>One sentence's audio as the browser sent it. It lives in memory for the length of the request only.</summary>
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
    Task<SpeechClipOutcome> SetCaptureModeAsync(Guid meetingId, Guid actorId, string? mode, CancellationToken ct = default);
    /// <summary>Deletes the sentences of meetings whose retention period has run out; returns how many.</summary>
    Task<int> EraseExpiredAsync(CancellationToken ct = default);
}

public static class SpeechClipRules
{
    /// <summary>A minute of 16 kHz mono 16-bit WAV is under 2 MB; compressed formats are far smaller.</summary>
    public const int MaxClipBytes = 3 * 1024 * 1024;
    public const long MaxDurationMs = 60_000;
    public const int MaxTextLength = 8000;

    /// <summary>A browser's clock may be off; further than this and the sentence's time is the server's.</summary>
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A sentence said during the meeting may still arrive this long after it ended: the browser keeps what it could
    /// not send (offline, tab closed, transcriber down) and sends it on the next visit, as AI-eCabinet does.
    /// </summary>
    public static readonly TimeSpan LateUpload = TimeSpan.FromDays(7);

    /// <summary>What a browser's own capture produces: WAV it encoded itself, or MediaRecorder's containers.</summary>
    public static readonly IReadOnlySet<string> ContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "audio/wav", "audio/x-wav", "audio/wave", "audio/webm", "audio/ogg",
    };

    /// <summary>Mics are on only while the meeting runs, and only when it is captured per mic.</summary>
    public static bool Capturing(Meeting meeting)
        => meeting.Status == MeetingStatus.InProgress && meeting.CaptureMode == CaptureMode.PerMic;

    /// <summary>Sentences are taken while the meeting runs, and for <see cref="LateUpload"/> after it ended.</summary>
    public static bool Accepts(Meeting meeting, DateTimeOffset now)
        => meeting.CaptureMode == CaptureMode.PerMic && (meeting.Status == MeetingStatus.InProgress
            || (meeting.Status == MeetingStatus.Ended && meeting.ActualEndedAt is { } ended && now - ended <= LateUpload));

    /// <summary>Fixed once the meeting starts, as AI-eCabinet does, and once it is cancelled.</summary>
    public static bool ModeChangeable(MeetingStatus status)
        => status is not (MeetingStatus.InProgress or MeetingStatus.Ended or MeetingStatus.Cancelled);

    /// <summary>Who runs the meeting's record: its current Chair and Secretaries.</summary>
    public static bool RunsRecord(Meeting meeting, Guid actor) => meeting.Participants.Any(p => p.MeetingId == meeting.Id
        && p.UserId == actor && p.Role is MeetingRole.Chair or MeetingRole.Secretary && ParticipantRules.Active(p));
}
