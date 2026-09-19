using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// An AI-written summary of the meeting, offered to the secretary as raw material (SCRUM-36).
///
/// It is a draft and nothing more. The secretary reviews it, edits it and decides what reaches the
/// minutes; the AI never writes into <see cref="MeetingMinutes"/> itself. Minutes carry weight
/// precisely because a named person is answerable for them.
/// </summary>
public class MeetingSummary : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    /// <summary>Which model produced it, so a summary can be judged against how it was made.</summary>
    public string Model { get; set; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Set once the secretary has read it. Until then the UI must mark the text as AI-generated
    /// and unreviewed wherever it appears.
    /// </summary>
    public Guid? ReviewedById { get; set; }
    public User? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }
}
