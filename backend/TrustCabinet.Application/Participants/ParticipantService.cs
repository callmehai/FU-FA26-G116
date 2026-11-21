using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Domain;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Application.Audit;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Speaking;
using TrustCabinet.Application.Users;
using TrustCabinet.Domain.GuestAccess;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Participants;

public sealed partial class ParticipantService(
    IParticipantRepository repository,
    IMeetingRepository meetings,
    IParticipantGuestAccess guestAccess,
    IMeetingInvitationStager stager,
    ISpeakingRequestCloser speaking,
    IAuditLogRepository audit,
    IUnitOfWork unitOfWork,
    IMeetingParticipantAccessPublisher accessPublisher,
    ICallerContext caller,
    TimeProvider clock,
    ILogger<ParticipantService> logger,
    IParticipantsChangePublisher publisher,
    IAgendaChangePublisher agendaPublisher,
    ICallerAccountQuery accounts
) : IParticipantService
{
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
            own is not null
                ? ParticipantRules.CanRespond(s.Meeting.Status, own)
                : actor.Guest is { } guest
                    && ParticipantRules.Respondable(s.Meeting.Status)
                    && s.Guests.SingleOrDefault(g => g.Id == guest.InvitationId)?.Response
                        == InvitationResponse.Pending
        );
    }

    private static ParticipantFailure HandoverAccess(ParticipantSnapshot s, ParticipantActor actor) =>
        actor.Guest is not null
            ? ParticipantFailure.Forbidden
            : Own(s, actor)?.Role == MeetingRole.Chair && ParticipantRules.Respondable(s.Meeting.Status)
                ? ParticipantFailure.None
                : ParticipantFailure.CannotHandOverChair;

    private static bool EligibleChairRecipient(MeetingParticipant participant, bool active) =>
        ParticipantRules.Current(participant) && participant.Role == MeetingRole.Member && active;

    private static ParticipantResult ChairCandidates(ParticipantSnapshot s, ParticipantActor actor, ParticipantQuery query)
    {
        var eligible = s.Meeting.Participants
            .Where(p => p.UserId != actor.UserId && EligibleChairRecipient(p, p.User.IsActive))
            .Select(p => new ChairHandoverCandidate(p.Id, p.UserId, p.User.FullName, p.User.Email))
            .ToArray();
        // Without marks or case, like every search box: "nguyen" finds "Nguyễn".
        var search = query.Search?.Trim() is { Length: > 0 } typed ? SearchText.Fold(typed) : null;
        var matches = eligible.Where(p => search is null
                || SearchText.Contains(p.FullName, search)
                || SearchText.Contains(p.Email, search))
            .OrderBy(p => p.FullName, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.ParticipantId).ToArray();
        var offset = (long)(query.Page - 1) * query.PageSize;
        return new(Data: new ChairHandoverCandidatePage(
            offset >= matches.Length ? [] : matches.Skip((int)offset).Take(query.PageSize).ToArray(),
            matches.Length, eligible.Length, query.Page, query.PageSize, Revision(s.Meeting)),
            Revision: s.Meeting.ParticipantsRevision);
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
                || !GuestAccessRules.MeetingOpen(s.Meeting.Status)
                || !s.Guests.Any(g => g.Id == guest.InvitationId)
                || !await guestAccess.CanAccessAsync(guest, ct)
            )
                return ParticipantFailure.NotFound;
            return ParticipantFailure.None;
        }
        if (
            actor.UserId is not { } id
            || !await meetings.IsUserActiveAsync(id, ct)
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
            return await unitOfWork.ExecuteAsync(async token =>
            {
                var s = await repository.LoadAsync(meetingId, false, token);
                var failure = await AccessAsync(s, actor, token);
                if (failure != ParticipantFailure.None)
                    return new ParticipantResult(failure);
                var snapshot = s!;
                var m = snapshot.Meeting;
                var permissions = Permissions(snapshot, actor);
                if (operation == ParticipantOperation.ChairHandoverCandidates)
                {
                    var handoverAccess = HandoverAccess(snapshot, actor);
                    if (handoverAccess != ParticipantFailure.None)
                        return new(handoverAccess);
                }
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
                    return await OptionsAsync(snapshot, query, token);
                if (operation == ParticipantOperation.ChairHandoverCandidates)
                    return ChairCandidates(snapshot, actor, query);
                var profiles = await guestAccess.GetProfilesAsync(
                    meetingId,
                    snapshot.Guests.Select(g => g.Id).ToArray(),
                    token
                );
                // A Guest sees names, not addresses: only their own email reaches them (PO 01/11/2026, SCRUM-116).
                var viewerGuest = actor.Guest?.InvitationId;
                // Whose system role may record proxy samples: told only to the people who manage the roster.
                var collectorRoles = new HashSet<Guid>();
                if (viewerGuest is null)
                    foreach (var roleId in m.Participants.Where(ParticipantRules.Current).Select(p => p.User.RoleId).Distinct())
                        if ((await accounts.GetCapabilitiesAsync(roleId, token)).Contains(Capability.VoiceCollector))
                            collectorRoles.Add(roleId);
                var internalRows = m
                    .Participants.Where(ParticipantRules.Current)
                    .Select(p => new ParticipantRow(
                        p.Id,
                        "Internal",
                        p.User.FullName,
                        viewerGuest is null ? p.User.Email : null,
                        p.Role.ToString(),
                        p.UserId,
                        p.User.IsActive,
                        null,
                        null,
                        null,
                        p.Response.ToString(),
                        p.RespondedAt,
                        viewerGuest is null ? collectorRoles.Contains(p.User.RoleId) : null
                    ));
                var guestRows = snapshot.Guests.Select(g =>
                {
                    profiles.TryGetValue(g.Id, out var profile);
                    return new ParticipantRow(
                        g.Id,
                        "Guest",
                        profile?.DisplayName ?? g.InvitedName,
                        viewerGuest is null || viewerGuest == g.Id ? g.Email : null,
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
                if (query.Search?.Trim() is { Length: > 0 } typed)
                {
                    // Without marks or case, like every search box: "nguyen" finds "Nguyễn".
                    var search = SearchText.Fold(typed);
                    rows = rows.Where(r =>
                            SearchText.Contains(r.DisplayName, search)
                            || (viewerGuest is null && SearchText.Contains(r.Email, search))
                            || SearchText.Contains(r.InvitedName, search)
                        )
                        .ToArray();
                }
                return new(
                    Data: new ParticipantPage(
                        offset >= rows.Length
                            ? []
                            : rows.Skip((int)offset).Take(query.PageSize).ToArray(),
                        rows.Length,
                        query.Page,
                        query.PageSize,
                        Revision(m)
                    ),
                    Revision: m.ParticipantsRevision
                    );
            }, TransactionMode.RepeatableRead, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Participant read failed for {MeetingId}", meetingId);
            return new(ParticipantFailure.ReadFailed);
        }
    }

    public async Task<ParticipantResult> ReadCreationOptionsAsync(
        ParticipantQuery query,
        CancellationToken ct
    )
    {
        if (
            query.Invalid
            || query.Page < 1
            || query.PageSize is < 1 or > 100
            || query.Search?.Length > 200
        )
            return new(ParticipantFailure.InvalidRequest);
        try
        {
            return await unitOfWork.ExecuteAsync(
                token => OptionsAsync(null, query, token), TransactionMode.RepeatableRead, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Participant creation options read failed");
            return new(ParticipantFailure.ReadFailed);
        }
    }

    private async Task<ParticipantResult> OptionsAsync(
        ParticipantSnapshot? s,
        ParticipantQuery query,
        CancellationToken ct
    )
    {
        var (total, items) = await repository.ListInvitationOptionsAsync(s, query, ct);
        if (s is null)
            return new(
                Data: new ParticipantCreationOptions(
                    items,
                    total,
                    query.Page,
                    query.PageSize,
                    ["Chair", "Secretary", "Member"]
                )
            );
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
        AgendaChange? agendaChange = null;
        ParticipantResult result;
        ParticipantsChange? changeEvent = null;
        HashSet<Guid> readers = [];
        HashSet<MeetingGuestSession> guestReaders = [];
        try
        {
            result = await unitOfWork.ExecuteAsync(async token =>
            {
                var s = await repository.LoadAsync(meetingId, true, token);
                if (actor.UserId is { } actorId)
                    await repository.LockUserAsync(actorId, token);
                var access = await AccessAsync(s, actor, token);
                if (access != ParticipantFailure.None)
                    return new ParticipantResult(access);
                var snapshot = s!;
                var m = snapshot.Meeting;
                var response = operation == ParticipantOperation.Respond;
                Dictionary<Guid, User>? lockedUsers = null;
                if (response)
                {
                    if (Own(snapshot, actor) is null && actor.Guest is null)
                        return new ParticipantResult(ParticipantFailure.NotFound);
                    if (!ParticipantRules.Respondable(m.Status))
                        return new ParticipantResult(ParticipantFailure.CannotRespond);
                }
                else if (operation == ParticipantOperation.HandOverChair)
                {
                    var handoverAccess = HandoverAccess(snapshot, actor);
                    if (handoverAccess != ParticipantFailure.None)
                        return new ParticipantResult(handoverAccess);
                }
                else
                {
                    if (actor.Guest is not null || actor.UserId is not { } id)
                        return new ParticipantResult(ParticipantFailure.Forbidden);
                    var authorized = ParticipantRules.IsManager(m, id);
                    if (!authorized)
                        return new ParticipantResult(ParticipantFailure.Forbidden);
                    if (!ParticipantRules.Editable(m.Status))
                        return new ParticipantResult(ParticipantFailure.Locked);
                }
                if (input.ParseFailure != ParticipantFailure.None)
                    return new ParticipantResult(input.ParseFailure);
                if (!response && input.Revision is null)
                    return new ParticipantResult(ParticipantFailure.InvalidRequest);
                if (operation == ParticipantOperation.HandOverChair)
                {
                    if (input.NewChairParticipantId is null || input.NewChairParticipantId == Guid.Empty || !input.Confirmed)
                        return new ParticipantResult(ParticipantFailure.InvalidRole);
                    lockedUsers = await LockCurrentUsersAsync(snapshot, token);
                    var recipient = m.Participants.SingleOrDefault(p => p.Id == input.NewChairParticipantId);
                    if (recipient is null || recipient.UserId == actor.UserId
                        || !EligibleChairRecipient(recipient, lockedUsers.TryGetValue(recipient.UserId, out var user) && user.IsActive))
                        return new ParticipantResult(ParticipantFailure.InvalidChairRecipient);
                    var outgoing = Own(snapshot, actor)!;
                    input = input with
                    {
                        Changes = [new(outgoing.Id, nameof(MeetingRole.Member)), new(recipient.Id, nameof(MeetingRole.Chair))],
                        ChairReplacement = new(outgoing.Id, recipient.Id, true),
                    };
                }
                if (!response && input.Revision != m.ParticipantsRevision)
                    return new ParticipantResult(ParticipantFailure.Stale);
                var now = AgendaExecution.UtcMicroseconds(clock.GetUtcNow());
                var before = await EvidenceAsync(snapshot, token);
                readers.UnionWith(await ReadersAsync(m, token));
                guestReaders.UnionWith(await GuestReadersAsync(m, token));
                result = operation switch
                {
                    ParticipantOperation.AddInternal => await AddInternalAsync(
                        snapshot,
                        actor.UserId!.Value,
                        input,
                        now,
                        token
                    ),
                    ParticipantOperation.AddGuest => await AddGuestAsync(
                        snapshot,
                        actor.UserId!.Value,
                        input,
                        now,
                        token
                    ),
                    ParticipantOperation.ChangeAssignments or ParticipantOperation.HandOverChair => await ChangeAsync(
                        snapshot,
                        actor.UserId!.Value,
                        input,
                        now,
                        token,
                        lockedUsers
                    ),
                    ParticipantOperation.RemoveInternal or ParticipantOperation.RevokeGuest => Remove(
                        snapshot,
                        targetId,
                        actor.UserId!.Value,
                        operation,
                        now
                    ),
                    ParticipantOperation.Respond => Respond(snapshot, actor, input, now),
                    _ => new ParticipantResult(ParticipantFailure.InvalidRequest),
                };
                if (
                    result.Failure != ParticipantFailure.None
                    || result.Success == ParticipantSuccess.NoChanges
                )
                    return result with { Revision = response ? null : m.ParticipantsRevision };
                if (operation is ParticipantOperation.RemoveInternal or ParticipantOperation.RevokeGuest)
                {
                    var userId = operation == ParticipantOperation.RemoveInternal
                        ? snapshot.Meeting.Participants.Single(p => p.Id == targetId).UserId : (Guid?)null;
                    var affected = await repository.ListPresentedItemsAsync(meetingId, userId, targetId, token);
                    foreach (var item in affected)
                    {
                        item.PresenterId = null;
                        item.PresenterGuestInvitationId = null;
                        item.LegacyPresenterName = "";
                        item.UpdatedAt = now;
                    }
                    if (affected.Count > 0)
                        agendaChange = new(Guid.CreateVersion7(), meetingId, "PresenterCleared", null, null, now);
                    // Someone no longer in the meeting is not waiting to speak (BR-337). A member who keeps another
                    // assignment keeps the raised hand. Readers refetch the queue on participantsChanged.
                    if (userId is null || !snapshot.Meeting.Participants.Any(p => p.UserId == userId && p.Id != targetId
                            && ParticipantRules.Current(p)))
                        await speaking.CloseOpenAsync(meetingId, userId,
                            userId is null ? [targetId!.Value] : null, now, now, token);
                }
                if (!response)
                    m.ParticipantsRevision = checked(m.ParticipantsRevision + 1);
                var after = await EvidenceAsync(snapshot, token);
                var changedIds = before
                    .Keys.Union(after.Keys)
                    .Where(id => before.GetValueOrDefault(id) != after.GetValueOrDefault(id))
                    .ToArray();
                audit.Add(
                    new AuditLogEntry
                    {
                        ActorId = actor.UserId,
                        Action = AuditActions.MeetingParticipants(operation),
                        ObjectType = AuditActions.MeetingObjectType,
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
                await unitOfWork.SaveChangesAsync(token);
                readers.UnionWith(await ReadersAsync(m, token));
                guestReaders.UnionWith(await GuestReadersAsync(m, token));
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
                return result;
            }, TransactionMode.ReadCommitted, ct);
        }
        catch (PersistenceException ex)
        {
            unitOfWork.DiscardChanges();
            logger.LogError(
                ex.InnerException ?? ex,
                "Participant mutation {Operation} failed for {MeetingId}",
                operation,
                meetingId
            );
            return new(ParticipantFailure.SaveFailed);
        }
        if (agendaChange is not null)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await agendaPublisher.PublishAsync(agendaChange, timeout.Token); }
            catch (Exception ex) { logger.LogWarning(ex, "Agenda presenter cleanup committed; delivery failed for {MeetingId}", meetingId); }
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

    private async Task<IReadOnlyList<Guid>> ReadersAsync(Meeting meeting, CancellationToken ct)
    {
        var candidates = meeting
            .Participants.Select(p => p.UserId)
            .Append(meeting.CreatedById)
            .Concat(meeting.AssignedApproverId is { } approver ? [approver] : [])
            .Distinct()
            .Where(id => MeetingReadPolicy.CanRead(meeting, id))
            .ToArray();
        return await repository.ListActiveReaderIdsAsync(candidates, ct);
    }

    private async Task<IReadOnlyList<MeetingGuestSession>> GuestReadersAsync(
        Meeting meeting,
        CancellationToken ct
    ) =>
        GuestAccessRules.MeetingOpen(meeting.Status)
            ? (await guestAccess.ListReadersAsync(meeting.Id, ct))
                .Where(g => g.IsValid && g.MeetingId == meeting.Id)
                .ToArray()
            : [];

    private async Task<Dictionary<Guid, string>> EvidenceAsync(ParticipantSnapshot s, CancellationToken ct)
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
        foreach (var g in await repository.ListStagedGuestsAsync(s.Meeting.Id, ct))
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
        Guid actor,
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
            return Invalid("role", "Chọn Chủ toạ, Thư ký hoặc Thành viên.");
        // Locks prevent a selected account from changing email/active state between validation and save.
        var users = await repository.LockUsersAsync(ids.Order().ToArray(), ct);
        if (users.Count != ids.Length || users.Any(u => !u.IsActive))
            return Invalid("userIds", "Người được chọn không tồn tại hoặc đã ngừng hoạt động.");
        var existing = s
            .Meeting.Participants.Where(ParticipantRules.Current)
            .Where(p => ids.Contains(p.UserId))
            .ToArray();
        if (existing.Length > 0)
            return Invalid("userIds", "Người được chọn đã có trong danh sách thành phần.");
        var emails = users.Select(u => u.Email).ToArray();
        if (await repository.ExistsActiveGuestWithEmailsAsync(s.Meeting.Id, emails, ct))
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
        var staged = users.Select(user =>
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
            return (Participant: p, Account: user);
        }).ToArray();
        repository.AddMany(staged);
        var created = new List<Guid>();
        foreach (var (p, _) in staged)
        {
            if (!s.Meeting.Participants.Contains(p))
                s.Meeting.Participants.Add(p);
            Stage(s.Meeting, p, null, InvitationEventKind.Invitation, actor, now);
            created.Add(p.Id);
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
        await repository.LockUsersAsync(userIds, ct);
        if (
            await repository.ExistsActiveGuestWithEmailAsync(s.Meeting.Id, email, ct)
            || await repository.ExistsCurrentParticipantWithEmailAsync(s.Meeting.Id, email, ct)
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
        repository.Add(guest);
        // The voter list was fixed when the vote opened, for Guests as for everyone (BR-190; PO 08/11/2026 withdrew the
        // Guest exception of 30/10/2026): a new invitation never joins it. The meeting lock held here serializes with
        // opening and closing, so the vote cannot change state under this write.
        var openVote = await repository.FindOpenVoteAsync(s.Meeting.Id, ct);
        if (openVote is not null)
            await KeepOpenVotePlaceAsync(openVote, guest, now, ct);
        Stage(s.Meeting, null, guest, InvitationEventKind.Invitation, actor, now);
        return new(
            Data: new { createdIds = new[] { guest.Id } },
            Success: ParticipantSuccess.Created
        );
    }

    /// <summary>
    /// The one change an invitation makes to an Open vote's list: a Guest who was on it when the vote opened, then was
    /// removed and invited again under the same email, keeps that place under the new invitation id (SCRUM-133). The
    /// list is matched by email across this meeting's invitations (only revoked ones can match: the caller has already
    /// refused an email that is still invited). One who already voted keeps nothing to move. Nobody is added and the
    /// eligible count never changes (BR-190).
    /// </summary>
    private async Task KeepOpenVotePlaceAsync(
        VotingSession vote,
        MeetingGuestInvitation guest,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        if (await repository.ExistsVoteReceiptWithEmailAsync(vote.Id, guest.Email, ct))
            return;
        var earlier = await repository.FindEligibleVoterByEmailAsync(vote.Id, guest.Email, ct);
        if (earlier is null) return;
        earlier.GuestInvitation = guest;
        earlier.UpdatedAt = now;
    }

    private async Task<Dictionary<Guid, User>> LockCurrentUsersAsync(ParticipantSnapshot s, CancellationToken ct)
    {
        // Fresh untracked rows are authoritative for account state; never overwrite tracked users.
        var userIds = s.Meeting.Participants.Where(ParticipantRules.Current).Select(p => p.UserId).Distinct().Order().ToArray();
        return (await repository.LockUsersAsync(userIds, ct)).ToDictionary(u => u.Id);
    }

    private async Task<ParticipantResult> ChangeAsync(
        ParticipantSnapshot s,
        Guid actor,
        ParticipantInput input,
        DateTimeOffset now,
        CancellationToken ct,
        Dictionary<Guid, User>? lockedUsers = null
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
        var users = lockedUsers ?? await LockCurrentUsersAsync(s, ct);
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
                && !EligibleChairRecipient(participant, users[participant.UserId].IsActive)
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
            await unitOfWork.SaveChangesAsync(ct);
        }
        foreach (var c in affected)
        {
            var p = current[c.ParticipantId];
            p.Role = final[p.Id];
            p.UpdatedAt = now;
            Stage(s.Meeting, p, null, InvitationEventKind.RoleChanged, actor, now);
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
            Stage(s.Meeting, null, g, InvitationEventKind.Removed, actor, now);
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
            Stage(s.Meeting, p, null, InvitationEventKind.Removed, actor, now);
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
            if (p.Role == MeetingRole.Chair)
                return new(ParticipantFailure.ChairCannotRespond);
            if (p.Response == value)
                return new(
                    Data: new InvitationResponseView(
                        p.Id,
                        "Internal",
                        p.Response.ToString(),
                        p.RespondedAt,
                        Permissions(s, actor).CanRespond
                    ),
                    Success: ParticipantSuccess.NoChanges
                );
            // The first answer is final, as in AI-eCabinet.
            if (p.Response != InvitationResponse.Pending)
                return new(ParticipantFailure.ResponseFinal);
            p.Response = value;
            p.RespondedAt = now;
            p.UpdatedAt = now;
            return new(
                Data: new InvitationResponseView(p.Id, "Internal", value.ToString(), now, Permissions(s, actor).CanRespond),
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
                    Permissions(s, actor).CanRespond
                ),
                Success: ParticipantSuccess.NoChanges
            );
        if (g.Response != InvitationResponse.Pending)
            return new(ParticipantFailure.ResponseFinal);
        g.Response = value;
        g.RespondedAt = now;
        g.UpdatedAt = now;
        return new(
            Data: new InvitationResponseView(g.Id, "Guest", value.ToString(), now, Permissions(s, actor).CanRespond),
            Success: ParticipantSuccess.Responded
        );
    }

    private void Stage(
        Meeting meeting,
        MeetingParticipant? p,
        MeetingGuestInvitation? g,
        InvitationEventKind kind,
        Guid actor,
        DateTimeOffset now
    )
    {
        // Nobody is told in-app about their own action, e.g. the Chair who handed over the role
        // and so became a Member. The email still goes out: it keeps their calendar in step.
        if (ParticipantRules.Respondable(meeting.Status))
            stager.Stage(meeting, p, g, kind, now, notify: p?.UserId != actor);
    }

    [GeneratedRegex(@"^[^\s@<>\r\n]+@[^\s@<>\r\n]+\.[^\s@<>\r\n]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
