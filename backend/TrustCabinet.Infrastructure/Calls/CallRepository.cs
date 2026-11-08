using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Infrastructure.GuestAccess;
using TrustCabinet.Infrastructure.Speaking;

namespace TrustCabinet.Infrastructure.Calls;

/// <summary>
/// Room access and call history. Reads meeting participation straight from
/// <c>meeting_participants</c> rather than through the meeting repository: entering the room is a
/// stricter rule than reading the meeting (the creator or approver alone may not join).
///
/// Only current, active internal assignments grant room access; removed assignments remain history.
/// Guests come from their UC-04 session instead and are never read from or written to <c>meeting_participants</c>.
/// </summary>
public sealed class CallRepository(TrustCabinetDbContext db, TimeProvider? clock = null) : ICallRepository
{
    public const string ActiveRoomIndex = "ux_call_sessions_active_meeting_room";
    public const string OpenUserStintIndex = "ux_call_participants_open_user_stint";
    public const string OpenGuestStintIndex = "ux_call_participants_open_guest_stint";

    private static readonly MeetingStatus[] ParticipantVisible =
    [
        MeetingStatus.Published, MeetingStatus.InProgress, MeetingStatus.Ended,
    ];

    public Task<CallRoomAccess?> FindRoomAccessAsync(Guid meetingId, Guid userId, CancellationToken ct = default)
        => db.MeetingParticipants.AsNoTracking()
            .Where(ParticipantRules.ActiveExpression)
            .Where(p => p.MeetingId == meetingId
                && p.UserId == userId
                && (ParticipantVisible.Contains(p.Meeting.Status)
                    // As MeetingReadPolicy: invitees were told of a cancellation after publishing, so the lobby
                    // says the meeting was cancelled rather than that the page does not exist.
                    || (p.Meeting.Status == MeetingStatus.Cancelled && p.Meeting.CancelledFromStatus == MeetingStatus.Published)))
            .Select(p => new CallRoomAccess(p.MeetingId, p.Meeting.Status, p.Meeting.Mode, p.Role, p.User.FullName))
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlySet<Guid>> ListAllowedUserIdsAsync(Guid meetingId, CancellationToken ct = default)
        => (await db.MeetingParticipants.AsNoTracking()
            .Where(ParticipantRules.ActiveExpression)
            .Where(p => p.MeetingId == meetingId
                && p.Meeting.Status == MeetingStatus.InProgress
                && p.Meeting.Mode != MeetingMode.InPerson)
            .Select(p => p.UserId)
            .ToListAsync(ct))
            .ToHashSet();

    public Task<CallRoomAccess?> FindGuestRoomAccessAsync(Guid meetingId, Guid guestSessionId, Guid? invitationId,
        CancellationToken ct = default)
        => AdmittedGuests(meetingId)
            .Where(x => x.Id == guestSessionId && (invitationId == null || x.InvitationId == invitationId))
            .Select(x => new CallRoomAccess(meetingId, x.Invitation.Meeting.Status, x.Invitation.Meeting.Mode,
                MeetingRole.Guest, x.Invitation.SelfDeclaredDisplayName!))
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlySet<Guid>> ListAllowedGuestSessionIdsAsync(Guid meetingId, CancellationToken ct = default)
        => (await AdmittedGuests(meetingId)
            .Where(x => x.Invitation.Meeting.Status == MeetingStatus.InProgress && x.Invitation.Meeting.Mode != MeetingMode.InPerson)
            .Select(x => x.Id)
            .ToListAsync(ct))
            .ToHashSet();

    /// <summary>The same sessions the guest's agenda and roster reads accept (GuestSessionAccess): current, and named.</summary>
    private IQueryable<GuestSession> AdmittedGuests(Guid meetingId)
        => GuestAccessService.CurrentSessions(db, (clock ?? TimeProvider.System).GetUtcNow())
            .Where(x => x.Invitation.MeetingId == meetingId && x.Invitation.SelfDeclaredDisplayName != null);

    public Task<CallSession?> FindActiveRoomSessionAsync(Guid meetingId, CancellationToken ct = default)
        => db.CallSessions.SingleOrDefaultAsync(
            s => s.MeetingId == meetingId && s.Kind == CallKind.MeetingRoom && s.Status == CallStatus.Active, ct);

    public async Task<CallSession> EnsureActiveRoomSessionAsync(
        Guid meetingId, Guid? startedById, DateTimeOffset startedAt, CancellationToken ct = default)
    {
        if (await FindActiveRoomSessionAsync(meetingId, ct) is { } existing)
        {
            return existing;
        }

        var session = new CallSession
        {
            Kind = CallKind.MeetingRoom,
            MeetingId = meetingId,
            Status = CallStatus.Active,
            StartedById = startedById,
            StartedAt = startedAt,
        };
        db.CallSessions.Add(session);
        try
        {
            await db.SaveChangesAsync(ct);
            return session;
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: ActiveRoomIndex,
        })
        {
            // Another webhook created it first; use theirs.
            db.Entry(session).State = EntityState.Detached;
            return await FindActiveRoomSessionAsync(meetingId, ct)
                ?? throw new InvalidOperationException("Active room session vanished after a unique violation.");
        }
    }

    public async Task RecordJoinAsync(Guid sessionId, CallMember member, DateTimeOffset joinedAt, CancellationToken ct = default)
    {
        var alreadyOpen = await OpenStints(sessionId, member).AnyAsync(ct);
        if (alreadyOpen)
        {
            return;
        }

        var stint = new CallParticipant
        {
            CallSessionId = sessionId,
            UserId = member.UserId,
            GuestSessionId = member.GuestSessionId,
            JoinedAt = joinedAt,
        };
        db.CallParticipants.Add(stint);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: OpenUserStintIndex or OpenGuestStintIndex,
        })
        {
            // A concurrent webhook or reconciliation opened the same stint first; theirs stands.
            db.Entry(stint).State = EntityState.Detached;
        }
    }

    public async Task RecordLeaveAsync(Guid sessionId, CallMember member, DateTimeOffset leftAt, CancellationToken ct = default)
    {
        await using var tx = await LockMeetingOfSessionAsync(sessionId, ct);
        var open = await OpenStints(sessionId, member).ToListAsync(ct);
        foreach (var stint in open)
        {
            stint.LeftAt = leftAt < stint.JoinedAt ? stint.JoinedAt : leftAt;
            stint.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // Leaving the room ends the person's raised hand or turn (UC-79 EX-3, BR-337), in the same save.
        if (open.Count > 0 && tx.MeetingId is { } meetingId)
        {
            var invitations = member.GuestSessionId is { } guest
                ? await db.GuestSessions.Where(g => g.Id == guest).Select(g => g.InvitationId).ToListAsync(ct)
                : null;
            if (member.UserId is not null || invitations is { Count: > 0 })
                await SpeakingRequestCleanup.CloseOpenAsync(db, meetingId, member.UserId, invitations,
                    (clock ?? TimeProvider.System).GetUtcNow(), ct);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Closing stints also closes raised hands (BR-337), so it takes the meeting row first, as raising a hand does:
    /// a hand raised while its owner leaves is either seen and closed here, or refused for not being in the room.
    /// Joins the caller's transaction when there is one.
    /// </summary>
    private async Task<MeetingLock> LockMeetingOfSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var owned = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        var meetingId = await db.CallSessions.Where(s => s.Id == sessionId && s.Kind == CallKind.MeetingRoom)
            .Select(s => s.MeetingId).SingleOrDefaultAsync(ct);
        if (meetingId is { } id)
            await db.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {id} FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);
        return new(owned, meetingId);
    }

    private sealed class MeetingLock(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? owned, Guid? meetingId)
        : IAsyncDisposable
    {
        public Guid? MeetingId => meetingId;
        public Task CommitAsync(CancellationToken ct) => owned?.CommitAsync(ct) ?? Task.CompletedTask;
        public ValueTask DisposeAsync() => owned?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    private IQueryable<CallParticipant> OpenStints(Guid sessionId, CallMember member)
        => member.GuestSessionId is { } guest
            ? db.CallParticipants.Where(p => p.CallSessionId == sessionId && p.GuestSessionId == guest && p.LeftAt == null)
            : db.CallParticipants.Where(p => p.CallSessionId == sessionId && p.UserId == member.UserId && p.LeftAt == null);

    public async Task EndSessionAsync(Guid sessionId, DateTimeOffset endedAt, CancellationToken ct = default)
    {
        await using var tx = await LockMeetingOfSessionAsync(sessionId, ct);
        var session = await db.CallSessions.SingleOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || session.Status == CallStatus.Ended)
        {
            return;
        }

        var participants = await db.CallParticipants.Where(p => p.CallSessionId == sessionId).ToListAsync(ct);
        // Provider clocks and delayed events can be ahead of the local confirmation time.
        // Preserve existing evidence and never end a session before any of its known stints.
        var effectiveEnd = endedAt < session.StartedAt ? session.StartedAt : endedAt;
        foreach (var stint in participants)
        {
            if (stint.JoinedAt > effectiveEnd) effectiveEnd = stint.JoinedAt;
            if (stint.LeftAt is { } leftAt && leftAt > effectiveEnd) effectiveEnd = leftAt;
        }
        foreach (var stint in participants.Where(p => p.LeftAt == null))
        {
            stint.LeftAt = effectiveEnd;
            stint.UpdatedAt = DateTimeOffset.UtcNow;
        }

        session.Status = CallStatus.Ended;
        session.EndedAt = effectiveEnd;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        // Nobody is left in the room, so nobody is waiting to speak or speaking (BR-337).
        if (tx.MeetingId is { } meetingId)
            await SpeakingRequestCleanup.CloseOpenAsync(db, meetingId, null, null, (clock ?? TimeProvider.System).GetUtcNow(), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<ActiveRoomSession>> ListActiveRoomSessionsAsync(CancellationToken ct = default)
    {
        var rows = await db.CallSessions.AsNoTracking()
            .Where(s => s.Kind == CallKind.MeetingRoom && s.Status == CallStatus.Active && s.MeetingId != null)
            .Select(s => new
            {
                s.Id,
                MeetingId = s.MeetingId!.Value,
                s.Meeting!.Status,
                s.Meeting.Mode,
                Open = s.Participants.Where(p => p.LeftAt == null).Select(p => new { p.UserId, p.GuestSessionId }).ToList(),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new ActiveRoomSession(r.Id, r.MeetingId, r.Status, r.Mode,
                r.Open.Select(p => new CallMember(p.UserId, p.GuestSessionId)).ToHashSet()))
            .ToArray();
    }
}
