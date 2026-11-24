using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using TrustCabinet.Application.Audit;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
// The criteria record of Application shares this class's name.
using AuditLogCriteria = TrustCabinet.Application.Audit.AuditLogQuery;

namespace TrustCabinet.Infrastructure.Audit;

/// <summary>
/// The audit log reads of UC-12. Raw SQL because the Guest test and the keyword read the jsonb column, and because each
/// row's object is named by one SQL expression that both the list and the keyword search read.
/// </summary>
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

    /// <summary>A Guid held in <c>details</c> under <paramref name="key"/>, or null when the value is not one.</summary>
    private static string DetailsGuid(string key)
        => $"(CASE WHEN a.details->>'{key}' ~* '{GuidPattern}' THEN (a.details->>'{key}')::uuid END)";

    /// <summary>Spelled out: FromSqlRaw reads a brace as a parameter placeholder, so no <c>{8}</c> quantifiers.</summary>
    private static readonly string GuidPattern = "^" + string.Join("-",
        new[] { 8, 4, 4, 4, 12 }.Select(n => string.Concat(Enumerable.Repeat("[0-9a-f]", n)))) + "$";

    /// <summary>
    /// The object's name, per row (UC-12; PO 19/11/2026). The live row first, so a renamed meeting reads as it is now;
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

    /// <summary>What lives inside a meeting, and whose name therefore belongs to it.</summary>
    private static readonly string MeetingScopedTypes = string.Join(", ", new[]
    {
        AuditActions.MeetingObjectType, AuditActions.MeetingDocumentObjectType, AuditActions.VoteObjectType,
        AuditActions.MinutesObjectType, AuditActions.RecordingObjectType, AuditActions.SpeechClipObjectType,
        AuditActions.TranscriptSpeakerObjectType, AuditActions.GuestSessionObjectType,
    }.Select(t => $"'{t}'"));

    /// <summary>
    /// The meeting a meeting-scoped object belongs to: from its live row, else from the meeting id its entry recorded
    /// (a deleted sentence, a discarded take, a vote draft deleted at the end of the meeting, a speaker by its
    /// recording). Null when neither tells.
    /// </summary>
    private static readonly string ObjectMeetingSql =
        "COALESCE(CASE a.object_type"
        + $" WHEN '{AuditActions.MeetingObjectType}' THEN a.object_id"
        + $" WHEN '{AuditActions.MeetingDocumentObjectType}' THEN (SELECT x.meeting_id FROM meeting_documents x WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.VoteObjectType}' THEN (SELECT x.meeting_id FROM voting_sessions x WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.MinutesObjectType}' THEN (SELECT x.meeting_id FROM meeting_minutes x WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.RecordingObjectType}' THEN (SELECT x.meeting_id FROM recordings x WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.SpeechClipObjectType}' THEN (SELECT x.meeting_id FROM speech_clips x WHERE x.id = a.object_id)"
        + $" WHEN '{AuditActions.TranscriptSpeakerObjectType}' THEN COALESCE((SELECT r.meeting_id FROM transcript_speakers x"
        + " JOIN transcripts t ON t.id = x.transcript_id JOIN recordings r ON r.id = t.recording_id WHERE x.id = a.object_id),"
        + $" (SELECT r.meeting_id FROM recordings r WHERE r.id = {DetailsGuid("recordingId")}))"
        + $" WHEN '{AuditActions.GuestSessionObjectType}' THEN (SELECT i.meeting_id FROM meeting_guest_invitations i"
        + $" WHERE i.id::text = {GuestInvitationSql})"
        + " END,"
        + $" CASE WHEN a.object_type IN ({MeetingScopedTypes}) THEN COALESCE({DetailsGuid("meetingId")}, {DetailsGuid("MeetingId")}) END)";

    /// <summary>
    /// Hải 20/11/2026: nothing of a Mật meeting is named in the log — not the meeting, nor its documents, votes, minutes,
    /// recordings, sentences, speakers or Guest sessions — for any viewer, and its name never matches a search.
    /// </summary>
    private static readonly string SecretSql =
        $"EXISTS (SELECT 1 FROM meetings sm WHERE sm.id = ({ObjectMeetingSql}) AND sm.sensitivity = '{nameof(Sensitivity.Secret)}')";

    private static readonly string SecretNameKeysSql =
        "ARRAY[" + string.Join(", ", AuditLogSecrecy.NameKeys.Select(k => $"'{k}'")) + "]";

    private static readonly string NamesSql =
        $"SELECT a.id AS \"Id\", CASE WHEN {SecretSql} THEN NULL ELSE {ObjectNameSql} END AS \"Name\","
        + $" {SecretSql} AS \"Secret\" FROM audit_log a WHERE a.id = ANY(@ids)";

    /// <summary>
    /// Without marks or case, like every search box: "hop thuong" finds "Họp thường" (unaccent folds đ to d too). A
    /// substring test, not LIKE: unaccent maps full-width ％ ＿ ＼ to % _ \, which would turn back into wildcards.
    /// </summary>
    private static string Matches(string text) => $"strpos(lower(unaccent({text})), lower(unaccent(@q))) > 0";

    public Task<int> CountAsync(AuditLogCriteria query, CancellationToken ct = default)
        => Filtered(query).CountAsync(ct);

    public Task<List<AuditLogEntry>> FindPageAsync(AuditLogCriteria query, int skip, CancellationToken ct = default)
        => Filtered(query).OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Skip(skip).Take(query.PageSize).ToListAsync(ct);

    public async Task<AuditLogNames> FindNamesAsync(
        List<Guid> userIds, List<Guid> invitationIds, Guid[] entryIds, CancellationToken ct = default)
    {
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => new AuditLogPerson(u.FullName, u.Email), ct);
        var invitations = await db.MeetingGuestInvitations.AsNoTracking()
            .Where(i => invitationIds.Contains(i.Id))
            .Select(i => new { i.Id, i.InvitedName, i.Email })
            .ToDictionaryAsync(i => i.Id, i => new AuditLogPerson(i.InvitedName, i.Email), ct);
        var objects = (await db.Database
                .SqlQueryRaw<ObjectNameRow>(
                    NamesSql,
                    new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = entryIds })
                .ToListAsync(ct))
            .ToDictionary(n => n.Id, n => new AuditLogObject(n.Name, n.Secret));
        return new AuditLogNames(users, invitations, objects);
    }

    private sealed record ObjectNameRow(Guid Id, string? Name, bool Secret);

    /// <summary>Every criterion must hold.</summary>
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
        // The object (UC-12): its id and the name the list shows. Then the recorded values, at any depth: a key name
        // or a null never matches, so "reason" does not return every row that has a reason field. Nothing of a Mật
        // meeting matches by name: neither its object name nor the names its entry recorded.
        // ponytail: walks every row's details in the period; add a generated text column with a trigram index if the
        // log grows past what a sequential scan answers quickly.
        if (query.Search is { } search)
            Where($"({Matches("a.object_id::text")} OR (NOT {SecretSql} AND {Matches(ObjectNameSql)})"
                    + $" OR EXISTS (SELECT 1 FROM jsonb_path_query(CASE WHEN {SecretSql} THEN a.details - {SecretNameKeysSql}"
                    + " ELSE a.details END, '$.**') v "
                    + $"WHERE jsonb_typeof(v) IN ('string', 'number', 'boolean') AND {Matches("v #>> ARRAY[]::text[]")}))",
                "q", search, NpgsqlDbType.Text);

        return db.AuditLog.FromSqlRaw(sql.ToString(), parameters.ToArray()).AsNoTracking();
    }
}
