using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Users;
using TrustCabinet.Infrastructure.Audit;

namespace TrustCabinet.Infrastructure.Users;

public static class UserServiceCollectionExtensions
{
    /// <summary>
    /// Registers the storage of user management (SCRUM-13) and of the audit log it records through; the
    /// services themselves are registered by <c>AddApplication</c>.
    ///
    /// All scoped: each depends on the request's DbContext. <see cref="ICallerContext"/> is not
    /// registered here — it is the web project's to supply, since it reads the HTTP request.
    /// </summary>
    public static IServiceCollection AddTrustCabinetUserManagement(this IServiceCollection services)
    {
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        // The caller's own account is read through whatever serves the accounts, so the capability gate never sees
        // another account store than the user screens (a test that swaps in fake accounts swaps both).
        services.AddScoped<ICallerAccountQuery>(sp => (ICallerAccountQuery)sp.GetRequiredService<IUserAccountRepository>());
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditLogQuery, TrustCabinet.Infrastructure.Audit.AuditLogQuery>();

        return services;
    }
}
