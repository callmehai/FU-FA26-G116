using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One stretch of speech in a transcript, timestamped so clicking the text jumps to that moment
/// in the recording (SCRUM-31).
/// </summary>
public class TranscriptSegment : Entity
{
    public Guid TranscriptId { get; set; }
    public Transcript Transcript { get; set; } = null!;

    /// <summary>Order within the transcript, starting at 1.</summary>
    public int Position { get; set; }

    public long StartMs { get; set; }

    public long EndMs { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// What the diarizer called this voice — "Người nói 1", "Người nói 2". A label within this one
    /// recording and nothing more.
    ///
    /// **No voice is stored here.** Who a label is comes from the Secretary (TranscriptSpeaker), helped by a
    /// suggestion from voices enrolled under their own consent (SCRUM-35, ADR-0027); a voice embedding is
    /// biometric data under Luật BVDLCN 91/2025/QH15 and lives only, encrypted, in voice_samples.
    /// </summary>
    public string SpeakerLabel { get; set; } = string.Empty;

    /// <summary>
    /// The real person, once the secretary has said which label is whom. Assigned by hand, per
    /// recording; nothing infers it.
    /// </summary>
    public Guid? SpeakerUserId { get; set; }
    public User? SpeakerUser { get; set; }

    /// <summary>Set when a human corrected the machine's text, so the two can be told apart.</summary>
    public Guid? EditedById { get; set; }
    public User? EditedBy { get; set; }

    public DateTimeOffset? EditedAt { get; set; }
}
