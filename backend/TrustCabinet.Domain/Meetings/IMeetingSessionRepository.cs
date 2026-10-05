using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Domain.Meetings;

/// <summary>
/// Meeting Session persistence. Changes are saved through <see cref="IMeetingRepository"/>, which
/// shares the unit of work and holds the parent meeting lock.
/// </summary>
public interface IMeetingSessionRepository
{
    /// <summary>Sessions of one meeting ordered by planned start time.</summary>
    Task<IReadOnlyList<MeetingSessionDetails>> ListByMeetingAsync(Guid meetingId, CancellationToken ct = default);

    /// <summary>A tracked session, only when it belongs to <paramref name="meetingId"/>.</summary>
    Task<MeetingSession?> FindAsync(Guid meetingId, Guid sessionId, CancellationToken ct = default);

    Task AddAsync(MeetingSession session, CancellationToken ct = default);

    /// <summary>BR-59: whether any session of the meeting is InProgress.</summary>
    Task<bool> HasSessionInProgressAsync(Guid meetingId, CancellationToken ct = default);
}
