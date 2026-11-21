using TrustCabinet.Application.Users;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>No account and no capability: the services that ask fall back to what a plain caller may do.</summary>
public sealed class NoCallerAccounts : ICallerAccountQuery
{
    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<User?>(null);

    public Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(Guid roleId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyCollection<Capability>>([]);
}
