using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One uploaded file behind a <see cref="Document"/>. Versions are never edited or deleted.
/// </summary>
public class DocumentVersion : Entity
{
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    /// <summary>1 for the first upload, incrementing per document.</summary>
    public int VersionNumber { get; set; }

    /// <summary>The name the uploader's file had, shown in the UI and used when downloading.</summary>
    public required string FileName { get; set; }

    /// <summary>Where the bytes are, relative to the configured document store.</summary>
    public required string StoragePath { get; set; }

    /// <summary>
    /// Determined by inspecting the file's contents, not by trusting its extension (SCRUM-21).
    /// </summary>
    public required string ContentType { get; set; }

    public long ByteSize { get; set; }

    /// <summary>SHA-256 of the stored bytes, hex-encoded. Detects a file changed underneath us.</summary>
    public required string ContentSha256 { get; set; }

    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
}
