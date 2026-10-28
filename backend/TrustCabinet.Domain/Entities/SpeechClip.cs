using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One sentence an attendee said into their own microphone, transcribed as soon as they paused
/// (SCRUM-109, capture mode <see cref="Enums.CaptureMode.PerMic"/>). The speaker is the owner of
/// the mic, so nothing has to guess who said it.
///
/// **No audio is kept.** The browser sends the sentence's audio, the speech worker turns it into
/// text and both forget it (ADR-0026); only the words, their time and their speaker are stored —
/// less personal data than a room recording, and nothing biometric.
/// </summary>
public class SpeechClip : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    /// <summary>Who spoke: always the signed-in owner of the mic that sent it, never chosen by anyone.</summary>
    public Guid SpeakerId { get; set; }
    public User Speaker { get; set; } = null!;

    /// <summary>
    /// The id the speaker's browser gave the sentence before sending it. A retry after a lost
    /// response sends the same id, so the sentence is stored once.
    /// </summary>
    public Guid ClientClipId { get; set; }

    /// <summary>When the sentence began, by the speaker's clock, clamped to the meeting's time.</summary>
    public DateTimeOffset StartedAt { get; set; }

    public long DurationMs { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>Set when the Secretary corrected the machine's text, so the two can be told apart.</summary>
    public Guid? EditedById { get; set; }
    public User? EditedBy { get; set; }
    public DateTimeOffset? EditedAt { get; set; }
}
