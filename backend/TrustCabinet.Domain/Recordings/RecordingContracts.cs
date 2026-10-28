using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Domain.Recordings;

public enum RecordingFailure
{
    None, NotFound, Inactive, Forbidden, Locked, Invalid, ReadFailed, SaveFailed, ExportForbidden, SecretMeeting,
    /// <summary>An online meeting is captured per mic (ADR-0040): it has no room to record.</summary>
    NoRoom,
    /// <summary>The room mic switches on only while the meeting runs (ADR-0041).</summary>
    NotRunning,
    /// <summary>The meeting's room mic already streams a take, from this device or another.</summary>
    TakeOpen,
    /// <summary>A part out of order, of the wrong size, or a first part that is not the room mic's WAV.</summary>
    BadPart,
    /// <summary>The take was switched off (saved) already; nothing more is added to it.</summary>
    TakeClosed,
    TooLarge
}

public sealed record RecordingOutcome(RecordingFailure Failure = RecordingFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null, Guid? RecordingId = null);

/// <summary>
/// One upload, already streamed to storage by the endpoint. The service decides whether it may be
/// kept; when it may not, the stored file is deleted again so nothing personal is left behind.
/// </summary>
public sealed record RecordingUpload(string FileName, string ContentType, string StoragePath, long ByteSize,
    RecordingSource Source, bool ConsentConfirmed, string? ConsentPurpose, long? DurationMs);

/// <summary>
/// <c>Status</c> <c>Pending</c> is the room mic's take being streamed: everyone sees that the room is being recorded,
/// nobody hears it yet. <c>CanClose</c> lets the Chair or a Secretary save a take left open on another device.
/// </summary>
public sealed record RecordingView(Guid Id, string FileName, string ContentType, long ByteSize, long DurationMs,
    string Source, string Status, DateTimeOffset CreatedAt, string CreatedByName, DateTimeOffset? ConsentObtainedAt,
    string ConsentPurpose, DateTimeOffset? RetainUntil, DateTimeOffset? DeletedAt, bool CanErase,
    string TranscriptStatus, bool CanDownload = false, Guid CreatedById = default,
    IReadOnlyList<RecordingTake>? Takes = null, bool CanClose = false);

/// <summary><c>CanRecord</c>: the caller may switch the room mic on now (ADR-0041).</summary>
public sealed record RecordingList(Guid MeetingId, bool CanUpload, IReadOnlyList<RecordingView> Items, bool CanRecord = false);

/// <summary>
/// Switching the room mic on. The participants are told once, before the meeting's first take: a later take is
/// appended to the same recording and needs no second confirmation.
/// </summary>
public sealed record RoomTakeStart(bool ConsentConfirmed, string? ConsentPurpose);

/// <summary>A take opened: the browser sends its audio in parts of exactly <c>PartSize</c> bytes, the last one shorter.</summary>
public sealed record RoomTakeOpened(Guid RecordingId, int PartSize, long MaxBytes);

/// <summary>
/// A take saved: <c>RecordingId</c> is the meeting's room recording it went into, null when the mic heard nothing
/// and nothing was kept.
/// </summary>
public sealed record RoomTakeSaved(Guid? RecordingId, bool Appended);

public sealed record RecordingPlayback(string Url, DateTimeOffset ExpiresAt);

public sealed record StoredRecordingFile(string StoragePath, string ContentType, string FileName);

public interface IRecordingService
{
    Task<RecordingOutcome> ListAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    Task<RecordingOutcome> CanUploadAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    Task<RecordingOutcome> AddAsync(Guid meetingId, Guid actorId, RecordingUpload upload, CancellationToken ct = default);
    Task<RecordingOutcome> PlaybackAsync(Guid recordingId, Guid actorId, CancellationToken ct = default);
    /// <summary>A signed link that saves the original file; the Chair or a Secretary, never for a Secret meeting.</summary>
    Task<RecordingOutcome> DownloadAsync(Guid recordingId, Guid actorId, CancellationToken ct = default);
    Task<StoredRecordingFile?> FindPlayableAsync(Guid recordingId, CancellationToken ct = default);
    Task<RecordingOutcome> EraseAsync(Guid recordingId, Guid actorId, CancellationToken ct = default);
    /// <summary>Erases every recording whose retention period has run out; returns how many.</summary>
    Task<int> EraseExpiredAsync(CancellationToken ct = default);
}

/// <summary>
/// ADR-0041 — the room mic, as in AI-eCabinet: the Secretary's browser streams what the room says in parts while the
/// mic is on, so a closed tab or a crash loses nothing the server has; switching it off saves the take into the
/// meeting's one room recording, which is transcribed again as a whole.
/// </summary>
public interface IRoomTakeService
{
    Task<RecordingOutcome> BeginAsync(Guid meetingId, Guid actorId, RoomTakeStart start, CancellationToken ct = default);
    /// <summary>Writes part <paramref name="part"/> (from 1) at its place; sending a part again overwrites it.</summary>
    Task<RecordingOutcome> AppendAsync(Guid recordingId, Guid actorId, int part, Stream content, long? length, bool final,
        CancellationToken ct = default);
    /// <summary>Saves the take; its maker, or the Chair or a Secretary for one left open elsewhere.</summary>
    Task<RecordingOutcome> CompleteAsync(Guid recordingId, Guid actorId, CancellationToken ct = default);
    /// <summary>Saves the takes no part has reached for a long time — a laptop that never came back; returns how many.</summary>
    Task<int> CompleteIdleAsync(CancellationToken ct = default);
}

public interface IRecordingRepository
{
    Task<IReadOnlyList<Recording>> ListAsync(Guid meetingId, CancellationToken ct);
    Task<Recording?> FindAsync(Guid recordingId, bool forUpdate, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ListExpiredAsync(DateTimeOffset now, int take, CancellationToken ct);
    /// <summary>The meeting's newest kept room recording, the one a take is appended to.</summary>
    Task<Recording?> FindRoomRecordingAsync(Guid meetingId, bool forUpdate, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ListIdleTakesAsync(DateTimeOffset before, int take, CancellationToken ct);
    void Add(Recording recording);
    void Remove(Recording recording);
    /// <summary>Deletes the transcript made from the recording, segments included.</summary>
    Task DeleteTranscriptAsync(Guid recordingId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Where the audio bytes live. Paths are relative to the store's root and never come from the client.</summary>
public interface IRecordingStorage
{
    Task<(string StoragePath, long ByteSize)> SaveAsync(Stream content, string extension, long maxBytes, CancellationToken ct);
    /// <summary>A new empty file, for a take whose parts are written as they arrive.</summary>
    string Create(string extension);
    /// <summary>Writes up to <paramref name="maxBytes"/> at <paramref name="offset"/>; returns the bytes written.</summary>
    Task<long> WriteAtAsync(string storagePath, long offset, Stream content, long maxBytes, CancellationToken ct);
    /// <summary>Cuts the file to <paramref name="length"/>: a final part sent again shorter leaves no tail.</summary>
    void Truncate(string storagePath, long length);
    /// <summary>A new file of <paramref name="first"/> followed by <paramref name="second"/> past its first <paramref name="skip"/> bytes.</summary>
    Task<string> ConcatAsync(string first, string second, long skip, CancellationToken ct);
    /// <summary>Writes the real lengths into a streamed WAV's header, which went out before they were known.</summary>
    void SealWav(string storagePath);
    Stream OpenRead(string storagePath);
    void Delete(string storagePath);
}

/// <summary>Raised by <see cref="IRecordingStorage.SaveAsync"/> when the stream passes the size limit.</summary>
public sealed class RecordingTooLargeException() : Exception("The recording exceeds the upload limit.");

/// <summary>Signs and checks the short-lived URL an &lt;audio&gt; element plays from, since it cannot send a bearer token.</summary>
public interface IRecordingPlaybackSigner
{
    /// <param name="download">A link that saves the file rather than plays it. Signed apart, so a playback link,
    /// which every listener gets, cannot be turned into a download.</param>
    RecordingPlayback Sign(Guid recordingId, DateTimeOffset now, bool download = false);
    bool Verify(Guid recordingId, long expires, string signature, DateTimeOffset now, bool download = false);
}

public static class RecordingRules
{
    public const string DefaultPurpose = "Lập biên bản cuộc họp";

    /// <summary>The hub's change type for "the meeting's recordings changed": a take opened or saved, a file kept or erased, a transcript done.</summary>
    public const string RecordingsChanged = "RecordingsChanged";

    /// <summary>Containers browsers record to and phones/recorders save as. The worker decodes them all with ffmpeg.</summary>
    public static readonly IReadOnlyDictionary<string, string> Extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/mpeg"] = ".mp3", ["audio/mp3"] = ".mp3", ["audio/mp4"] = ".m4a", ["audio/x-m4a"] = ".m4a",
        ["audio/m4a"] = ".m4a", ["audio/aac"] = ".aac", ["audio/wav"] = ".wav", ["audio/x-wav"] = ".wav",
        ["audio/wave"] = ".wav", ["audio/webm"] = ".webm", ["audio/ogg"] = ".ogg", ["video/webm"] = ".webm",
    };

    /// <summary>Recording happens during the sitting or is uploaded right after it — never before it starts.</summary>
    public static bool UploadAllowed(MeetingStatus status) => status is MeetingStatus.InProgress or MeetingStatus.Ended;

    /// <summary>
    /// ADR-0040: a meeting held in a room is recorded through the room's one mic, by the Secretary; an online meeting
    /// has no room, every word of it is already on its speaker's own mic.
    /// </summary>
    public static bool HasRoom(MeetingMode mode) => mode is MeetingMode.InPerson or MeetingMode.Hybrid;

    /// <summary>16 kHz mono 16-bit PCM, as the room mic streams it: one second weighs this much.</summary>
    public const int RoomBytesPerSecond = 32_000;

    /// <summary>The streamed WAV's header; a later take's own is dropped when it is appended.</summary>
    public const int WavHeaderBytes = 44;

    /// <summary>The room mic switches on only while the meeting runs, by its Secretary, in a meeting with a room.</summary>
    public static bool CanRecord(Meeting meeting, Guid actor) => meeting.Status == MeetingStatus.InProgress
        && HasRoom(meeting.Mode) && IsSecretary(meeting, actor);

    /// <summary>A take, or the room recording takes were appended to: streamed room-mic audio.</summary>
    public static bool IsRoomMic(Recording recording) => recording.Source == RecordingSource.BrowserCapture
        && recording.ContentType == "audio/wav" && recording.Takes is not null;

    public static long PcmMs(long byteSize) => Math.Max(0, byteSize - WavHeaderBytes) * 1000 / RoomBytesPerSecond;

    /// <summary>
    /// The first part opens with the WAV header the browser writes: PCM, one channel, 16 kHz, 16-bit. Anything else
    /// would be appended as noise, so it is refused before a byte is kept.
    /// </summary>
    public static bool IsRoomWavHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length < WavHeaderBytes) return false;
        static bool Tag(ReadOnlySpan<byte> at, string tag) => at[..4].SequenceEqual(System.Text.Encoding.ASCII.GetBytes(tag));
        static int U16(ReadOnlySpan<byte> at) => at[0] | at[1] << 8;
        static int U32(ReadOnlySpan<byte> at) => at[0] | at[1] << 8 | at[2] << 16 | at[3] << 24;
        return Tag(header, "RIFF") && Tag(header[8..], "WAVE") && Tag(header[12..], "fmt ") && U32(header[16..]) == 16
            && U16(header[20..]) == 1 && U16(header[22..]) == 1 && U32(header[24..]) == 16_000
            && U32(header[28..]) == RoomBytesPerSecond && U16(header[32..]) == 2 && U16(header[34..]) == 16
            && Tag(header[36..], "data");
    }

    public static bool IsSecretary(Meeting meeting, Guid actor) => meeting.Participants
        .Any(p => p.MeetingId == meeting.Id && p.UserId == actor && p.Role == MeetingRole.Secretary && ParticipantRules.Active(p));

    /// <summary>
    /// A recording is personal data of everyone who spoke, so only the people of the meeting hear it — not an
    /// approver or a creator who is not on the roster, even though both can read the meeting itself.
    /// </summary>
    public static bool CanListen(Meeting meeting, Guid actor) => MeetingReadPolicy.CanRead(meeting, actor)
        && meeting.Participants.Any(p => p.MeetingId == meeting.Id && p.UserId == actor && ParticipantRules.Active(p));

    /// <summary>
    /// Taking the meeting's audio or words out of the system — the original file, a transcript as text — is for the
    /// people who run its record, the Chair and the Secretaries, as in AI-eCabinet. A Secret meeting's never leave.
    /// </summary>
    public static RecordingFailure Export(Meeting meeting, Guid actor)
        => !SpeechClipRules.RunsRecord(meeting, actor) ? RecordingFailure.ExportForbidden
            : meeting.Sensitivity == Sensitivity.Secret ? RecordingFailure.SecretMeeting : RecordingFailure.None;

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
