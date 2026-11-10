using System.Text.Json;

namespace TrustCabinet.Domain.Audit;

/// <summary>Who an audit entry is attributed to (BR-441).</summary>
public enum AuditActorKind { User, Guest, System }

/// <summary>
/// UC-13's criteria, already validated (BR-442). <paramref name="ActorUserId"/> narrows to one account;
/// <paramref name="ActorKind"/> <c>Guest</c> or <c>System</c> to every row of that kind — at most one of the two is set.
/// </summary>
public sealed record AuditLogQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    Guid? ActorUserId,
    AuditActorKind? ActorKind,
    string? Action,
    string? Search,
    int Page,
    int PageSize);

/// <summary>For a Guest, <paramref name="Id"/> is the invitation; for the system, all three are null.</summary>
public sealed record AuditActorView(AuditActorKind Kind, Guid? Id, string? Name, string? Email);

public sealed record AuditLogItem(
    Guid Id,
    DateTimeOffset OccurredAt,
    AuditActorView Actor,
    string Action,
    string ActionLabel,
    string ObjectType,
    Guid? ObjectId,
    string? ObjectName,
    string IpAddress,
    JsonElement Details);

public sealed record AuditLogPage(IReadOnlyList<AuditLogItem> Items, int Page, int PageSize, int TotalCount, int TotalPages);

/// <summary>The <c>:Audit Log Query Service</c> of UC-13. Read only: nothing here writes the audit log (BR-440).</summary>
public interface IAuditLogQueryService
{
    /// <summary>One page, newest first; null when the audit log could not be read (EX-2).</summary>
    Task<AuditLogPage?> GetPageAsync(AuditLogQuery query, CancellationToken ct = default);
}
