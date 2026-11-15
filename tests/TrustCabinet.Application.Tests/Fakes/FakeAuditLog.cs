using TrustCabinet.Application.Audit;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>The audit log in a list, as both its writer and its reader. Filters by actor only.</summary>
public sealed class FakeAuditLog : IAuditLogRepository, IAuditLogQuery
{
    public List<AuditLogEntry> Rows { get; } = [];
    public Dictionary<Guid, AuditLogPerson> Users { get; } = [];
    public Dictionary<Guid, string> Roles { get; } = [];
    public Dictionary<Guid, AuditLogPerson> Invitations { get; } = [];

    /// <summary>Every read, in order: <c>count</c>, <c>page</c>, <c>names</c>.</summary>
    public List<string> Reads { get; } = [];

    /// <summary>Thrown by every read, in place of answering.</summary>
    public Exception? Fail { get; set; }

    public void Add(AuditLogEntry entry) => Rows.Add(entry);

    public Task<int> CountAsync(AuditLogQuery query, CancellationToken ct = default)
        => Read("count", () => Filtered(query).Count());

    public Task<List<AuditLogEntry>> FindPageAsync(AuditLogQuery query, int skip, CancellationToken ct = default)
        => Read("page", () => Filtered(query).OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Skip(skip).Take(query.PageSize).ToList());

    public Task<AuditLogNames> FindNamesAsync(
        List<Guid> userIds, List<Guid> roleIds, List<Guid> invitationIds, CancellationToken ct = default)
        => Read("names", () => new AuditLogNames(
            Users.Where(u => userIds.Contains(u.Key)).ToDictionary(),
            Roles.Where(r => roleIds.Contains(r.Key)).ToDictionary(),
            Invitations.Where(i => invitationIds.Contains(i.Key)).ToDictionary()));

    private IEnumerable<AuditLogEntry> Filtered(AuditLogQuery query)
        => Rows.Where(a => query.ActorUserId is not { } actor || a.ActorId == actor);

    private Task<T> Read<T>(string name, Func<T> answer)
    {
        Reads.Add(name);
        return Fail is { } failure ? Task.FromException<T>(failure) : Task.FromResult(answer());
    }
}
