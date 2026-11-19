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
/// UC-13 — one page of the audit log, newest first, with the actor and the object of each row named (BR-441, BR-442).
/// The object is named as people know it — the meeting's title, the document's file name, the account's full name
/// (PO 19/11/2026, replacing BR-443's "no meeting titles") — so a reader can tell two meetings apart; the id stays in the
/// row for the detail view. Only the name is read: viewing the log still opens nothing it names.
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

    /// <summary>A Guid held in <c>details</c> under <paramref name="key"/>, or null when the value is not one.</summary>
    private static string DetailsGuid(string key)
        => $"(CASE WHEN a.details->>'{key}' ~* '{GuidPattern}' THEN (a.details->>'{key}')::uuid END)";

    /// <summary>Spelled out: FromSqlRaw reads a brace as a parameter placeholder, so no <c>{8}</c> quantifiers.</summary>
    private static readonly string GuidPattern = "^" + string.Join("-",
        new[] { 8, 4, 4, 4, 12 }.Select(n => string.Concat(Enumerable.Repeat("[0-9a-f]", n)))) + "$";

    /// <summary>
    /// The object's name, per row (BR-442; PO 19/11/2026). The live row first, so a renamed meeting reads as it is now;
    /// then what the entry itself recorded, so a deleted role or vote draft keeps the name it had; then, for what lives
    /// inside a meeting and is gone (a discarded take, a deleted sentence), the meeting's title. One correlated lookup per
    /// row: a page is at most 100 rows, and the keyword search reads the same expression.
    /// </summary>
    private static readonly string ObjectNameSql =
        "COALESCE(CASE a.object_type"
        + $" WHEN '{AuditActions.UserObjectType}' THEN (SELECT x.full_name FROM users x WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.RoleObjectType}' THEN (SELECT x.name FROM roles x WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.MeetingObjectType}' THEN (SELECT x.title FROM meetings x WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.MeetingDocumentObjectType}' THEN (SELECT x.file_name FROM meeting_documents x WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.VoteObjectType}' THEN (SELECT x.title FROM voting_sessions x WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.MinutesObjectType}' THEN (SELECT m.title FROM meeting_minutes x JOIN meetings m ON m.id = x.meeting_id WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.RecordingObjectType}' THEN (SELECT m.title FROM recordings x JOIN meetings m ON m.id = x.meeting_id WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.SpeechClipObjectType}' THEN (SELECT m.title FROM speech_clips x JOIN meetings m ON m.id = x.meeting_id WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.TranscriptSpeakerObjectType}' THEN (SELECT m.title FROM transcript_speakers x"
        + " JOIN transcripts t ON t.id = x.transcript_id JOIN recordings r ON r.id = t.recording_id"
        + " JOIN meetings m ON m.id = r.meeting_id WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.VoiceConsentObjectType}' THEN (SELECT u.full_name FROM voice_consents x JOIN users u ON u.id = x.user_id WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.VoiceSampleObjectType}' THEN (SELECT u.full_name FROM voice_samples x JOIN users u ON u.id = x.user_id WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.CalendarShareObjectType}' THEN (SELECT o.full_name || ' → ' || v.full_name FROM calendar_shares x"
        + " JOIN users o ON o.id = x.owner_id JOIN users v ON v.id = x.viewer_id WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.GuestSessionObjectType}' THEN (SELECT i.invited_name FROM meeting_guest_invitations i"
        + $" WHERE i.id::text = {GuestInvitationSql})"
        + " END,"
        + " NULLIF(a.details->>'name', ''), NULLIF(a.details->>'Name', ''), NULLIF(a.details->>'title', ''),"
        + " NULLIF(a.details->>'Title', ''), NULLIF(a.details->>'fileName', ''), NULLIF(a.details->>'FileName', ''),"
        + $" CASE WHEN a.object_type IN ('{AuditActions.RecordingObjectType}', '{AuditActions.SpeechClipObjectType}',"
        + $" '{AuditActions.MinutesObjectType}', '{AuditActions.VoteObjectType}') THEN (SELECT m.title FROM meetings m"
        + $" WHERE m.id = COALESCE({DetailsGuid("meetingId")}, {DetailsGuid("MeetingId")})) END)";

    private static readonly string NamesSql =
        $"SELECT a.id AS \"Id\", {ObjectNameSql} AS \"Name\" FROM audit_log a WHERE a.id = ANY(@ids)";

    /// <summary>
    /// Without marks or case, like every search box: "hop thuong" finds "Họp thường" (unaccent folds đ to d too). A
    /// substring test, not LIKE: unaccent maps full-width ％ ＿ ＼ to % _ \, which would turn back into wildcards.
    /// </summary>
    private static string Matches(string text) => $"strpos(lower(unaccent({text})), lower(unaccent(@q))) > 0";

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
        // The object (BR-442): its id and the name the list shows. Then the recorded values, at any depth: a key name
        // or a null never matches, so "reason" does not return every row that has a reason field.
        // ponytail: walks every row's details in the period; add a generated text column with a trigram index if the
        // log grows past what a sequential scan answers quickly.
        if (query.Search is { } search)
            Where($"({Matches("a.object_id::text")} OR {Matches(ObjectNameSql)}"
                    + " OR EXISTS (SELECT 1 FROM jsonb_path_query(a.details, '$.**') v "
                    + $"WHERE jsonb_typeof(v) IN ('string', 'number', 'boolean') AND {Matches("v #>> ARRAY[]::text[]")}))",
                "q", search, NpgsqlDbType.Text);

        return db.AuditLog.FromSqlRaw(sql.ToString(), parameters.ToArray()).AsNoTracking();
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

        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email }).ToDictionaryAsync(u => u.Id, ct);
        // Every object of the page named in one query, by the same expression the keyword search reads.
        var ids = page.Select(a => a.Id).ToArray();
        var names = (await db.Database
                .SqlQueryRaw<ObjectNameRow>(
                    NamesSql,
                    new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = ids })
                .ToListAsync(ct))
            .Where(n => n.Name is not null).ToDictionary(n => n.Id, n => n.Name!);
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

            return new AuditLogItem(a.Id, a.OccurredAt, actor, a.Action, AuditEventCatalog.Label(a.Action),
                a.ObjectType, a.ObjectId, names.GetValueOrDefault(a.Id), a.IpAddress, details[a.Id]);
        }).ToList();
    }

    private sealed record ObjectNameRow(Guid Id, string? Name);

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
