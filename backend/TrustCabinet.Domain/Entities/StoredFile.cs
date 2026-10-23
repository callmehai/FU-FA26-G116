using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One uploaded file, immutable once written (ADR-0018). The bytes live in the object store under
/// <see cref="StorageKey"/> (ADR-0024); a library <see cref="Document"/> and any number of
/// <see cref="MeetingDocument"/> rows only point at it, so attaching never copies bytes.
/// </summary>
public class StoredFile : Entity
{
    /// <summary><c>stored-files/{Id}</c>: no file name, meeting or uploader, because object keys
    /// show up in the store's logs and dashboard.</summary>
    public required string StorageKey { get; set; }

    /// <summary>The name at upload, kept here so the row still says what it was after every
    /// library and meeting row pointing at it is gone.</summary>
    public required string OriginalFileName { get; set; }

    /// <summary>Determined by inspecting the bytes, never from the extension or the request's
    /// Content-Type (BR-215).</summary>
    public required string ContentType { get; set; }

    public long ByteSize { get; set; }

    /// <summary>SHA-256 of the stored bytes, lower-case hex.</summary>
    public required string ContentSha256 { get; set; }

    /// <summary>PDF only, counted by the browser before upload and stored for display.</summary>
    public int? PageCount { get; set; }

    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;

    public DateTimeOffset UploadedAt { get; set; }

    /// <summary>Set by the orphan purge once the object is deleted (BR-213). The row stays so a
    /// removed meeting document and the audit log can still refer to it.</summary>
    public DateTimeOffset? PurgedAt { get; set; }
}
