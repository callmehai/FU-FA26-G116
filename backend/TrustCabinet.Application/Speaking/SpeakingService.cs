using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Participants;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.GuestAccess;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Speaking;

/// <summary>
/// Raising a hand in the online room and the Chair's queue of speakers (SCRUM-27, UC-50, BR-104–BR-106).
/// Contract in docs/03-api/20-dang-ky-phat-bieu.md. Never turns a microphone on or off (BR-104–BR-106).
/// </summary>
public sealed class SpeakingService(ISpeakingRequestRepository requests, IParticipantRepository participants,
    IParticipantGuestAccess guests, ISpeakingChangePublisher publisher, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<SpeakingService> logger) : ISpeakingService
{
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
        if (actor.UserId is not { } id || !await requests.IsActiveUserAsync(id, ct))
            return (SpeakingFailure.Inactive, null);
        return s is not null && MeetingReadPolicy.CanRead(s.Meeting, id)
            ? (SpeakingFailure.None, new(id, null)) : (SpeakingFailure.NotFound, null);
    }

    public async Task<SpeakingOutcome> ListAsync(Guid meetingId, ParticipantActor actor, CancellationToken ct = default)
    {
        try
        {
            return await unitOfWork.ExecuteAsync(async token =>
            {
                var s = await participants.LoadAsync(meetingId, false, token);
                var (failure, viewer) = await AccessAsync(s, actor, token);
                if (failure != SpeakingFailure.None) return new SpeakingOutcome(failure);
                return new SpeakingOutcome(Data: await QueueAsync(s!, viewer!, token));
            }, TransactionMode.RepeatableRead, ct);
        }
        catch (PersistenceException exception)
        {
            logger.LogError(exception.InnerException ?? exception, "Failed to read the speaking queue of meeting {MeetingId}", meetingId);
            return new(SpeakingFailure.ReadFailed);
        }
    }

    public async Task<SpeakingOutcome> MutateAsync(Guid meetingId, Guid? requestId, ParticipantActor actor,
        SpeakingOperation operation, CancellationToken ct = default)
    {
        SpeakingOutcome outcome;
        SpeakingRequest? request = null;
        var now = default(DateTimeOffset);
        var changed = false;
        try
        {
            outcome = await unitOfWork.ExecuteAsync(async token =>
            {
                // The meeting row first, as every meeting write does: it serializes the queue with ending the meeting,
                // roster changes and other queue writes, so positions are never handed out twice.
                var s = await participants.LoadAsync(meetingId, true, token);
                if (actor.UserId is { } userId) await participants.LockUserAsync(userId, token);
                var (failure, viewer) = await AccessAsync(s, actor, token);
                if (failure != SpeakingFailure.None) return new SpeakingOutcome(failure);
                var meeting = s!.Meeting;

                if (requestId is { } id)
                {
                    request = await requests.FindAsync(meetingId, id, token);
                    if (request is null) return new SpeakingOutcome(SpeakingFailure.NotFound);
                    // Someone else's request, for an action only its owner takes: the same answer as one that does not
                    // exist, whatever the meeting's state.
                    if (operation is SpeakingOperation.Lower or SpeakingOperation.Start && !SpeakingRules.IsMine(request, viewer!))
                        return new SpeakingOutcome(SpeakingFailure.NotFound);
                }
                if (meeting.Status != MeetingStatus.InProgress) return new SpeakingOutcome(SpeakingFailure.NotInProgress);

                now = Now;
                SpeakingFailure result;
                var created = false;
                // Every operation that succeeds changes its request, except a raise that finds the hand already up and
                // a move at the end of the queue: those two send no event.
                changed = true;
                switch (operation)
                {
                    case SpeakingOperation.Raise:
                        (result, created, request) = await RaiseAsync(meetingId, actor, viewer!, now, token);
                        changed = created;
                        break;
                    case SpeakingOperation.Lower:
                        result = await OwnerAsync(meetingId, actor, viewer!, request!, [SpeakingRequestStatus.Queued,
                            SpeakingRequestStatus.Invited], token);
                        if (result == SpeakingFailure.None) SpeakingRules.Close(request!, now, now);
                        break;
                    case SpeakingOperation.Start:
                        result = await OwnerAsync(meetingId, actor, viewer!, request!, [SpeakingRequestStatus.Invited], token);
                        if (result == SpeakingFailure.None)
                        {
                            request!.Status = SpeakingRequestStatus.Speaking;
                            request.StartedAt = now;
                            request.UpdatedAt = now;
                        }
                        break;
                    case SpeakingOperation.Finish:
                        // The person ends their own turn from the room; closing someone else's is the Chair's (UC-50 step 7).
                        result = SpeakingRules.IsChair(meeting, viewer!)
                            ? (request!.Status is SpeakingRequestStatus.Invited or SpeakingRequestStatus.Speaking
                                ? SpeakingFailure.None : SpeakingFailure.NotOpen)
                            : !SpeakingRules.IsMine(request!, viewer!) ? SpeakingFailure.NotChair
                            : await OwnerAsync(meetingId, actor, viewer!, request!,
                                [SpeakingRequestStatus.Invited, SpeakingRequestStatus.Speaking], token);
                        if (result == SpeakingFailure.None) SpeakingRules.Close(request!, now, now);
                        break;
                    default:
                        (result, changed) = await ChairAsync(meeting, viewer!, request!, operation, now, token);
                        break;
                }
                if (result != SpeakingFailure.None) return new SpeakingOutcome(result);

                await unitOfWork.SaveChangesAsync(token);
                return new SpeakingOutcome(Data: await QueueAsync(s, viewer!, token), Created: created);
            }, result => result.Failure == SpeakingFailure.None, TransactionMode.ReadCommitted, ct);
        }
        catch (UniqueConflictException exception) when (exception.Conflict == UniqueConflict.SpeakingFloorTaken)
        {
            unitOfWork.DiscardChanges();
            return new(SpeakingFailure.FloorTaken);
        }
        catch (UniqueConflictException exception) when (exception.Conflict == UniqueConflict.SpeakingRequestAlreadyOpen)
        {
            // Two presses at once: the database kept the first request, and the hand is raised either way (EX-2).
            unitOfWork.DiscardChanges();
            return await ListAsync(meetingId, actor, ct);
        }
        catch (PersistenceException exception)
        {
            unitOfWork.DiscardChanges();
            logger.LogError(exception.InnerException ?? exception, "Failed speaking {Operation} for meeting {MeetingId}", operation, meetingId);
            return new(SpeakingFailure.SaveFailed);
        }

        if (outcome.Failure != SpeakingFailure.None || !changed) return outcome;
        var invited = operation == SpeakingOperation.Invite ? new SpeakingViewer(request!.UserId, request.GuestInvitationId) : null;
        var change = new SpeakingChange(Guid.CreateVersion7(), meetingId, operation.ToString(), request!.Id, now);
        await PublishAsync(publisher, logger, change, invited);
        return outcome;
    }

    private async Task<(SpeakingFailure, bool, SpeakingRequest?)> RaiseAsync(Guid meetingId, ParticipantActor actor,
        SpeakingViewer viewer, DateTimeOffset now, CancellationToken ct)
    {
        if (!await InRoomAsync(meetingId, actor, ct)) return (SpeakingFailure.NotInRoom, false, null);
        var open = await requests.FindOpenAsync(meetingId, viewer, ct);
        // UC-50 EX-2: the hand stays raised, nothing changes.
        if (open is not null) return (SpeakingFailure.None, false, open);

        var last = await requests.LastQueuePositionAsync(meetingId, ct);
        var agendaItemId = await requests.FindCurrentAgendaItemIdAsync(meetingId, ct);
        var request = new SpeakingRequest
        {
            Id = Guid.CreateVersion7(), MeetingId = meetingId, UserId = viewer.UserId,
            GuestInvitationId = viewer.GuestInvitationId, AgendaItemId = agendaItemId, RequestedAt = now,
            QueuePosition = last + 1, Status = SpeakingRequestStatus.Queued, CreatedAt = now, UpdatedAt = now,
        };
        requests.Add(request);
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
    /// Reorder, decline and invite (UC-50 step 4, AF-2, AF-3): the current Chair only, from the room or the meeting
    /// page alike (BR-104–BR-106). <c>Changed</c> is false for a move at the end of the queue.
    /// </summary>
    private async Task<(SpeakingFailure Failure, bool Changed)> ChairAsync(Meeting meeting, SpeakingViewer viewer, SpeakingRequest request,
        SpeakingOperation operation, DateTimeOffset now, CancellationToken ct)
    {
        if (!SpeakingRules.IsChair(meeting, viewer)) return (SpeakingFailure.NotChair, false);
        switch (operation)
        {
            case SpeakingOperation.Decline:
                if (request.Status is not (SpeakingRequestStatus.Queued or SpeakingRequestStatus.Invited))
                    return (SpeakingFailure.NotOpen, false);
                SpeakingRules.Close(request, now, now);
                return (SpeakingFailure.None, true);
            case SpeakingOperation.Invite:
                if (request.Status != SpeakingRequestStatus.Queued) return (SpeakingFailure.NotOpen, false);
                if (await requests.IsFloorHeldAsync(meeting.Id, ct))
                    return (SpeakingFailure.FloorTaken, false);
                request.Status = SpeakingRequestStatus.Invited;
                request.InvitedAt = now;
                request.UpdatedAt = now;
                return (SpeakingFailure.None, true);
            default:
                if (request.Status != SpeakingRequestStatus.Queued) return (SpeakingFailure.NotOpen, false);
                var up = operation == SpeakingOperation.MoveUp;
                var neighbour = await requests.FindQueuedNeighbourAsync(meeting.Id, request.QueuePosition, up, ct);
                // Already first or last: nothing to move, and nothing is wrong.
                if (neighbour is null) return (SpeakingFailure.None, false);
                (request.QueuePosition, neighbour.QueuePosition) = (neighbour.QueuePosition, request.QueuePosition);
                request.UpdatedAt = neighbour.UpdatedAt = now;
                return (SpeakingFailure.None, true);
        }
    }

    /// <summary>UC-50 step 2: connected to this meeting's open room right now, as LiveKit last reported.</summary>
    private Task<bool> InRoomAsync(Guid meetingId, ParticipantActor actor, CancellationToken ct)
        => requests.IsInRoomAsync(meetingId, actor, ct);

    private async Task<SpeakingQueue> QueueAsync(ParticipantSnapshot s, SpeakingViewer viewer, CancellationToken ct)
    {
        var meeting = s.Meeting;
        var rows = await requests.ListOpenAsync(meeting.Id, ct);
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
}

/// <summary>Ends every open request when the meeting ends, inside the caller's transaction (BR-104–BR-106).</summary>
public sealed class SpeakingMeetingEnd(ISpeakingRequestRepository requests, IUnitOfWork unitOfWork) : ISpeakingMeetingEnd
{
    public Task PrepareAsync(Guid meetingId, DateTimeOffset endedAt, CancellationToken ct = default)
        => unitOfWork.ExecuteAsync(token => requests.CloseAllOpenAsync(meetingId, endedAt, token), TransactionMode.None, ct);
}
