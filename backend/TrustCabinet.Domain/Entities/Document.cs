using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One file in someone's personal library (ADR-0018). Deleting it removes only this row: a meeting
/// the file was attached to keeps its own <see cref="MeetingDocument"/> pointing at the same
/// <see cref="StoredFile"/>.
/// </summary>
public class Document : Entity
{
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    /// <summary>Null for a file at the library root.</summary>
    public Guid? FolderId { get; set; }
    public Folder? Folder { get; set; }

    public Guid StoredFileId { get; set; }
    public StoredFile StoredFile { get; set; } = null!;

    /// <summary>The name shown in the library; renaming it does not rename the meeting copy.</summary>
    public required string FileName { get; set; }
}
