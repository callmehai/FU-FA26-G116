using TrustCabinet.Application.Audit;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Audit;

public sealed class AuditLogRepository(TrustCabinetDbContext db) : IAuditLogRepository
{
    public void Add(AuditLogEntry entry) => db.AuditLog.Add(entry);
}
