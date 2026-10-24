using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>A folder in someone's personal library, nested at most five levels (ADR-0018 §9).</summary>
public class Folder : Entity
{
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    /// <summary>Null for a folder at the library root.</summary>
    public Guid? ParentId { get; set; }
    public Folder? Parent { get; set; }

    /// <summary>Not unique: two folders may share a name.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// Set on the folder created the first time its owner uploads straight into a meeting. Later
    /// uploads find it by this, not by name, so renaming or moving it does not split the files
    /// (BR-227).
    /// </summary>
    public Guid? MeetingId { get; set; }
    public Meeting? Meeting { get; set; }
}
