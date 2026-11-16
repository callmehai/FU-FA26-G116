using TrustCabinet.Application.Attendance;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// The meeting lock and attendance targets in lists. Locks are recorded in order; targets answer from the
/// lists. Nothing here saves.
/// </summary>
public sealed class FakeAttendanceRepository : IAttendanceRepository
{
    /// <summary>Meetings locked by <see cref="LockMeetingAsync"/>, in order.</summary>
    public List<Guid> LockedMeetings { get; } = [];

    /// <summary>Lock target calls in order, as (meeting, internal ids, guest ids).</summary>
    public List<(Guid MeetingId, Guid[] InternalIds, Guid[] GuestIds)> TargetLocks { get; } = [];

    public List<MeetingParticipant> InternalTargets { get; } = [];
    public List<MeetingGuestInvitation> GuestTargets { get; } = [];

    /// <summary>Accounts counted as active by <see cref="IsUserActiveAsync"/>.</summary>
    public HashSet<Guid> ActiveUsers { get; } = [];

    /// <summary>Display names answering <see cref="FindRecordersAsync"/>, by account id.</summary>
    public Dictionary<Guid, string> UserNames { get; } = [];

    /// <summary>Thrown by every read in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    public Task LockMeetingAsync(Guid meetingId, CancellationToken ct)
    {
        LockedMeetings.Add(meetingId);
        return Fail is { } failure ? Task.FromException(failure) : Task.CompletedTask;
    }

    public Task<(MeetingParticipant[] Internal, MeetingGuestInvitation[] Guests)> LockTargetsAsync(
        Guid meetingId, Guid[] internalIds, Guid[] guestIds, CancellationToken ct)
    {
        TargetLocks.Add((meetingId, internalIds, guestIds));
        return Read(() => (
            Internal: InternalTargets.Where(p => p.MeetingId == meetingId && internalIds.Contains(p.Id)).ToArray(),
            Guests: GuestTargets.Where(g => g.MeetingId == meetingId && guestIds.Contains(g.Id)).ToArray()));
    }

    public Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct)
        => Read(() => ActiveUsers.Contains(userId));

    public Task<IReadOnlyDictionary<Guid, AttendanceRecorder>> FindRecordersAsync(Guid[] userIds, CancellationToken ct)
        => Read<IReadOnlyDictionary<Guid, AttendanceRecorder>>(() => userIds
            .Where(UserNames.ContainsKey).ToDictionary(id => id, id => new AttendanceRecorder(id, UserNames[id])));

    private Task<T> Read<T>(Func<T> answer) => Fail is { } failure ? Task.FromException<T>(failure) : Task.FromResult(answer());
}
