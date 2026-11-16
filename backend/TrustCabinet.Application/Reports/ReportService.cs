using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Audit;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Application.Reports;

/// <summary>
/// SCRUM-38 — the meeting report (UC-70a) and its Excel export (UC-70b). Every figure is computed in the database over
/// the meetings the caller may read under <see cref="MeetingReadPolicy"/>, applied in the query before anything is
/// counted (BR-432) — for a System Admin as for anyone else. Only counts and per-meeting numbers leave the database:
/// never an id or a title (BR-437).
/// </summary>
public sealed class ReportService(IMeetingReportQuery reports, IMeetingReportRenderer renderer, IAuditService audit,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<ReportService> logger) : IReportService
{
    public async Task<ReportOutcome> GetMeetingReportAsync(Guid actorId, string? from, string? to, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        if (ReportPeriod.Parse(from, to, now) is not { } period) return new(ReportFailure.InvalidPeriod);
        try
        {
            return new(Report: await unitOfWork.ExecuteAsync(token => BuildAsync(actorId, period, now, token),
                TransactionMode.None, ct));
        }
        catch (PersistenceException error)
        {
            logger.LogError(error.InnerException ?? error, "Could not read the meeting report {From}–{To}", period.From, period.To);
            return new(ReportFailure.ReadFailed);
        }
    }

    public async Task<ReportOutcome> ExportMeetingReportAsync(Guid actorId, string? from, string? to, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        if (ReportPeriod.Parse(from, to, now) is not { } period) return new(ReportFailure.InvalidPeriod);
        try
        {
            var report = await unitOfWork.ExecuteAsync(token => BuildAsync(actorId, period, now, token),
                TransactionMode.None, ct);
            var bytes = renderer.Build(report);
            // BR-438: no record, no file — the figures leave the system only with a trace of who took them.
            await audit.RecordAsync(AuditActions.ReportExported, AuditActions.ReportObjectType, null, actorId,
                new { report = "meetings", from = period.From, to = period.To }, ct);
            return new(File: new ReportFile($"bao-cao-cuoc-hop_{period.From}_{period.To}.xlsx", bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        }
        catch (PersistenceException error)
        {
            logger.LogError(error.InnerException ?? error, "Could not export the meeting report {From}–{To}", period.From, period.To);
            return new(ReportFailure.ExportFailed);
        }
    }

    private async Task<MeetingReport> BuildAsync(Guid actorId, ReportPeriod period, DateTimeOffset now, CancellationToken ct)
    {
        // Npgsql writes timestamptz parameters only in UTC.
        var start = period.Start.ToUniversalTime();
        var end = period.End.ToUniversalTime();
        // BR-434: one GROUP BY over month (Vietnam time), status and sensitivity; the three tables are sums of it.
        var groups = await reports.ListGroupsAsync(actorId, start, end, ct);

        // BR-435, BR-436 and the duration (PO 09/11/2026, Q2): per Ended meeting, the numbers its averages need — nothing that names the meeting.
        var ended = await reports.ListEndedRowsAsync(actorId, start, end, ct);

        var rates = ended
            .Where(m => m.InternalMarked + m.GuestMarked > 0 && m.InternalInvitees + m.GuestInvitees > 0)
            .Select(m => (double)(m.InternalPresent + m.GuestPresent) / (m.InternalInvitees + m.GuestInvitees))
            .ToArray();
        var minutes = ended
            .Where(m => m.ActualStartedAt is not null && m.ActualEndedAt is not null)
            .Select(m => (m.ActualEndedAt!.Value - m.ActualStartedAt!.Value).TotalMinutes)
            .ToArray();
        var hours = ended
            .Where(m => m.ActualEndedAt is not null && m.FirstIssuedAt is not null)
            .Select(m => (m.FirstIssuedAt!.Value - m.ActualEndedAt!.Value).TotalHours)
            .ToArray();

        return new MeetingReport(
            period.From, period.To, now.ToOffset(ReportPeriod.Vietnam), groups.Sum(g => g.Count),
            period.EachMonth().Select(at => new MonthCount(ReportPeriod.Month(at.Year, at.Month),
                groups.Where(g => g.Year == at.Year && g.Month == at.Month).Sum(g => g.Count))).ToArray(),
            Enum.GetValues<MeetingStatus>().Select(s => new StatusCount(s.ToString(),
                groups.Where(g => g.Status == s).Sum(g => g.Count))).ToArray(),
            Enum.GetValues<Sensitivity>().Select(s => new SensitivityCount(s.ToString(),
                groups.Where(g => g.Sensitivity == s).Sum(g => g.Count))).ToArray(),
            new AttendanceFigure(rates.Length == 0 ? null : Math.Round(rates.Average(), 4), rates.Length,
                ended.Count - rates.Length),
            new DurationFigure(minutes.Length == 0 ? null : Math.Round(minutes.Average(), 1), minutes.Length,
                ended.Count - minutes.Length),
            new TurnaroundFigure(hours.Length == 0 ? null : Math.Round(hours.Average(), 2), hours.Length,
                ended.Count - hours.Length));
    }
}
