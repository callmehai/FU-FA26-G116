using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Audit;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Application.Reports;

/// <summary>
/// SCRUM-38 — the meeting report (UC-79a) and its Excel export (UC-79b). Every figure is computed in the database over
/// the meetings the caller may read under <see cref="MeetingReadPolicy"/>, applied in the query before anything is
/// counted (BR-149, BR-150) — for a System Admin as for anyone else. The meeting report carries only counts (BR-152). The report
/// query (supervisor review 22/11/2026) groups one main table as the caller picks; grouped by person or by meeting it
/// names what the caller can already open, never a meeting's content.
/// </summary>
public sealed class ReportService(IMeetingReportQuery reports, IReportFactQuery facts, IMeetingReportRenderer renderer,
    IAuditService audit, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<ReportService> logger) : IReportService
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<ReportOutcome> GetMeetingReportAsync(Guid actorId, string? from, string? to, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        if (ReportPeriod.Parse(from, to, now) is not { } period) return new(ReportFailure.InvalidPeriod);
        try
        {
            return new(Report: await unitOfWork.ExecuteAsync(token => BuildAsync(actorId, period, now, token),
                TransactionMode.None, ct));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError((e as PersistenceException)?.InnerException ?? e, "Could not read the meeting report {From}–{To}", period.From, period.To);
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
            // BR-010: no record, no file — the figures leave the system only with a trace of who took them.
            await audit.RecordAsync(AuditActions.ReportExported, AuditActions.ReportObjectType, null, actorId,
                new { report = "meetings", from = period.From, to = period.To }, ct);
            return new(File: new ReportFile($"bao-cao-cuoc-hop_{period.From}_{period.To}.xlsx", bytes, XlsxContentType));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError((e as PersistenceException)?.InnerException ?? e, "Could not export the meeting report {From}–{To}", period.From, period.To);
            return new(ReportFailure.ExportFailed);
        }
    }

    private async Task<MeetingReport> BuildAsync(Guid actorId, ReportPeriod period, DateTimeOffset now, CancellationToken ct)
    {
        // Npgsql writes timestamptz parameters only in UTC.
        var start = period.Start.ToUniversalTime();
        var end = period.End.ToUniversalTime();
        // BR-149, BR-150: one GROUP BY over month (Vietnam time), status and sensitivity; the three tables are sums of it.
        var groups = await reports.ListGroupsAsync(actorId, start, end, ct);

        // BR-151 and the duration (PO 09/11/2026, Q2): per Ended meeting, the numbers its averages need — nothing that names the meeting.
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

        // The figures a manager reads first: what the caller may see of the votes, minutes and recordings.
        var meetings = (await facts.ListMeetingsAsync(actorId, start, end, ct)).ToDictionary(m => m.Id);
        var votes = Join(meetings, await facts.ListVotesAsync(actorId, start, end, ct), v => v.MeetingId, SeesVote)
            .Select(f => f.Row).ToArray();
        var minutesRows = Join(meetings, await facts.ListMinutesAsync(actorId, start, end, ct), m => m.MeetingId, SeesMinutes)
            .Select(f => f.Row).ToArray();
        var recordings = Join(meetings, await facts.ListRecordingsAsync(actorId, start, end, ct), r => r.MeetingId, SeesRecording)
            .Select(f => f.Row).ToArray();
        var closed = votes.Where(v => v.Status == VotingStatus.Closed).ToArray();

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
                ended.Count - hours.Length),
            new VoteFigure(closed.Length, closed.Count(v => v.Conclusion == VoteConclusion.Passed),
                closed.Count(v => v.Conclusion == VoteConclusion.NotPassed),
                closed.Count(v => v.Conclusion == VoteConclusion.Deferred)),
            new MinutesFigure(minutesRows.Where(m => m.IssuedAt is not null).Select(m => m.MeetingId).Distinct().Count(),
                minutesRows.Count(m => m.Status == MinutesStatus.WaitingApproval)),
            new RecordingFigure(recordings.Length, ReportCatalog.RecordedHours(recordings),
                ReportCatalog.TranscribedHours(recordings)));
    }

    public async Task<ReportOutcome> QueryAsync(Guid actorId, ReportQueryInput input, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        if (Parse(input, now) is not ({ } definition, { } query, { } period)) return new(Refusal(input, now));
        try
        {
            return new(Table: await unitOfWork.ExecuteAsync(token => TableAsync(actorId, definition, query, period, now, token),
                TransactionMode.None, ct));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError((e as PersistenceException)?.InnerException ?? e, "Could not read the {Entity} report {From}–{To}",
                definition.Entity, period.From, period.To);
            return new(ReportFailure.ReadFailed);
        }
    }

    public async Task<ReportOutcome> ExportQueryAsync(Guid actorId, ReportQueryInput input, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        if (Parse(input, now) is not ({ } definition, { } query, { } period)) return new(Refusal(input, now));
        try
        {
            var table = await unitOfWork.ExecuteAsync(token => TableAsync(actorId, definition, query, period, now, token),
                TransactionMode.None, ct);
            var bytes = renderer.Build(table);
            // BR-010, as for the meeting report: no record, no file.
            await audit.RecordAsync(AuditActions.ReportExported, AuditActions.ReportObjectType, null, actorId,
                new
                {
                    report = definition.Entity, groupBy = table.GroupBy, from = period.From, to = period.To,
                    filters = string.Join(", ", table.Filters.Select(f => $"{f.Key}={f.Value}")), rows = table.Rows.Count,
                }, ct);
            return new(File: new ReportFile($"bao-cao-{definition.FileSlug}_{period.From}_{period.To}.xlsx", bytes, XlsxContentType));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError((e as PersistenceException)?.InnerException ?? e, "Could not export the {Entity} report {From}–{To}",
                definition.Entity, period.From, period.To);
            return new(ReportFailure.ExportFailed);
        }
    }

    /// <summary>The table, its query and the period; any of them null when the input names something that is not there.</summary>
    private static (IReportDefinition? Definition, ParsedReportQuery? Query, ReportPeriod? Period) Parse(
        ReportQueryInput input, DateTimeOffset now)
    {
        var period = ReportPeriod.Parse(input.From, input.To, now);
        var definition = ReportCatalog.All.GetValueOrDefault(string.IsNullOrEmpty(input.Entity) ? ReportCatalog.Meetings.Entity : input.Entity);
        var query = definition?.Parse(input);
        return (definition, query, period);
    }

    /// <summary>A bad period is told first (MSG463), as on the meeting report; then a bad parameter (MSG484).</summary>
    private static ReportFailure Refusal(ReportQueryInput input, DateTimeOffset now)
        => ReportPeriod.Parse(input.From, input.To, now) is null ? ReportFailure.InvalidPeriod : ReportFailure.InvalidParameter;

    private async Task<ReportTable> TableAsync(Guid actorId, IReportDefinition definition, ParsedReportQuery query,
        ReportPeriod period, DateTimeOffset now, CancellationToken ct)
    {
        var start = period.Start.ToUniversalTime();
        var end = period.End.ToUniversalTime();
        var meetings = (await facts.ListMeetingsAsync(actorId, start, end, ct)).ToDictionary(m => m.Id);
        return definition switch
        {
            ReportDefinition<ReportMeetingRow> d => d.Run(
                meetings.Values.Select(m => new ReportFact<ReportMeetingRow>(m, m)).ToList(), query, period, now),
            ReportDefinition<ReportParticipantRow> d => d.Run(
                Join(meetings, await facts.ListParticipantsAsync(actorId, start, end, ct), p => p.MeetingId, (_, _) => true),
                query, period, now),
            ReportDefinition<ReportVoteRow> d => d.Run(
                Join(meetings, await facts.ListVotesAsync(actorId, start, end, ct), v => v.MeetingId, SeesVote), query, period, now),
            ReportDefinition<ReportDocumentRow> d => d.Run(
                Join(meetings, await facts.ListDocumentsAsync(actorId, start, end, ct), x => x.MeetingId, (_, _) => true),
                query, period, now),
            ReportDefinition<ReportMinutesRow> d => d.Run(
                Join(meetings, await facts.ListMinutesAsync(actorId, start, end, ct), m => m.MeetingId, SeesMinutes),
                query, period, now),
            ReportDefinition<ReportRecordingRow> d => d.Run(
                Join(meetings, await facts.ListRecordingsAsync(actorId, start, end, ct), r => r.MeetingId, SeesRecording),
                query, period, now),
            _ => throw new InvalidOperationException($"No reader for the {definition.Entity} report."),
        };
    }

    /// <summary>Rows of the meetings read, each with its meeting, and only those the caller may see.</summary>
    private static List<ReportFact<T>> Join<T>(IReadOnlyDictionary<Guid, ReportMeetingRow> meetings, IEnumerable<T> rows,
        Func<T, Guid> meetingOf, Func<ReportMeetingRow, T, bool> sees)
        => rows.Select(r => meetings.TryGetValue(meetingOf(r), out var m) && sees(m, r) ? new ReportFact<T>(m, r) : null)
            .OfType<ReportFact<T>>().ToList();

    /// <summary>A draft vote is the Chair's and Secretaries' until it opens, as in the Votes tab.</summary>
    private static bool SeesVote(ReportMeetingRow meeting, ReportVoteRow vote)
        => vote.Status != VotingStatus.Draft || meeting.ViewerManages;

    /// <summary>BR-139: anyone else reads only issued minutes and the revisions they replaced.</summary>
    private static bool SeesMinutes(ReportMeetingRow meeting, ReportMinutesRow minutes)
        => minutes.Status is MinutesStatus.Issued or MinutesStatus.Superseded || meeting.ViewerManages;

    /// <summary>Recordings are listed to the meeting's own participants only (<c>RecordingRules.CanListen</c>).</summary>
    private static bool SeesRecording(ReportMeetingRow meeting, ReportRecordingRow recording) => meeting.ViewerOnRoster;
}
