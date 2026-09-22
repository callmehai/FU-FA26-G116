using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Users;
using TrustCabinet.Infrastructure.Access;
using TrustCabinet.Infrastructure.Audit;

namespace TrustCabinet.Infrastructure.Users;

public static class UserServiceCollectionExtensions
{
    /// <summary>
    /// Registers user management (SCRUM-13) and the audit writer it records through.
    ///
    /// All scoped: each depends on the request's DbContext. <see cref="ICallerContext"/> is not
    /// registered here — it is the web project's to supply, since it reads the HTTP request.
    /// </summary>
    public static IServiceCollection AddTrustCabinetUserManagement(this IServiceCollection services)
    {
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuditService, AuditService>();

        // The lock that keeps user writes and role-matrix writes from each removing the last
        // administrator at the same time. Registered here and in AddTrustCabinetRolePermissions,
        // both with TryAdd, because either feature can be registered without the other and both
        // need it — see IAdministratorInvariantGuard.
        services.TryAddScoped<IAdministratorInvariantGuard, AdministratorInvariantGuard>();

        return services;
    }
}
