namespace TrustCabinet.Application.Meetings;

/// <summary>The one read <see cref="MeetingAudience"/> needs that no repository already answers.</summary>
public interface IMeetingAudienceQuery
{
    /// <summary>
    /// Those of <paramref name="candidateUserIds"/> whose account is active now, read untracked from the row. The
    /// realtime events of a meeting reach only these: a deactivated reader hears nothing more.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListActiveUserIdsAsync(Guid[] candidateUserIds, CancellationToken ct = default);
}
