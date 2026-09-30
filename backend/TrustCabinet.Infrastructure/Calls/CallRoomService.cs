using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Calls;

/// <summary>
/// Meeting call room rules (SCRUM-39, SCRUM-41): who may enter, chair mute, and keeping
/// <c>call_sessions</c>/<c>call_participants</c> in step with what LiveKit reports.
/// </summary>
public sealed class CallRoomService(
    ICallRepository calls,
    ILiveKitGateway liveKit,
    IOptions<LiveKitOptions> options,
    ILogger<CallRoomService> logger) : ICallRoomService
{
    public async Task<CallResult<CallRoomView>> GetRoomAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        CallRoomAccess? access;
        try
        {
            access = await calls.FindRoomAccessAsync(meetingId, actorId, ct);
        }
        catch (Exception error) when (IsDatabaseFailure(error))
        {
            logger.LogError(error, "Could not read call room access for meeting {MeetingId}", meetingId);
            return CallResult<CallRoomView>.Fail(CallOutcome.ReadFailed);
        }

        if (access is null)
        {
            return CallResult<CallRoomView>.Fail(CallOutcome.NotFound);
        }

        var status = CallRoomRules.StatusOf(access.Status, access.Mode);
        int? count = null;
        if (status == CallRoomStatus.Open && liveKit.IsConfigured)
        {
            try
            {
                count = (await liveKit.ListParticipantsAsync(CallRoomNames.ForMeeting(meetingId), ct)).Count;
            }
            catch (LiveKitUnavailableException error)
            {
                // The count is a nicety on the pre-join screen; a LiveKit hiccup must not fail the page.
                logger.LogWarning(error, "Could not count participants of meeting {MeetingId}", meetingId);
            }
        }

        return CallResult<CallRoomView>.Ok(new CallRoomView(meetingId, status, access.Role, liveKit.IsConfigured, count));
    }

    public async Task<CallResult<CallJoinTicket>> JoinAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        CallRoomAccess? access;
        try
        {
            access = await calls.FindRoomAccessAsync(meetingId, actorId, ct);
        }
        catch (Exception error) when (IsDatabaseFailure(error))
        {
            logger.LogError(error, "Could not read call room access for meeting {MeetingId}", meetingId);
            return CallResult<CallJoinTicket>.Fail(CallOutcome.ReadFailed);
        }

        if (access is null)
        {
            return CallResult<CallJoinTicket>.Fail(CallOutcome.NotFound);
        }

        if (access.Mode == MeetingMode.InPerson)
        {
            return CallResult<CallJoinTicket>.Fail(CallOutcome.InPersonMeeting);
        }

        if (access.Status != MeetingStatus.InProgress)
        {
            return CallResult<CallJoinTicket>.Fail(CallOutcome.RoomNotOpen);
        }

        if (!liveKit.IsConfigured)
        {
            return CallResult<CallJoinTicket>.Fail(CallOutcome.NotConfigured);
        }

        // Nothing is written here: the session starts when LiveKit reports the person actually in.
        var metadata = JsonSerializer.Serialize(new { meetingRole = access.Role.ToString() });
        return CallResult<CallJoinTicket>.Ok(liveKit.CreateJoinTicket(
            CallRoomNames.ForMeeting(meetingId), actorId, access.FullName, metadata,
            TimeSpan.FromMinutes(Math.Clamp(options.Value.TokenTtlMinutes, 1, 60))));
    }

    public async Task<CallOutcome> MuteAsync(Guid meetingId, Guid actorId, Guid targetUserId, CancellationToken ct = default)
    {
        CallRoomAccess? access;
        try
        {
            access = await calls.FindRoomAccessAsync(meetingId, actorId, ct);
        }
        catch (Exception error) when (IsDatabaseFailure(error))
        {
            logger.LogError(error, "Could not read call room access for meeting {MeetingId}", meetingId);
            return CallOutcome.ReadFailed;
        }

        if (access is null)
        {
            return CallOutcome.NotFound;
        }

        if (access.Role != MeetingRole.Chair)
        {
            return CallOutcome.NotChair;
        }

        if (access.Status != MeetingStatus.InProgress || access.Mode == MeetingMode.InPerson)
        {
            return CallOutcome.RoomNotOpen;
        }

        if (!liveKit.IsConfigured)
        {
            return CallOutcome.NotConfigured;
        }

        var room = CallRoomNames.ForMeeting(meetingId);
        try
        {
            var target = (await liveKit.ListParticipantsAsync(room, ct))
                .FirstOrDefault(p => p.Identity == targetUserId.ToString("D"));
            var microphone = target?.Tracks.FirstOrDefault(t => t.Source == CallTrackSource.Microphone);
            if (microphone is null)
            {
                return CallOutcome.TargetNotInRoom;
            }

            // Mute only, never unmute: nobody gets their microphone switched on by someone else.
            if (!microphone.Muted)
            {
                await liveKit.MuteTrackAsync(room, target!.Identity, microphone.Sid, ct);
            }

            return CallOutcome.Success;
        }
        catch (LiveKitUnavailableException error)
        {
            logger.LogWarning(error, "Could not mute {UserId} in meeting {MeetingId}", targetUserId, meetingId);
            return CallOutcome.ProviderFailed;
        }
    }

    public async Task HandleWebhookAsync(CallWebhookEvent webhook, CancellationToken ct = default)
    {
        if (!CallRoomNames.TryParseMeeting(webhook.RoomName, out var meetingId))
        {
            logger.LogDebug("Ignoring webhook {Event} for room {Room}", webhook.Event, webhook.RoomName);
            return;
        }

        switch (webhook.Event)
        {
            case "participant_joined":
                if (TryUser(webhook, out var joiner))
                {
                    var joinedAt = webhook.ParticipantJoinedAt ?? webhook.CreatedAt;
                    var session = await calls.EnsureActiveRoomSessionAsync(meetingId, joiner, joinedAt, ct);
                    await calls.RecordJoinAsync(session.Id, joiner, joinedAt, ct);
                }

                break;

            case "participant_left":
            case "participant_connection_aborted":
                if (TryUser(webhook, out var leaver)
                    && await calls.FindActiveRoomSessionAsync(meetingId, ct) is { } open)
                {
                    await calls.RecordLeaveAsync(open.Id, leaver, webhook.CreatedAt, ct);
                }

                break;

            case "room_finished":
                if (await calls.FindActiveRoomSessionAsync(meetingId, ct) is { } finished)
                {
                    await calls.EndSessionAsync(finished.Id, webhook.CreatedAt, ct);
                }

                break;

            case "track_published" when webhook.TrackSource == CallTrackSource.ScreenShare:
                await StopOtherScreenSharesAsync(meetingId, webhook, ct);
                break;
        }
    }

    public async Task ReconcileAsync(CancellationToken ct = default)
    {
        if (!liveKit.IsConfigured)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var sessions = await calls.ListActiveRoomSessionsAsync(ct);
        foreach (var session in sessions)
        {
            await ReconcileRoomAsync(session.MeetingId, session, now, ct);
        }

        // Rooms LiveKit has but the database never heard of — every participant_joined was lost.
        var known = sessions.Select(s => s.MeetingId).ToHashSet();
        foreach (var name in await liveKit.ListRoomNamesAsync(ct))
        {
            if (CallRoomNames.TryParseMeeting(name, out var meetingId) && !known.Contains(meetingId))
            {
                await ReconcileRoomAsync(meetingId, null, now, ct);
            }
        }
    }

    private async Task ReconcileRoomAsync(Guid meetingId, ActiveRoomSession? session, DateTimeOffset now, CancellationToken ct)
    {
        var room = CallRoomNames.ForMeeting(meetingId);
        var allowed = await calls.ListAllowedUserIdsAsync(meetingId, ct);
        var live = await liveKit.ListParticipantsAsync(room, ct);

        // A meeting that left InProgress (or was switched to in-person) closes its room.
        if (session is not null
            && (session.MeetingStatus != MeetingStatus.InProgress || session.MeetingMode == MeetingMode.InPerson))
        {
            await liveKit.DeleteRoomAsync(room, ct);
            await calls.EndSessionAsync(session.SessionId, now, ct);
            return;
        }

        var present = new List<RoomParticipant>();
        foreach (var participant in live)
        {
            var isMember = Guid.TryParse(participant.Identity, out var userId) && allowed.Contains(userId);
            if (!isMember && options.Value.EnforceRoster)
            {
                logger.LogInformation("Removing {Identity} from {Room}: no longer allowed in", participant.Identity, room);
                await liveKit.RemoveParticipantAsync(room, participant.Identity, ct);
                continue;
            }

            if (isMember)
            {
                present.Add(participant);
            }
        }

        if (session is null)
        {
            if (present.Count == 0)
            {
                await liveKit.DeleteRoomAsync(room, ct);
                return;
            }

            var first = present.MinBy(p => p.JoinedAt)!;
            var created = await calls.EnsureActiveRoomSessionAsync(meetingId, Guid.Parse(first.Identity), first.JoinedAt, ct);
            session = new ActiveRoomSession(created.Id, meetingId, MeetingStatus.InProgress, MeetingMode.Online, new HashSet<Guid>());
        }

        if (present.Count == 0 && live.Count == 0)
        {
            await calls.EndSessionAsync(session.SessionId, now, ct);
            return;
        }

        var presentIds = present.Select(p => Guid.Parse(p.Identity)).ToHashSet();
        foreach (var gone in session.OpenUserIds.Where(id => !presentIds.Contains(id)))
        {
            await calls.RecordLeaveAsync(session.SessionId, gone, now, ct);
        }

        foreach (var participant in present.Where(p => !session.OpenUserIds.Contains(Guid.Parse(p.Identity))))
        {
            await calls.RecordJoinAsync(session.SessionId, Guid.Parse(participant.Identity), participant.JoinedAt, ct);
        }
    }

    /// <summary>
    /// One presenter at a time (SCRUM-43). The client stops its own share when someone else starts;
    /// this is the server-side backstop for a client that does not.
    /// </summary>
    private async Task StopOtherScreenSharesAsync(Guid meetingId, CallWebhookEvent webhook, CancellationToken ct)
    {
        var room = CallRoomNames.ForMeeting(meetingId);
        foreach (var participant in await liveKit.ListParticipantsAsync(room, ct))
        {
            if (participant.Identity == webhook.ParticipantIdentity)
            {
                continue;
            }

            foreach (var track in participant.Tracks.Where(t => t.Source == CallTrackSource.ScreenShare && !t.Muted))
            {
                await liveKit.MuteTrackAsync(room, participant.Identity, track.Sid, ct);
            }
        }
    }

    private static bool TryUser(CallWebhookEvent webhook, out Guid userId)
        => Guid.TryParseExact(webhook.ParticipantIdentity, "D", out userId);

    private static bool IsDatabaseFailure(Exception error)
        => error is DbUpdateException or NpgsqlException or TimeoutException;
}
