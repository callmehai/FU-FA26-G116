using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Audit;

/// <summary>An account or a Guest invitation as an audit page names it.</summary>
public sealed record AuditLogPerson(string Name, string Email);

/// <summary>The names of one audit page: accounts, roles and Guest invitations, nothing else (BR-443).</summary>
public sealed record AuditLogNames(
    IReadOnlyDictionary<Guid, AuditLogPerson> Users,
    IReadOnlyDictionary<Guid, string> Roles,
    IReadOnlyDictionary<Guid, AuditLogPerson> Invitations);

/// <summary>UC-13's reads of the audit log, untracked. Every criterion of the query must hold (BR-31).</summary>
public interface IAuditLogQuery
{
    /// <summary>How many rows meet the criteria.</summary>
    Task<int> CountAsync(AuditLogQuery query, CancellationToken ct = default);

    /// <summary>The rows meeting the criteria, newest first: <paramref name="skip"/> skipped, then <c>PageSize</c> taken.</summary>
    Task<List<AuditLogEntry>> FindPageAsync(AuditLogQuery query, int skip, CancellationToken ct = default);

    /// <summary>The accounts, roles and invitations of these ids that exist, in three reads.</summary>
    Task<AuditLogNames> FindNamesAsync(
        List<Guid> userIds, List<Guid> roleIds, List<Guid> invitationIds, CancellationToken ct = default);
}
