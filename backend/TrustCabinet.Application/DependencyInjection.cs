using Microsoft.Extensions.DependencyInjection;

namespace TrustCabinet.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the use case services. Empty for now: each module moves its registrations here, into a
    /// <c>private static void Add&lt;Module&gt;(IServiceCollection)</c> of its own and with the lifetime it had,
    /// in the subtask that moves its services (SCRUM-143, rule R10).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
