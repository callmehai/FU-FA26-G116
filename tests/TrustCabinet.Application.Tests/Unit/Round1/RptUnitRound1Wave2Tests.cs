using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Reports;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 wave-2 causation tests for FE-15 (Reporting): one xUnit test per UTC listed in
// wave2_W2A.txt (Report.ExportMeetingReport + Report.GetMeetingReport).
// Each test carries BOTH [Trait("UTC", "<Class.Method>/<UTCnnn>")] (what apply-trx matches on,
// since the trx carries no trait) AND [Trait("Trait", "UTC")] (so --filter "Trait=UTC" selects
// them), plus a DisplayName containing the same "<Class.Method>/<UTCnnn>" string.
// The asserts cover the Expected triple (Return / Exception / Log Message).
// Tester: Phạm Gia Khánh.
public sealed class RptUnitRound1Wave2Tests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    private readonly Guid _actor = Guid.CreateVersion7();

    private static ReportService Service(IMeetingReportQuery query, FakeMeetingReportRenderer renderer,
        FakeAuditService audit, FakeUnitOfWork uow, Wave2Logger<ReportService> log)
        => new(query, new FakeReportFactQuery(), renderer, audit, uow, new FixedClock(Now), log);

    [Fact(DisplayName = "ReportService.ExportMeetingReportAsync/UTC002 single-month period exports its own file")]
    [Trait("UTC", "ReportService.ExportMeetingReportAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Export_UTC002()
    {
        var query = new FakeMeetingReportQuery();
        var renderer = new FakeMeetingReportRenderer();
        var audit = new FakeAuditService();
        var log = new Wave2Logger<ReportService>();
        query.Groups.Add(new MeetingReportGroup(2026, 11, MeetingStatus.Published, Sensitivity.Normal, 3));

        var outcome = await Service(query, renderer, audit, new FakeUnitOfWork(), log)
            .ExportMeetingReportAsync(_actor, "2026-11", "2026-11", default);

        Assert.Equal(ReportFailure.None, outcome.Failure); // Return: no failure.
        Assert.Equal("bao-cao-cuoc-hop_2026-11_2026-11.xlsx", outcome.File!.FileName); // Return: file name.
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            outcome.File.ContentType); // Return: content type.
        Assert.Equal("2026-11", Assert.Single(renderer.Received).From); // Effect: rendered that period.
        Assert.Equal([AuditActions.ReportExported], audit.Actions); // Effect: export audited.
        Assert.Empty(log.Entries); // Log: N/A.
        // Exception: N/A — the awaited call completed without throwing.
    }

    [Fact(DisplayName = "ReportService.ExportMeetingReportAsync/UTC004 store failure answers export failed")]
    [Trait("UTC", "ReportService.ExportMeetingReportAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Export_UTC004()
    {
        var query = new FakeMeetingReportQuery { Fail = new PersistenceUnavailableException() };
        var renderer = new FakeMeetingReportRenderer();
        var audit = new FakeAuditService();
        var log = new Wave2Logger<ReportService>();

        var outcome = await Service(query, renderer, audit, new FakeUnitOfWork(), log)
            .ExportMeetingReportAsync(_actor, "2026-01", "2026-12", default);

        Assert.Equal(ReportFailure.ExportFailed, outcome.Failure); // Return.
        Assert.Null(outcome.File); // Return: no file.
        Assert.Empty(renderer.Received); // Effect: nothing rendered.
        Assert.Empty(audit.Actions); // Effect: no export recorded.
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Error
            && e.Message.Contains("Could not export the meeting report")); // Log.
        // Exception: N/A — the store error is converted into ExportFailed, not thrown.
    }

    [Fact(DisplayName = "ReportService.GetMeetingReportAsync/UTC004 meetings without numbers average to null")]
    [Trait("UTC", "ReportService.GetMeetingReportAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Get_UTC004()
    {
        var query = new FakeMeetingReportQuery();
        var renderer = new FakeMeetingReportRenderer();
        var audit = new FakeAuditService();
        var log = new Wave2Logger<ReportService>();
        query.Groups.Add(new MeetingReportGroup(2026, 5, MeetingStatus.Draft, Sensitivity.Normal, 2));
        query.EndedRows.AddRange(
        [
            new MeetingReportEndedRow(0, 0, 0, 0, 0, 0, null, null, null),
            new MeetingReportEndedRow(0, 0, 0, 0, 0, 0, null, null, null),
        ]);

        var outcome = await Service(query, renderer, audit, new FakeUnitOfWork(), log)
            .GetMeetingReportAsync(_actor, "2026-01", "2026-12", default);

        Assert.Equal(ReportFailure.None, outcome.Failure); // Return: no failure.
        Assert.Equal(2, outcome.Report!.Total); // Return: the meetings are counted.
        Assert.Null(outcome.Report.Attendance.AverageRate); // Return.
        Assert.Equal((0, 2), (outcome.Report.Attendance.Meetings,
            outcome.Report.Attendance.MeetingsWithoutAttendance)); // Return.
        Assert.Null(outcome.Report.Duration.AverageMinutes); // Return.
        Assert.Equal((0, 2), (outcome.Report.Duration.Meetings,
            outcome.Report.Duration.MeetingsWithoutTimes)); // Return.
        Assert.Null(outcome.Report.MinutesTurnaround.AverageHours); // Return.
        Assert.Equal((0, 2), (outcome.Report.MinutesTurnaround.Meetings,
            outcome.Report.MinutesTurnaround.MeetingsWithoutIssuedMinutes)); // Return.
        Assert.Equal(12, outcome.Report.ByMonth.Count); // Return: every month, zeros included.
        Assert.Equal(8, outcome.Report.ByStatus.Count); // Return: all 8 statuses.
        Assert.Equal(3, outcome.Report.BySensitivity.Count); // Return: all 3 sensitivities.
        Assert.Empty(audit.Actions); // Effect: a read is never audited.
        Assert.Empty(log.Entries); // Log: N/A.
        // Exception: N/A — the awaited call completed without throwing.
    }

    /// <summary>A report query that honours cancellation, so the cancellation UTCs can be
    /// observed: the shared fake ignores the token. Private to this wave-2 file.</summary>
    private sealed class CancelQuery(FakeMeetingReportQuery inner) : IMeetingReportQuery
    {
        public Task<List<MeetingReportGroup>> ListGroupsAsync(Guid actorId, DateTimeOffset start,
            DateTimeOffset end, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return inner.ListGroupsAsync(actorId, start, end, ct);
        }

        public Task<List<MeetingReportEndedRow>> ListEndedRowsAsync(Guid actorId, DateTimeOffset start,
            DateTimeOffset end, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return inner.ListEndedRowsAsync(actorId, start, end, ct);
        }
    }

    /// <summary>Captures log records so a test can assert the Log Message line of a UTC.
    /// Private to this wave-2 file.</summary>
    private sealed class Wave2Logger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Error)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error,
            Func<TState, Exception?, string> format)
            => Entries.Add((level, format(state, error), error));
    }
}
