using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.CalendarSharing;

/// <summary>
/// BR-424 — called by UC-06c (deactivate an account), UC-08 (change a role) and UC-11 (change a role's rights) inside their
/// own transaction, after their change is saved: every Effective share touching those people that is no longer live stops
/// for good, and both sides are told. A Pending invitation is kept (PO 09/11/2026, Q-C): its asked side may then only
/// decline it (UC-84c EX-5). A share past its end date is left alone (BR-425). Regaining the right or the account later
/// restarts nothing.
/// </summary>
public sealed class CalendarShareStopper(TrustCabinetDbContext db, CalendarShareCloser closer, TimeProvider clock)
    : ICalendarShareStopper
{
    public async Task<int> StopDeadSharesAsync(IReadOnlyCollection<Guid> userIds, Guid? actorId,
        CalendarShareStopReason reason, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return 0;
        var ids = userIds.Distinct().ToArray();
        var now = clock.GetUtcNow();
        // A share past its end date has already ended (BR-425, PO 09/11/2026 Q-B): it is left as it is — not stopped,
        // not audited, nobody told — even though its row still reads Effective, since nothing writes the lapse.
        var today = VietnamTime.Today(now);
        // Locked in id order, the order every other writer of these rows meets them in, so two sweeps never deadlock.
        var shares = await db.CalendarShares.FromSqlInterpolated($"""
            SELECT * FROM calendar_shares
            WHERE status = 'Effective' AND end_date >= {today}
              AND (owner_id = ANY({ids}) OR viewer_id = ANY({ids}))
            ORDER BY id
            FOR UPDATE
            """).ToListAsync(ct);
        if (shares.Count == 0) return 0;

        var people = await CalendarSharePeople.LoadAsync(db, shares.SelectMany(s => new[] { s.OwnerId, s.ViewerId }), ct);
        var stopped = 0;
        foreach (var share in shares.Where(s => !CalendarSharePeople.IsLive(s, people)))
        {
            await closer.StopAsync(share, CalendarSharePeople.NameOf(people, share.OwnerId),
                CalendarSharePeople.NameOf(people, share.ViewerId), actorId, reason, now, ct);
            stopped++;
        }

        return stopped;
    }
}
