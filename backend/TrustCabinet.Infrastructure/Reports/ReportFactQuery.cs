using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Reports;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Reports;

/// <summary>
/// The rows behind the report query, one read per main table. Each read keeps the meetings the caller may read under
/// <see cref="MeetingReadPolicy.ForUser"/> whose planned start is in the period, in the query, before anything leaves the
/// database (BR-149, BR-150) — the same filter as <see cref="MeetingReportQuery"/>.
/// </summary>
public sealed class ReportFactQuery(TrustCabinetDbContext db) : IReportFactQuery
{
    public Task<List<ReportMeetingRow>> ListMeetingsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default)
        => Readable(actorId, start, end)
            .Select(m => new ReportMeetingRow(
                m.Id,
                m.Title,
                m.PlannedStartTime,
                m.Status,
                m.Mode,
                m.Sensitivity,
                m.ActualStartedAt,
                m.ActualEndedAt,
                m.CreatedById,
                m.CreatedBy.FullName,
                m.Participants.AsQueryable().Where(ParticipantRules.CurrentExpression)
                    .Where(p => p.Role == MeetingRole.Chair).Select(p => (Guid?)p.UserId).FirstOrDefault(),
                m.Participants.AsQueryable().Where(ParticipantRules.CurrentExpression)
                    .Where(p => p.Role == MeetingRole.Chair).Select(p => p.User.FullName).FirstOrDefault(),
                // BR-151: current participants and guests whose invitation stands, as MeetingReportQuery counts them.
                m.Participants.AsQueryable().Count(ParticipantRules.CurrentExpression)
                    + db.MeetingGuestInvitations.Count(g => g.MeetingId == m.Id && g.RevokedAt == null),
                m.Participants.AsQueryable().Where(ParticipantRules.CurrentExpression)
                    .Count(p => p.Attendance == AttendanceStatus.Present)
                    + db.MeetingGuestInvitations.Count(g => g.MeetingId == m.Id && g.RevokedAt == null
                        && g.Attendance == AttendanceStatus.Present),
                m.Participants.AsQueryable().Where(ParticipantRules.CurrentExpression)
                    .Count(p => p.Attendance == AttendanceStatus.Present || p.Attendance == AttendanceStatus.Absent)
                    + db.MeetingGuestInvitations.Count(g => g.MeetingId == m.Id && g.RevokedAt == null
                        && (g.Attendance == AttendanceStatus.Present || g.Attendance == AttendanceStatus.Absent)),
                m.Participants.AsQueryable().Where(ParticipantRules.ActiveExpression)
                    .Any(p => p.UserId == actorId && (p.Role == MeetingRole.Chair || p.Role == MeetingRole.Secretary)),
                m.Participants.AsQueryable().Where(ParticipantRules.ActiveExpression).Any(p => p.UserId == actorId)))
            .ToListAsync(ct);

    public async Task<List<ReportParticipantRow>> ListParticipantsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default)
    {
        var meetings = Readable(actorId, start, end).Select(m => m.Id);
        var members = await db.MeetingParticipants.AsNoTracking()
            .Where(ParticipantRules.CurrentExpression)
            .Where(p => meetings.Contains(p.MeetingId))
            .Select(p => new ReportParticipantRow(p.MeetingId, p.UserId.ToString(), p.User.FullName, p.Role, p.Attendance,
                p.Response))
            .ToListAsync(ct);
        var guests = await db.MeetingGuestInvitations.AsNoTracking()
            .Where(g => g.RevokedAt == null && meetings.Contains(g.MeetingId))
            .Select(g => new { g.Id, g.MeetingId, Name = g.SelfDeclaredDisplayName ?? g.InvitedName, g.Attendance, g.Response })
            .ToListAsync(ct);
        // A guest is known by the invitation, never by the email address (PO 01/11/2026: a guest's email stays hidden).
        members.AddRange(guests.Select(g => new ReportParticipantRow(g.MeetingId, $"guest:{g.Id}", g.Name, MeetingRole.Guest,
            g.Attendance, g.Response)));
        return members;
    }

    public Task<List<ReportVoteRow>> ListVotesAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default)
    {
        var meetings = Readable(actorId, start, end).Select(m => m.Id);
        return db.VotingSessions.AsNoTracking()
            .Where(v => meetings.Contains(v.MeetingId))
            .Select(v => new ReportVoteRow(v.MeetingId, v.Status, v.Conclusion, v.IsAnonymous, v.EligibleCount, v.Receipts.Count))
            .ToListAsync(ct);
    }

    public Task<List<ReportDocumentRow>> ListDocumentsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default)
    {
        var meetings = Readable(actorId, start, end).Select(m => m.Id);
        return db.MeetingDocuments.AsNoTracking()
            .Where(d => d.RemovedAt == null && meetings.Contains(d.MeetingId))
            .Select(d => new ReportDocumentRow(d.MeetingId, d.AddedById, d.AddedBy.FullName, d.FileName, d.StoredFile.ByteSize,
                d.StoredFile.PageCount, d.Comments.Count, d.ReadConfirmations.Count))
            .ToListAsync(ct);
    }

    public Task<List<ReportMinutesRow>> ListMinutesAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default)
    {
        var meetings = Readable(actorId, start, end).Select(m => m.Id);
        return db.MeetingMinutes.AsNoTracking()
            .Where(x => meetings.Contains(x.MeetingId))
            .Select(x => new ReportMinutesRow(x.MeetingId, x.Status, x.CreatedById, x.CreatedBy.FullName, x.IssuedAt))
            .ToListAsync(ct);
    }

    public Task<List<ReportRecordingRow>> ListRecordingsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default)
    {
        var meetings = Readable(actorId, start, end).Select(m => m.Id);
        return db.Recordings.AsNoTracking()
            .Where(r => r.Status != RecordingStatus.Deleted && meetings.Contains(r.MeetingId))
            .Select(r => new ReportRecordingRow(r.MeetingId, r.Source, r.Status, r.DurationMs,
                r.Transcript == null ? null : (TranscriptStatus?)r.Transcript.Status))
            .ToListAsync(ct);
    }

    /// <summary>The meetings the caller may read whose planned start is in [start, end) (BR-149, BR-150).</summary>
    private IQueryable<Meeting> Readable(Guid actorId, DateTimeOffset start, DateTimeOffset end)
        => db.Meetings.AsNoTracking()
            .Where(MeetingReadPolicy.ForUser(actorId))
            .Where(m => m.PlannedStartTime >= start && m.PlannedStartTime < end);
}
