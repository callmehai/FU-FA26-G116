using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.CalendarSharing;

/// <summary>
/// SCRUM-142 — UC-84a → UC-84f: request, share directly, answer, change, withdraw, end and list calendar shares. Each write
/// follows its sequence diagram: check, then save the share with its audit row, then notify the other side — all in one
/// transaction, so a save that fails tells nobody (EX "cannot be saved").
/// </summary>
public sealed class CalendarSharingService(
    TrustCabinetDbContext db,
    IAuditService audit,
    CalendarShareCloser closer,
    TimeProvider clock,
    ILogger<CalendarSharingService> logger) : ICalendarSharingService
{
    private DateOnly Today => VietnamTime.Today(clock.GetUtcNow());

    /// <summary>
    /// UC-84f: both directions, each row with what the caller may do. Shares past their end date are left out (BR-425).
    /// The "shared with me" half is for Meeting Organizers only (BR-430); the caller's own shares are listed to anyone,
    /// because any active account may own a calendar and has to answer, change or end its shares (UC-84b → UC-84e).
    /// </summary>
    public async Task<CalendarShareRead<ShareLists>> ListAsync(Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var today = Today;
            var organizer = await IsOrganizerAsync(actorId, ct);
            var shares = await db.CalendarShares.AsNoTracking()
                .Include(s => s.Owner).Include(s => s.Viewer)
                .Where(s => (s.OwnerId == actorId || (organizer && s.ViewerId == actorId)) && s.EndDate >= today)
                .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
                .ToListAsync(ct);
            // Whose viewer still holds MeetingCreator, for the Pending rows only: a request whose viewer lost it can only be
            // declined (BR-430), so its row offers no Accept.
            var pendingViewers = shares.Where(s => s.Status == CalendarShareStatus.Pending).Select(s => s.ViewerId).Distinct()
                .ToArray();
            var organizers = (await db.Users.AsNoTracking()
                .Where(u => pendingViewers.Contains(u.Id)
                    && db.RoleCapabilities.Any(rc => rc.RoleId == u.RoleId && rc.Capability == Capability.MeetingCreator))
                .Select(u => u.Id).ToListAsync(ct)).ToHashSet();
            ShareView View(CalendarShare s) => CalendarShareViews.From(s, Person(s.Owner), Person(s.Viewer), actorId, today,
                SenderDeactivated(s, s.Owner.IsActive, s.Viewer.IsActive), organizers.Contains(s.ViewerId));
            return new(new ShareLists(
                [.. shares.Where(s => s.OwnerId == actorId).Select(View)],
                [.. shares.Where(s => s.ViewerId == actorId).Select(View)]));
        }
        catch (Exception error) when (IsDatabaseFailure(error))
        {
            logger.LogError(error, "Could not read the calendar shares of {UserId}", actorId);
            return new(null, CalendarShareFailure.ReadFailed);
        }
    }

    /// <summary>UC-84a/UC-84b step 1: the people to choose from, filtered and paged on the server.</summary>
    public async Task<CalendarShareRead<ShareCandidatePage>> CandidatesAsync(Guid actorId, CandidateQuery query,
        CancellationToken ct = default)
    {
        try
        {
            var people = db.Users.AsNoTracking().Where(u => u.IsActive && u.Id != actorId);
            if (query.Purpose == CandidatePurpose.Request)
            {
                // UC-84a: only a Meeting Organizer asks (BR-430); any active internal account owns a calendar.
                if (!await IsOrganizerAsync(actorId, ct)) return new(null, CalendarShareFailure.NotOrganizer);
            }
            else
            {
                // UC-84b: only a Meeting Organizer can receive a share (BR-430).
                people = people.Where(u => db.RoleCapabilities.Any(rc => rc.RoleId == u.RoleId
                    && rc.Capability == Capability.MeetingCreator));
            }

            if (query.Q?.Trim() is { Length: > 0 } search)
            {
                var pattern = $"%{search.Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
                people = people.Where(u => EF.Functions.ILike(u.FullName, pattern, "\\")
                    || EF.Functions.ILike(u.Email, pattern, "\\"));
            }

            var today = Today;
            var total = await people.CountAsync(ct);
            var request = query.Purpose == CandidatePurpose.Request;
            var items = await people
                .OrderBy(u => u.FullName).ThenBy(u => u.Id)
                .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
                .Select(u => new ShareCandidate(u.Id, u.FullName, u.Email,
                    // The same direction as the share about to be sent: the candidate owns it for a request, views it for
                    // a direct share. A share past its end date no longer counts (BR-425).
                    db.CalendarShares.Any(s => (request ? s.OwnerId == u.Id && s.ViewerId == actorId
                            : s.OwnerId == actorId && s.ViewerId == u.Id)
                        && (s.Status == CalendarShareStatus.Pending || s.Status == CalendarShareStatus.Effective)
                        && s.EndDate >= today)))
                .ToListAsync(ct);
            return new(new ShareCandidatePage(items, total, query.Page, query.PageSize));
        }
        catch (Exception error) when (IsDatabaseFailure(error))
        {
            logger.LogError(error, "Could not list calendar share candidates for {UserId}", actorId);
            return new(null, CalendarShareFailure.CandidatesReadFailed);
        }
    }
    /// <summary>UC-84a, checked in the order of its step 2: the right (EX-1), the owner (EX-4), the pair (EX-2), the range (EX-3).</summary>
    public Task<CalendarShareOutcome> RequestAsync(Guid actorId, Guid? ownerId, ShareValues values,
        CancellationToken ct = default)
        => WriteAsync(async token =>
        {
            var people = await CalendarSharePeople.LoadAsync(db, [actorId, ownerId ?? Guid.Empty], token);
            if (!people.TryGetValue(actorId, out var viewer) || !viewer.IsActive || !viewer.IsOrganizer)
                return new(CalendarShareFailure.NotOrganizer);
            // EX-4: yourself, an unknown id and a locked account all look the same — nothing about an account is disclosed.
            if (ownerId is not { } id || id == actorId || !people.TryGetValue(id, out var owner) || !owner.IsActive)
                return new(CalendarShareFailure.NotFound);

            var today = Today;
            var now = clock.GetUtcNow();
            await CloseAfterEndDateForPairAsync(id, actorId, today, now, token);
            if (await HasOpenShareAsync(id, actorId, token)) return new(CalendarShareFailure.AlreadyShared);
            if (!CalendarShareRules.TryParseLevel(values.Level, out var level)
                || values.StartDate is not { } start || values.EndDate is not { } end
                || !CalendarShareRules.ValidNewRange(start, end, today))
                return new(CalendarShareFailure.InvalidValues);

            var share = new CalendarShare
            {
                OwnerId = id, ViewerId = actorId, InitiatedBy = CalendarShareInitiator.Viewer, Level = level,
                StartDate = start, EndDate = end, Status = CalendarShareStatus.Pending, CreatedAt = now, UpdatedAt = now,
            };
            db.CalendarShares.Add(share);
            db.Notifications.Add(CalendarShareNotifications.Requested(share, viewer.FullName, now));
            await audit.RecordAsync(AuditActions.CalendarShareRequested, AuditActions.CalendarShareObjectType, share.Id,
                actorId, CalendarShareAudit.Details(share), token);
            return Done(CalendarShareSuccess.Requested, share, owner, viewer, actorId, today);
        }, ct);

    /// <summary>UC-84b: always Level 1; the recipient asks for Level 2 through UC-84d once it is Effective.</summary>
    public Task<CalendarShareOutcome> ShareDirectlyAsync(Guid actorId, Guid? viewerId, DateOnly? startDate,
        DateOnly? endDate, CancellationToken ct = default)
        => WriteAsync(async token =>
        {
            var people = await CalendarSharePeople.LoadAsync(db, [actorId, viewerId ?? Guid.Empty], token);
            if (!people.TryGetValue(actorId, out var owner) || !owner.IsActive) return new(CalendarShareFailure.NotFound);
            if (viewerId is not { } id || id == actorId || !people.TryGetValue(id, out var viewer) || !viewer.IsActive
                || !viewer.IsOrganizer)
                return new(CalendarShareFailure.CannotReceive);

            var today = Today;
            var now = clock.GetUtcNow();
            await CloseAfterEndDateForPairAsync(actorId, id, today, now, token);
            if (await HasOpenShareAsync(actorId, id, token)) return new(CalendarShareFailure.AlreadyShared);
            if (startDate is not { } start || endDate is not { } end || !CalendarShareRules.ValidNewRange(start, end, today))
                return new(CalendarShareFailure.InvalidValues);

            var share = new CalendarShare
            {
                OwnerId = actorId, ViewerId = id, InitiatedBy = CalendarShareInitiator.Owner,
                Level = CalendarShareLevel.Level1, StartDate = start, EndDate = end, Status = CalendarShareStatus.Pending,
                CreatedAt = now, UpdatedAt = now,
            };
            db.CalendarShares.Add(share);
            db.Notifications.Add(CalendarShareNotifications.Received(share, owner.FullName, now));
            await audit.RecordAsync(AuditActions.CalendarShareShared, AuditActions.CalendarShareObjectType, share.Id,
                actorId, CalendarShareAudit.Details(share), token);
            return Done(CalendarShareSuccess.Shared, share, owner, viewer, actorId, today);
        }, ct);

    /// <summary>UC-84c: accept a Pending share (Normal Flow) or a pending change (AF-1).</summary>
    public Task<CalendarShareOutcome> AcceptAsync(Guid actorId, Guid shareId, ShareValues values,
        CancellationToken ct = default)
        => ActOnAsync(actorId, shareId, async (share, people, today, now, token) =>
        {
            var answerer = CalendarShareRules.Answerer(share);
            if (answerer is null) return new(CalendarShareFailure.NotAllowed);
            if (answerer != actorId) return new(CalendarShareFailure.NotFound); // EX-1: not the asked side
            CalendarShareLevel? level = null;
            if (values.Level is not null)
            {
                if (!CalendarShareRules.TryParseLevel(values.Level, out var parsed)) return new(CalendarShareFailure.InvalidValues);
                level = parsed;
            }

            var owner = people[share.OwnerId];
            var viewer = people[share.ViewerId];
            if (share.Status == CalendarShareStatus.Pending)
            {
                // UC-84c EX-5 (PO 09/11/2026, Q-C): an invitation whose sender was deactivated stays, but only to decline.
                if (!people[CalendarShareRules.Sender(share)].IsActive) return new(CalendarShareFailure.SenderDeactivated);
                // A request whose viewer lost the Meeting Organizer right cannot become Effective either (BR-430).
                if (!viewer.IsOrganizer) return new(CalendarShareFailure.NotAllowed);
                var requested = CalendarShareAudit.Snapshot(share);
                if (share.InitiatedBy == CalendarShareInitiator.Viewer)
                {
                    // The owner may lower the level or shorten the range, within what was asked (BR-414, BR-431).
                    var newLevel = level ?? share.Level;
                    var start = values.StartDate ?? share.StartDate;
                    var end = values.EndDate ?? share.EndDate;
                    if (!CalendarShareRules.ValidNarrowing(share, newLevel, start, end, today))
                        return new(CalendarShareFailure.InvalidValues);
                    share.Level = newLevel;
                    share.StartDate = start;
                    share.EndDate = end;
                }
                else if (Differs(level, share.Level) || Differs(values.StartDate, share.StartDate)
                    || Differs(values.EndDate, share.EndDate))
                {
                    // A direct share is accepted as offered; the viewer asks for more through UC-84d afterwards.
                    return new(CalendarShareFailure.InvalidValues);
                }

                share.Status = CalendarShareStatus.Effective;
                share.DecidedAt = now;
                share.UpdatedAt = now;
                db.Notifications.Add(CalendarShareNotifications.Accepted(share, CalendarShareRules.Sender(share),
                    people[actorId].FullName, now));
                await audit.RecordAsync(AuditActions.CalendarShareAccepted, AuditActions.CalendarShareObjectType, share.Id,
                    actorId, new
                    {
                        share.OwnerId, share.ViewerId, Requested = requested, Granted = CalendarShareAudit.Snapshot(share),
                    }, token);
                return Done(CalendarShareSuccess.Accepted, share, owner, viewer, actorId, today);
            }

            // AF-1: approve the viewer's change exactly as asked, checked again now (BR-431).
            if (Differs(level, share.PendingLevel!.Value) || Differs(values.EndDate, share.PendingEndDate!.Value)
                || Differs(values.StartDate, share.StartDate) || !CalendarShareRules.ValidPendingApproval(share, today))
                return new(CalendarShareFailure.InvalidValues);
            var before = CalendarShareAudit.Snapshot(share);
            share.Level = share.PendingLevel.Value;
            share.EndDate = share.PendingEndDate.Value;
            share.ClearPendingChange();
            share.UpdatedAt = now;
            db.Notifications.Add(CalendarShareNotifications.Updated(share, owner.FullName, now));
            await audit.RecordAsync(AuditActions.CalendarShareChangeAccepted, AuditActions.CalendarShareObjectType,
                share.Id, actorId,
                new { share.OwnerId, share.ViewerId, Before = before, After = CalendarShareAudit.Snapshot(share) }, token);
            return Done(CalendarShareSuccess.Updated, share, owner, viewer, actorId, today);
        }, ct);

    /// <summary>UC-84c: decline a Pending share (Normal Flow step 4) or a pending change (AF-1 step 3).</summary>
    public Task<CalendarShareOutcome> DeclineAsync(Guid actorId, Guid shareId, CancellationToken ct = default)
        => ActOnAsync(actorId, shareId, async (share, people, today, now, token) =>
        {
            var answerer = CalendarShareRules.Answerer(share);
            if (answerer is null) return new(CalendarShareFailure.NotAllowed);
            if (answerer != actorId) return new(CalendarShareFailure.NotFound);
            var owner = people[share.OwnerId];
            var viewer = people[share.ViewerId];
            if (share.Status == CalendarShareStatus.Pending)
            {
                CalendarShareCloser.Close(share, CalendarShareStatus.Declined, actorId, now);
                share.DecidedAt = now;
                db.Notifications.Add(CalendarShareNotifications.Declined(share, CalendarShareRules.Sender(share),
                    people[actorId].FullName, now));
                await audit.RecordAsync(AuditActions.CalendarShareDeclined, AuditActions.CalendarShareObjectType, share.Id,
                    actorId, CalendarShareAudit.Details(share), token);
                return Done(CalendarShareSuccess.Declined, share, owner, viewer, actorId, today);
            }

            var declined = new { Level = share.PendingLevel.ToString(), EndDate = share.PendingEndDate };
            share.ClearPendingChange();
            share.UpdatedAt = now;
            db.Notifications.Add(CalendarShareNotifications.ChangeDeclined(share, owner.FullName, now));
            await audit.RecordAsync(AuditActions.CalendarShareChangeDeclined, AuditActions.CalendarShareObjectType,
                share.Id, actorId, new { share.OwnerId, share.ViewerId, Declined = declined }, token);
            return Done(CalendarShareSuccess.ChangeDeclined, share, owner, viewer, actorId, today);
        }, ct);

    /// <summary>UC-84d: the viewer asks for a change (Normal Flow); the owner applies one at once (AF-1).</summary>
    public Task<CalendarShareOutcome> ChangeAsync(Guid actorId, Guid shareId, ShareValues values,
        CancellationToken ct = default)
        => ActOnAsync(actorId, shareId, async (share, people, today, now, token) =>
        {
            if (share.Status != CalendarShareStatus.Effective) return new(CalendarShareFailure.NotAllowed);
            var isViewer = share.ViewerId == actorId;
            // EX-2: one pending change at a time. The owner may still change the share meanwhile (UC-84d AF-1).
            if (isViewer && share.HasPendingChange) return new(CalendarShareFailure.NotAllowed);
            CalendarShareLevel? level = null;
            if (values.Level is not null)
            {
                if (!CalendarShareRules.TryParseLevel(values.Level, out var parsed)) return new(CalendarShareFailure.InvalidValues);
                level = parsed;
            }

            var owner = people[share.OwnerId];
            var viewer = people[share.ViewerId];
            if (isViewer)
            {
                var newLevel = level ?? share.Level;
                var end = values.EndDate ?? share.EndDate;
                if (Differs(values.StartDate, share.StartDate)
                    || !CalendarShareRules.ValidViewerChange(share, newLevel, end, today))
                    return new(CalendarShareFailure.InvalidValues);
                share.PendingLevel = newLevel;
                share.PendingEndDate = end;
                share.PendingRequestedAt = now;
                share.UpdatedAt = now;
                db.Notifications.Add(CalendarShareNotifications.ChangeRequested(share, viewer.FullName, now));
                await audit.RecordAsync(AuditActions.CalendarShareChangeRequested, AuditActions.CalendarShareObjectType,
                    share.Id, actorId, new
                    {
                        share.OwnerId, share.ViewerId, Current = CalendarShareAudit.Snapshot(share),
                        Asked = new { Level = newLevel.ToString(), EndDate = end },
                    }, token);
                return Done(CalendarShareSuccess.ChangeRequested, share, owner, viewer, actorId, today);
            }

            var ownerLevel = level ?? share.Level;
            var ownerStart = values.StartDate ?? share.StartDate;
            var ownerEnd = values.EndDate ?? share.EndDate;
            if (!CalendarShareRules.ValidOwnerChange(share, ownerLevel, ownerStart, ownerEnd, today))
                return new(CalendarShareFailure.InvalidValues);
            var before = CalendarShareAudit.Snapshot(share);
            share.Level = ownerLevel;
            share.StartDate = ownerStart;
            share.EndDate = ownerEnd;
            share.UpdatedAt = now;
            db.Notifications.Add(CalendarShareNotifications.Updated(share, owner.FullName, now));
            await audit.RecordAsync(AuditActions.CalendarShareUpdated, AuditActions.CalendarShareObjectType, share.Id,
                actorId, new { share.OwnerId, share.ViewerId, Before = before, After = CalendarShareAudit.Snapshot(share) },
                token);
            return Done(CalendarShareSuccess.Updated, share, owner, viewer, actorId, today);
        }, ct);

    /// <summary>UC-84e Normal Flow: the sender withdraws a Pending share, or the viewer a pending change.</summary>
    public Task<CalendarShareOutcome> WithdrawAsync(Guid actorId, Guid shareId, CancellationToken ct = default)
        => ActOnAsync(actorId, shareId, async (share, people, today, now, token) =>
        {
            var owner = people[share.OwnerId];
            var viewer = people[share.ViewerId];
            var other = share.OwnerId == actorId ? share.ViewerId : share.OwnerId;
            if (share.Status == CalendarShareStatus.Pending && CalendarShareRules.Sender(share) == actorId)
            {
                CalendarShareCloser.Close(share, CalendarShareStatus.Withdrawn, actorId, now);
                db.Notifications.Add(CalendarShareNotifications.Withdrawn(share, other, people[actorId].FullName, now));
                await audit.RecordAsync(AuditActions.CalendarShareWithdrawn, AuditActions.CalendarShareObjectType, share.Id,
                    actorId, CalendarShareAudit.Details(share), token);
                return Done(CalendarShareSuccess.Withdrawn, share, owner, viewer, actorId, today);
            }

            if (share.Status == CalendarShareStatus.Effective && share.HasPendingChange && share.ViewerId == actorId)
            {
                var withdrawn = new { Level = share.PendingLevel.ToString(), EndDate = share.PendingEndDate };
                share.ClearPendingChange();
                share.UpdatedAt = now;
                db.Notifications.Add(CalendarShareNotifications.Withdrawn(share, other, people[actorId].FullName, now));
                await audit.RecordAsync(AuditActions.CalendarShareChangeWithdrawn, AuditActions.CalendarShareObjectType,
                    share.Id, actorId, new { share.OwnerId, share.ViewerId, Withdrawn = withdrawn }, token);
                return Done(CalendarShareSuccess.Withdrawn, share, owner, viewer, actorId, today);
            }

            return new(CalendarShareFailure.NotAllowed);
        }, ct);

    /// <summary>UC-84e AF-1: either side ends an Effective share ("Ngừng chia sẻ" / "Thôi xem lịch").</summary>
    public Task<CalendarShareOutcome> EndAsync(Guid actorId, Guid shareId, CancellationToken ct = default)
        => ActOnAsync(actorId, shareId, async (share, people, today, now, token) =>
        {
            if (share.Status != CalendarShareStatus.Effective) return new(CalendarShareFailure.NotAllowed);
            var other = share.OwnerId == actorId ? share.ViewerId : share.OwnerId;
            CalendarShareCloser.Close(share, CalendarShareStatus.Ended, actorId, now);
            db.Notifications.Add(CalendarShareNotifications.Ended(share, other, people[actorId].FullName, now));
            await audit.RecordAsync(AuditActions.CalendarShareEnded, AuditActions.CalendarShareObjectType, share.Id,
                actorId, CalendarShareAudit.Details(share), token);
            return Done(CalendarShareSuccess.Ended, share, people[share.OwnerId], people[share.ViewerId], actorId, today);
        }, ct);

    private delegate Task<CalendarShareOutcome> ShareAction(CalendarShare share, IReadOnlyDictionary<Guid, PersonState> people,
        DateOnly today, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Locks the share, answers 404 to anyone who is not one of its two people, and refuses (MSG450) a share past its end
    /// date (BR-425). An Effective share that died without a hook (BR-424) is stopped first — the stop is kept.
    /// </summary>
    private Task<CalendarShareOutcome> ActOnAsync(Guid actorId, Guid shareId, ShareAction action, CancellationToken ct)
        => WriteAsync(async token =>
        {
            var share = await CalendarSharePeople.LockAsync(db, shareId, token);
            if (share is null || !CalendarShareRules.IsParty(share, actorId)) return new(CalendarShareFailure.NotFound);
            var people = await CalendarSharePeople.LoadAsync(db, [share.OwnerId, share.ViewerId], token);
            var today = Today;
            var now = clock.GetUtcNow();
            // BR-425: past its end date the share — and anything waiting on it — has ended; nothing is written.
            if (CalendarShareRules.IsLapsed(share, today)) return new(CalendarShareFailure.NotAllowed);

            if (share.Status == CalendarShareStatus.Effective && !CalendarSharePeople.IsLive(share, people))
            {
                // A way of losing the right that has no hook yet: stop it now rather than let it run on (BR-424).
                await closer.StopAsync(share, CalendarSharePeople.NameOf(people, share.OwnerId),
                    CalendarSharePeople.NameOf(people, share.ViewerId), null, CalendarSharePeople.ReasonFor(share, people),
                    now, token);
                return new(CalendarShareFailure.NotAllowed);
            }

            return await action(share, people, today, now, token);
        }, ct);

    /// <summary>
    /// An open share of the pair past its end date has ended already (BR-425) but still holds the pair's one open place
    /// in the unique index (BR-423): close it, silently, so the new share can take the place.
    /// </summary>
    private async Task CloseAfterEndDateForPairAsync(Guid ownerId, Guid viewerId, DateOnly today, DateTimeOffset now,
        CancellationToken ct)
    {
        var lapsed = await db.CalendarShares.FromSqlInterpolated($"""
            SELECT * FROM calendar_shares
            WHERE owner_id = {ownerId} AND viewer_id = {viewerId}
              AND status IN ('Pending', 'Effective') AND end_date < {today}
            FOR UPDATE
            """).ToListAsync(ct);
        foreach (var share in lapsed) await closer.CloseAfterEndDateAsync(share, now, ct);
    }

    private Task<bool> HasOpenShareAsync(Guid ownerId, Guid viewerId, CancellationToken ct)
        => db.CalendarShares.AnyAsync(s => s.OwnerId == ownerId && s.ViewerId == viewerId
            && (s.Status == CalendarShareStatus.Pending || s.Status == CalendarShareStatus.Effective), ct);

    private Task<bool> IsOrganizerAsync(Guid userId, CancellationToken ct)
        => db.Users.AnyAsync(u => u.Id == userId && u.IsActive
            && db.RoleCapabilities.Any(rc => rc.RoleId == u.RoleId && rc.Capability == Capability.MeetingCreator), ct);

    private async Task<CalendarShareOutcome> WriteAsync(Func<CancellationToken, Task<CalendarShareOutcome>> write,
        CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            var outcome = await write(ct);
            await transaction.CommitAsync(ct);
            return outcome;
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_calendar_shares_open_pair",
        })
        {
            // Two requests for the same pair at once: the index lets one through (BR-423, EX-2).
            db.ChangeTracker.Clear();
            logger.LogInformation(error, "A calendar share of the same pair was saved first");
            return new(CalendarShareFailure.AlreadyShared);
        }
        catch (Exception error) when (IsDatabaseFailure(error))
        {
            db.ChangeTracker.Clear();
            logger.LogError(error, "Could not save the calendar share");
            return new(CalendarShareFailure.SaveFailed);
        }
    }

    private static bool IsDatabaseFailure(Exception error) => error is DbUpdateException or NpgsqlException or TimeoutException;

    private static bool Differs<T>(T? sent, T current) where T : struct => sent is { } value && !value.Equals(current);

    private static SharePerson Person(User user) => new(user.Id, user.FullName, user.Email);

    private static CalendarShareOutcome Done(CalendarShareSuccess success, CalendarShare share, PersonState owner,
        PersonState viewer, Guid actorId, DateOnly today)
        => new(Success: success, Share: CalendarShareViews.From(share, owner.View, viewer.View, actorId, today,
            SenderDeactivated(share, owner.IsActive, viewer.IsActive), viewer.IsOrganizer));

    /// <summary>UC-84c EX-5: a Pending invitation whose sender's account was deactivated.</summary>
    private static bool SenderDeactivated(CalendarShare share, bool ownerActive, bool viewerActive)
        => share.Status == CalendarShareStatus.Pending
            && !(share.InitiatedBy == CalendarShareInitiator.Viewer ? viewerActive : ownerActive);
}

internal static class CalendarShareViews
{
    internal static ShareView From(CalendarShare share, SharePerson owner, SharePerson viewer, Guid actorId, DateOnly today,
        bool senderDeactivated, bool viewerIsOrganizer)
        => new(
            share.Id,
            share.InitiatedBy.ToString(),
            share.Level.ToString(),
            owner,
            viewer,
            share.StartDate,
            share.EndDate,
            share.Status.ToString(),
            share.HasPendingChange
                ? new PendingChangeView(share.PendingLevel!.Value.ToString(), share.PendingEndDate!.Value,
                    share.PendingRequestedAt!.Value)
                : null,
            share.CreatedAt,
            share.DecidedAt,
            share.ClosedAt,
            senderDeactivated,
            CalendarShareRules.Actions(share, actorId, today,
                acceptBlocked: share.Status == CalendarShareStatus.Pending && (senderDeactivated || !viewerIsOrganizer)));
}
