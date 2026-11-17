using TrustCabinet.Application.Abstractions;

namespace TrustCabinet.Infrastructure.Persistence;

public sealed class DatabaseProbe(TrustCabinetDbContext db) : IDatabaseProbe
{
    public Task<bool> CanConnectAsync(CancellationToken ct) => db.Database.CanConnectAsync(ct);
}
