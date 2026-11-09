using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.GuestAccess;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Speaking;
using TrustCabinet.Infrastructure.Configurations;

namespace TrustCabinet.Infrastructure.Speaking;

/// <summary>
/// Raising a hand in the online room and the Chair's queue of speakers (SCRUM-27, UC-79, BR-337 → BR-339).
/// Contract in docs/03-api/20-dang-ky-phat-bieu.md. Never turns a microphone on or off (BR-338).
/// </summary>
public sealed class SpeakingService(TrustCabinetDbContext db, IParticipantRepository participants,
    IParticipantGuestAccess guests, ISpeakingChangePublisher publisher, TimeProvider clock,
    ILogger<SpeakingService> logger) : ISpeakingService
{
    // The execution strategy wraps a transient failure in InvalidOperationException, as ParticipantService handles.
    private static bool Persistence(Exception ex) =>
        ex is DbUpdateException or NpgsqlException or TimeoutException
            or InvalidOperationException { InnerException: NpgsqlException or TimeoutException };
    private DateTimeOffset Now => AgendaExecution.UtcMicroseconds(clock.GetUtcNow());

    /// <summary>Who the caller is in this meeting, re-read on every request. A Guest needs a live verified session.</summary>
    private async Task<(SpeakingFailure Failure, SpeakingViewer? Viewer)> AccessAsync(ParticipantSnapshot? s,
        ParticipantActor actor, CancellationToken ct)
    {
        if (actor.Guest is { } g)
            return actor.UserId is null && g.IsValid && s is not null && g.MeetingId == s.Meeting.Id
                && GuestAccessRules.MeetingOpen(s.Meeting.Status) && s.Guests.Any(x => x.Id == g.InvitationId)
                && await guests.CanAccessAsync(g, ct)
                ? (SpeakingFailure.None, new(null, g.InvitationId)) : (SpeakingFailure.NotFound, null);
        if (actor.UserId is not { } id || !await db.Users.AnyAsync(u => u.Id == id && u.IsActive, ct))
            return (SpeakingFailure.Inactive, null);
        return s is not null && MeetingReadPolicy.CanRead(s.Meeting, id)
            ? (SpeakingFailure.None, new(id, null)) : (SpeakingFailure.NotFound, null);
    }

    public async Task<SpeakingOutcome> ListAsync(Guid meetingId, ParticipantActor actor, CancellationToken ct = default)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var s = await participants.LoadAsync(meetingId, false, ct);
            var (failure, viewer) = await AccessAsync(s, actor, ct);
            if (failure != SpeakingFailure.None) return new(failure);
            return new(Data: await QueueAsync(s!, viewer!, ct));
        }
        catch (Exception ex) when (Persistence(ex))
        {
            logger.LogError(ex, "Failed to read the speaking queue of meeting {MeetingId}", meetingId);
            return new(SpeakingFailure.ReadFailed);
        }
    }

    public async Task<SpeakingOutcome> MutateAsync(Guid meetingId, Guid? requestId, ParticipantActor actor,
        SpeakingOperation operation, CancellationToken ct = default)
    {
        SpeakingOutcome outcome;
        SpeakingChange? change = null;
        SpeakingViewer? invited = null;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            // The meeting row first, as every meeting write does: it serializes the queue with ending the meeting,
            // roster changes and other queue writes, so positions are never handed out twice.
            var s = await participants.LoadAsync(meetingId, true, ct);
            if (actor.UserId is { } userId)
                await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR SHARE")
                    .AsNoTracking().ToArrayAsync(ct);
            var (failure, viewer) = await AccessAsync(s, actor, ct);
            if (failure != SpeakingFailure.None) return new(failure);
            var meeting = s!.Meeting;

            SpeakingRequest? request = null;
            if (requestId is { } id)
            {
                request = await db.SpeakingRequests.SingleOrDefaultAsync(r => r.Id == id && r.MeetingId == meetingId, ct);
                if (request is null) return new(SpeakingFailure.NotFound);
                // Someone else's request, for an action only its owner takes: the same answer as one that does not
                // exist, whatever the meeting's state.
                if (operation is SpeakingOperation.Lower or SpeakingOperation.Start && !SpeakingRules.IsMine(request, viewer!))
                    return new(SpeakingFailure.NotFound);
            }
            if (meeting.Status != MeetingStatus.InProgress) return new(SpeakingFailure.NotInProgress);

            var now = Now;
            SpeakingFailure result;
            var created = false;
            switch (operation)
            {
                case SpeakingOperation.Raise:
                    (result, created, request) = await RaiseAsync(meetingId, actor, viewer!, now, ct);
                    break;
                case SpeakingOperation.Lower:
                    result = await OwnerAsync(meetingId, actor, viewer!, request!, [SpeakingRequestStatus.Queued,
                        SpeakingRequestStatus.Invited], ct);
                    if (result == SpeakingFailure.None) SpeakingRules.Close(request!, now, now);
                    break;
                case SpeakingOperation.Start:
                    result = await OwnerAsync(meetingId, actor, viewer!, request!, [SpeakingRequestStatus.Invited], ct);
                    if (result == SpeakingFailure.None)
                    {
                        request!.Status = SpeakingRequestStatus.Speaking;
                        request.StartedAt = now;
                        request.UpdatedAt = now;
                    }
                    break;
                case SpeakingOperation.Finish:
                    // The person ends their own turn from the room; closing someone else's is the Chair's (UC-79 step 7).
                    result = SpeakingRules.IsChair(meeting, viewer!)
                        ? (request!.Status is SpeakingRequestStatus.Invited or SpeakingRequestStatus.Speaking
                            ? SpeakingFailure.None : SpeakingFailure.NotOpen)
                        : !SpeakingRules.IsMine(request!, viewer!) ? SpeakingFailure.NotChair
                        : await OwnerAsync(meetingId, actor, viewer!, request!,
                            [SpeakingRequestStatus.Invited, SpeakingRequestStatus.Speaking], ct);
                    if (result == SpeakingFailure.None) SpeakingRules.Close(request!, now, now);
                    break;
                default:
                    result = await ChairAsync(meeting, viewer!, request!, operation, now, ct);
                    break;
            }
            if (result != SpeakingFailure.None) return new(result);

            // A raise that finds the hand already up, or a move at the end of the queue, changes nothing: no event.
            var changed = db.ChangeTracker.HasChanges();
            await db.SaveChangesAsync(ct);
            var queue = await QueueAsync(s, viewer!, ct);
            await tx.CommitAsync(ct);
            outcome = new(Data: queue, Created: created);
            if (!changed) return outcome;
            if (operation == SpeakingOperation.Invite) invited = new(request!.UserId, request.GuestInvitationId);
            change = new(Guid.CreateVersion7(), meetingId, operation.ToString(), request!.Id, now);
        }
        catch (DbUpdateException ex) when (Violates(ex, SpeakingRequestConfiguration.FloorIndex))
        {
            db.ChangeTracker.Clear();
            return new(SpeakingFailure.FloorTaken);
        }
        catch (DbUpdateException ex) when (Violates(ex, SpeakingRequestConfiguration.OpenUserIndex)
            || Violates(ex, SpeakingRequestConfiguration.OpenGuestIndex))
        {
            // Two presses at once: the database kept the first request, and the hand is raised either way (EX-2).
            db.ChangeTracker.Clear();
            return await ListAsync(meetingId, actor, ct);
        }
        catch (Exception ex) when (Persistence(ex))
        {
            db.ChangeTracker.Clear();
            logger.LogError(ex, "Failed speaking {Operation} for meeting {MeetingId}", operation, meetingId);
            return new(SpeakingFailure.SaveFailed);
        }

        await PublishAsync(publisher, logger, change, invited);
        return outcome;
    }

    private async Task<(SpeakingFailure, bool, SpeakingRequest?)> RaiseAsync(Guid meetingId, ParticipantActor actor,
        SpeakingViewer viewer, DateTimeOffset now, CancellationToken ct)
    {
        if (!await InRoomAsync(meetingId, actor, ct)) return (SpeakingFailure.NotInRoom, false, null);
        var open = await db.SpeakingRequests.Where(r => r.MeetingId == meetingId
                && SpeakingRules.OpenStatuses.Contains(r.Status)
                && (viewer.UserId != null ? r.UserId == viewer.UserId : r.GuestInvitationId == viewer.GuestInvitationId))
            .SingleOrDefaultAsync(ct);
        // UC-79 EX-2: the hand stays raised, nothing changes.
        if (open is not null) return (SpeakingFailure.None, false, open);

        var last = await db.SpeakingRequests.Where(r => r.MeetingId == meetingId)
            .MaxAsync(r => (int?)r.QueuePosition, ct) ?? 0;
        var agendaItemId = await db.AgendaItems
            .Where(i => i.MeetingId == meetingId && i.Status == AgendaItemStatus.InProgress)
            .Select(i => (Guid?)i.Id).SingleOrDefaultAsync(ct);
        var request = new SpeakingRequest
        {
            Id = Guid.CreateVersion7(), MeetingId = meetingId, UserId = viewer.UserId,
            GuestInvitationId = viewer.GuestInvitationId, AgendaItemId = agendaItemId, RequestedAt = now,
            QueuePosition = last + 1, Status = SpeakingRequestStatus.Queued, CreatedAt = now, UpdatedAt = now,
        };
        db.SpeakingRequests.Add(request);
        return (SpeakingFailure.None, true, request);
    }

    /// <summary>An action only the request's owner takes, from the room.</summary>
    private async Task<SpeakingFailure> OwnerAsync(Guid meetingId, ParticipantActor actor, SpeakingViewer viewer,
        SpeakingRequest request, SpeakingRequestStatus[] from, CancellationToken ct)
    {
        // Callers send someone else's request to NotFound or NotChair first; kept so this check never relies on them.
        if (!SpeakingRules.IsMine(request, viewer)) return SpeakingFailure.NotFound;
        if (!from.Contains(request.Status)) return SpeakingFailure.NotOpen;
        return await InRoomAsync(meetingId, actor, ct) ? SpeakingFailure.None : SpeakingFailure.NotInRoom;
    }

    /// <summary>
    /// Reorder, decline and invite (UC-79 step 4, AF-2, AF-3): the current Chair only, from the room or the meeting
    /// page alike (BR-338).
    /// </summary>
    private async Task<SpeakingFailure> ChairAsync(Meeting meeting, SpeakingViewer viewer, SpeakingRequest request,
        SpeakingOperation operation, DateTimeOffset now, CancellationToken ct)
    {
        if (!SpeakingRules.IsChair(meeting, viewer)) return SpeakingFailure.NotChair;
        switch (operation)
        {
            case SpeakingOperation.Decline:
                if (request.Status is not (SpeakingRequestStatus.Queued or SpeakingRequestStatus.Invited))
                    return SpeakingFailure.NotOpen;
                SpeakingRules.Close(request, now, now);
                return SpeakingFailure.None;
            case SpeakingOperation.Invite:
                if (request.Status != SpeakingRequestStatus.Queued) return SpeakingFailure.NotOpen;
                if (await db.SpeakingRequests.AnyAsync(r => r.MeetingId == meeting.Id
                        && (r.Status == SpeakingRequestStatus.Invited || r.Status == SpeakingRequestStatus.Speaking), ct))
                    return SpeakingFailure.FloorTaken;
                request.Status = SpeakingRequestStatus.Invited;
                request.InvitedAt = now;
                request.UpdatedAt = now;
                return SpeakingFailure.None;
            default:
                if (request.Status != SpeakingRequestStatus.Queued) return SpeakingFailure.NotOpen;
                var up = operation == SpeakingOperation.MoveUp;
                var neighbour = await (up
                        ? db.SpeakingRequests.Where(r => r.MeetingId == meeting.Id && r.Status == SpeakingRequestStatus.Queued
                            && r.QueuePosition < request.QueuePosition).OrderByDescending(r => r.QueuePosition)
                        : db.SpeakingRequests.Where(r => r.MeetingId == meeting.Id && r.Status == SpeakingRequestStatus.Queued
                            && r.QueuePosition > request.QueuePosition).OrderBy(r => r.QueuePosition))
                    .FirstOrDefaultAsync(ct);
                // Already first or last: nothing to move, and nothing is wrong.
                if (neighbour is null) return SpeakingFailure.None;
                (request.QueuePosition, neighbour.QueuePosition) = (neighbour.QueuePosition, request.QueuePosition);
                request.UpdatedAt = neighbour.UpdatedAt = now;
                return SpeakingFailure.None;
        }
    }

    /// <summary>UC-79 step 2: connected to this meeting's open room right now, as LiveKit last reported.</summary>
    private Task<bool> InRoomAsync(Guid meetingId, ParticipantActor actor, CancellationToken ct)
    {
        var open = db.CallParticipants.Where(p => p.LeftAt == null && p.CallSession.MeetingId == meetingId
            && p.CallSession.Kind == CallKind.MeetingRoom && p.CallSession.Status == CallStatus.Active);
        return actor.Guest is { } guest
            ? open.AnyAsync(p => p.GuestSessionId == guest.SessionId, ct)
            : open.AnyAsync(p => p.UserId == actor.UserId, ct);
    }

    private async Task<SpeakingQueue> QueueAsync(ParticipantSnapshot s, SpeakingViewer viewer, CancellationToken ct)
    {
        var meeting = s.Meeting;
        var rows = await db.SpeakingRequests.AsNoTracking()
            .Where(r => r.MeetingId == meeting.Id && SpeakingRules.OpenStatuses.Contains(r.Status))
            .Select(r => new
            {
                r.Id, r.UserId, r.GuestInvitationId, r.Status, r.QueuePosition, r.RequestedAt, r.InvitedAt, r.StartedAt,
                r.AgendaItemId, AgendaItemTitle = r.AgendaItem == null ? null : r.AgendaItem.Title,
                UserName = r.User == null ? null : r.User.FullName,
                GuestName = r.GuestInvitation == null ? null
                    : r.GuestInvitation.SelfDeclaredDisplayName ?? r.GuestInvitation.InvitedName,
            })
            .ToListAsync(ct);
        var views = rows
            // Whoever holds the floor first, then the queue in the Chair's order.
            .OrderBy(r => r.Status == SpeakingRequestStatus.Queued ? 1 : 0).ThenBy(r => r.QueuePosition)
            .Select(r =>
            {
                var mine = (viewer.UserId is { } u && r.UserId == u)
                    || (viewer.GuestInvitationId is { } g && r.GuestInvitationId == g);
                string? role = r.UserId is { } userId
                    ? meeting.Participants.Where(p => p.UserId == userId && ParticipantRules.Active(p))
                        .Select(p => (MeetingRole?)p.Role).OrderBy(p => p).FirstOrDefault()?.ToString()
                    : MeetingRole.Guest.ToString();
                return new SpeakingRequestView(r.Id, r.UserId is null ? "Guest" : "User",
                    r.UserName ?? r.GuestName ?? string.Empty, role, r.Status.ToString(), r.QueuePosition, r.RequestedAt,
                    r.InvitedAt, r.StartedAt, r.AgendaItemId, r.AgendaItemTitle, mine);
            })
            .ToArray();
        return new(meeting.Id, meeting.Status.ToString(), SpeakingRules.IsChair(meeting, viewer),
            views.FirstOrDefault(v => v.IsMine)?.Id, views);
    }

    /// <summary>A committed write stays successful even when push fails.</summary>
    internal static async Task PublishAsync(ISpeakingChangePublisher publisher, ILogger logger, SpeakingChange? change,
        SpeakingViewer? invited)
    {
        if (change is null) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await publisher.PublishAsync(change, invited, timeout.Token); }
        catch (Exception ex) { logger.LogWarning(ex, "Speaking change committed; realtime delivery failed for {EventId}", change.EventId); }
    }

    private static bool Violates(DbUpdateException ex, string index)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
            && pg.ConstraintName == index;
}

/// <summary>
/// Closes the open requests of people who are no longer there: the meeting ended, they left the room, or they were
/// removed from the meeting (BR-337). Runs inside the caller's transaction; the caller saves.
/// </summary>
public static class SpeakingRequestCleanup
{
    /// <param name="userId">Only this member's requests; with <paramref name="guestInvitationIds"/> null, everyone's.</param>
    /// <param name="endedAt">When the person left or the meeting ended.</param>
    /// <param name="now">When the rows are written.</param>
    /// <returns>How many requests were closed, so a caller tells the panels only when something changed.</returns>
    public static async Task<int> CloseOpenAsync(TrustCabinetDbContext db, Guid meetingId, Guid? userId,
        IReadOnlyCollection<Guid>? guestInvitationIds, DateTimeOffset endedAt, DateTimeOffset now, CancellationToken ct)
    {
        var query = db.SpeakingRequests.Where(r => r.MeetingId == meetingId && SpeakingRules.OpenStatuses.Contains(r.Status));
        if (userId is not null || guestInvitationIds is not null)
            query = query.Where(r => (userId != null && r.UserId == userId)
                || (guestInvitationIds != null && r.GuestInvitationId != null && guestInvitationIds.Contains(r.GuestInvitationId.Value)));
        var open = await query.ToListAsync(ct);
        return open.Count(r => SpeakingRules.Close(r, endedAt, now));
    }
}

public sealed class SpeakingMeetingEnd(TrustCabinetDbContext db) : ISpeakingMeetingEnd
{
    public async Task PrepareAsync(Guid meetingId, DateTimeOffset endedAt, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Closing speaking requests requires the caller's meeting transaction and lock.");
        await SpeakingRequestCleanup.CloseOpenAsync(db, meetingId, null, null, endedAt, endedAt, ct);
    }
}

public static class SpeakingServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetSpeaking(this IServiceCollection services)
    {
        services.AddScoped<ISpeakingService, SpeakingService>();
        services.AddScoped<ISpeakingMeetingEnd, SpeakingMeetingEnd>();
        return services;
    }
}
