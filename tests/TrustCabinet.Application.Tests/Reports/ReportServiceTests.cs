using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Reports;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Reports;

public sealed class ReportServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingReportQuery _query = new();
    private readonly FakeMeetingReportRenderer _renderer = new();
    private readonly FakeAuditService _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Guid _actor = Guid.CreateVersion7();

    private ReportService Service() => new(_query, new FakeReportFactQuery(), _renderer, _audit, _unitOfWork,
        new FixedClock(Now), NullLogger<ReportService>.Instance);

    [Fact]
    public async Task Viewing_sums_groups_and_averages_ended_rows_without_auditing()
    {
        _query.Groups.AddRange(
        [
            new MeetingReportGroup(2026, 2, MeetingStatus.Published, Sensitivity.Secret, 2),
            new MeetingReportGroup(2026, 3, MeetingStatus.Draft, Sensitivity.Normal, 1),
        ]);
        _query.EndedRows.AddRange(
        [
            new MeetingReportEndedRow(4, 3, 4, 1, 0, 1,
                new DateTimeOffset(2026, 6, 10, 2, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 10, 4, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 10, 14, 0, 0, TimeSpan.Zero)),
            new MeetingReportEndedRow(0, 0, 0, 0, 0, 0, null, null, null),
        ]);

        var outcome = await Service().GetMeetingReportAsync(_actor, "2026-02", "2026-03", default);

        Assert.Equal(ReportFailure.None, outcome.Failure);
        Assert.NotNull(outcome.Report);
        Assert.Equal(3, outcome.Report.Total);
        Assert.Equal([2, 1], outcome.Report.ByMonth.Select(m => m.Count));
        Assert.Equal(2, outcome.Report.ByStatus.Single(s => s.Status == "Published").Count);
        // 3 of 5 invitees present on the first meeting; the second has no attendance to average.
        Assert.Equal(0.6, outcome.Report.Attendance.AverageRate);
        Assert.Equal((1, 1), (outcome.Report.Attendance.Meetings, outcome.Report.Attendance.MeetingsWithoutAttendance));
        Assert.Equal(120, outcome.Report.Duration.AverageMinutes);
        Assert.Equal(10, outcome.Report.MinutesTurnaround.AverageHours);
        Assert.Empty(_audit.Actions);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        Assert.Equal(["groups", "ended"], _query.Calls);
    }

    [Fact]
    public async Task An_invalid_period_reads_nothing()
    {
        var outcome = await Service().GetMeetingReportAsync(_actor, "2026-05", "2026-04", default);

        Assert.Equal(ReportFailure.InvalidPeriod, outcome.Failure);
        Assert.Empty(_unitOfWork.Modes);
        Assert.Empty(_query.Calls);
    }

    [Fact]
    public async Task Export_renders_the_same_figures_and_records_the_export()
    {
        _query.Groups.Add(new MeetingReportGroup(2026, 2, MeetingStatus.Published, Sensitivity.Secret, 1));

        var outcome = await Service().ExportMeetingReportAsync(_actor, "2026-02", "2026-03", default);

        Assert.Equal(ReportFailure.None, outcome.Failure);
        Assert.NotNull(outcome.File);
        Assert.Equal("bao-cao-cuoc-hop_2026-02_2026-03.xlsx", outcome.File.FileName);
        Assert.Equal(_renderer.Bytes, outcome.File.Bytes);
        Assert.Single(_renderer.Received);
        Assert.Equal([AuditActions.ReportExported], _audit.Actions);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task A_store_failure_while_reading_answers_read_failed()
    {
        _query.Fail = new PersistenceUnavailableException();

        var outcome = await Service().GetMeetingReportAsync(_actor, "2026-02", "2026-03", default);

        Assert.Equal(ReportFailure.ReadFailed, outcome.Failure);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task A_store_failure_while_exporting_answers_export_failed()
    {
        _query.Fail = new PersistenceUnavailableException();

        var outcome = await Service().ExportMeetingReportAsync(_actor, "2026-02", "2026-03", default);

        Assert.Equal(ReportFailure.ExportFailed, outcome.Failure);
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task A_store_failure_while_recording_the_export_answers_export_failed()
    {
        _audit.Fail = new PersistenceUnavailableException();

        var outcome = await Service().ExportMeetingReportAsync(_actor, "2026-02", "2026-03", default);

        Assert.Equal(ReportFailure.ExportFailed, outcome.Failure);
        Assert.Null(outcome.File);
    }
}
