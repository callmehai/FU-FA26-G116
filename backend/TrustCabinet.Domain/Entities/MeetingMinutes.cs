using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// The minutes of a meeting (SCRUM-32, SCRUM-33).
///
/// Issued minutes are never edited. A correction is a new revision pointing at the one it
/// replaces, so the record of what was issued at the time survives — the whole point of minutes
/// being a document people rely on.
/// </summary>
public class MeetingMinutes : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    /// <summary>1 for the first minutes, incrementing for each correction.</summary>
    public int RevisionNumber { get; set; } = 1;

    /// <summary>The revision this one corrects. Null on the first.</summary>
    public Guid? SupersedesId { get; set; }
    public MeetingMinutes? Supersedes { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// When the AI draft (<see cref="MeetingSummary"/>) the content was last built from had last changed; null when it
    /// was built without one. A draft that changed since is newer than these minutes, whatever was saved by hand after.
    /// </summary>
    public DateTimeOffset? BuiltFromDraftAt { get; set; }

    public MinutesStatus Status { get; set; } = MinutesStatus.Draft;

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    /// <summary>The Secretary who last sent this revision to the Chair (UC-61).</summary>
    public Guid? SubmittedById { get; set; }
    public User? SubmittedBy { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public Guid? ApprovedById { get; set; }
    public User? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Why it was sent back, shown to the secretary so they know what to fix.</summary>
    public string RejectionReason { get; set; } = string.Empty;

    /// <summary>The Chair who last returned this revision, and when (UC-65).</summary>
    public Guid? ReturnedById { get; set; }
    public User? ReturnedBy { get; set; }

    public DateTimeOffset? ReturnedAt { get; set; }

    /// <summary>
    /// When it was issued. The content is locked by <see cref="Status"/>: only a Draft or a Rejected revision is edited.
    /// </summary>
    public DateTimeOffset? IssuedAt { get; set; }

    public ICollection<MinutesSignature> Signatures { get; set; } = [];
}
