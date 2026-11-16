using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.CalendarSharing;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.CalendarSharing;

public sealed class CalendarShareRepository(TrustCabinetDbContext db) : ICalendarShareRepository
{
    public Task<bool> IsOrganizerAsync(Guid userId, CancellationToken ct = default)
        => db.Users.AnyAsync(u => u.Id == userId && u.IsActive
            && db.RoleCapabilities.Any(rc => rc.RoleId == u.RoleId && rc.Capability == Capability.MeetingCreator), ct);

    public Task<List<CalendarShare>> FindListedAsync(Guid userId, bool includeViewed, DateOnly today,
        CancellationToken ct = default)
        => db.CalendarShares.AsNoTracking()
            .Include(s => s.Owner).Include(s => s.Viewer)
            .Where(s => (s.OwnerId == userId || (includeViewed && s.ViewerId == userId)) && s.EndDate >= today)
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
            .ToListAsync(ct);

    public Task<List<Guid>> FindMeetingCreatorIdsAsync(Guid[] userIds, CancellationToken ct = default)
        => db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id)
                && db.RoleCapabilities.Any(rc => rc.RoleId == u.RoleId && rc.Capability == Capability.MeetingCreator))
            .Select(u => u.Id).ToListAsync(ct);

    public async Task<ShareCandidatePage> FindCandidatesAsync(Guid userId, CandidateQuery query, DateOnly today,
        CancellationToken ct = default)
    {
        var people = db.Users.AsNoTracking().Where(u => u.IsActive && u.Id != userId);
        if (query.Purpose != CandidatePurpose.Request)
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

        var total = await people.CountAsync(ct);
        var request = query.Purpose == CandidatePurpose.Request;
        var items = await people
            .OrderBy(u => u.FullName).ThenBy(u => u.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(u => new ShareCandidate(u.Id, u.FullName, u.Email,
                // The same direction as the share about to be sent: the candidate owns it for a request, views it for
                // a direct share. A share past its end date no longer counts (BR-425).
                db.CalendarShares.Any(s => (request ? s.OwnerId == u.Id && s.ViewerId == userId
                        : s.OwnerId == userId && s.ViewerId == u.Id)
                    && (s.Status == CalendarShareStatus.Pending || s.Status == CalendarShareStatus.Effective)
                    && s.EndDate >= today)))
            .ToListAsync(ct);
        return new ShareCandidatePage(items, total, query.Page, query.PageSize);
    }

    public async Task<Dictionary<Guid, PersonState>> LoadPeopleAsync(IEnumerable<Guid> userIds,
        CancellationToken ct = default)
    {
        var list = userIds.Distinct().ToArray();
        return await db.Users.AsNoTracking()
            .Where(u => list.Contains(u.Id))
            .Select(u => new PersonState(u.Id, u.FullName, u.Email, u.IsActive,
                db.RoleCapabilities.Any(rc => rc.RoleId == u.RoleId && rc.Capability == Capability.MeetingCreator)))
            .ToDictionaryAsync(p => p.Id, ct);
    }

    public async Task<Dictionary<Guid, PersonState>> LockPeopleAsync(IEnumerable<Guid> userIds,
        CancellationToken ct = default)
    {
        var list = userIds.Distinct().Order().ToArray();
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = ANY({list}) ORDER BY id FOR SHARE")
            .AsNoTracking().ToArrayAsync(ct);
        return await LoadPeopleAsync(list, ct);
    }

    public Task<CalendarShare?> LockAsync(Guid shareId, CancellationToken ct = default)
        => db.CalendarShares.FromSqlInterpolated($"SELECT * FROM calendar_shares WHERE id = {shareId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);

    public Task<List<CalendarShare>> LockLapsedOpenAsync(Guid ownerId, Guid viewerId, DateOnly today,
        CancellationToken ct = default)
        => db.CalendarShares.FromSqlInterpolated($"""
            SELECT * FROM calendar_shares
            WHERE owner_id = {ownerId} AND viewer_id = {viewerId}
              AND status IN ('Pending', 'Effective') AND end_date < {today}
            FOR UPDATE
            """).ToListAsync(ct);

    public Task<List<CalendarShare>> LockEffectiveOfPeopleAsync(Guid[] userIds, DateOnly today,
        CancellationToken ct = default)
    {
        return db.CalendarShares.FromSqlInterpolated($"""
            SELECT * FROM calendar_shares
            WHERE status = 'Effective' AND end_date >= {today}
              AND (owner_id = ANY({userIds}) OR viewer_id = ANY({userIds}))
            ORDER BY id
            FOR UPDATE
            """).ToListAsync(ct);
    }

    public Task<bool> HasOpenShareAsync(Guid ownerId, Guid viewerId, CancellationToken ct = default)
        => db.CalendarShares.AnyAsync(s => s.OwnerId == ownerId && s.ViewerId == viewerId
            && (s.Status == CalendarShareStatus.Pending || s.Status == CalendarShareStatus.Effective), ct);

    public Task<List<CalendarShare>> FindSharedWithAsync(Guid viewerId, DateOnly today, CancellationToken ct = default)
        => db.CalendarShares.AsNoTracking().Include(s => s.Owner)
            .Where(s => s.ViewerId == viewerId && s.EndDate >= today
                && (s.Status == CalendarShareStatus.Effective || s.Status == CalendarShareStatus.Stopped))
            .ToListAsync(ct);

    public Task<List<CalendarShare>> FindEffectiveFromAsync(Guid viewerId, Guid[] ownerIds, DateOnly today,
        CancellationToken ct = default)
    {
        return db.CalendarShares.AsNoTracking()
            .Where(s => s.ViewerId == viewerId && ownerIds.Contains(s.OwnerId)
                && s.Status == CalendarShareStatus.Effective && s.EndDate >= today && s.Owner.IsActive)
            .ToListAsync(ct);
    }

    public async Task<Dictionary<Guid, Invitee>> FindInviteesAsync(Guid[] userIds, CancellationToken ct = default)
        => await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new Invitee(u.Id, u.FullName, u.Email, u.IsActive))
            .ToDictionaryAsync(u => u.Id, ct);

    public void Add(CalendarShare share) => db.CalendarShares.Add(share);
}
