using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Domain.Speaking;

/// <summary>UC-79 Register to Speak (SCRUM-27). Contract in docs/03-api/20-dang-ky-phat-bieu.md.</summary>
public enum SpeakingOperation { Raise, Lower, MoveUp, MoveDown, Decline, Invite, Start, Finish }

public enum SpeakingFailure
{
    None,
    /// <summary>The caller may not read the meeting, or the request is not in it: 404, nothing disclosed.</summary>
    NotFound,
    /// <summary>The caller's account is deactivated.</summary>
    Inactive,
    /// <summary>The meeting is not In Progress (UC-79 EX-1).</summary>
    NotInProgress,
    /// <summary>The caller is not connected to the meeting room (UC-79 step 2).</summary>
    NotInRoom,
    /// <summary>A Chair action by someone who is not the current Chair (UC-79 EX-4, BR-338).</summary>
    NotChair,
    /// <summary>Someone already holds the floor (BR-338).</summary>
    FloorTaken,
    /// <summary>The request is not in a state this action applies to.</summary>
    NotOpen,
    ReadFailed,
    SaveFailed
}

/// <summary>Who is asking: a member or a Guest invitation, never both.</summary>
public sealed record SpeakingViewer(Guid? UserId, Guid? GuestInvitationId);

public sealed record SpeakingRequestView(Guid Id, string Kind, string DisplayName, string? Role, string Status,
    int QueuePosition, DateTimeOffset RequestedAt, DateTimeOffset? InvitedAt, DateTimeOffset? StartedAt,
    Guid? AgendaItemId, string? AgendaItemTitle, bool IsMine);

/// <summary>
/// The open queue in order, plus the caller's own open request. <see cref="CanManage"/> tells the panel whether to show
/// the Chair's controls; the server rechecks it on every action.
/// </summary>
public sealed record SpeakingQueue(Guid MeetingId, string MeetingStatus, bool CanManage, Guid? MyRequestId,
    IReadOnlyList<SpeakingRequestView> Requests);

public sealed record SpeakingOutcome(SpeakingFailure Failure = SpeakingFailure.None, SpeakingQueue? Data = null,
    bool Created = false);

/// <summary>
/// Pushed on <c>/hubs/meetings</c> as <c>speakingChanged</c> to every reader: an invalidation, readers refetch. When a
/// person is invited, <c>speakingInvited</c> also goes to that person alone, so the room can tell them it is their turn.
/// </summary>
public sealed record SpeakingChange(Guid EventId, Guid MeetingId, string ChangeType, Guid? RequestId,
    DateTimeOffset OccurredAt);

public interface ISpeakingChangePublisher
{
    Task PublishAsync(SpeakingChange change, SpeakingViewer? invited, CancellationToken ct);
}

/// <summary>
/// Ending the meeting ends every open request: a turn in progress is Finished, the rest Cancelled (BR-337).
/// Caller owns the meeting lock, transaction and SaveChanges, as for <c>IAgendaMeetingEnd</c>.
/// </summary>
public interface ISpeakingMeetingEnd
{
    Task PrepareAsync(Guid meetingId, DateTimeOffset endedAt, CancellationToken ct = default);
}

public interface ISpeakingService
{
    Task<SpeakingOutcome> ListAsync(Guid meetingId, ParticipantActor actor, CancellationToken ct = default);
    Task<SpeakingOutcome> MutateAsync(Guid meetingId, Guid? requestId, ParticipantActor actor,
        SpeakingOperation operation, CancellationToken ct = default);
}

public static class SpeakingRules
{
    public static readonly SpeakingRequestStatus[] OpenStatuses =
        [SpeakingRequestStatus.Queued, SpeakingRequestStatus.Invited, SpeakingRequestStatus.Speaking];

    public static bool IsOpen(SpeakingRequestStatus status) => OpenStatuses.Contains(status);

    /// <summary>Re-read on every request, so a Chair handed over through UC-73 loses the controls at once (EX-4).</summary>
    public static bool IsChair(Meeting meeting, SpeakingViewer viewer) => viewer.UserId is { } id
        && meeting.Participants.Any(p => p.UserId == id && ParticipantRules.Active(p) && p.Role == MeetingRole.Chair);

    public static bool IsMine(SpeakingRequest request, SpeakingViewer viewer)
        => (viewer.UserId is { } user && request.UserId == user)
            || (viewer.GuestInvitationId is { } guest && request.GuestInvitationId == guest);

    /// <summary>
    /// Ends a request at <paramref name="endedAt"/>: a turn that started is Finished, anything else is Cancelled
    /// (BR-337). <paramref name="now"/> is when the row is written, which may be later than when the person left.
    /// Returns false when the request was already closed.
    /// </summary>
    public static bool Close(SpeakingRequest request, DateTimeOffset endedAt, DateTimeOffset now)
    {
        if (!IsOpen(request.Status)) return false;
        request.Status = request.Status == SpeakingRequestStatus.Speaking
            ? SpeakingRequestStatus.Finished : SpeakingRequestStatus.Cancelled;
        // A provider time can be a little behind the last step we recorded; never end before it.
        var last = request.StartedAt ?? request.InvitedAt ?? request.RequestedAt;
        request.EndedAt = endedAt < last ? last : endedAt;
        request.UpdatedAt = now;
        return true;
    }
}
