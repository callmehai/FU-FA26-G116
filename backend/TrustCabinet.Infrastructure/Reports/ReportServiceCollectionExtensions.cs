using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Reports;

namespace TrustCabinet.Infrastructure.Reports;

public static class ReportServiceCollectionExtensions
{
    /// <summary>
    /// SCRUM-38 — the meeting report and its Excel export. The service is registered by
    /// <c>AddApplication</c>.
    /// </summary>
    public static IServiceCollection AddTrustCabinetReports(this IServiceCollection services)
    {
        services.AddScoped<IMeetingReportQuery, MeetingReportQuery>();
        services.AddScoped<IMeetingReportRenderer, MeetingReportRenderer>();
        return services;
    }
}
