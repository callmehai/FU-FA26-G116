using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Speaking;
using TrustCabinet.Infrastructure.Recordings;
using TrustCabinet.Infrastructure.Speaking;

namespace TrustCabinet.Infrastructure.Calls;

/// <summary>
/// Meeting call room rules (SCRUM-39, SCRUM-41): who may enter, chair mute, and keeping
/// <c>call_sessions</c>/<c>call_participants</c> in step with what LiveKit reports. Verified guests (SCRUM-59)
/// enter like members — same grants, no chair mute — under a <see cref="CallMember"/> identity of their own.
/// </summary>
public sealed class CallRoomService(
    ICallRepository calls,
    ILiveKitGateway liveKit,
    IOptions<LiveKitOptions> options,
    IAuditService audit,
    ILogger<CallRoomService> logger,
    IOptions<RecordingOptions>? recording = null,
    TimeProvider? timeProvider = null,
    ISpeakingChangePublisher? speaking = null) : ICallRoomService
{
    private DateTimeOffset UtcNow => (timeProvider ?? TimeProvider.System).GetUtcNow();

    /// <summary>
    /// Someone left, or the room closed, and the repository closed <paramref name="closedHands"/> raised hands with it
    /// (BR-337): the queue panels refetch. Nothing is sent when no hand was up.
    /// </summary>
    private Task SpeakingLeftAsync(Guid meetingId, int closedHands)
        => speaking is null || closedHands == 0 ? Task.CompletedTask : SpeakingService.PublishAsync(speaking, logger,
            new SpeakingChange(Guid.CreateVersion7(), meetingId, "Left", null, UtcNow), null);

    // Called only after an Online/Hybrid meeting's End transaction has committed.
    public async Task CloseMeetingRoomAsync(Guid meetingId, CancellationToken ct = default)
    {
        if (!liveKit.IsConfigured) return;
        await liveKit.DeleteRoomAsync(CallRoomNames.ForMeeting(meetingId), ct);
        // The room may accept an old ticket after Meeting.End committed. Its connection history
        // ends when the provider confirms closure, not at the earlier business transition time.
        var closedAt = UtcNow;
        var session = await calls.FindActiveRoomSessionAsync(meetingId, ct);
        if (session is not null) await calls.EndSessionAsync(session.Id, closedAt, ct);
    }

    public async Task<CallResult<CallRoomView>> GetRoomAsync(Guid meetingId, CallCaller caller, CancellationToken ct = default)
    {
        CallRoomAccess? access;
        try
        {
            access = await FindAccessAsync(meetingId, caller, ct);
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

        return CallResult<CallRoomView>.Ok(new CallRoomView(meetingId, status, access.Role, liveKit.IsConfigured, count,
            (recording?.Value ?? new RecordingOptions()).RetentionDays));
    }

    public async Task<CallResult<CallJoinTicket>> JoinAsync(
        Guid meetingId, CallCaller caller, bool recordingNoticeAcknowledged = false, CancellationToken ct = default)
    {
        CallRoomAccess? access;
        try
        {
            access = await FindAccessAsync(meetingId, caller, ct);
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

        var member = MemberOf(meetingId, caller)!.Value;
        if (caller.Guest is { } guest)
        {
            // A guest has no account consent behind them: the recording notice is confirmed on the pre-join screen
            // and kept as evidence before any token exists (SCRUM-59). The audit row also carries the guest's IP.
            if (!recordingNoticeAcknowledged)
            {
                return CallResult<CallJoinTicket>.Fail(CallOutcome.RecordingNoticeRequired);
            }

            try
            {
                await audit.RecordAsync(AuditActions.CallGuestAdmitted, AuditActions.GuestSessionObjectType, guest.SessionId,
                    actorId: null,
                    new { meetingId, invitationId = guest.InvitationId, displayName = access.FullName, recordingNoticeAcknowledged = true },
                    ct);
            }
            catch (Exception error) when (IsDatabaseFailure(error))
            {
                logger.LogError(error, "Could not record the admission of guest session {SessionId}", guest.SessionId);
                return CallResult<CallJoinTicket>.Fail(CallOutcome.ReadFailed);
            }
        }

        // Nothing else is written here: the session starts when LiveKit reports the person actually in.
        var metadata = JsonSerializer.Serialize(new { meetingRole = access.Role.ToString() });
        return CallResult<CallJoinTicket>.Ok(liveKit.CreateJoinTicket(
            CallRoomNames.ForMeeting(meetingId), member.Identity, access.FullName, metadata,
            TimeSpan.FromMinutes(Math.Clamp(options.Value.TokenTtlMinutes, 1, 60))));
    }

    public async Task<CallOutcome> MuteAsync(Guid meetingId, Guid actorId, CallMember target, CancellationToken ct = default)
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
            var participant = (await liveKit.ListParticipantsAsync(room, ct))
                .FirstOrDefault(p => p.Identity == target.Identity);
            var microphone = participant?.Tracks.FirstOrDefault(t => t.Source == CallTrackSource.Microphone);
            if (microphone is null)
            {
                return CallOutcome.TargetNotInRoom;
            }

            // Mute only, never unmute: nobody gets their microphone switched on by someone else.
            if (!microphone.Muted)
            {
                await liveKit.MuteTrackAsync(room, participant!.Identity, microphone.Sid, ct);
            }

            return CallOutcome.Success;
        }
        catch (LiveKitUnavailableException error)
        {
            logger.LogWarning(error, "Could not mute {Identity} in meeting {MeetingId}", target.Identity, meetingId);
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
                if (CallMember.TryParse(webhook.ParticipantIdentity, out var joiner))
                {
                    var joinedAt = webhook.ParticipantJoinedAt ?? webhook.CreatedAt;
                    var session = await calls.EnsureActiveRoomSessionAsync(meetingId, joiner.UserId, joinedAt, ct);
                    await calls.RecordJoinAsync(session.Id, joiner, joinedAt, ct);

                    // A join ticket outlives the meeting by up to its TTL, and LiveKit recreates a
                    // deleted room for whoever still holds one. Close it again now (UC-22, BR-143)
                    // instead of at the next reconciliation; the stint above records the visit.
                    var access = await FindAccessAsync(meetingId, joiner, null, ct);
                    if (access is not null && CallRoomRules.StatusOf(access.Status, access.Mode) != CallRoomStatus.Open)
                    {
                        logger.LogInformation("Closing {Room} again: {Identity} joined after it closed", webhook.RoomName, joiner.Identity);
                        await liveKit.DeleteRoomAsync(webhook.RoomName!, ct);
                        await calls.EndSessionAsync(session.Id, UtcNow, ct);
                    }
                    else if (access is null && options.Value.EnforceRoster)
                    {
                        // The same old ticket, held by a guest whose session or invitation has ended since, or by a
                        // member just removed from the roster: no room status to read, just take them out now rather
                        // than at the next reconciliation.
                        logger.LogInformation("Removing {Identity} from {Room}: room access ended", joiner.Identity, webhook.RoomName);
                        await liveKit.RemoveParticipantAsync(webhook.RoomName!, joiner.Identity, ct);
                    }
                }

                break;

            case "participant_left":
            case "participant_connection_aborted":
                if (CallMember.TryParse(webhook.ParticipantIdentity, out var leaver)
                    && await calls.FindActiveRoomSessionAsync(meetingId, ct) is { } open)
                {
                    await SpeakingLeftAsync(meetingId, await calls.RecordLeaveAsync(open.Id, leaver, webhook.CreatedAt, ct));
                }

                break;

            case "room_finished":
                if (await calls.FindActiveRoomSessionAsync(meetingId, ct) is { } finished)
                {
                    await SpeakingLeftAsync(meetingId, await calls.EndSessionAsync(finished.Id, webhook.CreatedAt, ct));
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

        var now = UtcNow;
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

    public async Task EnforceRoomAccessAsync(Guid meetingId, CancellationToken ct = default)
    {
        if (!liveKit.IsConfigured)
        {
            return;
        }

        // ponytail: lists every active room to find one; a by-meeting query if rooms ever number in the hundreds.
        // No active session means nobody is known to be in the room (a Published meeting, an empty room): nothing to
        // take anyone out of, so LiveKit is not called. A room whose joins were all lost is the janitor's.
        if ((await calls.ListActiveRoomSessionsAsync(ct)).FirstOrDefault(s => s.MeetingId == meetingId) is { } session)
        {
            await ReconcileRoomAsync(meetingId, session, UtcNow, ct);
        }
    }

    private async Task ReconcileRoomAsync(Guid meetingId, ActiveRoomSession? session, DateTimeOffset now, CancellationToken ct)
    {
        var room = CallRoomNames.ForMeeting(meetingId);
        var allowedUsers = await calls.ListAllowedUserIdsAsync(meetingId, ct);
        var allowedGuests = await calls.ListAllowedGuestSessionIdsAsync(meetingId, ct);
        var live = await liveKit.ListParticipantsAsync(room, ct);

        // A meeting that left InProgress (or was switched to in-person) closes its room.
        if (session is not null
            && (session.MeetingStatus != MeetingStatus.InProgress || session.MeetingMode == MeetingMode.InPerson))
        {
            await liveKit.DeleteRoomAsync(room, ct);
            await calls.EndSessionAsync(session.SessionId, UtcNow, ct);
            return;
        }

        var present = new List<(RoomParticipant Participant, CallMember Member)>();
        foreach (var participant in live)
        {
            var isMember = CallMember.TryParse(participant.Identity, out var member)
                && (member.GuestSessionId is { } guest ? allowedGuests.Contains(guest) : allowedUsers.Contains(member.UserId!.Value));
            if (!isMember && options.Value.EnforceRoster)
            {
                logger.LogInformation("Removing {Identity} from {Room}: no longer allowed in", participant.Identity, room);
                await liveKit.RemoveParticipantAsync(room, participant.Identity, ct);
                continue;
            }

            if (isMember)
            {
                present.Add((participant, member));
            }
        }

        if (session is null)
        {
            if (present.Count == 0)
            {
                // Nobody who belongs here. Close it — unless the roster is not enforced (load tests),
                // where the synthetic participants are exactly what is being tried out.
                if (options.Value.EnforceRoster || live.Count == 0)
                {
                    await liveKit.DeleteRoomAsync(room, ct);
                }

                return;
            }

            var first = present.MinBy(p => p.Participant.JoinedAt);
            var created = await calls.EnsureActiveRoomSessionAsync(meetingId, first.Member.UserId, first.Participant.JoinedAt, ct);
            session = new ActiveRoomSession(created.Id, meetingId, MeetingStatus.InProgress, MeetingMode.Online, new HashSet<CallMember>());
        }

        if (present.Count == 0 && live.Count == 0)
        {
            await SpeakingLeftAsync(meetingId, await calls.EndSessionAsync(session.SessionId, now, ct));
            return;
        }

        var presentMembers = present.Select(p => p.Member).ToHashSet();
        var closedHands = 0;
        foreach (var gone in session.OpenMembers.Where(m => !presentMembers.Contains(m)))
        {
            closedHands += await calls.RecordLeaveAsync(session.SessionId, gone, now, ct);
        }
        await SpeakingLeftAsync(meetingId, closedHands);

        foreach (var (participant, member) in present.Where(p => !session.OpenMembers.Contains(p.Member)))
        {
            await calls.RecordJoinAsync(session.SessionId, member, participant.JoinedAt, ct);
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

    /// <summary>The caller as a room member; null for a guest token of another meeting, which is answered as 404.</summary>
    private static CallMember? MemberOf(Guid meetingId, CallCaller caller)
        => caller.Guest is { } guest
            ? guest.IsValid && guest.MeetingId == meetingId ? CallMember.Guest(guest.SessionId) : null
            : CallMember.User(caller.UserId ?? throw new ArgumentException("A caller is a user or a guest.", nameof(caller)));

    private Task<CallRoomAccess?> FindAccessAsync(Guid meetingId, CallCaller caller, CancellationToken ct)
        => MemberOf(meetingId, caller) is { } member
            ? FindAccessAsync(meetingId, member, caller.Guest?.InvitationId, ct)
            : Task.FromResult<CallRoomAccess?>(null);

    private Task<CallRoomAccess?> FindAccessAsync(Guid meetingId, CallMember member, Guid? invitationId, CancellationToken ct)
        => member.GuestSessionId is { } guest
            ? calls.FindGuestRoomAccessAsync(meetingId, guest, invitationId, ct)
            : calls.FindRoomAccessAsync(meetingId, member.UserId!.Value, ct);

    private static bool IsDatabaseFailure(Exception error)
        => error is DbUpdateException or NpgsqlException or TimeoutException;
}
