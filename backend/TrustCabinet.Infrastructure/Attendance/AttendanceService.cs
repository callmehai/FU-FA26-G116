using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Attendance;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Attendance;

public sealed class AttendanceService(TrustCabinetDbContext db, AttendanceRepository repository,
    IParticipantRepository participants, IParticipantGuestAccess guests, IAttendanceChangePublisher publisher,
    ICallerContext caller, TimeProvider clock, ILogger<AttendanceService> logger) : IAttendanceService
{
    private static bool Persistence(Exception ex) => ex is DbUpdateException or NpgsqlException or TimeoutException;

    private async Task<AttendanceFailure> AccessAsync(ParticipantSnapshot? s, ParticipantActor actor, CancellationToken ct)
    {
        if (actor.Guest is { } g)
            return actor.UserId is null && g.IsValid && s is not null && g.MeetingId == s.Meeting.Id
                && ParticipantRules.Published(s.Meeting.Status) && s.Guests.Any(x => x.Id == g.InvitationId)
                && await guests.CanAccessAsync(g, ct) ? AttendanceFailure.None : AttendanceFailure.NotFound;
        if (actor.UserId is not { } id || !await db.Users.AnyAsync(u => u.Id == id && u.IsActive, ct)) return AttendanceFailure.Forbidden;
        return s is not null && MeetingReadPolicy.CanRead(s.Meeting, id) ? AttendanceFailure.None : AttendanceFailure.NotFound;
    }

    public async Task<AttendanceResult> ReadAsync(Guid meetingId, ParticipantActor actor, AttendanceQuery query, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var snapshot = await participants.LoadAsync(meetingId, false, ct);
            var access = await AccessAsync(snapshot, actor, ct);
            if (access != AttendanceFailure.None) return new(access);
            var s = snapshot!;
            if (query.Invalid || query.Page < 1 || query.PageSize is < 1 or > 100) return new(AttendanceFailure.InvalidQuery);
            var current = s.Meeting.Participants.Where(ParticipantRules.Current).ToArray();
            var profiles = await guests.GetProfilesAsync(meetingId, s.Guests.Select(g => g.Id).ToArray(), ct);
            var recorders = await RecordersAsync(current.Select(p => p.AttendanceRecordedById)
                .Concat(s.Guests.Select(g => g.AttendanceRecordedById)), ct);
            var rows = current.Select(p => Row(meetingId, p.Id, "Internal", p.User.FullName, p.Role.ToString(), p.User.IsActive,
                p.Response.ToString(), p.Attendance, p.AttendanceVersion,
                p.AttendanceRecordedById, p.AttendanceRecordedAt, recorders))
                .Concat(s.Guests.Select(g => Row(meetingId, g.Id, "Guest",
                    profiles.GetValueOrDefault(g.Id)?.DisplayName ?? g.InvitedName, null, null,
                    g.Response.ToString(), g.Attendance, g.AttendanceVersion,
                    g.AttendanceRecordedById, g.AttendanceRecordedAt, recorders)))
                .OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Kind, StringComparer.Ordinal).ThenBy(r => r.Id).ToArray();
            var totals = new AttendanceTotals(rows.Count(r => r.Attendance == "Present"), rows.Count(r => r.Attendance == "Absent"),
                rows.Count(r => r.Attendance is null && r.LegacyAttendance is null), rows.Count(r => r.LegacyAttendance is not null), rows.Length);
            var offset = (long)(query.Page - 1) * query.PageSize;
            var page = offset >= rows.Length ? [] : rows.Skip((int)offset).Take(query.PageSize).ToArray();
            return new(Page: new(meetingId, s.Meeting.Status.ToString(), clock.GetUtcNow(),
                new(actor.Guest is null && actor.UserId is { } id && ParticipantRules.IsSecretary(s.Meeting, id)
                    && s.Meeting.Status == MeetingStatus.InProgress, true), page, rows.Length, query.Page, query.PageSize, totals));
        }
        catch (Exception ex) when (Persistence(ex) || ex is InvalidDataException)
        { logger.LogError(ex, "Attendance read failed for {MeetingId}", meetingId); return new(AttendanceFailure.ReadFailed); }
    }

    public async Task<AttendanceResult> SaveAsync(Guid meetingId, ParticipantActor actor, AttendanceInput input, CancellationToken ct)
    {
        AttendanceChange? change = null;
        AttendanceResult result;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await repository.LockMeetingAsync(meetingId, ct);
            if (actor.UserId is { } userId)
                await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR SHARE").AsNoTracking().ToArrayAsync(ct);
            var s = await participants.LoadAsync(meetingId, false, ct);
            var access = await AccessAsync(s, actor, ct);
            if (access != AttendanceFailure.None) return new(access);
            if (actor.Guest is not null || actor.UserId is not { } actorId || !ParticipantRules.IsSecretary(s!.Meeting, actorId))
                return new(AttendanceFailure.Forbidden);
            if (s.Meeting.Status != MeetingStatus.InProgress) return new(AttendanceFailure.Locked);
            if (input.ParseFailure != AttendanceFailure.None) return new(input.ParseFailure);
            if (!AttendanceRules.Valid(input)) return new(AttendanceFailure.Invalid);
            var entries = input.Entries!.Select(e => e!).ToArray();
            var targets = await repository.LockTargetsAsync(meetingId,
                entries.Where(e => e.Kind == "Internal").Select(e => e.Id).ToArray(),
                entries.Where(e => e.Kind == "Guest").Select(e => e.Id).ToArray(), ct);
            var internalTargets = targets.Internal.Where(ParticipantRules.Current).ToDictionary(p => p.Id);
            var guestTargets = targets.Guests.Where(g => g.RevokedAt is null).ToDictionary(g => g.Id);
            foreach (var e in entries)
            {
                long? version = e.Kind == "Internal" ? internalTargets.GetValueOrDefault(e.Id)?.AttendanceVersion
                    : guestTargets.GetValueOrDefault(e.Id)?.AttendanceVersion;
                if (version is null || AttendanceRules.Token(meetingId, e.Kind!, e.Id, version.Value) != e.ExpectedVersion)
                    return new(AttendanceFailure.Stale);
            }
            var now = AgendaExecution.UtcMicroseconds(clock.GetUtcNow());
            var evidence = new List<object>();
            foreach (var e in entries)
            {
                var next = e.Attendance == "Present" ? AttendanceStatus.Present : AttendanceStatus.Absent;
                if (e.Kind == "Internal")
                {
                    var p = internalTargets[e.Id];
                    if (p.Attendance == next) continue;
                    evidence.Add(new { e.Id, e.Kind, before = new { status = p.Attendance.ToString(), p.AttendanceVersion, p.AttendanceRecordedById, p.AttendanceRecordedAt },
                        after = new { status = next.ToString(), AttendanceVersion = checked(p.AttendanceVersion + 1), AttendanceRecordedById = actorId, AttendanceRecordedAt = now } });
                    p.Attendance = next; p.AttendanceVersion = checked(p.AttendanceVersion + 1);
                    p.AttendanceRecordedById = actorId; p.AttendanceRecordedAt = now; p.UpdatedAt = now;
                }
                else
                {
                    var g = guestTargets[e.Id];
                    if (g.Attendance == next) continue;
                    evidence.Add(new { e.Id, e.Kind, before = new { status = g.Attendance.ToString(), g.AttendanceVersion, g.AttendanceRecordedById, g.AttendanceRecordedAt },
                        after = new { status = next.ToString(), AttendanceVersion = checked(g.AttendanceVersion + 1), AttendanceRecordedById = actorId, AttendanceRecordedAt = now } });
                    g.Attendance = next; g.AttendanceVersion = checked(g.AttendanceVersion + 1);
                    g.AttendanceRecordedById = actorId; g.AttendanceRecordedAt = now; g.UpdatedAt = now;
                }
            }
            // Resolve all response data before commit: a later read failure must not mask a successful save.
            var recorders = await RecordersAsync(targets.Internal.Select(p => p.AttendanceRecordedById)
                .Concat(targets.Guests.Select(g => g.AttendanceRecordedById)), ct);
            var saved = entries.Select(e => e.Kind == "Internal"
                ? Saved(meetingId, e.Id, e.Kind, internalTargets[e.Id].Attendance, internalTargets[e.Id].AttendanceVersion,
                    internalTargets[e.Id].AttendanceRecordedById, internalTargets[e.Id].AttendanceRecordedAt, recorders)
                : Saved(meetingId, e.Id, e.Kind!, guestTargets[e.Id].Attendance, guestTargets[e.Id].AttendanceVersion,
                    guestTargets[e.Id].AttendanceRecordedById, guestTargets[e.Id].AttendanceRecordedAt, recorders)).ToArray();
            if (evidence.Count > 0)
            {
                db.AuditLog.Add(new AuditLogEntry { ActorId = actorId, Action = "meeting.attendance.recorded", ObjectType = "Meeting",
                    ObjectId = meetingId, IpAddress = caller.IpAddress, Details = JsonSerializer.Serialize(new { entries = evidence }),
                    OccurredAt = now, CreatedAt = now, UpdatedAt = now });
                await db.SaveChangesAsync(ct);
                change = new(Guid.CreateVersion7(), meetingId, now);
            }
            await tx.CommitAsync(ct);
            result = new(Saved: new(meetingId, evidence.Count, saved));
        }
        catch (Exception ex) when (Persistence(ex) || ex is OverflowException or InvalidDataException)
        {
            db.ChangeTracker.Clear();
            logger.LogError(ex, "Attendance save failed for {MeetingId}; re-read after an uncertain commit", meetingId);
            return new(AttendanceFailure.SaveFailed);
        }
        if (change is not null)
        {
            // Publication is awaited, bounded, and independent of the HTTP disconnect after commit.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await publisher.PublishAsync(change, timeout.Token); }
            catch (Exception ex) { logger.LogWarning(ex, "Attendance event {EventId} failed after commit", change.EventId); }
        }
        return result;
    }

    private Task<Dictionary<Guid, AttendanceRecorder>> RecordersAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var keys = ids.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        return db.Users.AsNoTracking().Where(u => keys.Contains(u.Id)).Select(u => new AttendanceRecorder(u.Id, u.FullName)).ToDictionaryAsync(u => u.Id, ct);
    }

    private static AttendanceRow Row(Guid meetingId, Guid id, string kind, string name, string? role, bool? active,
        string? response, AttendanceStatus status, long version, Guid? recorder, DateTimeOffset? at, Dictionary<Guid, AttendanceRecorder> recorders)
    {
        var display = AttendanceRules.Display(status);
        return new(id, kind, name, role, active, response, display.Official, display.Legacy, AttendanceRules.Token(meetingId, kind, id, version),
            recorder is { } key ? recorders.GetValueOrDefault(key) : null, at);
    }
    private static AttendanceSavedEntry Saved(Guid meetingId, Guid id, string kind, AttendanceStatus status, long version,
        Guid? recorder, DateTimeOffset? at, Dictionary<Guid, AttendanceRecorder> recorders)
        => new(id, kind, AttendanceRules.Display(status).Official, null, AttendanceRules.Token(meetingId, kind, id, version),
            recorder is { } key ? recorders.GetValueOrDefault(key) : null, at);
}
