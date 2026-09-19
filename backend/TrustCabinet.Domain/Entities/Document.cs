using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A document attached to a meeting. The file itself lives in a <see cref="DocumentVersion"/>:
/// uploading a replacement adds a version rather than overwriting, so an earlier draft that was
/// already circulated stays readable (SCRUM-21).
/// </summary>
public class Document : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    /// <summary>Optional: a document may be tied to one agenda item rather than the meeting at large.</summary>
    public Guid? AgendaItemId { get; set; }
    public AgendaItem? AgendaItem { get; set; }

    public required string Title { get; set; }

    public DocumentCategory Category { get; set; } = DocumentCategory.Material;

    /// <summary>
    /// The version shown by default. Nullable only for the instant between creating the document
    /// row and storing its first version.
    /// </summary>
    public Guid? CurrentVersionId { get; set; }
    public DocumentVersion? CurrentVersion { get; set; }

    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;

    public ICollection<DocumentVersion> Versions { get; set; } = [];
    public ICollection<DocumentComment> Comments { get; set; } = [];
}
