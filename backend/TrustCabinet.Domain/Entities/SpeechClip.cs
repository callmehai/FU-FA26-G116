using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One sentence an attendee who joined from afar said into their own microphone, transcribed as soon
/// as they paused (SCRUM-109; online and hybrid meetings, ADR-0040). The speaker is the owner of the
/// mic, so nothing has to guess who said it.
///
/// The sentence's audio is kept beside its words so the meeting can be heard again, sentence by
/// sentence, as AI-eCabinet does (ADR-0039). It is personal data, captured while the speaker's call
/// mic is on after they were told the meeting is recorded: they can delete their sentence, and it goes
/// with the rest of the meeting's recordings when the retention period ends. Nothing biometric is
/// derived from it.
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

    /// <summary>
    /// The stretches of speech the worker found in the sentence: their time, how many of its words each holds, and how
    /// loud each reached the mic. That is what tells a sentence a neighbour's mic overheard from the speaker's own
    /// (<see cref="SpeechClipFold"/>). Numbers only, never audio or a voice vector. Null for sentences stored before
    /// it was kept.
    /// </summary>
    public List<SpeechClipSegment>? Segments { get; set; }

    /// <summary>
    /// Set when the Chair or the Secretary took the sentence out of the record (overheard from another mic, or not
    /// part of the meeting). Nothing is deleted: the sentence stays, and restoring it clears these.
    /// </summary>
    public DateTimeOffset? ExcludedAt { get; set; }
    public Guid? ExcludedById { get; set; }
    public User? ExcludedBy { get; set; }
    public SpeechClipExclusion? ExclusionReason { get; set; }

    /// <summary>
    /// Where the sentence's audio lies in the recordings store, as the browser sent it. Null for a sentence stored
    /// before audio was kept, or one whose audio could not be written.
    /// </summary>
    public string? AudioPath { get; set; }
    public string? AudioContentType { get; set; }

    /// <summary>
    /// The loudness of the audio, one byte (0–255) per 100 ms, to draw the meeting's waveform without fetching any
    /// audio. Null when the audio is not 16-bit PCM WAV.
    /// </summary>
    public byte[]? Peaks { get; set; }
}

/// <summary>Why a sentence was taken out of the record.</summary>
public enum SpeechClipExclusion
{
    /// <summary>Another attendee's words, overheard by this mic.</summary>
    Leak,
    Other,
}
