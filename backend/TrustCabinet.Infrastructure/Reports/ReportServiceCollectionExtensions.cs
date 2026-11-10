using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Reports;

namespace TrustCabinet.Infrastructure.Reports;

public static class ReportServiceCollectionExtensions
{
    /// <summary>SCRUM-38 — the meeting report and its Excel export.</summary>
    public static IServiceCollection AddTrustCabinetReports(this IServiceCollection services)
    {
        services.AddScoped<IReportService, ReportService>();
        return services;
    }
}
