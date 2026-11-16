using TrustCabinet.Application.Reports;

namespace TrustCabinet.Infrastructure.Reports;

public sealed class MeetingReportRenderer : IMeetingReportRenderer
{
    public byte[] Build(MeetingReport report) => MeetingReportXlsx.Build(report);
}
