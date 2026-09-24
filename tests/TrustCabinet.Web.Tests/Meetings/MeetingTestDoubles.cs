using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web.Tests.Meetings;

internal sealed class FakeMeetingRepository : IMeetingRepository
{
    private readonly List<Meeting> _pendingAdds = [];

    public List<Meeting> Meetings { get; } = [];

    public List<MeetingApproverOption> EligibleApprovers { get; } = [];

    public HashSet<Guid> IneligibleApproverIds { get; } = [];

    public Exception? NextSaveThrows { get; set; }

    public Exception? NextReadThrows { get; set; }

    public int SaveCount { get; private set; }

    public Task<Meeting?> FindAccessibleAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        if (NextReadThrows is { } exception)
        {
            NextReadThrows = null;
            throw exception;
        }

        return Task.FromResult(Meetings.SingleOrDefault(m => m.Id == id
            && (m.CreatedById == actorId
                || (m.Status != TrustCabinet.Domain.Enums.MeetingStatus.Draft
                    && m.AssignedApproverId == actorId)
                || ((m.Status == TrustCabinet.Domain.Enums.MeetingStatus.Published
                        || m.Status == TrustCabinet.Domain.Enums.MeetingStatus.InProgress
                        || m.Status == TrustCabinet.Domain.Enums.MeetingStatus.Ended
                        || m.Status == TrustCabinet.Domain.Enums.MeetingStatus.MinutesIssued
                        || m.Status == TrustCabinet.Domain.Enums.MeetingStatus.Archived)
                    && m.Participants.Any(participant => participant.UserId == actorId)))));
    }

    public Task<Meeting?> FindOwnedForUpdateAsync(Guid id, Guid creatorId, CancellationToken ct = default)
        => Task.FromResult(Meetings.SingleOrDefault(m => m.Id == id && m.CreatedById == creatorId));

    public Task<IReadOnlyList<MeetingApproverOption>> ListEligibleApproversAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MeetingApproverOption>>([.. EligibleApprovers]);

    public Task<bool> IsEligibleApproverAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(id != Guid.Empty && !IneligibleApproverIds.Contains(id));

    public Task AddAsync(Meeting meeting, CancellationToken ct = default)
    {
        _pendingAdds.Add(meeting);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCount++;
        if (NextSaveThrows is { } exception)
        {
            NextSaveThrows = null;
            _pendingAdds.Clear();
            throw exception;
        }

        Meetings.AddRange(_pendingAdds);
        _pendingAdds.Clear();
        return Task.CompletedTask;
    }

    public async Task<T> InTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        var snapshot = Meetings.Select(Clone).ToArray();
        try
        {
            return await operation(ct);
        }
        catch
        {
            Meetings.Clear();
            Meetings.AddRange(snapshot);
            throw;
        }
    }

    private static Meeting Clone(Meeting meeting)
        => new()
        {
            Id = meeting.Id,
            CreatedAt = meeting.CreatedAt,
            UpdatedAt = meeting.UpdatedAt,
            Title = meeting.Title,
            Description = meeting.Description,
            Status = meeting.Status,
            Sensitivity = meeting.Sensitivity,
            Mode = meeting.Mode,
            Location = meeting.Location,
            StartsAt = meeting.StartsAt,
            EndsAt = meeting.EndsAt,
            CreatedById = meeting.CreatedById,
            AssignedApproverId = meeting.AssignedApproverId,
            StatusHistory = [.. meeting.StatusHistory],
        };
}
