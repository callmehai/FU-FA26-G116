using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Recordings;
using TrustCabinet.Domain.GuestAccess;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Meetings;

/// <summary>Who an event about one meeting may reach: active accounts and live guest sessions.</summary>
public sealed record MeetingAudienceMembers(IReadOnlyList<Guid> UserIds, IReadOnlyList<MeetingGuestSession> Guests)
{
    public static readonly MeetingAudienceMembers None = new([], []);
}

/// <summary>
/// Who may hear of a meeting now — the meeting hub's join checks and the recipients of every realtime event it
/// carries. A security boundary: an event reaches only those allowed to read that meeting at the moment it is sent,
/// so each audience is read again for every event and never widened. The web layer turns these ids into hub groups.
/// </summary>
public sealed class MeetingAudience(IUnitOfWork unitOfWork, IMeetingRepository meetings, IMeetingAudienceQuery audience,
    IAgendaGuestAccess guests, IParticipantRepository participants, IParticipantGuestAccess participantGuests)
{
    /// <summary>Whether a user's account is active: the hub refuses the connection of one who is not.</summary>
    public Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct)
        => unitOfWork.ExecuteAsync(token => meetings.IsUserActiveAsync(userId, token), TransactionMode.None, ct);

    /// <summary>Whether an active user may join a meeting's hub group: the meeting must be one they may read.</summary>
    public Task<bool> CanUserJoinAsync(Guid meetingId, Guid userId, CancellationToken ct)
        => unitOfWork.ExecuteAsync(async token => await meetings.IsUserActiveAsync(userId, token)
            && await meetings.FindAccessibleAsync(meetingId, userId, token) is not null, TransactionMode.None, ct);

    /// <summary>Whether a guest session may connect to, or join, this meeting: its own, live, and the meeting open.</summary>
    public Task<bool> CanGuestJoinAsync(MeetingGuestSession guest, Guid meetingId, CancellationToken ct)
        => unitOfWork.ExecuteAsync(async token =>
        {
            if (!guest.IsValid || guest.MeetingId != meetingId || !await guests.CanReadAsync(guest, token))
                return false;
            var meeting = await meetings.FindWorkflowAsync(meetingId, ct: token);
            return meeting is not null && GuestAccessRules.MeetingOpen(meeting.Status);
        }, TransactionMode.None, ct);

    /// <summary>Every active user and guest session who may read the meeting now: agenda, documents, speaking, votes.</summary>
    public Task<MeetingAudienceMembers> ReadersAsync(Guid meetingId, CancellationToken ct)
        => unitOfWork.ExecuteAsync(async token =>
        {
            var meeting = await meetings.FindWorkflowAsync(meetingId, ct: token);
            if (meeting is null) return MeetingAudienceMembers.None;
            var candidates = meeting.Participants.Select(p => p.UserId).Append(meeting.CreatedById)
                .Concat(meeting.AssignedApproverId is { } approver ? [approver] : [])
                .Distinct().Where(actor => MeetingReadPolicy.CanRead(meeting, actor)).ToArray();
            var active = await audience.ListActiveUserIdsAsync(candidates, token);
            IReadOnlyList<MeetingGuestSession> readers = [];
            if (GuestAccessRules.MeetingOpen(meeting.Status))
            {
                readers = (await guests.ListReadersAsync(meeting.Id, token))
                    .Where(g => g.IsValid && g.MeetingId == meeting.Id).Distinct().ToList();
            }
            return new MeetingAudienceMembers(active, readers);
        }, TransactionMode.None, ct);

    /// <summary>The meeting's current, active Chair and Secretary: vote drafts (BR-253) and notes (BR-395).</summary>
    public Task<IReadOnlyList<Guid>> ManagersAsync(Guid meetingId, CancellationToken ct)
        => unitOfWork.ExecuteAsync<IReadOnlyList<Guid>>(async token =>
        {
            var meeting = await meetings.FindWorkflowAsync(meetingId, ct: token);
            if (meeting is null) return [];
            var candidates = meeting.Participants.Select(p => p.UserId).Distinct()
                .Where(id => ParticipantRules.IsManager(meeting, id)).ToArray();
            return await audience.ListActiveUserIdsAsync(candidates, token);
        }, TransactionMode.None, ct);

    /// <summary>
    /// Who may listen to the meeting's recordings (SCRUM-109): the active roster, not an approver or creator outside it.
    /// </summary>
    public Task<IReadOnlyList<Guid>> ListenersAsync(Guid meetingId, CancellationToken ct)
        => unitOfWork.ExecuteAsync<IReadOnlyList<Guid>>(async token =>
        {
            var meeting = await meetings.FindWorkflowAsync(meetingId, ct: token);
            if (meeting is null) return [];
            var candidates = meeting.Participants.Select(p => p.UserId).Distinct()
                .Where(id => RecordingRules.CanListen(meeting, id)).ToArray();
            return await audience.ListActiveUserIdsAsync(candidates, token);
        }, TransactionMode.None, ct);

    /// <summary>
    /// Who sees the attendance list: every active reader, and the guest sessions still invited to this meeting that
    /// pass both the roster's and the agenda's live session checks.
    /// </summary>
    public Task<MeetingAudienceMembers> AttendanceReadersAsync(Guid meetingId, CancellationToken ct)
        => unitOfWork.ExecuteAsync(async token =>
        {
            var s = await participants.LoadAsync(meetingId, false, token);
            if (s is null) return MeetingAudienceMembers.None;
            var candidates = s.Meeting.Participants.Select(p => p.UserId).Append(s.Meeting.CreatedById)
                .Concat(s.Meeting.AssignedApproverId is { } approver ? [approver] : [])
                .Distinct().Where(id => MeetingReadPolicy.CanRead(s.Meeting, id)).ToArray();
            var readers = await audience.ListActiveUserIdsAsync(candidates, token);
            var sessions = new List<MeetingGuestSession>();
            if (GuestAccessRules.MeetingOpen(s.Meeting.Status))
            {
                foreach (var session in (await participantGuests.ListReadersAsync(meetingId, token)).Distinct())
                {
                    if (!session.IsValid || session.MeetingId != meetingId || !s.Guests.Any(g => g.Id == session.InvitationId)
                        || !await participantGuests.CanAccessAsync(session, token)) continue;
                    if (await guests.CanReadAsync(session, token)) sessions.Add(session);
                }
            }
            return new MeetingAudienceMembers(readers, sessions);
        }, TransactionMode.None, ct);
}
