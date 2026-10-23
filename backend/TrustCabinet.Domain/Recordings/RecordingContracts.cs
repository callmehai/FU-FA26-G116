using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Domain.Recordings;

public enum RecordingFailure
{
    None, NotFound, Inactive, Forbidden, Locked, Invalid, ReadFailed, SaveFailed
}

public sealed record RecordingOutcome(RecordingFailure Failure = RecordingFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null, Guid? RecordingId = null);

/// <summary>
/// One upload, already streamed to storage by the endpoint. The service decides whether it may be
/// kept; when it may not, the stored file is deleted again so nothing personal is left behind.
/// </summary>
public sealed record RecordingUpload(string FileName, string ContentType, string StoragePath, long ByteSize,
    RecordingSource Source, bool ConsentConfirmed, string? ConsentPurpose, long? DurationMs);

public sealed record RecordingView(Guid Id, string FileName, string ContentType, long ByteSize, long DurationMs,
    string Source, string Status, DateTimeOffset CreatedAt, string CreatedByName, DateTimeOffset? ConsentObtainedAt,
    string ConsentPurpose, DateTimeOffset? RetainUntil, DateTimeOffset? DeletedAt, bool CanErase,
    string TranscriptStatus);

public sealed record RecordingList(Guid MeetingId, bool CanUpload, IReadOnlyList<RecordingView> Items);

public sealed record RecordingPlayback(string Url, DateTimeOffset ExpiresAt);

public sealed record StoredRecordingFile(string StoragePath, string ContentType, string FileName);

public interface IRecordingService
{
    Task<RecordingOutcome> ListAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    Task<RecordingOutcome> CanUploadAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    Task<RecordingOutcome> AddAsync(Guid meetingId, Guid actorId, RecordingUpload upload, CancellationToken ct = default);
    Task<RecordingOutcome> PlaybackAsync(Guid recordingId, Guid actorId, CancellationToken ct = default);
    Task<StoredRecordingFile?> FindPlayableAsync(Guid recordingId, CancellationToken ct = default);
    Task<RecordingOutcome> EraseAsync(Guid recordingId, Guid actorId, CancellationToken ct = default);
    /// <summary>Erases every recording whose retention period has run out; returns how many.</summary>
    Task<int> EraseExpiredAsync(CancellationToken ct = default);
}

public interface IRecordingRepository
{
    Task<IReadOnlyList<Recording>> ListAsync(Guid meetingId, CancellationToken ct);
    Task<Recording?> FindAsync(Guid recordingId, bool forUpdate, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ListExpiredAsync(DateTimeOffset now, int take, CancellationToken ct);
    void Add(Recording recording);
    /// <summary>Deletes the transcript made from the recording, segments included.</summary>
    Task DeleteTranscriptAsync(Guid recordingId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Where the audio bytes live. Paths are relative to the store's root and never come from the client.</summary>
public interface IRecordingStorage
{
    Task<(string StoragePath, long ByteSize)> SaveAsync(Stream content, string extension, long maxBytes, CancellationToken ct);
    Stream OpenRead(string storagePath);
    void Delete(string storagePath);
}

/// <summary>Raised by <see cref="IRecordingStorage.SaveAsync"/> when the stream passes the size limit.</summary>
public sealed class RecordingTooLargeException() : Exception("The recording exceeds the upload limit.");

/// <summary>Signs and checks the short-lived URL an &lt;audio&gt; element plays from, since it cannot send a bearer token.</summary>
public interface IRecordingPlaybackSigner
{
    RecordingPlayback Sign(Guid recordingId, DateTimeOffset now);
    bool Verify(Guid recordingId, long expires, string signature, DateTimeOffset now);
}

public static class RecordingRules
{
    public const string DefaultPurpose = "Lập biên bản cuộc họp";

    /// <summary>Containers browsers record to and phones/recorders save as. The worker decodes them all with ffmpeg.</summary>
    public static readonly IReadOnlyDictionary<string, string> Extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/mpeg"] = ".mp3", ["audio/mp3"] = ".mp3", ["audio/mp4"] = ".m4a", ["audio/x-m4a"] = ".m4a",
        ["audio/m4a"] = ".m4a", ["audio/aac"] = ".aac", ["audio/wav"] = ".wav", ["audio/x-wav"] = ".wav",
        ["audio/wave"] = ".wav", ["audio/webm"] = ".webm", ["audio/ogg"] = ".ogg", ["video/webm"] = ".webm",
    };

    /// <summary>Recording happens during the sitting or is uploaded right after it — never before it starts.</summary>
    public static bool UploadAllowed(MeetingStatus status) => status is MeetingStatus.InProgress or MeetingStatus.Ended;

    public static bool IsSecretary(Meeting meeting, Guid actor) => meeting.Participants
        .Any(p => p.MeetingId == meeting.Id && p.UserId == actor && p.Role == MeetingRole.Secretary && ParticipantRules.Active(p));

    /// <summary>
    /// A recording is personal data of everyone who spoke, so only the people of the meeting hear it — not an
    /// approver or a creator who is not on the roster, even though both can read the meeting itself.
    /// </summary>
    public static bool CanListen(Meeting meeting, Guid actor) => MeetingReadPolicy.CanRead(meeting, actor)
        && meeting.Participants.Any(p => p.MeetingId == meeting.Id && p.UserId == actor && ParticipantRules.Active(p));

    public static IReadOnlyDictionary<string, string[]> Validate(RecordingUpload upload)
    {
        var errors = new Dictionary<string, string[]>();
        if (!upload.ConsentConfirmed)
            errors["consentConfirmed"] = ["Phải xác nhận đã thông báo cho người dự và không ai phản đối trước khi lưu bản ghi."];
        if (upload.ConsentPurpose?.Length > 1000) errors["consentPurpose"] = ["Mục đích không quá 1000 ký tự."];
        if (upload.FileName.Length is 0 or > 300) errors["file"] = ["Tên tệp phải có từ 1 đến 300 ký tự."];
        if (upload.ByteSize == 0) errors["file"] = ["Tệp ghi âm rỗng."];
        if (upload.DurationMs is < 0) errors["durationMs"] = ["Thời lượng không hợp lệ."];
        return errors;
    }
}
