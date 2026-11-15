using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Audit;

/// <summary>Writes the audit log. Append-only: nothing here updates or deletes a row (BR-440).</summary>
public interface IAuditLogRepository
{
    /// <summary>Stages the row; it is written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void Add(AuditLogEntry entry);
}
