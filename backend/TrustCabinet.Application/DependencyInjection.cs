using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Access;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Authentication;
using TrustCabinet.Application.Notifications;
using TrustCabinet.Application.Users;

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
        AddAuthentication(services);
        AddAccess(services);
        AddUsers(services);
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

    private static void AddAuthentication(IServiceCollection services)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
    }

    private static void AddAccess(IServiceCollection services)
    {
        services.AddScoped<IRolePermissionService, RolePermissionService>();
        services.AddScoped<IRoleService, RoleService>();

        // One registration for the lock that keeps role-matrix writes and user writes from each removing the
        // last administrator at the same time: RolePermissionService, RoleService and UserService guard one
        // invariant between them. See IAdministratorInvariantGuard.
        services.AddScoped<IAdministratorInvariantGuard, AdministratorInvariantGuard>();
    }

    private static void AddUsers(IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
    }
}
