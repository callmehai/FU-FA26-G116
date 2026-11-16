using TrustCabinet.Application.Speaking;

namespace TrustCabinet.Infrastructure.Speaking;

public sealed class SpeakingRequestCloser(TrustCabinetDbContext db) : ISpeakingRequestCloser
{
    public Task<int> CloseOpenAsync(Guid meetingId, Guid? userId, IReadOnlyCollection<Guid>? guestInvitationIds,
        DateTimeOffset endedAt, DateTimeOffset now, CancellationToken ct)
        => SpeakingRequestCleanup.CloseOpenAsync(db, meetingId, userId, guestInvitationIds, endedAt, now, ct);
}
