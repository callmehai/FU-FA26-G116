using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Domain.Recordings;

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

    /// <summary>
    /// SCRUM-129: a guest's sentences are taken only while the meeting runs. A guest's session ends with the meeting,
    /// so the late upload a member's browser may still make (<see cref="LateUpload"/>) has no session to come from.
    /// </summary>
    public static bool AcceptsGuest(Meeting meeting) => Capturing(meeting);

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
