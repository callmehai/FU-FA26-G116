using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A remark left on a document before the meeting, so the sitting does not have to be spent
/// reading (SCRUM-23). Threaded: a reply points at its parent.
/// </summary>
public class DocumentComment : Entity
{
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    /// <summary>
    /// The version the comment was written against. A later upload does not move the comment,
    /// because the sentence it refers to may no longer be there.
    /// </summary>
    public Guid? DocumentVersionId { get; set; }
    public DocumentVersion? DocumentVersion { get; set; }

    /// <summary>1-based page the comment refers to, when the viewer could pin it to one.</summary>
    public int? PageNumber { get; set; }

    /// <summary>Null for a top-level comment.</summary>
    public Guid? ParentCommentId { get; set; }
    public DocumentComment? ParentComment { get; set; }
    public ICollection<DocumentComment> Replies { get; set; } = [];

    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public required string Body { get; set; }

    /// <summary>Set when the secretary has folded this remark into the agenda or the minutes.</summary>
    public bool IsResolved { get; set; }
}
