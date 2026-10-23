using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Infrastructure.Recordings;

/// <summary>
/// SCRUM-29 — recordings of a sitting. A recording is personal data of everyone who spoke (Decree 13/2023/NĐ-CP,
/// now Luật BVDLCN 91/2025/QH15), so three obligations are kept in data, not just promised in a document: consent is
/// confirmed before the file is kept, the purpose is stored with it, and it is erased on request or when its
/// retention period ends — the row stays as evidence that it existed and was erased.
/// </summary>
public sealed class RecordingService(IMeetingRepository meetings, IRecordingRepository recordings,
    IRecordingStorage storage, IRecordingPlaybackSigner signer, IAuditService audit, TimeProvider clock,
    IOptions<RecordingOptions> options, ILogger<RecordingService> logger) : IRecordingService
{
    private static bool PersistenceError(Exception ex) => ex is DbUpdateException or NpgsqlException or TimeoutException;

    public async Task<RecordingOutcome> ListAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var (meeting, failure) = await ReadableMeetingAsync(meetingId, actorId, ct);
            if (meeting is null) return new(failure);
            var secretary = RecordingRules.IsSecretary(meeting, actorId);
            var items = await recordings.ListAsync(meetingId, ct);
            return new(Data: new RecordingList(meetingId, secretary && RecordingRules.UploadAllowed(meeting.Status),
                items.Select(r => View(r, secretary)).ToArray()));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to list recordings of {MeetingId}", meetingId);
            return new(RecordingFailure.ReadFailed);
        }
    }

    /// <summary>Checked before the endpoint streams a single byte to disk, so a refused upload costs nothing.</summary>
    public async Task<RecordingOutcome> CanUploadAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var (meeting, failure) = await ReadableMeetingAsync(meetingId, actorId, ct);
            if (meeting is null) return new(failure);
            if (!RecordingRules.IsSecretary(meeting, actorId)) return new(RecordingFailure.Forbidden);
            return RecordingRules.UploadAllowed(meeting.Status) ? new() : new(RecordingFailure.Locked);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to check recording upload for {MeetingId}", meetingId);
            return new(RecordingFailure.SaveFailed);
        }
    }

    public async Task<RecordingOutcome> AddAsync(Guid meetingId, Guid actorId, RecordingUpload upload, CancellationToken ct = default)
    {
        var kept = false;
        try
        {
            var outcome = await meetings.InTransactionAsync(async token =>
            {
                var meeting = await meetings.FindWorkflowAsync(meetingId, true, token);
                if (!await meetings.IsUserActiveAsync(actorId, token)) return new RecordingOutcome(RecordingFailure.Inactive);
                if (meeting is null || !RecordingRules.CanListen(meeting, actorId)) return new(RecordingFailure.NotFound);
                if (!RecordingRules.IsSecretary(meeting, actorId)) return new(RecordingFailure.Forbidden);
                if (!RecordingRules.UploadAllowed(meeting.Status)) return new(RecordingFailure.Locked);
                var errors = RecordingRules.Validate(upload);
                if (errors.Count > 0) return new(RecordingFailure.Invalid, Errors: errors);

                var now = clock.GetUtcNow();
                var purpose = string.IsNullOrWhiteSpace(upload.ConsentPurpose) ? RecordingRules.DefaultPurpose : upload.ConsentPurpose.Trim();
                var recording = new Recording
                {
                    MeetingId = meetingId, Source = upload.Source, Status = RecordingStatus.Available,
                    FileName = upload.FileName, StoragePath = upload.StoragePath, ContentType = upload.ContentType,
                    ByteSize = upload.ByteSize, DurationMs = upload.DurationMs ?? 0, ConsentObtainedAt = now,
                    ConsentPurpose = purpose, RetainUntil = now.AddDays(options.Value.RetentionDays),
                    CreatedById = actorId, CreatedAt = now, UpdatedAt = now,
                };
                recordings.Add(recording);
                await audit.RecordAsync(AuditActions.RecordingUploaded, AuditActions.RecordingObjectType, recording.Id, actorId,
                    new { meetingId, upload.FileName, upload.ByteSize, source = upload.Source.ToString(), purpose,
                        retainUntil = recording.RetainUntil }, token);
                await recordings.SaveChangesAsync(token);
                return new RecordingOutcome(RecordingId: recording.Id);
            }, ct);
            kept = outcome.Failure == RecordingFailure.None;
            return outcome;
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to store recording for {MeetingId}", meetingId);
            return new(RecordingFailure.SaveFailed);
        }
        finally
        {
            // Whatever refused the upload, the bytes already on disk are someone's voice: do not leave them behind.
            if (!kept) DeleteQuietly(upload.StoragePath);
        }
    }

    public async Task<RecordingOutcome> PlaybackAsync(Guid recordingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var recording = await recordings.FindAsync(recordingId, false, ct);
            if (recording is null) return new(RecordingFailure.NotFound);
            var (meeting, failure) = await ReadableMeetingAsync(recording.MeetingId, actorId, ct);
            if (meeting is null) return new(failure);
            if (recording.Status != RecordingStatus.Available) return new(RecordingFailure.NotFound);
            return new(Data: signer.Sign(recordingId, clock.GetUtcNow()));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to sign playback for {RecordingId}", recordingId);
            return new(RecordingFailure.ReadFailed);
        }
    }

    public async Task<StoredRecordingFile?> FindPlayableAsync(Guid recordingId, CancellationToken ct = default)
    {
        var recording = await recordings.FindAsync(recordingId, false, ct);
        return recording is { Status: RecordingStatus.Available }
            ? new(recording.StoragePath, recording.ContentType, recording.FileName) : null;
    }

    public async Task<RecordingOutcome> EraseAsync(Guid recordingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            string? path = null;
            var outcome = await meetings.InTransactionAsync(async token =>
            {
                var recording = await recordings.FindAsync(recordingId, true, token);
                if (recording is null) return new RecordingOutcome(RecordingFailure.NotFound);
                var meeting = await meetings.FindWorkflowAsync(recording.MeetingId, false, token);
                if (!await meetings.IsUserActiveAsync(actorId, token)) return new(RecordingFailure.Inactive);
                if (meeting is null || !RecordingRules.CanListen(meeting, actorId)) return new(RecordingFailure.NotFound);
                if (!RecordingRules.IsSecretary(meeting, actorId)) return new(RecordingFailure.Forbidden);
                if (recording.Status == RecordingStatus.Deleted) return new(RecordingId: recording.Id);
                path = await MarkErasedAsync(recording, actorId, "request", token);
                return new RecordingOutcome(RecordingId: recording.Id);
            }, ct);
            if (path is not null) DeleteQuietly(path);
            return outcome;
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to erase recording {RecordingId}", recordingId);
            return new(RecordingFailure.SaveFailed);
        }
    }

    public async Task<int> EraseExpiredAsync(CancellationToken ct = default)
    {
        var erased = 0;
        foreach (var id in await recordings.ListExpiredAsync(clock.GetUtcNow(), 50, ct))
        {
            string? path = null;
            await meetings.InTransactionAsync(async token =>
            {
                var recording = await recordings.FindAsync(id, true, token);
                if (recording is { Status: not RecordingStatus.Deleted, RetainUntil: { } until } && until < clock.GetUtcNow())
                    path = await MarkErasedAsync(recording, null, "retention", token);
                return 0;
            }, ct);
            if (path is null) continue;
            DeleteQuietly(path);
            erased++;
        }
        return erased;
    }

    /// <summary>
    /// The row is kept and marked, the transcript made from it goes with the audio: a transcript is the same speech
    /// in another form. The file itself is deleted by the caller once the transaction has committed, so a rollback
    /// never leaves a row pointing at bytes that are already gone.
    /// </summary>
    private async Task<string> MarkErasedAsync(Recording recording, Guid? actorId, string reason, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var path = recording.StoragePath;
        recording.Status = RecordingStatus.Deleted;
        recording.DeletedAt = now;
        recording.UpdatedAt = now;
        await recordings.DeleteTranscriptAsync(recording.Id, ct);
        await audit.RecordAsync(AuditActions.RecordingErased, AuditActions.RecordingObjectType, recording.Id, actorId,
            new { recording.MeetingId, recording.FileName, reason }, ct);
        await recordings.SaveChangesAsync(ct);
        return path;
    }

    private async Task<(Meeting? Meeting, RecordingFailure Failure)> ReadableMeetingAsync(Guid meetingId, Guid actorId,
        CancellationToken ct)
    {
        if (!await meetings.IsUserActiveAsync(actorId, ct)) return (null, RecordingFailure.Inactive);
        var meeting = await meetings.FindWorkflowAsync(meetingId, false, ct);
        return meeting is not null && RecordingRules.CanListen(meeting, actorId)
            ? (meeting, RecordingFailure.None) : (null, RecordingFailure.NotFound);
    }

    private void DeleteQuietly(string path)
    {
        try { storage.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The row already says Deleted; a sweeper retrying file deletion would be the next step if this shows up.
            logger.LogError(ex, "Could not delete recording file {Path}", path);
        }
    }

    private static RecordingView View(Recording r, bool secretary) => new(r.Id, r.FileName, r.ContentType, r.ByteSize,
        r.DurationMs, r.Source.ToString(), r.Status.ToString(), r.CreatedAt, r.CreatedBy.FullName, r.ConsentObtainedAt,
        r.ConsentPurpose, r.RetainUntil, r.DeletedAt, secretary && r.Status != RecordingStatus.Deleted);
}
