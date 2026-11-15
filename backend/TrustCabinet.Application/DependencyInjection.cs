using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Notifications;

namespace TrustCabinet.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the use case services. Each module moves its registrations here, into a
    /// <c>private static void Add&lt;Module&gt;(IServiceCollection)</c> of its own and with the lifetime it had,
    /// in the subtask that moves its services (SCRUM-143, rule R10).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        AddAudit(services);
        AddNotifications(services);
        return services;
    }

    /// <summary><see cref="ICallerContext"/> is the web project's to supply, since it reads the HTTP request.</summary>
    private static void AddAudit(IServiceCollection services)
    {
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
    }

    private static void AddNotifications(IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
    }
}
