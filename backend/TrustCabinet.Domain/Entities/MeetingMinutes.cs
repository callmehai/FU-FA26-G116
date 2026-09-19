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

    public MinutesStatus Status { get; set; } = MinutesStatus.Draft;

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public DateTimeOffset? SubmittedAt { get; set; }

    public Guid? ApprovedById { get; set; }
    public User? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Why it was sent back, shown to the secretary so they know what to fix.</summary>
    public string RejectionReason { get; set; } = string.Empty;

    /// <summary>
    /// When it was issued. Once set, the application treats <see cref="Content"/> as read-only.
    /// </summary>
    public DateTimeOffset? IssuedAt { get; set; }

    public ICollection<MinutesSignature> Signatures { get; set; } = [];
}
