using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One voice the diarizer found in a recording — "SPEAKER_00" — and who it is (SCRUM-35).
///
/// The machine only <b>suggests</b>: <see cref="SuggestedUserId"/> is the closest enrolled voice among the people of
/// the meeting who consented, when it is close enough. Only the secretary decides, in <see cref="AssignedUserId"/>.
/// The voice's own embedding is not stored here or anywhere: it is biometric data of people who may never have
/// enrolled, and the suggestion is all it was needed for.
/// </summary>
public class TranscriptSpeaker : Entity
{
    public Guid TranscriptId { get; set; }
    public Transcript Transcript { get; set; } = null!;

    public required string Label { get; set; }

    public long SpeechMs { get; set; }

    public Guid? SuggestedUserId { get; set; }
    public User? SuggestedUser { get; set; }

    /// <summary>Cosine similarity of the best match, kept so the secretary can see how sure the machine was.</summary>
    public double? SuggestionScore { get; set; }

    public Guid? AssignedUserId { get; set; }
    public User? AssignedUser { get; set; }

    public Guid? AssignedById { get; set; }
    public User? AssignedBy { get; set; }

    public DateTimeOffset? AssignedAt { get; set; }
}
