using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Notes;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Notes;

/// <summary>
/// SCRUM-30, UC-82 — the Secretary types running notes during the sitting, one per agenda item and one for the meeting
/// as a whole; the Chair reads them; nobody else learns they exist (BR-395). The minutes draft reads them (BR-402).
/// </summary>
public sealed class MeetingNoteService(TrustCabinetDbContext db, IMeetingRepository meetings,
    INotesChangePublisher publisher, TimeProvider clock, ILogger<MeetingNoteService> logger) : IMeetingNoteService
{
    public async Task<NoteOutcome> GetAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var (meeting, failure) = await ReadableAsync(meetingId, actorId, ct);
            if (meeting is null) return new(failure);
            // EX-8: a reader who is neither Chair nor Secretary is answered as if there were no notes.
            if (!ParticipantRules.IsManager(meeting, actorId)) return new(NoteFailure.NotFound);

            var secretaries = Secretaries(meeting);
            var notes = await db.MeetingNotes.AsNoTracking().Include(n => n.Author)
                .Where(n => n.MeetingId == meetingId && secretaries.Contains(n.AuthorId))
                .OrderBy(n => n.AgendaItem == null ? 0 : n.AgendaItem.Position)
                .ThenBy(n => n.Author.FullName)
                .ToListAsync(ct);
            var current = await db.AgendaItems.AsNoTracking()
                .Where(a => a.MeetingId == meetingId && a.Status == AgendaItemStatus.InProgress)
                .Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
            var canWrite = secretaries.Contains(actorId)
                && MeetingNoteRules.AcceptsNotes(meeting.Status, await HasMinutesAsync(meetingId, ct));
            return new(State: new(notes.Select(MeetingNoteRules.View).ToList(), current, canWrite,
                meeting.Sensitivity == Sensitivity.Secret));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not read the notes of meeting {MeetingId}", meetingId);
            return new(NoteFailure.ReadFailed);
        }
    }

    public async Task<NoteOutcome> SaveAsync(Guid meetingId, Guid actorId, Guid? agendaItemId, string? body,
        DateTimeOffset? loadedAt, CancellationToken ct = default)
    {
        MeetingNote? saved;
        try
        {
            var (meeting, failure) = await ReadableAsync(meetingId, actorId, ct);
            if (meeting is null) return new(failure);
            // EX-8: a reader who is neither Chair nor Secretary is answered as if there were no notes, on a save too.
            if (!ParticipantRules.IsManager(meeting, actorId)) return new(NoteFailure.NotFound);
            // In the order of UC-82 step 4: the role, the meeting, the item, the length; the copy last, in the write.
            if (!Secretaries(meeting).Contains(actorId)) return new(NoteFailure.Forbidden);
            if (!MeetingNoteRules.AcceptsNotes(meeting.Status, await HasMinutesAsync(meetingId, ct))) return new(NoteFailure.Closed);
            if (agendaItemId is { } itemId && !await db.AgendaItems.AnyAsync(a => a.Id == itemId && a.MeetingId == meetingId, ct))
                return new(NoteFailure.ItemGone);
            var text = (body ?? "").Replace("\r\n", "\n", StringComparison.Ordinal);
            if (text.Length > MeetingNoteRules.MaxLength) return new(NoteFailure.TooLong);

            var existing = await db.MeetingNotes.AsNoTracking()
                .Where(n => n.MeetingId == meetingId && n.AgendaItemId == agendaItemId && n.AuthorId == actorId)
                .Select(n => new { n.Id }).FirstOrDefaultAsync(ct);
            var now = clock.GetUtcNow();
            if (existing is null)
            {
                // A note the caller has never seen saved. If it read one, that note has gone since: its copy is stale.
                if (loadedAt is not null) return new(NoteFailure.Stale);
                db.MeetingNotes.Add(new MeetingNote { MeetingId = meetingId, AgendaItemId = agendaItemId, AuthorId = actorId,
                    Body = text, CreatedAt = now, UpdatedAt = now });
                await db.SaveChangesAsync(ct);
            }
            else
            {
                // BR-399: the check and the write are one UPDATE, so a save from another window that lands in between makes
                // this one stale rather than overwritten. The 1 ms window absorbs the precision PostgreSQL drops.
                if (loadedAt is null) return new(NoteFailure.Stale);
                var from = loadedAt.Value.AddMilliseconds(-1);
                var to = loadedAt.Value.AddMilliseconds(1);
                var rows = await db.MeetingNotes
                    .Where(n => n.Id == existing.Id && n.UpdatedAt >= from && n.UpdatedAt <= to)
                    .ExecuteUpdateAsync(n => n.SetProperty(x => x.Body, text).SetProperty(x => x.UpdatedAt, now), ct);
                if (rows == 0) return new(NoteFailure.Stale);
            }
            saved = await db.MeetingNotes.AsNoTracking().Include(n => n.Author)
                .FirstAsync(n => n.MeetingId == meetingId && n.AgendaItemId == agendaItemId && n.AuthorId == actorId, ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another window created the same note first.
            logger.LogInformation(error, "Concurrent first save of a note in meeting {MeetingId}", meetingId);
            return new(NoteFailure.Stale);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            logger.LogInformation(error, "Agenda item {ItemId} went away while its note was saved", agendaItemId);
            return new(NoteFailure.ItemGone);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not save a note in meeting {MeetingId}", meetingId);
            return new(NoteFailure.SaveFailed);
        }

        try
        {
            await publisher.PublishAsync(new(Guid.CreateVersion7(), meetingId, agendaItemId, saved.UpdatedAt), ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The note is saved; the other window finds out on its next save (MSG418) instead of at once.
            logger.LogWarning(error, "Could not publish the note change in meeting {MeetingId}", meetingId);
        }
        return new(Note: MeetingNoteRules.View(saved));
    }

    /// <summary>The meeting's current Secretaries (BR-395). The model allows one; a list keeps the check honest if not.</summary>
    private static HashSet<Guid> Secretaries(Meeting meeting) => meeting.Participants
        .Where(p => p.MeetingId == meeting.Id && p.Role == MeetingRole.Secretary && ParticipantRules.Active(p))
        .Select(p => p.UserId).ToHashSet();

    private Task<bool> HasMinutesAsync(Guid meetingId, CancellationToken ct)
        => db.MeetingMinutes.AnyAsync(m => m.MeetingId == meetingId, ct);

    private async Task<(Meeting? Meeting, NoteFailure Failure)> ReadableAsync(Guid meetingId, Guid actorId, CancellationToken ct)
    {
        if (!await meetings.IsUserActiveAsync(actorId, ct)) return (null, NoteFailure.Inactive);
        var meeting = await meetings.FindWorkflowAsync(meetingId, false, ct);
        if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId)) return (null, NoteFailure.NotFound);
        return (meeting, NoteFailure.None);
    }
}

public static class MeetingNoteServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetMeetingNotes(this IServiceCollection services)
    {
        services.AddScoped<IMeetingNoteService, MeetingNoteService>();
        return services;
    }
}
