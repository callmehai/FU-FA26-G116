using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Access;

namespace TrustCabinet.Infrastructure.Access;

public static class RolePermissionServiceCollectionExtensions
{
    /// <summary>
    /// Registers the role ↔ capability matrix (SCRUM-60, UC-09 and UC-10).
    ///
    /// Scoped: both depend on the request's DbContext. <c>IAuditService</c> comes from
    /// <c>AddTrustCabinetUserManagement</c> — registered once there rather than twice, since a
    /// second registration of the same scoped service would be the one that wins and nobody would
    /// notice which.
    /// </summary>
    public static IServiceCollection AddTrustCabinetRolePermissions(this IServiceCollection services)
    {
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddScoped<IRolePermissionService, RolePermissionService>();

        return services;
    }
}
