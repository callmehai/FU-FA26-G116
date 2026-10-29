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

/// <summary>One sentence as everyone in the meeting reads it.</summary>
public sealed record SpeechClipView(Guid Id, Guid SpeakerId, string SpeakerName, DateTimeOffset StartedAt, long DurationMs,
    string Text, DateTimeOffset UpdatedAt, DateTimeOffset? EditedAt, string? EditedByName, bool CanDelete);

/// <summary>
/// The meeting's per-mic transcript, or what changed in it since <paramref name="ReadAt"/> of the previous read.
/// <c>CanCapture</c> says whether the caller's own mic may send sentences now; nobody can switch on anyone else's.
/// </summary>
public sealed record SpeechClipList(Guid MeetingId, string CaptureMode, bool CanCapture, bool CanEdit, bool CanChangeMode,
    DateTimeOffset ReadAt, IReadOnlyList<SpeechClipView> Items);

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

    /// <summary>What a browser's own capture produces: WAV it encoded itself, or MediaRecorder's containers.</summary>
    public static readonly IReadOnlySet<string> ContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "audio/wav", "audio/x-wav", "audio/wave", "audio/webm", "audio/ogg",
    };

    /// <summary>Sentences are taken only while the meeting runs, and only when it is captured per mic.</summary>
    public static bool Capturing(Meeting meeting)
        => meeting.Status == MeetingStatus.InProgress && meeting.CaptureMode == CaptureMode.PerMic;

    /// <summary>Fixed once the meeting starts, as AI-eCabinet does, and once it is cancelled.</summary>
    public static bool ModeChangeable(MeetingStatus status)
        => status is not (MeetingStatus.InProgress or MeetingStatus.Ended or MeetingStatus.Cancelled);

    /// <summary>Who runs the meeting's record: its current Chair and Secretaries.</summary>
    public static bool RunsRecord(Meeting meeting, Guid actor) => meeting.Participants.Any(p => p.MeetingId == meeting.Id
        && p.UserId == actor && p.Role is MeetingRole.Chair or MeetingRole.Secretary && ParticipantRules.Active(p));
}
