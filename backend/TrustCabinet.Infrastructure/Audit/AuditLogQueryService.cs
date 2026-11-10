using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Audit;

/// <summary>
/// UC-13 — one page of the audit log, newest first, with the actor of each row named (BR-441, BR-442). It names
/// accounts, roles and Guest invitations only; it never looks up a Meeting, document or minutes (BR-443), so the log
/// cannot become a way round the meeting read policy.
/// </summary>
internal sealed class AuditLogQueryService(TrustCabinetDbContext db, ILogger<AuditLogQueryService> logger)
    : IAuditLogQueryService
{
    /// <summary>
    /// A Guest has no account, so their rows have no actor and carry the invitation in the details — under four keys,
    /// one per writer (SCRUM-16 note §1.1): <c>document.viewed</c>, <c>call.guest_admitted</c>, <c>speech_clip.deleted</c>
    /// and <c>meeting.participants.*</c>. New writers use <c>guestInvitationId</c>. <see cref="GuestInvitationId"/> reads
    /// the same keys in the same order.
    /// </summary>
    private const string GuestInvitationSql =
        "COALESCE(a.details->>'guestInvitationId', a.details->>'invitationId', a.details->>'GuestInvitationId', "
        + "a.details->'guest'->>'InvitationId')";

    public async Task<AuditLogPage?> GetPageAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        try
        {
            var rows = Filtered(query);
            var total = await rows.CountAsync(ct);
            var skip = (long)(query.Page - 1) * query.PageSize;
            var page = skip >= total
                ? []
                : await rows.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
                    .Skip((int)skip).Take(query.PageSize).ToListAsync(ct);
            var items = await NameAsync(page, ct);
            var totalPages = (int)((total + (long)query.PageSize - 1) / query.PageSize);
            return new AuditLogPage(items, query.Page, query.PageSize, total, totalPages);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not read the audit log.");
            return null;
        }
    }

    /// <summary>Every criterion must hold (BR-31). Raw SQL because the Guest test and the keyword read the jsonb column.</summary>
    private IQueryable<AuditLogEntry> Filtered(AuditLogQuery query)
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

    private async Task<IReadOnlyList<AuditLogItem>> NameAsync(IReadOnlyList<AuditLogEntry> page, CancellationToken ct)
    {
        if (page.Count == 0) return [];
        var details = page.ToDictionary(a => a.Id, a => JsonDocument.Parse(a.Details).RootElement.Clone());
        var guests = page.Where(a => a.ActorId is null)
            .Select(a => (a.Id, Invitation: GuestInvitationId(details[a.Id])))
            .Where(x => x.Invitation is not null)
            .ToDictionary(x => x.Id, x => x.Invitation!.Value);

        var userIds = page.Where(a => a.ActorId is not null).Select(a => a.ActorId!.Value)
            .Concat(ObjectIds(page, AuditActions.UserObjectType)).Distinct().ToList();
        var roleIds = ObjectIds(page, AuditActions.RoleObjectType).Distinct().ToList();
        var invitationIds = guests.Values.Distinct().ToList();

        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email }).ToDictionaryAsync(u => u.Id, ct);
        var roles = await db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Name }).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var invitations = await db.MeetingGuestInvitations.AsNoTracking()
            .Where(i => invitationIds.Contains(i.Id))
            .Select(i => new { i.Id, i.InvitedName, i.Email }).ToDictionaryAsync(i => i.Id, ct);

        return page.Select(a =>
        {
            AuditActorView actor;
            if (a.ActorId is { } userId)
            {
                var user = users.GetValueOrDefault(userId);
                actor = new(AuditActorKind.User, userId, user?.FullName, user?.Email);
            }
            else if (guests.TryGetValue(a.Id, out var invitationId))
            {
                var invitation = invitations.GetValueOrDefault(invitationId);
                actor = new(AuditActorKind.Guest, invitationId, invitation?.InvitedName, invitation?.Email);
            }
            else
            {
                actor = new(AuditActorKind.System, null, null, null);
            }

            var objectName = a.ObjectId is not { } objectId ? null : a.ObjectType switch
            {
                AuditActions.UserObjectType => users.GetValueOrDefault(objectId)?.FullName,
                AuditActions.RoleObjectType => roles.GetValueOrDefault(objectId),
                _ => null,
            };
            return new AuditLogItem(a.Id, a.OccurredAt, actor, a.Action, AuditEventCatalog.Label(a.Action),
                a.ObjectType, a.ObjectId, objectName, a.IpAddress, details[a.Id]);
        }).ToList();
    }

    private static IEnumerable<Guid> ObjectIds(IEnumerable<AuditLogEntry> page, string objectType)
        => page.Where(a => a.ObjectType == objectType && a.ObjectId is not null).Select(a => a.ObjectId!.Value);

    /// <summary>The C# twin of <see cref="GuestInvitationSql"/>.</summary>
    internal static Guid? GuestInvitationId(JsonElement details)
    {
        if (details.ValueKind != JsonValueKind.Object) return null;
        foreach (var key in new[] { "guestInvitationId", "invitationId", "GuestInvitationId" })
            if (AsGuid(details, key) is { } id) return id;
        return details.TryGetProperty("guest", out var guest) && guest.ValueKind == JsonValueKind.Object
            ? AsGuid(guest, "InvitationId")
            : null;
    }

    private static Guid? AsGuid(JsonElement element, string key)
        => element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            && Guid.TryParse(value.GetString(), out var id) ? id : null;
}
