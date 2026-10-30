using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A document in a meeting (ADR-0018, ADR-0020). It points at an immutable
/// <see cref="StoredFile"/> rather than at the library row, so deleting the library copy does not
/// take it out of the meeting. A file has at most one active row per meeting.
/// </summary>
public class MeetingDocument : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public Guid StoredFileId { get; set; }
    public StoredFile StoredFile { get; set; } = null!;

    /// <summary>The library row it came from; null once that row is deleted.</summary>
    public Guid? DocumentId { get; set; }
    public Document? Document { get; set; }

    /// <summary>Copied when the file is added, so a later rename in the library does not change it.</summary>
    public required string FileName { get; set; }

    public Guid AddedById { get; set; }
    public User AddedBy { get; set; } = null!;

    public DateTimeOffset AddedAt { get; set; }

    /// <summary>Set when removed from the meeting (UC-33c). The row and its comments stay as evidence.</summary>
    public DateTimeOffset? RemovedAt { get; set; }
    public Guid? RemovedById { get; set; }
    public User? RemovedBy { get; set; }

    public ICollection<MeetingDocumentAgendaItem> AgendaItemLinks { get; set; } = [];
    public ICollection<VoteDocument> VoteLinks { get; set; } = [];
    public ICollection<DocumentComment> Comments { get; set; } = [];
}
