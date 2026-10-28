using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// An audio recording of a sitting — captured in the browser, uploaded afterwards, or mixed from
/// the call room (SCRUM-29, SCRUM-44).
///
/// A recording of people speaking is personal data under Luật BVDLCN 91/2025/QH15. Three obligations
/// follow, and the columns below exist so the system can show it kept them rather than merely
/// claiming so: consent is taken before capture starts, the purpose is stated, and the data is
/// erased on request or when its retention period ends.
/// </summary>
public class Recording : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public RecordingSource Source { get; set; }

    public RecordingStatus Status { get; set; } = RecordingStatus.Pending;

    public required string FileName { get; set; }

    public required string StoragePath { get; set; }

    public required string ContentType { get; set; }

    public long ByteSize { get; set; }

    public long DurationMs { get; set; }

    /// <summary>
    /// When the participants were told recording was about to start and did not object. Null means
    /// no consent is on file — the application must refuse to start capture in that case rather
    /// than recording anyway and asking later.
    /// </summary>
    public DateTimeOffset? ConsentObtainedAt { get; set; }

    /// <summary>
    /// Why this was recorded, in the words shown to participants. Luật 91/2025 requires the purpose
    /// to be stated at collection, so it is stored with the data instead of living only in a
    /// screen nobody can produce afterwards.
    /// </summary>
    public string ConsentPurpose { get; set; } = string.Empty;

    /// <summary>When the file is due to be erased. Null means retention has not been decided yet.</summary>
    public DateTimeOffset? RetainUntil { get; set; }

    /// <summary>
    /// Set when the bytes were erased. The row survives so the minutes can still say a recording
    /// existed and was deleted, and so an erasure request can be shown to have been honoured.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public Transcript? Transcript { get; set; }
}
