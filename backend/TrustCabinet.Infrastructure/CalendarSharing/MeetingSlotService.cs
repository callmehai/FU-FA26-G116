using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.CalendarSharing;

/// <summary>
/// SCRUM-137 — UC-85a (find meeting time slots) and UC-85b (create a draft meeting from one). Only calendars shared with
/// the caller are read, each at its shared level (BR-414, BR-417), plus the caller's own when ticked (BR-416, BR-418).
/// Nothing about a meeting leaves this class but when it keeps someone busy (BR-427).
/// </summary>
public sealed class MeetingSlotService(
    TrustCabinetDbContext db,
    CalendarBusyReader busy,
    IMeetingService meetings,
    TimeProvider clock,
    ILogger<MeetingSlotService> logger) : IMeetingSlotService
{
    private sealed record Invitee(Guid Id, string FullName, string Email, bool IsActive);

    public async Task<SlotSearchOutcome> FindAsync(Guid actorId, SlotSearchInput input, CancellationToken ct = default)
    {
        try
        {
            if (!await IsOrganizerAsync(actorId, ct)) return new(MeetingSlotFailure.NotOrganizer);

            var now = clock.GetUtcNow();
            var today = VietnamTime.Today(now);
            if (!MeetingSlotRules.TryValidate(input, actorId, today, out var search, out var errors))
                return new(MeetingSlotFailure.InvalidSearch, Errors: errors);

            var lookup = await PeopleAsync([.. search!.UserIds, actorId], ct);
            var unavailable = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < search.UserIds.Count; i++)
                if (!lookup.TryGetValue(search.UserIds[i], out var invitee) || !invitee.IsActive)
                    unavailable[$"userIds[{i}]"] = MeetingSlotRules.UserUnavailableError;
            if (unavailable.Count > 0) return new(MeetingSlotFailure.InvalidSearch, Errors: unavailable);

            var buffer = TimeSpan.FromMinutes(search.BufferMinutes);
            var from = VietnamTime.At(search.FromDate, search.WorkStart) - buffer;
            var to = VietnamTime.At(search.ToDate, search.WorkEnd) + buffer;
            var (shares, persons) = await CountedPeopleAsync(actorId, search.UserIds, search.IncludeMe, today, from, to, ct);

            var finding = MeetingSlotFinder.Find(search, now, persons);
            return new(MeetingSlotFailure.None, Result(search, finding, lookup, shares, actorId));
        }
        catch (Exception error) when (error is DbUpdateException or NpgsqlException or TimeoutException)
        {
            logger.LogError(error, "Could not search meeting time slots for {UserId}", actorId);
            return new(MeetingSlotFailure.ReadFailed);
        }
    }

    public async Task<SlotDraftOutcome> CreateDraftAsync(Guid actorId, SlotDraftInput input, CancellationToken ct = default)
    {
        Guid[] userIds;
        try
        {
            if (!await IsOrganizerAsync(actorId, ct)) return new(MeetingSlotFailure.NotOrganizer);

            userIds = [.. input.UserIds ?? []];
            if (!MeetingSlotRules.ValidInvitees(userIds, actorId))
                return new(MeetingSlotFailure.InvalidInvitees,
                    Errors: new Dictionary<string, string> { ["userIds"] = MeetingSlotRules.UserIdsError });

            var slotErrors = new Dictionary<string, string>(StringComparer.Ordinal);
            var startOk = MeetingSlotRules.TryParseTimestamp(input.PlannedStartTime, out var start);
            var endOk = MeetingSlotRules.TryParseTimestamp(input.PlannedEndTime, out var end);
            if (!startOk) slotErrors["plannedStartTime"] = MeetingSlotRules.SlotTimeError;
            if (!endOk) slotErrors["plannedEndTime"] = MeetingSlotRules.SlotTimeError;
            else if (startOk && end <= start) slotErrors["plannedEndTime"] = MeetingSlotRules.SlotEndError;
            var bufferMinutes = input.BufferMinutes ?? MeetingSlotRules.DefaultBuffer;
            if (!MeetingSlotRules.ValidBuffer(bufferMinutes)) slotErrors["bufferMinutes"] = MeetingSlotRules.BufferError;
            var includeMe = input.IncludeMe ?? false;
            var accepted = input.AcceptedBusyUserIds ?? [];
            if (accepted.Any(id => !userIds.Contains(id) && id != actorId))
                slotErrors["acceptedBusyUserIds"] = MeetingSlotRules.AcceptedBusyError;
            if (slotErrors.Count > 0) return new(MeetingSlotFailure.InvalidSlot, Errors: slotErrors);

            // BR-428: recheck against the same shared data as the search — never against undisclosed meetings — and
            // refuse only when someone free then is busy or unknown now.
            var buffer = TimeSpan.FromMinutes(bufferMinutes);
            var today = VietnamTime.Today(clock.GetUtcNow());
            var (_, persons) = await CountedPeopleAsync(actorId, userIds, includeMe, today, start - buffer, end + buffer, ct);
            var (nowBusy, nowUnknown) = MeetingSlotFinder.Check(start, end, bufferMinutes, persons);
            if (nowBusy.Concat(nowUnknown).Any(id => !accepted.Contains(id))) return new(MeetingSlotFailure.SlotTaken);
        }
        catch (Exception error) when (error is DbUpdateException or NpgsqlException or TimeoutException)
        {
            logger.LogError(error, "Could not recheck a meeting time slot for {UserId}", actorId);
            return new(MeetingSlotFailure.ReadFailed);
        }

        // BR-429: the first invitee chairs, the rest are Members, the finder is the Secretary. UC-14 steps 5–6 check the
        // values (MSG31), the roster (MSG277) and save the Draft (MSG35).
        var seeds = new List<ParticipantSeed>(userIds.Length + 1)
        {
            new(userIds[0], nameof(MeetingRole.Chair)),
        };
        seeds.AddRange(userIds.Skip(1).Select(id => new ParticipantSeed(id, nameof(MeetingRole.Member))));
        seeds.Add(new ParticipantSeed(actorId, nameof(MeetingRole.Secretary)));
        var meeting = new MeetingInput(input.Title, input.PlannedStartTime, input.PlannedEndTime, input.Mode, input.Location,
            input.OnlineLink, input.Sensitivity, input.Description);
        return new(MeetingSlotFailure.None, await meetings.CreateAsync(meeting, actorId, seeds, ct));
    }

    /// <summary>BR-430: an active account whose role holds MeetingCreator, checked again on every request.</summary>
    private Task<bool> IsOrganizerAsync(Guid actorId, CancellationToken ct)
        => db.Users.AnyAsync(u => u.Id == actorId && u.IsActive
            && db.RoleCapabilities.Any(rc => rc.RoleId == u.RoleId && rc.Capability == Capability.MeetingCreator), ct);

    private async Task<Dictionary<Guid, Invitee>> PeopleAsync(Guid[] ids, CancellationToken ct)
        => await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new Invitee(u.Id, u.FullName, u.Email, u.IsActive))
            .ToDictionaryAsync(u => u.Id, ct);

    /// <summary>
    /// The people whose calendars count (§2.2 of the plan): each invitee with an Effective share to the caller, not past its
    /// end date and with an active owner (BR-423, BR-424, BR-425), and the caller when ticked (BR-416, BR-418). The
    /// finder comes first, then the invitees in the order sent.
    /// </summary>
    private async Task<(Dictionary<Guid, CalendarShare> Shares, IReadOnlyList<SlotPerson> Persons)> CountedPeopleAsync(
        Guid actorId, IReadOnlyList<Guid> userIds, bool includeMe, DateOnly today, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct)
    {
        var ids = userIds.ToArray();
        var rows = await db.CalendarShares.AsNoTracking()
            .Where(s => s.ViewerId == actorId && ids.Contains(s.OwnerId)
                && s.Status == CalendarShareStatus.Effective && s.EndDate >= today && s.Owner.IsActive)
            .ToListAsync(ct);
        var shares = rows.GroupBy(s => s.OwnerId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id).First());

        var levels = shares.ToDictionary(pair => pair.Key, pair => (CalendarShareLevel?)pair.Value.Level);
        if (includeMe) levels[actorId] = null;
        var intervals = await busy.ReadAsync(levels, from, to, ct);

        var persons = new List<SlotPerson>();
        if (includeMe) persons.Add(new SlotPerson(actorId, null, intervals[actorId]));
        persons.AddRange(userIds.Where(shares.ContainsKey).Select(id =>
            new SlotPerson(id, new KnownDays(shares[id].StartDate, shares[id].EndDate), intervals[id])));
        return (shares, persons);
    }

    private static SlotSearchResult Result(SlotSearch search, SlotFinding finding, IReadOnlyDictionary<Guid, Invitee> lookup,
        IReadOnlyDictionary<Guid, CalendarShare> shares, Guid actorId)
    {
        var people = new List<SlotPersonView>();
        if (search.IncludeMe && lookup.TryGetValue(actorId, out var me))
            people.Add(new SlotPersonView(me.Id, me.FullName, me.Email, SlotPersonStatus.Own, null, null, null));
        foreach (var id in search.UserIds)
        {
            var person = lookup[id];
            people.Add(shares.TryGetValue(id, out var share)
                ? new SlotPersonView(id, person.FullName, person.Email, SlotPersonStatus.Shared, share.Level.ToString(),
                    share.StartDate, share.EndDate)
                : new SlotPersonView(id, person.FullName, person.Email, SlotPersonStatus.NotShared, null, null, null));
        }

        var days = finding.Slots.GroupBy(s => s.Date)
            .Select(day => new SlotDayView(day.Key, [.. day.Select(s => new SlotView(
                VietnamTime.ToLocal(s.Start), VietnamTime.ToLocal(s.End), s.FreeCount, s.CountedCount, s.BusyUserIds,
                s.UnknownUserIds))]))
            .ToList();
        return new SlotSearchResult(finding.Kind, finding.Truncated, people, days);
    }
}
