using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Infrastructure.Participants;

public sealed partial class ParticipantService(
    TrustCabinetDbContext db,
    IParticipantRepository repository,
    IParticipantGuestAccess guestAccess,
    IMeetingParticipantAccessPublisher accessPublisher,
    ICallerContext caller,
    TimeProvider clock,
    ILogger<ParticipantService> logger,
    IParticipantsChangePublisher publisher
) : IParticipantService
{
    private static bool Persistence(Exception ex) =>
        ex is DbUpdateException or NpgsqlException or TimeoutException;

    private static string Revision(Meeting m) =>
        m.ParticipantsRevision.ToString(CultureInfo.InvariantCulture);

    private static ParticipantResult Invalid(
        string field,
        string message,
        ParticipantFailure failure = ParticipantFailure.Invalid
    ) => new(failure, Errors: new Dictionary<string, string[]> { [field] = [message] });

    private static MeetingParticipant? Own(ParticipantSnapshot s, ParticipantActor actor) =>
        actor.Guest is null
            ? s.Meeting.Participants.SingleOrDefault(p =>
                p.UserId == actor.UserId && ParticipantRules.Active(p)
            )
            : null;

    private static ParticipantPermissions Permissions(ParticipantSnapshot s, ParticipantActor actor)
    {
        var own = Own(s, actor);
        var id = actor.UserId ?? Guid.Empty;
        return new(
            ParticipantRules.IsManager(s.Meeting, id)
                && ParticipantRules.Editable(s.Meeting.Status),
            ParticipantRules.CanReplaceChair(s.Meeting, id),
            (own is not null || actor.Guest is not null)
                && ParticipantRules.Respondable(s.Meeting.Status)
        );
    }

    private async Task<ParticipantFailure> AccessAsync(
        ParticipantSnapshot? s,
        ParticipantActor actor,
        CancellationToken ct
    )
    {
        if (actor.Guest is { } guest)
        {
            if (
                actor.UserId is not null
                || !guest.IsValid
                || s is null
                || guest.MeetingId != s.Meeting.Id
                || !ParticipantRules.Published(s.Meeting.Status)
                || !s.Guests.Any(g => g.Id == guest.InvitationId)
                || !await guestAccess.CanAccessAsync(guest, ct)
            )
                return ParticipantFailure.NotFound;
            return ParticipantFailure.None;
        }
        if (
            actor.UserId is not { } id
            || !await db.Users.AnyAsync(u => u.Id == id && u.IsActive, ct)
        )
            return ParticipantFailure.Inactive;
        return s is not null && MeetingReadPolicy.CanRead(s.Meeting, id)
            ? ParticipantFailure.None
            : ParticipantFailure.NotFound;
    }

    public async Task<ParticipantResult> ReadAsync(
        Guid meetingId,
        ParticipantActor actor,
        ParticipantOperation operation,
        ParticipantQuery query,
        CancellationToken ct
    )
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(
                IsolationLevel.RepeatableRead,
                ct
            );
            var s = await repository.LoadAsync(meetingId, false, ct);
            var failure = await AccessAsync(s, actor, ct);
            if (failure != ParticipantFailure.None)
                return new(failure);
            var snapshot = s!;
            var m = snapshot.Meeting;
            var permissions = Permissions(snapshot, actor);
            if (operation == ParticipantOperation.Options && !permissions.CanManageParticipants)
                return new(
                    actor.UserId is { } user && ParticipantRules.IsManager(m, user)
                        ? ParticipantFailure.Locked
                        : ParticipantFailure.Forbidden
                );
            if (
                query.Invalid
                || query.Page < 1
                || query.PageSize is < 1 or > 100
                || query.Search?.Length > 200
            )
                return new(ParticipantFailure.InvalidRequest);
            if (operation == ParticipantOperation.Context)
                return new(
                    Data: new ParticipantContext(
                        meetingId,
                        m.Status.ToString(),
                        Revision(m),
                        actor.Guest is not null ? "Guest" : Own(snapshot, actor)?.Role.ToString(),
                        permissions
                    ),
                    Revision: m.ParticipantsRevision
                );
            if (operation == ParticipantOperation.OwnResponse)
            {
                if (!ParticipantRules.Published(m.Status))
                    return new(ParticipantFailure.NotFound);
                var own = Own(snapshot, actor);
                var guest = actor.Guest is { } g
                    ? snapshot.Guests.Single(x => x.Id == g.InvitationId)
                    : null;
                return own is not null
                        ? new(
                            Data: new InvitationResponseView(
                                own.Id,
                                "Internal",
                                own.Response.ToString(),
                                own.RespondedAt,
                                permissions.CanRespond
                            )
                        )
                    : guest is not null
                        ? new(
                            Data: new InvitationResponseView(
                                guest.Id,
                                "Guest",
                                guest.Response.ToString(),
                                guest.RespondedAt,
                                permissions.CanRespond
                            )
                        )
                    : new(ParticipantFailure.NotFound);
            }
            if (operation == ParticipantOperation.Options)
                return await OptionsAsync(snapshot, query, ct);
            var profiles = await guestAccess.GetProfilesAsync(
                meetingId,
                snapshot.Guests.Select(g => g.Id).ToArray(),
                ct
            );
            var internalRows = m
                .Participants.Where(ParticipantRules.Current)
                .Select(p => new ParticipantRow(
                    p.Id,
                    "Internal",
                    p.User.FullName,
                    p.User.Email,
                    p.Role.ToString(),
                    p.UserId,
                    p.User.IsActive,
                    null,
                    null,
                    null,
                    p.Response.ToString(),
                    p.RespondedAt
                ));
            var guestRows = snapshot.Guests.Select(g =>
            {
                profiles.TryGetValue(g.Id, out var profile);
                return new ParticipantRow(
                    g.Id,
                    "Guest",
                    profile?.DisplayName ?? g.InvitedName,
                    g.Email,
                    null,
                    null,
                    null,
                    g.InvitedName,
                    profile?.DisplayName,
                    profile?.JobTitle,
                    g.Response.ToString(),
                    g.RespondedAt
                );
            });
            var rows = internalRows
                .Concat(guestRows)
                .OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Kind)
                .ThenBy(r => r.Id)
                .ToArray();
            var offset = (long)(query.Page - 1) * query.PageSize;
            var totals = new ResponseTotals(
                rows.Count(r => r.Response == "Pending"),
                rows.Count(r => r.Response == "Accepted"),
                rows.Count(r => r.Response == "Declined"),
                rows.Length
            );
            var search = query.Search?.Trim();
            if (!string.IsNullOrEmpty(search))
                rows = rows.Where(r =>
                        r.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || r.Email.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || (
                            r.InvitedName?.Contains(search, StringComparison.OrdinalIgnoreCase)
                            ?? false
                        )
                    )
                    .ToArray();
            return new(
                Data: new ParticipantPage(
                    offset >= rows.Length
                        ? []
                        : rows.Skip((int)offset).Take(query.PageSize).ToArray(),
                    rows.Length,
                    query.Page,
                    query.PageSize,
                    Revision(m),
                    totals
                ),
                Revision: m.ParticipantsRevision
            );
        }
        catch (Exception ex) when (Persistence(ex))
        {
            logger.LogError(ex, "Participant read failed for {MeetingId}", meetingId);
            return new(ParticipantFailure.ReadFailed);
        }
    }

    private async Task<ParticipantResult> OptionsAsync(
        ParticipantSnapshot s,
        ParticipantQuery query,
        CancellationToken ct
    )
    {
        var current = s.Meeting.Participants.Where(ParticipantRules.Current).ToArray();
        var excluded = current.Select(p => p.UserId).ToArray();
        var users = db
            .Users.AsNoTracking()
            .Where(u =>
                u.IsActive
                && !excluded.Contains(u.Id)
                && !db.MeetingGuestInvitations.Any(g =>
                    g.MeetingId == s.Meeting.Id && g.RevokedAt == null && g.Email == u.Email
                )
            );
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern =
                "%"
                + query
                    .Search.Trim()
                    .Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("%", "\\%", StringComparison.Ordinal)
                    .Replace("_", "\\_", StringComparison.Ordinal)
                + "%";
            users = users.Where(u =>
                EF.Functions.ILike(u.FullName, pattern, "\\")
                || EF.Functions.ILike(u.Email, pattern, "\\")
            );
        }
        var total = await users.CountAsync(ct);
        var offset = (long)(query.Page - 1) * query.PageSize;
        var items =
            offset >= total
                ? []
                : await users
                    .OrderBy(u => u.FullName)
                    .ThenBy(u => u.Id)
                    .Skip((int)offset)
                    .Take(query.PageSize)
                    .Select(u => new ParticipantOption(u.Id, u.FullName, u.Email))
                    .ToArrayAsync(ct);
        return new(
            Data: new ParticipantOptions(
                items,
                total,
                query.Page,
                query.PageSize,
                ["Chair", "Secretary", "Member"],
                Revision(s.Meeting)
            ),
            Revision: s.Meeting.ParticipantsRevision
        );
    }

    public async Task<ParticipantResult> MutateAsync(
        Guid meetingId,
        Guid? targetId,
        ParticipantActor actor,
        ParticipantOperation operation,
        ParticipantInput input,
        CancellationToken ct
    )
    {
        MeetingAccessChange? accessChange = null;
        ParticipantResult result;
        ParticipantsChange? changeEvent = null;
        HashSet<Guid> readers = [];
        HashSet<MeetingGuestSession> guestReaders = [];
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                ct
            );
            var s = await repository.LoadAsync(meetingId, true, ct);
            if (actor.UserId is { } actorId)
                await db
                    .Users.FromSqlInterpolated(
                        $"SELECT * FROM users WHERE id = {actorId} FOR SHARE"
                    )
                    .AsNoTracking()
                    .ToArrayAsync(ct);
            var access = await AccessAsync(s, actor, ct);
            if (access != ParticipantFailure.None)
                return new(access);
            var snapshot = s!;
            var m = snapshot.Meeting;
            var response = operation == ParticipantOperation.Respond;
            if (response)
            {
                if (Own(snapshot, actor) is null && actor.Guest is null)
                    return new(ParticipantFailure.NotFound);
                if (!ParticipantRules.Respondable(m.Status))
                    return new(ParticipantFailure.CannotRespond);
            }
            else
            {
                if (actor.Guest is not null || actor.UserId is not { } id)
                    return new(ParticipantFailure.Forbidden);
                var authorized = ParticipantRules.IsManager(m, id);
                if (!authorized)
                    return new(ParticipantFailure.Forbidden);
                if (!ParticipantRules.Editable(m.Status))
                    return new(ParticipantFailure.Locked);
            }
            if (input.ParseFailure != ParticipantFailure.None)
                return new(input.ParseFailure);
            if (!response && input.Revision is null)
                return new(ParticipantFailure.InvalidRequest);
            if (!response && input.Revision != m.ParticipantsRevision)
                return new(ParticipantFailure.Stale);
            var now = AgendaExecution.UtcMicroseconds(clock.GetUtcNow());
            var before = Evidence(snapshot);
            readers.UnionWith(await ReadersAsync(m, ct));
            guestReaders.UnionWith(await GuestReadersAsync(m, ct));
            result = operation switch
            {
                ParticipantOperation.AddInternal => await AddInternalAsync(
                    snapshot,
                    input,
                    now,
                    ct
                ),
                ParticipantOperation.AddGuest => await AddGuestAsync(
                    snapshot,
                    actor.UserId!.Value,
                    input,
                    now,
                    ct
                ),
                ParticipantOperation.ChangeAssignments => await ChangeAsync(
                    snapshot,
                    actor.UserId!.Value,
                    input,
                    now,
                    ct
                ),
                ParticipantOperation.RemoveInternal or ParticipantOperation.RevokeGuest => Remove(
                    snapshot,
                    targetId,
                    actor.UserId!.Value,
                    operation,
                    now
                ),
                ParticipantOperation.Respond => Respond(snapshot, actor, input, now),
                _ => new(ParticipantFailure.InvalidRequest),
            };
            if (
                result.Failure != ParticipantFailure.None
                || result.Success == ParticipantSuccess.NoChanges
            )
                return result with { Revision = response ? null : m.ParticipantsRevision };
            if (!response)
                m.ParticipantsRevision = checked(m.ParticipantsRevision + 1);
            var after = Evidence(snapshot);
            var changedIds = before
                .Keys.Union(after.Keys)
                .Where(id => before.GetValueOrDefault(id) != after.GetValueOrDefault(id))
                .ToArray();
            db.AuditLog.Add(
                new AuditLogEntry
                {
                    ActorId = actor.UserId,
                    Action = "meeting.participants." + operation,
                    ObjectType = "Meeting",
                    ObjectId = meetingId,
                    IpAddress = caller.IpAddress,
                    Details = JsonSerializer.Serialize(
                        new
                        {
                            guest = actor.Guest,
                            changes = changedIds.Select(id => new
                            {
                                id,
                                before = before.TryGetValue(id, out var old)
                                    ? JsonSerializer.Deserialize<JsonElement>(old)
                                    : (JsonElement?)null,
                                after = after.TryGetValue(id, out var current)
                                    ? JsonSerializer.Deserialize<JsonElement>(current)
                                    : (JsonElement?)null,
                            }),
                        }
                    ),
                    OccurredAt = now,
                    CreatedAt = now,
                    UpdatedAt = now,
                }
            );
            await db.SaveChangesAsync(ct);
            readers.UnionWith(await ReadersAsync(m, ct));
            guestReaders.UnionWith(await GuestReadersAsync(m, ct));
            await tx.CommitAsync(ct);
            changeEvent = new(Guid.CreateVersion7(), meetingId, Revision(m), now);
            if (
                operation is ParticipantOperation.RemoveInternal or ParticipantOperation.RevokeGuest
            )
                accessChange = new(
                    meetingId,
                    operation == ParticipantOperation.RevokeGuest
                        ? targetId!.Value
                        : snapshot.Meeting.Participants.Single(p => p.Id == targetId).UserId,
                    operation == ParticipantOperation.RevokeGuest,
                    Guid.CreateVersion7()
                );
            result = result with { Revision = response ? null : m.ParticipantsRevision };
        }
        catch (Exception ex) when (Persistence(ex))
        {
            db.ChangeTracker.Clear();
            logger.LogError(
                ex,
                "Participant mutation {Operation} failed for {MeetingId}",
                operation,
                meetingId
            );
            return new(ParticipantFailure.SaveFailed);
        }
        if (changeEvent is not null)
        {
            try
            {
                await publisher.PublishAsync(changeEvent, readers, guestReaders, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Participant event transport failed after commit for {MeetingId}",
                    meetingId
                );
            }
        }
        if (accessChange is not null)
        {
            try
            {
                await accessPublisher.PublishAsync(accessChange, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Access transport update failed after commit for {MeetingId}",
                    meetingId
                );
            }
        }
        return result;
    }

    private async Task<Guid[]> ReadersAsync(Meeting meeting, CancellationToken ct)
    {
        var candidates = meeting
            .Participants.Select(p => p.UserId)
            .Append(meeting.CreatedById)
            .Concat(meeting.AssignedApproverId is { } approver ? [approver] : [])
            .Distinct()
            .Where(id => MeetingReadPolicy.CanRead(meeting, id))
            .ToArray();
        return await db
            .Users.AsNoTracking()
            .Where(u => candidates.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id)
            .ToArrayAsync(ct);
    }

    private async Task<IReadOnlyList<MeetingGuestSession>> GuestReadersAsync(
        Meeting meeting,
        CancellationToken ct
    ) =>
        ParticipantRules.Published(meeting.Status)
            ? (await guestAccess.ListReadersAsync(meeting.Id, ct))
                .Where(g => g.IsValid && g.MeetingId == meeting.Id)
                .ToArray()
            : [];

    private Dictionary<Guid, string> Evidence(ParticipantSnapshot s)
    {
        var rows = s.Meeting.Participants.ToDictionary(
            p => p.Id,
            p =>
                JsonSerializer.Serialize(
                    new
                    {
                        p.Id,
                        p.UserId,
                        p.Role,
                        p.AssignmentStatus,
                        p.Response,
                        p.RespondedAt,
                        p.Attendance,
                        p.AttendanceRecordedById,
                        p.AttendanceRecordedAt,
                        p.RemovedAt,
                    }
                )
        );
        foreach (var g in db.MeetingGuestInvitations.Local.Where(g => g.MeetingId == s.Meeting.Id))
            rows[g.Id] = JsonSerializer.Serialize(
                new
                {
                    g.Id,
                    g.InvitedName,
                    g.Email,
                    g.InvitedById,
                    g.SensitivityAtInvitation,
                    g.Response,
                    g.RespondedAt,
                    g.RevokedAt,
                    g.Attendance,
                }
            );
        return rows;
    }

    private async Task<ParticipantResult> AddInternalAsync(
        ParticipantSnapshot s,
        ParticipantInput input,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var ids = input.UserIds ?? [];
        if (
            ids.Length is < 1 or > 100
            || ids.Contains(Guid.Empty)
            || ids.Distinct().Count() != ids.Length
        )
            return Invalid("userIds", "Chọn từ 1 đến 100 người dùng hợp lệ, không trùng nhau.");
        if (!ParticipantRules.TryRole(input.Role, out var role))
            return Invalid("role", "Chọn Chủ tọa, Thư ký hoặc Thành viên.");
        // Locks prevent a selected account from changing email/active state between validation and save.
        var users = await db
            .Users.FromSqlInterpolated(
                $"SELECT * FROM users WHERE id = ANY({ids.Order().ToArray()}) ORDER BY id FOR SHARE"
            )
            .AsNoTracking()
            .ToArrayAsync(ct);
        if (users.Length != ids.Length || users.Any(u => !u.IsActive))
            return Invalid("userIds", "Người được chọn không tồn tại hoặc đã ngừng hoạt động.");
        var existing = s
            .Meeting.Participants.Where(ParticipantRules.Current)
            .Where(p => ids.Contains(p.UserId))
            .ToArray();
        if (existing.Length > 0)
            return Invalid("userIds", "Người được chọn đã có trong danh sách thành phần.");
        var emails = users.Select(u => u.Email).ToArray();
        if (
            await db.MeetingGuestInvitations.AnyAsync(
                g => g.MeetingId == s.Meeting.Id && g.RevokedAt == null && emails.Contains(g.Email),
                ct
            )
        )
            return Invalid("userIds", "Người được chọn đã được mời với tư cách Khách.");
        if (
            !ParticipantRules.ValidRoles(
                s.Meeting.Status,
                s.Meeting.Participants.Where(ParticipantRules.Current)
                    .Select(p => p.Role)
                    .Concat(ids.Select(_ => role))
            )
        )
            return new(ParticipantFailure.RequiredRoles);
        var created = new List<Guid>();
        var trackedUsers = db.Users.Local.ToDictionary(u => u.Id);
        var detectChanges = db.ChangeTracker.AutoDetectChangesEnabled;
        // Batch inserts do not need a full graph scan for every outbox payload or user lookup.
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var user in users)
            {
                var p = new MeetingParticipant
                {
                    MeetingId = s.Meeting.Id,
                    UserId = user.Id,
                    Role = role,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                p.InitialInvitationDeliveryKey = $"participant:{p.Id}:invited";
                db.MeetingParticipants.Add(p);
                if (!s.Meeting.Participants.Contains(p))
                    s.Meeting.Participants.Add(p);
                // Use the tracked instance already loaded by EF, or attach the account without inserting it.
                p.User = trackedUsers.GetValueOrDefault(user.Id) ?? user;
                if (db.Entry(p.User).State == EntityState.Detached)
                    db.Attach(p.User);
                Stage(s.Meeting, p, null, InvitationEventKind.Invitation, now);
                created.Add(p.Id);
            }
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = detectChanges;
        }
        return new(Data: new { createdIds = created }, Success: ParticipantSuccess.Created);
    }

    private async Task<ParticipantResult> AddGuestAsync(
        ParticipantSnapshot s,
        Guid actor,
        ParticipantInput input,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var name = input.InvitedName?.Trim() ?? "";
        var email = input.Email?.Trim() ?? "";
        if (name.Length is < 1 or > 200)
            return Invalid("invitedName", "Nhập tên khách từ 1 đến 200 ký tự.");
        if (email.Length is < 1 or > 256 || !EmailPattern().IsMatch(email))
            return Invalid("email", "Nhập email hợp lệ, tối đa 256 ký tự.");
        var userIds = s
            .Meeting.Participants.Where(ParticipantRules.Current)
            .Select(p => p.UserId)
            .Order()
            .ToArray();
        await db
            .Users.FromSqlInterpolated(
                $"SELECT * FROM users WHERE id = ANY({userIds}) ORDER BY id FOR SHARE"
            )
            .AsNoTracking()
            .ToArrayAsync(ct);
        if (
            await db.MeetingGuestInvitations.AnyAsync(
                g => g.MeetingId == s.Meeting.Id && g.RevokedAt == null && g.Email == email,
                ct
            )
            || await db.MeetingParticipants.AnyAsync(
                p =>
                    p.MeetingId == s.Meeting.Id
                    && p.AssignmentStatus == AssignmentStatus.Current
                    && p.User.Email == email,
                ct
            )
        )
            return Invalid("email", "Email đã có trong danh sách thành phần hiện tại.");
        var guest = new MeetingGuestInvitation
        {
            MeetingId = s.Meeting.Id,
            InvitedName = name,
            Email = email,
            InvitedById = actor,
            SensitivityAtInvitation = s.Meeting.Sensitivity,
            CreatedAt = now,
            UpdatedAt = now,
        };
        guest.InitialInvitationDeliveryKey = $"guest:{guest.Id}:invited";
        db.MeetingGuestInvitations.Add(guest);
        Stage(s.Meeting, null, guest, InvitationEventKind.Invitation, now);
        return new(
            Data: new { createdIds = new[] { guest.Id } },
            Success: ParticipantSuccess.Created
        );
    }

    private async Task<ParticipantResult> ChangeAsync(
        ParticipantSnapshot s,
        Guid actor,
        ParticipantInput input,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var changes = input.Changes ?? [];
        if (
            changes.Length is < 1 or > 100
            || changes.Select(c => c.ParticipantId).Distinct().Count() != changes.Length
        )
            return new(ParticipantFailure.InvalidRole);
        var current = s
            .Meeting.Participants.Where(ParticipantRules.Current)
            .ToDictionary(p => p.Id);
        var oldChair = current.Values.SingleOrDefault(p => p.Role == MeetingRole.Chair);
        var live = ParticipantRules.Respondable(s.Meeting.Status);
        var touchesChair = changes.Any(c =>
            c.Role == nameof(MeetingRole.Chair) && c.ParticipantId != oldChair?.Id
            || c.ParticipantId == oldChair?.Id && c.Role != nameof(MeetingRole.Chair)
        );
        // Lock all current accounts in one order, including the outgoing Chair used for authorization.
        var userIds = current.Values.Select(p => p.UserId).Order().ToArray();
        var users = await db
            .Users.FromSqlInterpolated(
                $"SELECT * FROM users WHERE id = ANY({userIds}) ORDER BY id FOR SHARE"
            )
            .AsNoTracking()
            .ToDictionaryAsync(u => u.Id, ct);
        var actorParticipant = current.Values.FirstOrDefault(p => p.UserId == actor);
        if (actorParticipant is null)
            return new(ParticipantFailure.Forbidden);
        if (!users[actor].IsActive)
            return new(ParticipantFailure.Inactive);
        var canReplaceChair = actorParticipant.Role == MeetingRole.Chair
            || (actorParticipant.Role == MeetingRole.Secretary && oldChair is not null
                && !users[oldChair.UserId].IsActive);
        if (live && touchesChair && !canReplaceChair)
            return new(ParticipantFailure.ChairHandoverDenied);
        foreach (var c in changes)
        {
            if (!current.TryGetValue(c.ParticipantId, out var participant))
                return new(
                    live && c.Role == nameof(MeetingRole.Chair)
                        ? ParticipantFailure.InvalidChairRecipient
                        : ParticipantFailure.NotFound
                );
            if (
                live
                && c.Role == nameof(MeetingRole.Chair)
                && c.ParticipantId != oldChair?.Id
                && (!users[participant.UserId].IsActive || participant.Role == MeetingRole.Secretary)
            )
                return new(ParticipantFailure.InvalidChairRecipient);
        }
        var final = current.ToDictionary(p => p.Key, p => p.Value.Role);
        foreach (var c in changes)
        {
            if (!ParticipantRules.TryRole(c.Role, out var role))
                return new(ParticipantFailure.InvalidRole);
            final[c.ParticipantId] = role;
        }
        if (!ParticipantRules.ValidRoles(s.Meeting.Status, final.Values))
            return new(ParticipantFailure.RequiredRoles);
        var newChair = final.Single(p => p.Value == MeetingRole.Chair).Key;
        var replacing = oldChair is not null && oldChair.Id != newChair;
        if (
            replacing
            && (
                input.ChairReplacement is not { Confirmed: true } confirm
                || confirm.OutgoingParticipantId != oldChair!.Id
                || confirm.IncomingParticipantId != newChair
                || final[oldChair.Id] != MeetingRole.Member
            )
        )
            return new(ParticipantFailure.InvalidRole);
        if (!replacing && input.ChairReplacement is not null)
            return new(ParticipantFailure.InvalidRole);
        if (
            changes.Any(c =>
                final[c.ParticipantId] != current[c.ParticipantId].Role
                && !users[current[c.ParticipantId].UserId].IsActive
                && !(
                    replacing
                    && c.ParticipantId == oldChair!.Id
                    && final[c.ParticipantId] == MeetingRole.Member
                )
            )
        )
            return new(ParticipantFailure.InvalidRole);
        var affected = changes
            .Where(c => final[c.ParticipantId] != current[c.ParticipantId].Role)
            .ToArray();
        if (affected.Length == 0)
            return new(Success: ParticipantSuccess.NoChanges);
        var handover = live && replacing && oldChair!.UserId == actor;
        if (replacing)
        {
            oldChair!.Role = MeetingRole.Member;
            await db.SaveChangesAsync(ct);
        }
        foreach (var c in affected)
        {
            var p = current[c.ParticipantId];
            p.Role = final[p.Id];
            p.UpdatedAt = now;
            Stage(s.Meeting, p, null, InvitationEventKind.RoleChanged, now);
        }
        return new(
            Success: handover ? ParticipantSuccess.ChairHandedOver : ParticipantSuccess.RoleChanged
        );
    }

    private ParticipantResult Remove(
        ParticipantSnapshot s,
        Guid? id,
        Guid actor,
        ParticipantOperation op,
        DateTimeOffset now
    )
    {
        if (op == ParticipantOperation.RevokeGuest)
        {
            var g = s.Guests.SingleOrDefault(g => g.Id == id);
            if (g is null)
                return new(ParticipantFailure.NotFound);
            g.RevokedAt = now;
            g.RevokedById = actor;
            g.UpdatedAt = now;
            Stage(s.Meeting, null, g, InvitationEventKind.Removed, now);
        }
        else
        {
            var p = s.Meeting.Participants.SingleOrDefault(p =>
                p.Id == id && ParticipantRules.Current(p)
            );
            if (p is null)
                return new(ParticipantFailure.NotFound);
            if (
                !ParticipantRules.ValidRoles(
                    s.Meeting.Status,
                    s.Meeting.Participants.Where(x => ParticipantRules.Current(x) && x.Id != id)
                        .Select(x => x.Role)
                )
            )
                return new(ParticipantFailure.RequiredRoles);
            p.AssignmentStatus = AssignmentStatus.Removed;
            p.RemovedAt = now;
            p.RemovedById = actor;
            p.UpdatedAt = now;
            Stage(s.Meeting, p, null, InvitationEventKind.Removed, now);
        }
        return new(Success: ParticipantSuccess.Removed);
    }

    private static ParticipantResult Respond(
        ParticipantSnapshot s,
        ParticipantActor actor,
        ParticipantInput input,
        DateTimeOffset now
    )
    {
        if (input.Response is not ("Accepted" or "Declined"))
            return Invalid("response", "Chọn chấp nhận hoặc từ chối lời mời.");
        var value = Enum.Parse<InvitationResponse>(input.Response);
        var p = Own(s, actor);
        if (p is not null)
        {
            if (p.Response == value)
                return new(
                    Data: new InvitationResponseView(
                        p.Id,
                        "Internal",
                        p.Response.ToString(),
                        p.RespondedAt,
                        true
                    ),
                    Success: ParticipantSuccess.NoChanges
                );
            p.Response = value;
            p.RespondedAt = now;
            p.UpdatedAt = now;
            return new(
                Data: new InvitationResponseView(p.Id, "Internal", value.ToString(), now, true),
                Success: ParticipantSuccess.Responded
            );
        }
        var g = s.Guests.Single(x => x.Id == actor.Guest!.InvitationId);
        if (g.Response == value)
            return new(
                Data: new InvitationResponseView(
                    g.Id,
                    "Guest",
                    g.Response.ToString(),
                    g.RespondedAt,
                    true
                ),
                Success: ParticipantSuccess.NoChanges
            );
        g.Response = value;
        g.RespondedAt = now;
        g.UpdatedAt = now;
        return new(
            Data: new InvitationResponseView(g.Id, "Guest", value.ToString(), now, true),
            Success: ParticipantSuccess.Responded
        );
    }

    private void Stage(
        Meeting meeting,
        MeetingParticipant? p,
        MeetingGuestInvitation? g,
        InvitationEventKind kind,
        DateTimeOffset now
    )
    {
        if (ParticipantRules.Respondable(meeting.Status))
            MeetingInvitationEvents.Stage(db, meeting, p, g, kind, now, notify: true);
    }

    [GeneratedRegex(@"^[^\s@<>\r\n]+@[^\s@<>\r\n]+\.[^\s@<>\r\n]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
