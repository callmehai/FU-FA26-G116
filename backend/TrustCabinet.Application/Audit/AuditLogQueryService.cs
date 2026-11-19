using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Audit;

/// <summary>
/// UC-13 — one page of the audit log, newest first, with the actor and the object of each row named (BR-441, BR-442).
/// The object is named as people know it — the meeting's title, the document's file name, the account's full name
/// (PO 19/11/2026, replacing BR-443's "no meeting titles") — so a reader can tell two meetings apart; the id stays in the
/// row for the detail view. Only the name is read: viewing the log still opens nothing it names. Nothing of a Mật
/// meeting is named (Hải 20/11/2026).
/// </summary>
internal sealed class AuditLogQueryService(IAuditLogQuery auditLog, IUnitOfWork unitOfWork, ILogger<AuditLogQueryService> logger)
    : IAuditLogQueryService
{
    public async Task<AuditLogPage?> GetPageAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        try
        {
            return await unitOfWork.ExecuteAsync(async token =>
            {
                var total = await auditLog.CountAsync(query, token);
                var skip = (long)(query.Page - 1) * query.PageSize;
                var page = skip >= total
                    ? []
                    : await auditLog.FindPageAsync(query, (int)skip, token);
                var items = await NameAsync(page, token);
                var totalPages = (int)((total + (long)query.PageSize - 1) / query.PageSize);
                return new AuditLogPage(items, query.Page, query.PageSize, total, totalPages);
            }, TransactionMode.None, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not read the audit log.");
            return null;
        }
    }

    private async Task<IReadOnlyList<AuditLogItem>> NameAsync(IReadOnlyList<AuditLogEntry> page, CancellationToken ct)
    {
        if (page.Count == 0) return [];
        var details = page.ToDictionary(a => a.Id, a => JsonDocument.Parse(a.Details).RootElement.Clone());
        var guests = page.Where(a => a.ActorId is null)
            .Select(a => (a.Id, Invitation: GuestInvitationId(details[a.Id])))
            .Where(x => x.Invitation is not null)
            .ToDictionary(x => x.Id, x => x.Invitation!.Value);

        var userIds = page.Where(a => a.ActorId is not null).Select(a => a.ActorId!.Value).Distinct().ToList();
        var invitationIds = guests.Values.Distinct().ToList();

        // Every object of the page named in one read, by the same expression the keyword search reads.
        var (users, invitations, objects) = await auditLog.FindNamesAsync(
            userIds, invitationIds, page.Select(a => a.Id).ToArray(), ct);

        return page.Select(a =>
        {
            AuditActorView actor;
            if (a.ActorId is { } userId)
            {
                var user = users.GetValueOrDefault(userId);
                actor = new(AuditActorKind.User, userId, user?.Name, user?.Email);
            }
            else if (guests.TryGetValue(a.Id, out var invitationId))
            {
                var invitation = invitations.GetValueOrDefault(invitationId);
                actor = new(AuditActorKind.Guest, invitationId, invitation?.Name, invitation?.Email);
            }
            else
            {
                actor = new(AuditActorKind.System, null, null, null);
            }

            var named = objects.GetValueOrDefault(a.Id);
            var secret = named?.Secret == true;
            return new AuditLogItem(a.Id, a.OccurredAt, actor, a.Action, AuditEventCatalog.Label(a.Action),
                a.ObjectType, a.ObjectId, secret ? null : named?.Name, secret, a.IpAddress,
                secret ? WithoutNames(details[a.Id]) : details[a.Id]);
        }).ToList();
    }

    /// <summary>The details of an entry about a Mật meeting, less the names it recorded (<see cref="AuditLogSecrecy.NameKeys"/>).</summary>
    internal static JsonElement WithoutNames(JsonElement details)
    {
        if (details.ValueKind != JsonValueKind.Object) return details;
        var kept = new Dictionary<string, JsonElement>();
        foreach (var property in details.EnumerateObject())
            if (!AuditLogSecrecy.NameKeys.Contains(property.Name, StringComparer.Ordinal)) kept[property.Name] = property.Value;
        return JsonSerializer.SerializeToElement(kept);
    }

    /// <summary>
    /// A Guest has no account, so their rows have no actor and carry the invitation in the details — under four keys,
    /// one per writer (SCRUM-16 note §1.1): <c>document.viewed</c>, <c>call.guest_admitted</c>, <c>speech_clip.deleted</c>
    /// and <c>meeting.participants.*</c>. New writers use <c>guestInvitationId</c>. The C# twin of the SQL that
    /// <c>AuditLogQuery</c> in Infrastructure filters with: the same keys in the same order.
    /// </summary>
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
