using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Notes;

/// <summary>
/// SCRUM-30, UC-82 — the Secretary types running notes during the sitting, one per agenda item and one for the meeting
/// as a whole; the Chair reads them; nobody else learns they exist (BR-395). The minutes draft reads them (BR-402).
/// </summary>
public sealed class MeetingNoteService(IMeetingNoteRepository notes, IMeetingRepository meetings,
    INotesChangePublisher publisher, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<MeetingNoteService> logger) : IMeetingNoteService
{
    public async Task<NoteOutcome> GetAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            return await unitOfWork.ExecuteAsync(async token =>
            {
                var (meeting, failure) = await ReadableAsync(meetingId, actorId, false, token);
                if (meeting is null) return new NoteOutcome(failure);
                // EX-8: a reader who is neither Chair nor Secretary is answered as if there were no notes.
                if (!ParticipantRules.IsManager(meeting, actorId)) return new NoteOutcome(NoteFailure.NotFound);

                var secretaries = Secretaries(meeting);
                var rows = await notes.ListAsync(meetingId, secretaries, token);
                var current = await notes.FindCurrentItemAsync(meetingId, token);
                var canWrite = secretaries.Contains(actorId)
                    && MeetingNoteRules.AcceptsNotes(meeting.Status, await notes.HasMinutesAsync(meetingId, token));
                return new NoteOutcome(State: new(rows.Select(MeetingNoteRules.View).ToList(), current, canWrite,
                    meeting.Sensitivity == Sensitivity.Secret));
            }, TransactionMode.None, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError((error as PersistenceException)?.InnerException ?? error, "Could not read the notes of meeting {MeetingId}", meetingId);
            return new(NoteFailure.ReadFailed);
        }
    }

    public async Task<NoteOutcome> SaveAsync(Guid meetingId, Guid actorId, Guid? agendaItemId, string? body,
        DateTimeOffset? loadedAt, CancellationToken ct = default)
    {
        NoteOutcome outcome;
        try
        {
            // The meeting row is locked for the whole save, as deleting an agenda item and building the minutes lock it:
            // a note is saved either before those (and folded or written into the minutes) or after (and refused).
            outcome = await unitOfWork.ExecuteAsync(token => SaveLockedAsync(meetingId, actorId, agendaItemId, body,
                loadedAt, token), TransactionMode.ReadCommitted, ct);
        }
        catch (ReferenceConflictException error)
        {
            logger.LogInformation(error.InnerException ?? error, "Agenda item {ItemId} went away while its note was saved", agendaItemId);
            return new(NoteFailure.ItemGone);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError((error as PersistenceException)?.InnerException ?? error, "Could not save a note in meeting {MeetingId}", meetingId);
            return new(NoteFailure.SaveFailed);
        }
        if (outcome.Note is not { } saved) return outcome;

        try
        {
            await publisher.PublishAsync(new(Guid.CreateVersion7(), meetingId, agendaItemId, saved.UpdatedAt), ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The note is saved; the other window finds out on its next save (MSG418) instead of at once.
            logger.LogWarning(error, "Could not publish the note change in meeting {MeetingId}", meetingId);
        }
        return outcome;
    }

    private async Task<NoteOutcome> SaveLockedAsync(Guid meetingId, Guid actorId, Guid? agendaItemId, string? body,
        DateTimeOffset? loadedAt, CancellationToken ct)
    {
        var (meeting, failure) = await ReadableAsync(meetingId, actorId, true, ct);
        if (meeting is null) return new(failure);
        // In the order of UC-82 step 4: the role, the meeting, the item, the length; the copy last, in the write.
        // EX-1: a reader of the meeting who is not its current Secretary — the Chair, or a Secretary replaced while
        // typing — is told so (MSG416), and the panel keeps the text to copy. Nothing about the notes is disclosed.
        if (!Secretaries(meeting).Contains(actorId)) return new(NoteFailure.Forbidden);
        if (!MeetingNoteRules.AcceptsNotes(meeting.Status, await notes.HasMinutesAsync(meetingId, ct))) return new(NoteFailure.Closed);
        if (agendaItemId is { } itemId && !await notes.AgendaItemExistsAsync(meetingId, itemId, ct))
            return new(NoteFailure.ItemGone);
        var text = (body ?? "").Replace("\r\n", "\n", StringComparison.Ordinal);
        if (text.Length > MeetingNoteRules.MaxLength) return new(NoteFailure.TooLong);

        var existing = await notes.FindIdAsync(meetingId, agendaItemId, actorId, ct);
        var now = clock.GetUtcNow();
        if (existing is null)
        {
            // A note the caller has never seen saved. If it read one, that note has gone since: its copy is stale.
            if (loadedAt is not null) return new(NoteFailure.Stale);
            notes.Add(new MeetingNote { MeetingId = meetingId, AgendaItemId = agendaItemId, AuthorId = actorId,
                Body = text, CreatedAt = now, UpdatedAt = now });
            await unitOfWork.SaveChangesAsync(ct);
        }
        else
        {
            // BR-399: a save from another window that landed after this one was read makes it stale, never overwritten.
            // The 1 ms window absorbs the precision PostgreSQL drops.
            if (loadedAt is null) return new(NoteFailure.Stale);
            var from = loadedAt.Value.AddMilliseconds(-1);
            var to = loadedAt.Value.AddMilliseconds(1);
            var rows = await notes.TryUpdateAsync(existing.Value, from, to, text, now, ct);
            if (rows == 0) return new(NoteFailure.Stale);
        }
        // Read back under the same lock: no other save of this meeting can land in between.
        var saved = await notes.GetAsync(meetingId, agendaItemId, actorId, ct);
        return new(Note: MeetingNoteRules.View(saved));
    }

    /// <summary>The meeting's current Secretaries (BR-395). The model allows one; a list keeps the check honest if not.</summary>
    private static HashSet<Guid> Secretaries(Meeting meeting) => meeting.Participants
        .Where(p => p.MeetingId == meeting.Id && p.Role == MeetingRole.Secretary && ParticipantRules.Active(p))
        .Select(p => p.UserId).ToHashSet();

    private async Task<(Meeting? Meeting, NoteFailure Failure)> ReadableAsync(Guid meetingId, Guid actorId, bool forUpdate,
        CancellationToken ct)
    {
        if (!await meetings.IsUserActiveAsync(actorId, ct)) return (null, NoteFailure.Inactive);
        var meeting = await meetings.FindWorkflowAsync(meetingId, forUpdate, ct);
        if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId)) return (null, NoteFailure.NotFound);
        return (meeting, NoteFailure.None);
    }
}
