using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Application.Recordings;

/// <summary>
/// One sentence with what the list and the fold need of it. <c>SpeakerId</c> tells speakers apart: a member's user
/// id, or a guest's invitation id (<c>Guest</c>); a guest is named as the transcripts name them.
/// </summary>
public sealed record ClipRow(Guid Id, Guid SpeakerId, string SpeakerName, DateTimeOffset StartedAt, long DurationMs,
    string Text, DateTimeOffset UpdatedAt, DateTimeOffset? EditedAt, string? EditedByName, List<SpeechClipSegment>? Segments,
    DateTimeOffset? ExcludedAt, SpeechClipExclusion? ExclusionReason, string? ExcludedByName, bool HasAudio, byte[]? Peaks,
    bool Guest);

/// <summary>A sentence's sound as the combined recording mixes it in (ADR-0062).</summary>
public sealed record CombinedClip(DateTimeOffset StartedAt, long DurationMs, string AudioPath, DateTimeOffset CreatedAt);

/// <summary>What deciding whether a sentence may be heard needs of it.</summary>
public sealed record ClipPlayback(Guid MeetingId, string? AudioPath, DateTimeOffset? ExcludedAt);

/// <summary>
/// The per-mic transcript's sentences (SCRUM-109) and what the guest's mic checks (SCRUM-129). Rows come untracked,
/// ordered by when they were said and then when they arrived; a <see cref="SpeechClip"/> comes tracked, for the
/// service to change and save through <c>IUnitOfWork.SaveChangesAsync</c>.
/// </summary>
public interface ISpeechClipRepository
{
    /// <summary>Every sentence of the meeting. One query.</summary>
    Task<IReadOnlyList<ClipRow>> ListRowsAsync(Guid meetingId, CancellationToken ct);

    /// <summary>The meeting's sentences changed after <paramref name="after"/>. One query.</summary>
    Task<IReadOnlyList<ClipRow>> ListRowsChangedSinceAsync(Guid meetingId, DateTimeOffset after, CancellationToken ct);

    /// <summary>The meeting's sentences started from <paramref name="from"/> to <paramref name="to"/>, both included. One query.</summary>
    Task<IReadOnlyList<ClipRow>> ListRowsStartedBetweenAsync(Guid meetingId, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct);

    /// <summary>The guest's own sentences in the meeting, only those changed after <paramref name="since"/> when given. One query.</summary>
    Task<IReadOnlyList<ClipRow>> ListGuestRowsAsync(Guid meetingId, Guid invitationId, DateTimeOffset? since,
        CancellationToken ct);

    /// <summary>
    /// The sentence the speaker's browser numbered <paramref name="clientClipId"/>: a member's by
    /// <paramref name="speakerId"/>, otherwise a guest's by <paramref name="guestInvitationId"/>. One query.
    /// </summary>
    Task<ClipRow?> FindRowByClientIdAsync(Guid meetingId, Guid? speakerId, Guid? guestInvitationId, Guid clientClipId,
        CancellationToken ct);

    /// <summary>
    /// The speaker's latest sentence in the meeting started after <paramref name="after"/> and before
    /// <paramref name="before"/>, both excluded: a member's by <paramref name="speakerId"/>, otherwise a guest's by
    /// <paramref name="guestInvitationId"/>. One query.
    /// </summary>
    Task<ClipRow?> FindLastRowStartedBetweenAsync(Guid meetingId, Guid? speakerId, Guid? guestInvitationId,
        DateTimeOffset after, DateTimeOffset before, CancellationToken ct);

    /// <summary>The sentence, tracked; null when there is none. One query.</summary>
    Task<SpeechClip?> FindAsync(Guid clipId, CancellationToken ct);

    /// <summary>The guest's own sentence in the meeting, tracked; null for anyone else's. One query.</summary>
    Task<SpeechClip?> FindGuestClipAsync(Guid clipId, Guid meetingId, Guid invitationId, CancellationToken ct);

    /// <summary>Whether the guest's own sentence in the meeting has audio and is still in the record. One query.</summary>
    Task<bool> HasGuestAudioAsync(Guid clipId, Guid meetingId, Guid invitationId, CancellationToken ct);

    /// <summary>The sentence's meeting, audio and exclusion; null when there is none. One query.</summary>
    Task<ClipPlayback?> FindPlaybackAsync(Guid clipId, CancellationToken ct);

    /// <summary>The sentence's audio file; null when there is none or it has no audio. One query.</summary>
    Task<SpeechClipAudio?> FindAudioAsync(Guid clipId, CancellationToken ct);

    /// <summary>
    /// Whether the guest session is current at <paramref name="now"/>, of that invitation, for that meeting, and named
    /// (UC-04 step 5): the sessions the guest's agenda, roster and call room accept. One query.
    /// </summary>
    Task<bool> IsGuestSessionAdmittedAsync(Guid sessionId, Guid invitationId, Guid meetingId, DateTimeOffset now,
        CancellationToken ct);

    /// <summary>
    /// Whether the call room logged the guest session confirming the recording notice before joining (SCRUM-59). One
    /// query on the audit log.
    /// </summary>
    Task<bool> HasGuestConfirmedNoticeAsync(Guid sessionId, CancellationToken ct);

    /// <summary>The audio files of the sentences stored before <paramref name="cutoff"/>. One query.</summary>
    Task<IReadOnlyList<string>> ListAudioCreatedBeforeAsync(DateTimeOffset cutoff, CancellationToken ct);

    /// <summary>Deletes the sentences stored before <paramref name="cutoff"/>; returns how many. Runs immediately, not staged.</summary>
    Task<int> DeleteCreatedBeforeAsync(DateTimeOffset cutoff, CancellationToken ct);

    /// <summary>The meeting's sentences with sound still in the record, by when they were said. One query.</summary>
    Task<IReadOnlyList<CombinedClip>> ListMixableAsync(Guid meetingId, CancellationToken ct);

    /// <summary>When any sentence of the meeting last changed; null when it has none. One query.</summary>
    Task<DateTimeOffset?> FindLastChangeAsync(Guid meetingId, CancellationToken ct);

    /// <summary>Stages the sentence; it is written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void Add(SpeechClip clip);

    /// <summary>Stages the sentence's removal; it is written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void Remove(SpeechClip clip);
}
