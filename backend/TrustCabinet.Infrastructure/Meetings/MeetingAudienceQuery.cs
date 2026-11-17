using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Meetings;

namespace TrustCabinet.Infrastructure.Meetings;

public sealed class MeetingAudienceQuery(TrustCabinetDbContext db) : IMeetingAudienceQuery
{
    public async Task<IReadOnlyList<Guid>> ListActiveUserIdsAsync(Guid[] candidateUserIds, CancellationToken ct = default)
        => await db.Users.AsNoTracking().Where(u => candidateUserIds.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id).ToListAsync(ct);
}
