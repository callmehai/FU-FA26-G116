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
    /// **No voiceprint is stored.** There is deliberately no column holding a voice embedding or
    /// any other biometric feature, so the same speaker is not recognised across meetings. That is
    /// out of scope (SCRUM-35), and a voiceprint would be biometric data under Decree
    /// 13/2023/NĐ-CP with its own consent and erasure duties.
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
