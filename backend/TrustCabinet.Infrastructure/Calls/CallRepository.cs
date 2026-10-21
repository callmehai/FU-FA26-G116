using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Calls;

/// <summary>
/// Room access and call history. Reads meeting participation straight from
/// <c>meeting_participants</c> rather than through the meeting repository: entering the room is a
/// stricter rule than reading the meeting (the creator or approver alone may not join).
///
/// "Invited" means what <c>AgendaRules.CurrentParticipant</c> means — active account, not delegated
/// away — written out here because EF has to translate it. SCRUM-20 extends both when assignments
/// gain Current/Removed.
/// </summary>
public sealed class CallRepository(TrustCabinetDbContext db) : ICallRepository
{
    public const string ActiveRoomIndex = "ux_call_sessions_active_meeting_room";

    private static readonly MeetingStatus[] ParticipantVisible =
    [
        MeetingStatus.Published, MeetingStatus.InProgress, MeetingStatus.Ended,
    ];

    public Task<CallRoomAccess?> FindRoomAccessAsync(Guid meetingId, Guid userId, CancellationToken ct = default)
        => db.MeetingParticipants.AsNoTracking()
            .Where(p => p.MeetingId == meetingId
                && p.UserId == userId
                && p.Response != InvitationResponse.Delegated
                && p.DelegatedToUserId == null
                && p.User.IsActive
                && ParticipantVisible.Contains(p.Meeting.Status))
            .Select(p => new CallRoomAccess(p.MeetingId, p.Meeting.Status, p.Meeting.Mode, p.Role, p.User.FullName))
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlySet<Guid>> ListAllowedUserIdsAsync(Guid meetingId, CancellationToken ct = default)
        => (await db.MeetingParticipants.AsNoTracking()
            .Where(p => p.MeetingId == meetingId
                && p.Response != InvitationResponse.Delegated
                && p.DelegatedToUserId == null
                && p.User.IsActive
                && p.Meeting.Status == MeetingStatus.InProgress
                && p.Meeting.Mode != MeetingMode.InPerson)
            .Select(p => p.UserId)
            .ToListAsync(ct))
            .ToHashSet();

    public Task<CallSession?> FindActiveRoomSessionAsync(Guid meetingId, CancellationToken ct = default)
        => db.CallSessions.SingleOrDefaultAsync(
            s => s.MeetingId == meetingId && s.Kind == CallKind.MeetingRoom && s.Status == CallStatus.Active, ct);

    public async Task<CallSession> EnsureActiveRoomSessionAsync(
        Guid meetingId, Guid startedById, DateTimeOffset startedAt, CancellationToken ct = default)
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

    public async Task RecordJoinAsync(Guid sessionId, Guid userId, DateTimeOffset joinedAt, CancellationToken ct = default)
    {
        var alreadyOpen = await db.CallParticipants.AnyAsync(
            p => p.CallSessionId == sessionId && p.UserId == userId && p.LeftAt == null, ct);
        if (alreadyOpen)
        {
            return;
        }

        db.CallParticipants.Add(new CallParticipant { CallSessionId = sessionId, UserId = userId, JoinedAt = joinedAt });
        await db.SaveChangesAsync(ct);
    }

    public async Task RecordLeaveAsync(Guid sessionId, Guid userId, DateTimeOffset leftAt, CancellationToken ct = default)
    {
        var open = await db.CallParticipants
            .Where(p => p.CallSessionId == sessionId && p.UserId == userId && p.LeftAt == null)
            .ToListAsync(ct);
        foreach (var stint in open)
        {
            stint.LeftAt = leftAt < stint.JoinedAt ? stint.JoinedAt : leftAt;
            stint.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task EndSessionAsync(Guid sessionId, DateTimeOffset endedAt, CancellationToken ct = default)
    {
        var session = await db.CallSessions.SingleOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || session.Status == CallStatus.Ended)
        {
            return;
        }

        var open = await db.CallParticipants.Where(p => p.CallSessionId == sessionId && p.LeftAt == null).ToListAsync(ct);
        foreach (var stint in open)
        {
            stint.LeftAt = endedAt < stint.JoinedAt ? stint.JoinedAt : endedAt;
            stint.UpdatedAt = DateTimeOffset.UtcNow;
        }

        session.Status = CallStatus.Ended;
        session.EndedAt = endedAt;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
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
                Open = s.Participants.Where(p => p.LeftAt == null).Select(p => p.UserId).ToList(),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new ActiveRoomSession(r.Id, r.MeetingId, r.Status, r.Mode, r.Open.ToHashSet()))
            .ToArray();
    }
}
