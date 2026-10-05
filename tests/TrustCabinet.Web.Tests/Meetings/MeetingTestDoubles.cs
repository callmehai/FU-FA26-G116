using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Notifications;

namespace TrustCabinet.Web.Tests.Meetings;

internal sealed class FakeMeetingInvitationQueue : IMeetingInvitationQueue
{
    public List<Guid> MeetingIds { get; } = [];
    public Exception? NextQueueThrows { get; set; }

    public Task<int> QueueForPublishedMeetingAsync(Guid meetingId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (NextQueueThrows is { } exception)
        {
            NextQueueThrows = null;
            throw exception;
        }
        MeetingIds.Add(meetingId);
        return Task.FromResult(0);
    }

    public Task<Guid?> QueueNextUninitializedPublishedMeetingAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<Guid?>(null);
    }
}

/// <summary>Reads and writes the sessions held by <see cref="FakeMeetingRepository"/> meetings.</summary>
internal sealed class FakeMeetingSessionRepository(FakeMeetingRepository meetings) : IMeetingSessionRepository
{
    public Exception? NextListThrows { get; set; }

    public Task<IReadOnlyList<MeetingSessionDetails>> ListByMeetingAsync(Guid meetingId, CancellationToken ct = default)
    {
        if (NextListThrows is { } exception)
        {
            NextListThrows = null;
            throw exception;
        }

        return Task.FromResult<IReadOnlyList<MeetingSessionDetails>>(meetings.Meetings
            .Where(meeting => meeting.Id == meetingId)
            .SelectMany(meeting => meeting.Sessions)
            .OrderBy(session => session.PlannedStartsAt)
            .ThenBy(session => session.Id)
            .Select(MeetingSessionDetails.From)
            .ToArray());
    }

    public Task<MeetingSession?> FindAsync(Guid meetingId, Guid sessionId, CancellationToken ct = default)
        => Task.FromResult(meetings.Meetings
            .Where(meeting => meeting.Id == meetingId)
            .SelectMany(meeting => meeting.Sessions)
            .SingleOrDefault(session => session.Id == sessionId));

    public Task AddAsync(MeetingSession session, CancellationToken ct = default)
    {
        meetings.Meetings.Single(meeting => meeting.Id == session.MeetingId).Sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<bool> HasSessionInProgressAsync(Guid meetingId, CancellationToken ct = default)
        => Task.FromResult(meetings.Meetings
            .Where(meeting => meeting.Id == meetingId)
            .SelectMany(meeting => meeting.Sessions)
            .Any(session => session.Status == MeetingSessionStatus.InProgress));
}

internal sealed class FakeMeetingRepository : IMeetingRepository
{
    private readonly List<Meeting> _pendingAdds = [];
    private readonly List<Notification> _pendingNotifications = [];
    private readonly List<EmailMessage> _pendingEmailMessages = [];

    public List<Meeting> Meetings { get; } = [];

    public List<MeetingApproverOption> EligibleApprovers { get; } = [];

    public HashSet<Guid> IneligibleApproverIds { get; } = [];

    public Dictionary<Guid, HashSet<Capability>> UserCapabilities { get; } = [];

    public HashSet<Guid> InactiveUserIds { get; } = [];

    public List<Notification> Notifications { get; } = [];

    public List<EmailMessage> EmailMessages { get; } = [];

    public long? HistoryTotalCountOverride { get; set; }

    public Exception? NextSaveThrows { get; set; }

    public Exception? NextReadThrows { get; set; }

    public Exception? NextWorkflowReadThrows { get; set; }

    public Exception? NextApproverReadThrows { get; set; }

    public MeetingListQuery? LastListQuery { get; private set; }

    public Guid? LastListActorId { get; private set; }

    public bool LastWorkflowForUpdate { get; private set; }

    public Task<MeetingListPage> FindAuthorizedPageAsync(
        Guid actorId, MeetingListQuery query, CancellationToken ct = default)
    {
        LastListActorId = actorId;
        LastListQuery = query;
        if (NextReadThrows is { } exception)
        {
            NextReadThrows = null;
            throw exception;
        }

        var visible = Meetings.Where(m => MeetingReadPolicy.CanRead(m, actorId));
        if (query.Status is { } status) visible = visible.Where(m => m.Status == status);
        if (query.Sensitivity is { } sensitivity) visible = visible.Where(m => m.Sensitivity == sensitivity);
        if (query.From is { } from && query.To is { } to)
        {
            visible = visible.Where(m => m.Sessions.Any(s => s.Status != MeetingSessionStatus.Cancelled
                && s.PlannedEndsAt >= from && s.PlannedStartsAt <= to));
        }
        else if (query.From is { } onlyFrom)
        {
            visible = visible.Where(m => m.Sessions.Any(s => s.Status != MeetingSessionStatus.Cancelled
                && s.PlannedEndsAt >= onlyFrom));
        }
        else if (query.To is { } onlyTo)
        {
            visible = visible.Where(m => m.Sessions.Any(s => s.Status != MeetingSessionStatus.Cancelled
                && s.PlannedStartsAt <= onlyTo));
        }
        if (query.Query is { Length: > 0 } search)
        {
            visible = visible.Where(m => m.Title.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var total = visible.LongCount();
        var offset = ((long)query.Page - 1) * query.PageSize;
        if (offset >= total)
        {
            return Task.FromResult(new MeetingListPage([], query.Page, query.PageSize, total));
        }

        var ordered = (query.SortBy, query.SortDirection) switch
        {
            (MeetingSortBy.Title, MeetingSortDirection.Asc) => visible.OrderBy(m => m.Title),
            (MeetingSortBy.Title, MeetingSortDirection.Desc) => visible.OrderByDescending(m => m.Title),
            (MeetingSortBy.CreatedAt, MeetingSortDirection.Asc) => visible.OrderBy(m => m.CreatedAt),
            _ => visible.OrderByDescending(m => m.CreatedAt),
        };
        var items = ordered.ThenBy(m => m.Id)
            .Skip(checked((int)offset))
            .Take(query.PageSize)
            .Select(m => new MeetingListItem(
                m.Id, m.Title, m.CreatedAt, m.Status, m.Sensitivity))
            .ToArray();
        return Task.FromResult(new MeetingListPage(items, query.Page, query.PageSize, total));
    }

    public int SaveCount { get; private set; }

    public Task<Meeting?> FindAccessibleAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        if (NextReadThrows is { } exception)
        {
            NextReadThrows = null;
            throw exception;
        }

        return Task.FromResult(Meetings.SingleOrDefault(m => m.Id == id
            && MeetingReadPolicy.CanRead(m, actorId)));
    }

    public Task<Meeting?> FindOwnedForUpdateAsync(Guid id, Guid creatorId, CancellationToken ct = default)
        => Task.FromResult(Meetings.SingleOrDefault(m => m.Id == id && m.CreatedById == creatorId));

    public Task<Meeting?> FindOwnedAsync(Guid id, Guid creatorId, CancellationToken ct = default)
    {
        if (NextReadThrows is { } exception)
        {
            NextReadThrows = null;
            throw exception;
        }

        return Task.FromResult(Meetings.SingleOrDefault(m => m.Id == id && m.CreatedById == creatorId));
    }

    public Task<IReadOnlyList<MeetingApproverOption>> ListEligibleApproversAsync(CancellationToken ct = default)
    {
        if (NextApproverReadThrows is { } exception)
        {
            NextApproverReadThrows = null;
            throw exception;
        }

        return Task.FromResult<IReadOnlyList<MeetingApproverOption>>([.. EligibleApprovers]);
    }

    public Task<bool> IsEligibleApproverAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(id != Guid.Empty && !IneligibleApproverIds.Contains(id));

    public Task<Meeting?> FindWorkflowAsync(Guid id, bool forUpdate = false, CancellationToken ct = default)
    {
        LastWorkflowForUpdate = forUpdate;
        if (NextWorkflowReadThrows is { } exception)
        {
            NextWorkflowReadThrows = null;
            throw exception;
        }

        return Task.FromResult(Meetings.SingleOrDefault(m => m.Id == id));
    }

    public Task<MeetingHistoryPage?> FindHistoryPageAsync(
        Guid meetingId, int page, int pageSize, CancellationToken ct = default)
    {
        var meeting = Meetings.Single(m => m.Id == meetingId);
        var history = meeting.StatusHistory
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id);
        var total = HistoryTotalCountOverride ?? meeting.StatusHistory.Count;
        var offset = ((long)page - 1) * pageSize;
        if (offset >= total)
        {
            return Task.FromResult<MeetingHistoryPage?>(new MeetingHistoryPage([], page, pageSize, total));
        }

        if (offset > int.MaxValue)
        {
            return Task.FromResult<MeetingHistoryPage?>(null);
        }

        var records = history.Skip((int)offset)
            .Take(pageSize)
            .Select(item => new MeetingHistoryRecord(
                item.Id, meetingId, item.Action, item.FromStatus, item.ToStatus, item.ActorId,
                item.Actor?.FullName ?? "Test User", item.OccurredAt,
                item.Action == TrustCabinet.Domain.Enums.MeetingHistoryAction.Reject ? item.Reason : null,
                item.AssignedApproverId,
                item.PreviousOrganizerId,
                item.NewOrganizerId))
            .ToArray();
        return Task.FromResult<MeetingHistoryPage?>(new MeetingHistoryPage(
            records, page, pageSize, total));
    }

    public Task<bool> IsActiveUserWithCapabilityAsync(
        Guid userId, Capability capability, CancellationToken ct = default)
        => Task.FromResult(UserCapabilities.TryGetValue(userId, out var capabilities)
            && !InactiveUserIds.Contains(userId)
            && capabilities.Contains(capability));

    public Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(UserCapabilities.ContainsKey(userId) && !InactiveUserIds.Contains(userId));

    public Task<string?> FindUserDisplayNameAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<string?>("Test User");

    public Task AddNotificationsAsync(
        IReadOnlyCollection<Notification> notifications, CancellationToken ct = default)
    {
        _pendingNotifications.AddRange(notifications);
        return Task.CompletedTask;
    }

    public Task AddEmailMessagesAsync(
        IReadOnlyCollection<EmailMessage> messages, CancellationToken ct = default)
    {
        _pendingEmailMessages.AddRange(messages);
        return Task.CompletedTask;
    }

    public Task AddHistoryAsync(MeetingStatusHistory history, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

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
            _pendingNotifications.Clear();
            _pendingEmailMessages.Clear();
            throw exception;
        }

        Meetings.AddRange(_pendingAdds);
        _pendingAdds.Clear();
        Notifications.AddRange(_pendingNotifications);
        _pendingNotifications.Clear();
        EmailMessages.AddRange(_pendingEmailMessages);
        _pendingEmailMessages.Clear();
        return Task.CompletedTask;
    }

    public async Task<T> InTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        var snapshot = Meetings.Select(Clone).ToArray();
        var notifications = Notifications.ToArray();
        var emailMessages = EmailMessages.ToArray();
        try
        {
            return await operation(ct);
        }
        catch
        {
            Meetings.Clear();
            Meetings.AddRange(snapshot);
            Notifications.Clear();
            Notifications.AddRange(notifications);
            EmailMessages.Clear();
            EmailMessages.AddRange(emailMessages);
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
            CreatedById = meeting.CreatedById,
            AssignedApproverId = meeting.AssignedApproverId,
            Sessions = meeting.Sessions.Select(session => new MeetingSession
            {
                Id = session.Id,
                MeetingId = session.MeetingId,
                Title = session.Title,
                PlannedStartsAt = session.PlannedStartsAt,
                PlannedEndsAt = session.PlannedEndsAt,
                Status = session.Status,
                ActualStartedAt = session.ActualStartedAt,
                ActualEndedAt = session.ActualEndedAt,
            }).ToList(),
            AgendaItems = meeting.AgendaItems.Select(item => new AgendaItem
            {
                Id = item.Id,
                MeetingId = item.MeetingId,
                Position = item.Position,
                Title = item.Title,
                Description = item.Description,
                PlannedDurationMinutes = item.PlannedDurationMinutes,
                PresenterId = item.PresenterId,
                PresenterName = item.PresenterName,
                Status = item.Status,
            }).ToList(),
            Participants = meeting.Participants.Select(participant => new MeetingParticipant
            {
                Id = participant.Id,
                MeetingId = participant.MeetingId,
                UserId = participant.UserId,
                Role = participant.Role,
                Response = participant.Response,
                DelegatedToUserId = participant.DelegatedToUserId,
                User = participant.User,
            }).ToList(),
            StatusHistory = [.. meeting.StatusHistory],
        };
}
