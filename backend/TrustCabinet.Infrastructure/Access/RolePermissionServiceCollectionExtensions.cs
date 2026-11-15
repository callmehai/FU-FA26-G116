using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Access;

namespace TrustCabinet.Infrastructure.Access;

public static class RolePermissionServiceCollectionExtensions
{
    /// <summary>
    /// Registers the storage of the role ↔ capability matrix (SCRUM-60, UC-10 and UC-11) and of role
    /// management (SCRUM-63, UC-12a … UC-12c). Scoped: both depend on the request's DbContext. The services,
    /// and the administrator-invariant lock they share with user management, come from <c>AddApplication</c>.
    /// </summary>
    public static IServiceCollection AddTrustCabinetRolePermissions(this IServiceCollection services)
    {
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();

        return services;
    }
}
