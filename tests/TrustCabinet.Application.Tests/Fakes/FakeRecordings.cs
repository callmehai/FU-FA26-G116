using TrustCabinet.Application.Recordings;
using TrustCabinet.Application.Transcription;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// Recordings in a list. Rows read under their lock are in <see cref="Locked"/>; nothing here saves. Members no test
/// needs yet throw.
/// </summary>
public sealed class FakeRecordingRepository : IRecordingRepository
{
    public List<Recording> Recordings { get; } = [];
    public List<Guid> Locked { get; } = [];
    public List<Guid> TranscriptsDeleted { get; } = [];
    public List<CombinableMeeting> CombinableMeetings { get; } = [];

    public Task<IReadOnlyList<Recording>> ListAsync(Guid meetingId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Recording>>(Recordings.Where(r => r.MeetingId == meetingId)
            .OrderByDescending(r => r.CreatedAt).ToArray());

    public Task<Recording?> FindAsync(Guid recordingId, bool forUpdate, CancellationToken ct)
    {
        if (forUpdate) Locked.Add(recordingId);
        return Task.FromResult(Recordings.SingleOrDefault(r => r.Id == recordingId));
    }

    public Task<IReadOnlyList<Guid>> ListExpiredAsync(DateTimeOffset now, int take, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Guid>>(Recordings
            .Where(r => r.Status != RecordingStatus.Deleted && r.RetainUntil < now).Select(r => r.Id).Take(take).ToArray());

    public Task<Recording?> FindRoomRecordingAsync(Guid meetingId, bool forUpdate, CancellationToken ct)
    {
        var room = Recordings.Where(r => r.MeetingId == meetingId && RecordingRules.IsRoomMic(r)
            && r.Status == RecordingStatus.Available).MaxBy(r => r.CreatedAt);
        if (room is not null && forUpdate) Locked.Add(room.Id);
        return Task.FromResult(room);
    }

    public void Add(Recording recording) => Recordings.Add(recording);

    public void Remove(Recording recording) => Recordings.Remove(recording);

    public Task DeleteTranscriptAsync(Guid recordingId, CancellationToken ct)
    {
        TranscriptsDeleted.Add(recordingId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CombinableMeeting>> ListCombinableMeetingsAsync(DateTimeOffset settled, DateTimeOffset since, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CombinableMeeting>>(CombinableMeetings);

    public Task<IReadOnlyList<Recording>> ListByMeetingAsync(Guid meetingId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Recording>>(Recordings.Where(r => r.MeetingId == meetingId).ToArray());

    /// <summary>Runs on the row <see cref="LockAsync"/> returns: a change another request made just before the lock.</summary>
    public Action<Recording>? BeforeLock { get; set; }

    public async Task<Recording?> LockAsync(Guid recordingId, CancellationToken ct)
    {
        var row = await FindAsync(recordingId, true, ct);
        if (row is not null) BeforeLock?.Invoke(row);
        return row;
    }

    public Task<IReadOnlyList<Guid>> ListIdleTakesAsync(DateTimeOffset before, int take, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>
/// The sentences as their rows (<see cref="Rows"/>); a sentence staged with <see cref="Add"/> is read back as one, or,
/// with <see cref="ConcurrentCopy"/> set, the copy another request stored first is. Guests admitted and notices
/// confirmed are by session id.
/// </summary>
public sealed class FakeSpeechClipRepository : ISpeechClipRepository
{
    public List<ClipRow> Rows { get; } = [];

    /// <summary>The id the speaker's browser gave each row's sentence.</summary>
    public Dictionary<Guid, Guid> ClientIds { get; } = [];
    public List<SpeechClip> Added { get; } = [];
    public List<SpeechClip> Clips { get; } = [];
    public HashSet<Guid> AdmittedSessions { get; } = [];
    public HashSet<Guid> ConfirmedNotices { get; } = [];
    public List<CombinedClip> Mixable { get; } = [];
    public DateTimeOffset? LastChange { get; set; }

    /// <summary>What the next <see cref="Add"/> leaves behind in its place: the same sentence, sent twice at once.</summary>
    public ClipRow? ConcurrentCopy { get; set; }

    public Task<IReadOnlyList<ClipRow>> ListRowsAsync(Guid meetingId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ClipRow>>(Rows);

    public Task<ClipRow?> FindRowByClientIdAsync(Guid meetingId, Guid? speakerId, Guid? guestInvitationId, Guid clientClipId,
        CancellationToken ct)
        => Task.FromResult(Rows.SingleOrDefault(r => r.SpeakerId == (speakerId ?? guestInvitationId)
            && ClientIds.GetValueOrDefault(r.Id) == clientClipId));

    public Task<ClipRow?> FindLastRowStartedBetweenAsync(Guid meetingId, Guid? speakerId, Guid? guestInvitationId,
        DateTimeOffset after, DateTimeOffset before, CancellationToken ct)
        => Task.FromResult(Rows.Where(r => r.SpeakerId == (speakerId ?? guestInvitationId) && r.StartedAt > after && r.StartedAt < before)
            .MaxBy(r => r.StartedAt));

    public Task<SpeechClip?> FindAsync(Guid clipId, CancellationToken ct) => Task.FromResult(Clips.SingleOrDefault(c => c.Id == clipId));

    public Task<bool> IsGuestSessionAdmittedAsync(Guid sessionId, Guid invitationId, Guid meetingId, DateTimeOffset now,
        CancellationToken ct) => Task.FromResult(AdmittedSessions.Contains(sessionId));

    public Task<bool> HasGuestConfirmedNoticeAsync(Guid sessionId, CancellationToken ct)
        => Task.FromResult(ConfirmedNotices.Contains(sessionId));

    public Task<IReadOnlyList<string>> ListAudioCreatedBeforeAsync(DateTimeOffset cutoff, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<string>>(Clips.Where(c => c.CreatedAt < cutoff && c.AudioPath != null)
            .Select(c => c.AudioPath!).ToArray());

    public Task<int> DeleteCreatedBeforeAsync(DateTimeOffset cutoff, CancellationToken ct)
        => Task.FromResult(Clips.RemoveAll(c => c.CreatedAt < cutoff));

    public Task<IReadOnlyList<CombinedClip>> ListMixableAsync(Guid meetingId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CombinedClip>>(Mixable);

    public Task<DateTimeOffset?> FindLastChangeAsync(Guid meetingId, CancellationToken ct) => Task.FromResult(LastChange);

    public void Add(SpeechClip clip)
    {
        Added.Add(clip);
        var row = ConcurrentCopy ?? new ClipRow(clip.Id, clip.SpeakerId ?? clip.GuestInvitationId ?? Guid.Empty, "Thành viên",
            clip.StartedAt, clip.DurationMs, clip.Text, clip.UpdatedAt, clip.EditedAt, null, clip.Segments, clip.ExcludedAt,
            clip.ExclusionReason, null, clip.AudioPath != null, clip.Peaks, clip.GuestInvitationId != null);
        Rows.Add(row);
        ClientIds[row.Id] = clip.ClientClipId;
    }

    public void Remove(SpeechClip clip) => Clips.Remove(clip);

    public Task<IReadOnlyList<ClipRow>> ListRowsChangedSinceAsync(Guid meetingId, DateTimeOffset after, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<ClipRow>> ListRowsStartedBetweenAsync(Guid meetingId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<ClipRow>> ListGuestRowsAsync(Guid meetingId, Guid invitationId, DateTimeOffset? since, CancellationToken ct) => throw new NotSupportedException();
    public Task<SpeechClip?> FindGuestClipAsync(Guid clipId, Guid meetingId, Guid invitationId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> HasGuestAudioAsync(Guid clipId, Guid meetingId, Guid invitationId, CancellationToken ct) => throw new NotSupportedException();
    public Task<ClipPlayback?> FindPlaybackAsync(Guid clipId, CancellationToken ct) => throw new NotSupportedException();
    public Task<SpeechClipAudio?> FindAudioAsync(Guid clipId, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>
/// Files in memory, by path. Deleted paths are kept in order in <see cref="Deleted"/>, with how many commits the unit of
/// work had made by then in <see cref="CommitsAtDelete"/>: a file is deleted only once its row's change has committed.
/// </summary>
public sealed class FakeRecordingStorage(FakeUnitOfWork? unitOfWork = null) : IRecordingStorage
{
    private int _next;

    public Dictionary<string, byte[]> Files { get; } = [];
    public List<string> Deleted { get; } = [];
    public List<int> CommitsAtDelete { get; } = [];

    public async Task<(string StoragePath, long ByteSize)> SaveAsync(Stream content, string extension, long maxBytes, CancellationToken ct)
    {
        var path = Create(extension);
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, ct);
        Files[path] = copy.ToArray();
        return (path, Files[path].Length);
    }

    public string Create(string extension)
    {
        var path = $"file-{++_next}{extension}";
        Files[path] = [];
        return path;
    }

    public async Task<long> WriteAtAsync(string storagePath, long offset, Stream content, long maxBytes, CancellationToken ct)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, ct);
        var file = Files[storagePath];
        var written = (int)Math.Min(copy.Length, maxBytes);
        if (file.Length < offset + written) Array.Resize(ref file, (int)offset + written);
        copy.ToArray().AsSpan(0, written).CopyTo(file.AsSpan((int)offset));
        Files[storagePath] = file;
        return written;
    }

    public void Truncate(string storagePath, long length)
    {
        var file = Files[storagePath];
        Array.Resize(ref file, (int)length);
        Files[storagePath] = file;
    }

    public void SealWav(string storagePath) { }

    public Stream OpenRead(string storagePath) => new MemoryStream(Files[storagePath], writable: false);

    public void Delete(string storagePath)
    {
        Files.Remove(storagePath);
        Deleted.Add(storagePath);
        CommitsAtDelete.Add(unitOfWork?.Commits ?? 0);
    }

    public IEnumerable<StoredAudioFile> ListWrittenBefore(DateTimeOffset cutoff) => [];

    public Task<string> ConcatAsync(string first, string second, long skip, CancellationToken ct) => throw new NotSupportedException();
    public Task<string> CutAsync(string source, long from, long length, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>What was told to the meeting, with how many commits the unit of work had made by then.</summary>
public sealed class FakeSpeechChangePublisher(FakeUnitOfWork unitOfWork) : ISpeechChangePublisher
{
    public List<(SpeechChange Change, int CommitsBefore)> Published { get; } = [];

    public Task PublishAsync(SpeechChange change, CancellationToken ct)
    {
        Published.Add((change, unitOfWork.Commits));
        return Task.CompletedTask;
    }
}

/// <summary>Signs every link as <c>signed/{id}</c>; checks nothing.</summary>
public sealed class FakeRecordingSigner : IRecordingPlaybackSigner, ISpeechClipAudioSigner
{
    public RecordingPlayback Sign(Guid recordingId, DateTimeOffset now, bool download = false) => new($"signed/{recordingId}", now);
    public RecordingPlayback Sign(Guid clipId, DateTimeOffset now) => new($"signed/{clipId}", now);
    public bool Verify(Guid recordingId, long expires, string signature, DateTimeOffset now, bool download = false) => throw new NotSupportedException();
    public bool Verify(Guid clipId, long expires, string signature, DateTimeOffset now) => throw new NotSupportedException();
}

/// <summary>Hears <see cref="Text"/> in every sentence, or answers <see cref="Result"/> when a test sets it.</summary>
public sealed class FakeSpeechEngine : ISpeechEngine
{
    public string Text { get; set; } = "Xin chào";
    public SpeechTranscription? Result { get; set; }
    /// <summary>Thrown in place of answering: a worker that failed.</summary>
    public Exception? Fail { get; set; }
    public bool IsConfigured => true;

    /// <summary>The previous sentence each call was given to read ahead, in order.</summary>
    public List<string?> Contexts { get; } = [];

    /// <summary>When set, a call waits for its cancellation instead of answering, after completing this: a long run.</summary>
    public TaskCompletionSource? Hold { get; set; }

    /// <summary>Whether a held call ended by being cancelled.</summary>
    public bool Cancelled { get; private set; }

    public async Task<SpeechTranscription> TranscribeAsync(Stream audio, string contentType, bool diarize, bool clip, CancellationToken ct,
        string? context = null)
    {
        Contexts.Add(context);
        if (Hold is { } hold)
        {
            hold.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                Cancelled = true;
                throw;
            }
        }
        return await (Fail is { } failure ? Task.FromException<SpeechTranscription>(failure)
            : Task.FromResult(Result ?? new SpeechTranscription(1000, "vi", "test", "test", [new SpeechSegment(0, 1000, null, Text)], [])));
    }
}

/// <summary>
/// Transcripts in <see cref="Transcripts"/>, their segments and voices held on them; a new transcript staged by
/// <see cref="Add"/> is in <see cref="Added"/>. Nothing here saves, and <see cref="ReloadAsync"/> reads nothing again.
/// </summary>
public sealed class FakeTranscriptRepository : ITranscriptRepository
{
    public List<Transcript> Transcripts { get; } = [];
    public List<Transcript> Added { get; } = [];
    public List<TranscriptPerson> Guests { get; } = [];

    public void Add(Transcript transcript) => Added.Add(transcript);

    public Task<Transcript?> FindByRecordingAsync(Guid recordingId, bool tracked, CancellationToken ct)
        => Task.FromResult(Transcripts.SingleOrDefault(t => t.RecordingId == recordingId));

    public Task<TranscriptSegment?> FindSegmentAsync(Guid segmentId, CancellationToken ct)
        => Task.FromResult(Transcripts.SelectMany(t => t.Segments).SingleOrDefault(s => s.Id == segmentId));

    public Task<Guid?> FindSegmentRecordingAsync(Guid segmentId, CancellationToken ct)
        => Task.FromResult(Transcripts.SingleOrDefault(t => t.Segments.Any(s => s.Id == segmentId))?.RecordingId);

    public Task<Guid?> ClaimNextAsync(DateTimeOffset now, CancellationToken ct)
    {
        var next = Transcripts.Where(t => t.Status == TranscriptStatus.Pending).MinBy(t => t.CreatedAt);
        if (next is null) return Task.FromResult<Guid?>(null);
        next.Status = TranscriptStatus.Processing;
        return Task.FromResult<Guid?>(next.Id);
    }

    public Task<int> RequeueProcessingAsync(DateTimeOffset now, CancellationToken ct)
    {
        var running = Transcripts.Where(t => t.Status == TranscriptStatus.Processing).ToList();
        running.ForEach(t => t.Status = TranscriptStatus.Pending);
        return Task.FromResult(running.Count);
    }

    public Task<Transcript?> FindForProcessingAsync(Guid transcriptId, CancellationToken ct)
        => Task.FromResult(Transcripts.SingleOrDefault(t => t.Id == transcriptId));

    public Task ReloadAsync(Transcript transcript, CancellationToken ct) => Task.CompletedTask;

    public Task<bool> IsProcessingAsync(Guid transcriptId, CancellationToken ct)
        => Task.FromResult(Transcripts.Any(t => t.Id == transcriptId && t.Status == TranscriptStatus.Processing));

    public Task<TranscriptCorrections> ReadCorrectionsAsync(Guid transcriptId, CancellationToken ct)
    {
        var transcript = Transcripts.Single(t => t.Id == transcriptId);
        return Task.FromResult(new TranscriptCorrections(
            [.. transcript.Segments.OrderBy(s => s.Position).Select(s => new PreviousLine(s.StartMs, s.EndMs, s.SpeakerLabel,
                s.EditedAt is null ? null : s.Text, s.EditedById, s.EditedAt, s.SpeakerAssignedById, s.SpeakerAssignedAt))],
            [.. transcript.Speakers.OrderBy(s => s.Label).Select(s => new PreviousVoice(s.Label, s.AssignedUserId, s.AssignedGuestId,
                s.AssignedById, s.AssignedAt))]));
    }

    public Task DeleteSegmentsAsync(Guid transcriptId, CancellationToken ct)
    {
        Transcripts.Single(t => t.Id == transcriptId).Segments.Clear();
        return Task.CompletedTask;
    }

    public Task DeleteSpeakersAsync(Guid transcriptId, CancellationToken ct)
    {
        Transcripts.Single(t => t.Id == transcriptId).Speakers.Clear();
        return Task.CompletedTask;
    }

    public void AddSpeakers(IEnumerable<TranscriptSpeaker> speakers)
    {
        foreach (var speaker in speakers) Transcripts.Single(t => t.Id == speaker.TranscriptId).Speakers.Add(speaker);
    }

    public void AddSegments(IEnumerable<TranscriptSegment> segments)
    {
        foreach (var segment in segments) Transcripts.Single(t => t.Id == segment.TranscriptId).Segments.Add(segment);
    }

    public Task<IReadOnlyList<TranscriptPerson>> ListGuestsAsync(Guid meetingId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<TranscriptPerson>>(Guests);
}

/// <summary>The combined recording as the services that remove a source see it: each call noted, nothing to erase.</summary>
public sealed class FakeCombinedRecordings : ICombinedRecordingService
{
    public List<(Guid MeetingId, bool Rebuild)> Removed { get; } = [];

    public Task<bool> BuildNextAsync(CancellationToken ct = default) => Task.FromResult(false);

    public Task<string?> SourceRemovedAsync(Guid meetingId, bool rebuild, Guid? actorId, CancellationToken ct = default)
    {
        Removed.Add((meetingId, rebuild));
        return Task.FromResult<string?>(null);
    }
}

/// <summary>ffmpeg as the services see it (ADR-0069): there or not; converting is the Web tests' and the real one's.</summary>
public sealed class FakeAudioTranscoder : IAudioTranscoder
{
    public bool Available { get; set; } = true;
    public int Checks { get; private set; }

    public Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        Checks++;
        return Task.FromResult(Available);
    }

    public Task<Stream> OpenMp3Async(Stream source, CancellationToken ct) => Task.FromResult(source);
}
