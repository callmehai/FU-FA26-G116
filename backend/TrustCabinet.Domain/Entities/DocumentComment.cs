using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A remark left on a meeting document, so the sitting does not have to be spent reading
/// (SCRUM-23). It belongs to the meeting's copy, not the library row, so it stays in that
/// meeting's context. Replies are one level deep (BR-317).
/// </summary>
public class DocumentComment : Entity
{
    public Guid MeetingDocumentId { get; set; }
    public MeetingDocument MeetingDocument { get; set; } = null!;

    /// <summary>1-based page the comment refers to; PDF only, since DOCX has no pages in the viewer.</summary>
    public int? PageNumber { get; set; }

    /// <summary>Null for a top-level comment.</summary>
    public Guid? ParentCommentId { get; set; }
    public DocumentComment? ParentComment { get; set; }
    public ICollection<DocumentComment> Replies { get; set; } = [];

    /// <summary>Exactly one of <see cref="AuthorId"/> and <see cref="GuestInvitationId"/> is set.</summary>
    public Guid? AuthorId { get; set; }
    public User? Author { get; set; }

    /// <summary>Set when a Guest wrote the comment (ADR-0016).</summary>
    public Guid? GuestInvitationId { get; set; }
    public MeetingGuestInvitation? GuestInvitation { get; set; }

    public required string Body { get; set; }

    /// <summary>Set when the secretary has folded this remark into the agenda or the minutes.</summary>
    public bool IsResolved { get; set; }
}
