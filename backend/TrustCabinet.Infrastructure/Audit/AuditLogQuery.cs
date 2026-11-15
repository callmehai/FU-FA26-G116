using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using TrustCabinet.Application.Audit;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
// The criteria record of Application shares this class's name.
using AuditLogCriteria = TrustCabinet.Application.Audit.AuditLogQuery;

namespace TrustCabinet.Infrastructure.Audit;

/// <summary>The audit log reads of UC-13. Raw SQL because the Guest test and the keyword read the jsonb column.</summary>
internal sealed class AuditLogQuery(TrustCabinetDbContext db) : IAuditLogQuery
{
    /// <summary>
    /// A Guest has no account, so their rows have no actor and carry the invitation in the details — under four keys,
    /// one per writer (SCRUM-16 note §1.1): <c>document.viewed</c>, <c>call.guest_admitted</c>, <c>speech_clip.deleted</c>
    /// and <c>meeting.participants.*</c>. New writers use <c>guestInvitationId</c>.
    /// <see cref="AuditLogQueryService.GuestInvitationId"/> reads the same keys in the same order.
    /// </summary>
    private const string GuestInvitationSql =
        "COALESCE(a.details->>'guestInvitationId', a.details->>'invitationId', a.details->>'GuestInvitationId', "
        + "a.details->'guest'->>'InvitationId')";

    public Task<int> CountAsync(AuditLogCriteria query, CancellationToken ct = default)
        => Filtered(query).CountAsync(ct);

    public Task<List<AuditLogEntry>> FindPageAsync(AuditLogCriteria query, int skip, CancellationToken ct = default)
        => Filtered(query).OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Skip(skip).Take(query.PageSize).ToListAsync(ct);

    public async Task<AuditLogNames> FindNamesAsync(
        List<Guid> userIds, List<Guid> roleIds, List<Guid> invitationIds, CancellationToken ct = default)
    {
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => new AuditLogPerson(u.FullName, u.Email), ct);
        var roles = await db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Name }).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var invitations = await db.MeetingGuestInvitations.AsNoTracking()
            .Where(i => invitationIds.Contains(i.Id))
            .Select(i => new { i.Id, i.InvitedName, i.Email })
            .ToDictionaryAsync(i => i.Id, i => new AuditLogPerson(i.InvitedName, i.Email), ct);
        return new AuditLogNames(users, roles, invitations);
    }

    /// <summary>Every criterion must hold (BR-31).</summary>
    private IQueryable<AuditLogEntry> Filtered(AuditLogCriteria query)
    {
        var sql = new StringBuilder("SELECT a.* FROM audit_log a WHERE TRUE");
        var parameters = new List<NpgsqlParameter>();

        void Where(string condition, string name, object value, NpgsqlDbType type)
        {
            sql.Append(" AND ").Append(condition);
            parameters.Add(new NpgsqlParameter(name, type) { Value = value });
        }

        if (query.From is { } from) Where("a.occurred_at >= @from", "from", from, NpgsqlDbType.TimestampTz);
        if (query.To is { } to) Where("a.occurred_at <= @to", "to", to, NpgsqlDbType.TimestampTz);
        if (query.Action is { } action) Where("a.action = @action", "action", action, NpgsqlDbType.Text);
        if (query.ActorUserId is { } actor) Where("a.actor_id = @actor", "actor", actor, NpgsqlDbType.Uuid);
        if (query.ActorKind == AuditActorKind.Guest) sql.Append($" AND a.actor_id IS NULL AND {GuestInvitationSql} IS NOT NULL");
        if (query.ActorKind == AuditActorKind.System) sql.Append($" AND a.actor_id IS NULL AND {GuestInvitationSql} IS NULL");
        // The object (BR-442): its id, and for an account or a role the name the list shows (the only names it looks up,
        // BR-443 — never a meeting title). Then the recorded values, at any depth and unescaped: a key name or a null
        // never matches, so "reason" does not return every row that has a reason field.
        // ponytail: walks every row's details in the period; add a generated text column with a trigram index if the
        // log grows past what a sequential scan answers quickly.
        if (query.Search is { } search)
            Where("(a.object_id::text ILIKE @q"
                    + $" OR (a.object_type = '{AuditActions.UserObjectType}' AND EXISTS "
                    + "(SELECT 1 FROM users u WHERE u.id = a.object_id AND u.full_name ILIKE @q))"
                    + $" OR (a.object_type = '{AuditActions.RoleObjectType}' AND EXISTS "
                    + "(SELECT 1 FROM roles r WHERE r.id = a.object_id AND r.name ILIKE @q))"
                    + " OR EXISTS (SELECT 1 FROM jsonb_path_query(a.details, '$.**') v "
                    + "WHERE jsonb_typeof(v) IN ('string', 'number', 'boolean') AND v #>> ARRAY[]::text[] ILIKE @q))",
                "q", $"%{EscapeLike(search)}%", NpgsqlDbType.Text);

        return db.AuditLog.FromSqlRaw(sql.ToString(), parameters.ToArray()).AsNoTracking();
    }

    /// <summary>PostgreSQL's LIKE escapes with a backslash by default; a typed % or _ is matched as itself.</summary>
    private static string EscapeLike(string value)
        => value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
