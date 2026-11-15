using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Attendance;

namespace TrustCabinet.Infrastructure.Attendance;

public static class AttendanceServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetAttendance(this IServiceCollection services)
    {
        services.AddScoped<AttendanceRepository>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        return services;
    }
}
