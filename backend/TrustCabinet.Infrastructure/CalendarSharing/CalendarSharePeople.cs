using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.CalendarSharing;

/// <summary>What a share needs to know about one of its people right now: who, active, Meeting Organizer (BR-424, BR-430).</summary>
internal sealed record PersonState(Guid Id, string FullName, string Email, bool IsActive, bool IsOrganizer)
{
    public SharePerson View => new(Id, FullName, Email);
}

internal static class CalendarSharePeople
{
    /// <summary>Read fresh from the database — inside a transaction, after a caller's change was saved, it sees that change.</summary>
    internal static async Task<Dictionary<Guid, PersonState>> LoadAsync(TrustCabinetDbContext db, IEnumerable<Guid> ids,
        CancellationToken ct)
    {
        var list = ids.Distinct().ToArray();
        return await db.Users.AsNoTracking()
            .Where(u => list.Contains(u.Id))
            .Select(u => new PersonState(u.Id, u.FullName, u.Email, u.IsActive,
                db.RoleCapabilities.Any(rc => rc.RoleId == u.RoleId && rc.Capability == Capability.MeetingCreator)))
            .ToDictionaryAsync(p => p.Id, ct);
    }

    /// <summary>BR-424: an open share lives while both accounts are active and the viewer is still a Meeting Organizer.</summary>
    internal static bool IsLive(CalendarShare share, IReadOnlyDictionary<Guid, PersonState> people)
        => people.TryGetValue(share.OwnerId, out var owner) && owner.IsActive
            && people.TryGetValue(share.ViewerId, out var viewer) && viewer.IsActive && viewer.IsOrganizer;

    /// <summary>Why a dead share died: a locked account wins over a lost right.</summary>
    internal static CalendarShareStopReason ReasonFor(CalendarShare share, IReadOnlyDictionary<Guid, PersonState> people)
        => people.TryGetValue(share.OwnerId, out var owner) && owner.IsActive
            && people.TryGetValue(share.ViewerId, out var viewer) && viewer.IsActive
            ? CalendarShareStopReason.ViewerLostMeetingCreator
            : CalendarShareStopReason.AccountDeactivated;

    internal static string NameOf(IReadOnlyDictionary<Guid, PersonState> people, Guid id)
        => people.TryGetValue(id, out var person) ? person.FullName : "";

    /// <summary>The share row locked for the rest of the transaction, so two answers to it cannot both win.</summary>
    internal static Task<CalendarShare?> LockAsync(TrustCabinetDbContext db, Guid id, CancellationToken ct)
        => db.CalendarShares.FromSqlInterpolated($"SELECT * FROM calendar_shares WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
}
