using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Reports;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 causation tests for FE-15 (Reporting): one xUnit test per kept UTC below.
// Each test carries BOTH [Trait("UTC", "<Class.Method>/<UTCnnn>")] (what apply-trx matches on,
// since the trx carries no trait) AND [Trait("Trait", "UTC")] (so --filter "Trait=UTC" selects
// them), plus a DisplayName containing the same "<Class.Method>/<UTCnnn>" string.
// Tester: Phạm Gia Khánh.
public sealed class RptUnitRound1Tests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingReportQuery _query = new();
    private readonly FakeMeetingReportRenderer _renderer = new();
    private readonly FakeAuditService _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Guid _actor = Guid.CreateVersion7();

    private ReportService Service() => new(_query, _renderer, _audit, _unitOfWork,
        new FixedClock(Now), NullLogger<ReportService>.Instance);

    [Fact(DisplayName = "ReportService.GetMeetingReportAsync/UTC001 monthly totals with no audit row")]
    [Trait("UTC", "ReportService.GetMeetingReportAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task GetMeetingReportAsync_UTC001()
    {
        _query.Groups.AddRange(
        [
            new MeetingReportGroup(2026, 2, MeetingStatus.Published, Sensitivity.Secret, 2),
            new MeetingReportGroup(2026, 3, MeetingStatus.Draft, Sensitivity.Normal, 1),
        ]);

        var outcome = await Service().GetMeetingReportAsync(_actor, "2026-02", "2026-03", default);

        Assert.Equal(ReportFailure.None, outcome.Failure);
        Assert.Equal(3, outcome.Report!.Total);
        Assert.Empty(_audit.Actions);
    }

    [Fact(DisplayName = "ReportService.GetMeetingReportAsync/UTC002 open-ended period reads nothing")]
    [Trait("UTC", "ReportService.GetMeetingReportAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task GetMeetingReportAsync_UTC002()
    {
        var outcome = await Service().GetMeetingReportAsync(_actor, "2026-01", null, default);

        Assert.Equal(ReportFailure.InvalidPeriod, outcome.Failure);
        Assert.Empty(_query.Calls);
        Assert.Empty(_audit.Actions);
    }

    [Fact(DisplayName = "ReportService.GetMeetingReportAsync/UTC003 store failure answers read failed")]
    [Trait("UTC", "ReportService.GetMeetingReportAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task GetMeetingReportAsync_UTC003()
    {
        _query.Fail = new PersistenceUnavailableException();

        var outcome = await Service().GetMeetingReportAsync(_actor, "2026-01", "2026-12", default);

        Assert.Equal(ReportFailure.ReadFailed, outcome.Failure);
        Assert.Null(outcome.Report);
        Assert.Empty(_audit.Actions);
    }

    [Fact(DisplayName = "ReportService.ExportMeetingReportAsync/UTC001 export file is recorded in the audit log")]
    [Trait("UTC", "ReportService.ExportMeetingReportAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task ExportMeetingReportAsync_UTC001()
    {
        _query.Groups.Add(new MeetingReportGroup(2026, 2, MeetingStatus.Published, Sensitivity.Secret, 1));

        var outcome = await Service().ExportMeetingReportAsync(_actor, "2026-01", "2026-12", default);

        Assert.Equal(ReportFailure.None, outcome.Failure);
        Assert.Equal("bao-cao-cuoc-hop_2026-01_2026-12.xlsx", outcome.File!.FileName);
        Assert.Equal([AuditActions.ReportExported], _audit.Actions);
    }

    [Fact(DisplayName = "ReportService.ExportMeetingReportAsync/UTC003 reversed period builds nothing")]
    [Trait("UTC", "ReportService.ExportMeetingReportAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task ExportMeetingReportAsync_UTC003()
    {
        var outcome = await Service().ExportMeetingReportAsync(_actor, "2026-12", "2026-01", default);

        Assert.Equal(ReportFailure.InvalidPeriod, outcome.Failure);
        Assert.Null(outcome.File);
        Assert.Empty(_renderer.Received);
    }
}
