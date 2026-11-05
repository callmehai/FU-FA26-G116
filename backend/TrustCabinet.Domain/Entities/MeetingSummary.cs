using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

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

    /// <summary>
    /// The draft as JSON (<c>SummaryDraft</c>): the objectives, one part per agenda item, the conclusions and the vote
    /// results. Empty until the first run finishes; a rerun replaces it only once it succeeds.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    public SummaryStatus Status { get; set; } = SummaryStatus.Queued;

    /// <summary>Why the last run failed, in words the Secretary can act on. Empty otherwise.</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    public Guid RequestedById { get; set; }
    public User RequestedBy { get; set; } = null!;

    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>Who last changed the text by hand. It stays marked as AI-written all the same.</summary>
    public Guid? EditedById { get; set; }
    public User? EditedBy { get; set; }

    public DateTimeOffset? EditedAt { get; set; }

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
