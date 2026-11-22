using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Application.Recordings;

public enum SpeechClipFailure
{
    None, NotFound, Inactive, Forbidden, NotCapturing, Invalid, TooLarge, EngineUnavailable, ReadFailed,
    SaveFailed, ExportForbidden, SecretMeeting,
    /// <summary>A guest whose session never confirmed the recording notice on joining the call (SCRUM-129).</summary>
    NoticeRequired,
}

public sealed record SpeechClipOutcome(SpeechClipFailure Failure = SpeechClipFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

/// <summary>
/// One sentence as everyone in the meeting reads it. An excluded sentence still comes, so the line does not vanish
/// without a trace, but only the Chair and the Secretary get its words. <c>Leak</c> says it was probably overheard.
/// <c>HasAudio</c> says the caller may hear it (<see cref="ISpeechClipService.PlaybackAsync"/>); <c>Peaks</c> draws
/// its waveform, one byte per 100 ms. <c>WordStartsMs</c> is when each word of <c>Text</c> begins, in ms from the sentence's start
/// (<see cref="TrustCabinet.Domain.Transcription.WordTimings"/>), null when unknown or the words were corrected; <c>WordConfidence</c>
/// how sure the recogniser was of each word, 0–100 per cent, on the same terms. <c>SpeakerId</c> is the member's user id, or for a guest (<c>SpeakerIsGuest</c>,
/// SCRUM-129) their invitation's id; a guest's <c>SpeakerName</c> is the name they gave, or the invited one.
/// </summary>
public sealed record SpeechClipView(Guid Id, Guid SpeakerId, string SpeakerName, DateTimeOffset StartedAt, long DurationMs,
    string Text, DateTimeOffset UpdatedAt, DateTimeOffset? EditedAt, string? EditedByName, bool CanDelete,
    bool Excluded = false, string? ExclusionReason = null, string? ExcludedByName = null, SpeechClipLeak? Leak = null,
    bool HasAudio = false, byte[]? Peaks = null, bool SpeakerIsGuest = false, IReadOnlyList<long>? WordStartsMs = null,
    IReadOnlyList<int>? WordConfidence = null);

/// <summary>
/// SpeechClipFold's verdict: the sentence, or the words <c>[from, to)</c> of it, probably are <c>SpeakerName</c>'s,
/// overheard from their sentence <c>ClipId</c>. <c>Whole</c> when all of it is.
/// </summary>
public sealed record SpeechClipLeak(Guid ClipId, Guid SpeakerId, string SpeakerName, bool Whole, IReadOnlyList<int[]> Words);

/// <summary>
/// The meeting's per-mic transcript, or what changed in it since <paramref name="ReadAt"/> of the previous read.
/// <c>CanCapture</c> says whether the caller's own mic may send sentences now; nobody can switch on anyone else's.
/// <c>RetentionDays</c> is how long the words and audio are kept, said to the speaker before they switch on.
/// </summary>
public sealed record SpeechClipList(Guid MeetingId, bool CanCapture, bool CanEdit, DateTimeOffset ReadAt, IReadOnlyList<SpeechClipView> Items, bool CanExclude = false, bool CanExport = false,
    int RetentionDays = 0);

/// <summary>One sentence's audio as the browser sent it; written to the recordings store once it holds words.</summary>
public sealed record SpeechClipUpload(Guid ClientClipId, DateTimeOffset StartedAt, long DurationMs, string ContentType, byte[] Audio);

/// <summary>Told to everyone in the meeting so their transcript re-reads; it carries no words of its own.</summary>
public sealed record SpeechChange(Guid EventId, Guid MeetingId, string ChangeType, DateTimeOffset OccurredAt);

public interface ISpeechChangePublisher
{
    Task PublishAsync(SpeechChange change, CancellationToken ct);
}

public interface ISpeechClipService
{
    Task<SpeechClipOutcome> ListAsync(Guid meetingId, Guid actorId, DateTimeOffset? since, CancellationToken ct = default);
    /// <summary>Checked before the endpoint reads the body, so a refused sentence costs nothing.</summary>
    Task<SpeechClipOutcome> CanCaptureAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    Task<SpeechClipOutcome> AddAsync(Guid meetingId, Guid actorId, SpeechClipUpload upload, CancellationToken ct = default);
    Task<SpeechClipOutcome> EditAsync(Guid clipId, Guid actorId, string? text, CancellationToken ct = default);
    Task<SpeechClipOutcome> DeleteAsync(Guid clipId, Guid actorId, CancellationToken ct = default);
    /// <summary>The Chair or a Secretary takes a sentence out of the record (<c>reason</c> Leak or Other), or puts it back.</summary>
    Task<SpeechClipOutcome> SetExclusionAsync(Guid clipId, Guid actorId, bool excluded, string? reason, CancellationToken ct = default);
    /// <summary>The per-mic transcript as a text file, without the sentences taken out of the record.</summary>
    Task<SpeechClipOutcome> ExportAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    /// <summary>Deletes the sentences, and their audio, whose retention period has run out; returns how many.</summary>
    Task<int> EraseExpiredAsync(CancellationToken ct = default);
    /// <summary>
    /// A signed link to hear one sentence, for whoever may read the meeting's transcript; a sentence taken out of the
    /// record is heard only by the Chair and the Secretary, like its words.
    /// </summary>
    Task<SpeechClipOutcome> PlaybackAsync(Guid clipId, Guid actorId, CancellationToken ct = default);
    /// <summary>The stored audio a verified link points at, or null once the sentence is gone.</summary>
    Task<SpeechClipAudio?> FindAudioAsync(Guid clipId, CancellationToken ct = default);

    // SCRUM-129 — a guest's own mic. A guest never reads the meeting's transcript: only their own sentences, which they
    // send, hear again and delete through their current session of the meeting they were invited to.

    /// <summary>The guest's own sentences only, and whether their mic may send now.</summary>
    Task<SpeechClipOutcome> ListForGuestAsync(MeetingGuestSession guest, Guid meetingId, DateTimeOffset? since,
        CancellationToken ct = default);
    Task<SpeechClipOutcome> CanGuestCaptureAsync(MeetingGuestSession guest, Guid meetingId, CancellationToken ct = default);
    Task<SpeechClipOutcome> AddForGuestAsync(MeetingGuestSession guest, Guid meetingId, SpeechClipUpload upload,
        CancellationToken ct = default);
    Task<SpeechClipOutcome> DeleteForGuestAsync(MeetingGuestSession guest, Guid clipId, CancellationToken ct = default);
    Task<SpeechClipOutcome> PlaybackForGuestAsync(MeetingGuestSession guest, Guid clipId, CancellationToken ct = default);
}

public sealed record SpeechClipAudio(string StoragePath, string ContentType);

/// <summary>Signs and checks the short-lived link an &lt;audio&gt; element plays one sentence from.</summary>
public interface ISpeechClipAudioSigner
{
    RecordingPlayback Sign(Guid clipId, DateTimeOffset now);
    bool Verify(Guid clipId, long expires, string signature, DateTimeOffset now);
}
