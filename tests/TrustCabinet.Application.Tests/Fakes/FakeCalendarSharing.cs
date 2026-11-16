using TrustCabinet.Application.CalendarSharing;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// Calendar shares and their people in memory. Each read filters <see cref="Shares"/> the way the real query does; a
/// lock is only recorded in <see cref="Locks"/>, by name.
/// </summary>
public sealed class FakeCalendarShareRepository : ICalendarShareRepository
{
    public Dictionary<Guid, PersonState> People { get; } = [];
    public List<CalendarShare> Shares { get; } = [];
    public List<CalendarShare> Added { get; } = [];

    /// <summary>Every lock taken, in order: <c>people</c>, <c>share</c>, <c>lapsed</c>, <c>effective</c>.</summary>
    public List<string> Locks { get; } = [];

    /// <summary>Thrown by every call but <see cref="Add"/>, in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    /// <summary>Adds a person and returns their id.</summary>
    public Guid Person(string name, bool organizer = true, bool active = true)
    {
        var id = Guid.CreateVersion7();
        People[id] = new PersonState(id, name, $"{id}@trustcabinet.local", active, organizer);
        return id;
    }

    /// <summary>Adds a share between two people of <see cref="People"/>, with both loaded.</summary>
    public CalendarShare Share(Guid ownerId, Guid viewerId, CalendarShareStatus status, DateOnly start, DateOnly end,
        CalendarShareInitiator initiatedBy = CalendarShareInitiator.Viewer,
        CalendarShareLevel level = CalendarShareLevel.Level1)
    {
        var share = new CalendarShare
        {
            OwnerId = ownerId, Owner = UserOf(ownerId), ViewerId = viewerId, Viewer = UserOf(viewerId),
            InitiatedBy = initiatedBy, Level = level, StartDate = start, EndDate = end, Status = status,
        };
        Shares.Add(share);
        return share;
    }

    private User UserOf(Guid id)
        => new() { Id = id, FullName = People[id].FullName, Email = People[id].Email, IsActive = People[id].IsActive };

    public Task<bool> IsOrganizerAsync(Guid userId, CancellationToken ct = default)
        => Read(() => People.TryGetValue(userId, out var p) && p.IsActive && p.IsOrganizer);

    public Task<List<CalendarShare>> FindListedAsync(Guid userId, bool includeViewed, DateOnly today,
        CancellationToken ct = default)
        => Read(() => Shares.Where(s => (s.OwnerId == userId || (includeViewed && s.ViewerId == userId)) && s.EndDate >= today)
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id).ToList());

    public Task<List<Guid>> FindMeetingCreatorIdsAsync(Guid[] userIds, CancellationToken ct = default)
        => Read(() => userIds.Where(id => People.TryGetValue(id, out var p) && p.IsOrganizer).ToList());

    public Task<ShareCandidatePage> FindCandidatesAsync(Guid userId, CandidateQuery query, DateOnly today,
        CancellationToken ct = default)
        => Read(() =>
        {
            var people = People.Values.Where(p => p.IsActive && p.Id != userId
                && (query.Purpose == CandidatePurpose.Request || p.IsOrganizer)).ToList();
            return new ShareCandidatePage(
                [.. people.Select(p => new ShareCandidate(p.Id, p.FullName, p.Email, false))], people.Count, query.Page,
                query.PageSize);
        });

    public Task<Dictionary<Guid, PersonState>> LoadPeopleAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
        => Read(() => userIds.Distinct().Where(People.ContainsKey).ToDictionary(id => id, id => People[id]));

    public Task<Dictionary<Guid, PersonState>> LockPeopleAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        Locks.Add("people");
        return LoadPeopleAsync(userIds, ct);
    }

    public Task<CalendarShare?> LockAsync(Guid shareId, CancellationToken ct = default)
    {
        Locks.Add("share");
        return Read(() => Shares.SingleOrDefault(s => s.Id == shareId));
    }

    public Task<List<CalendarShare>> LockLapsedOpenAsync(Guid ownerId, Guid viewerId, DateOnly today,
        CancellationToken ct = default)
    {
        Locks.Add("lapsed");
        return Read(() => Shares.Where(s => s.OwnerId == ownerId && s.ViewerId == viewerId && IsOpen(s) && s.EndDate < today)
            .ToList());
    }

    public Task<List<CalendarShare>> LockEffectiveOfPeopleAsync(Guid[] userIds, DateOnly today,
        CancellationToken ct = default)
    {
        Locks.Add("effective");
        return Read(() => Shares.Where(s => s.Status == CalendarShareStatus.Effective && s.EndDate >= today
                && (userIds.Contains(s.OwnerId) || userIds.Contains(s.ViewerId)))
            .OrderBy(s => s.Id).ToList());
    }

    public Task<bool> HasOpenShareAsync(Guid ownerId, Guid viewerId, CancellationToken ct = default)
        => Read(() => Shares.Any(s => s.OwnerId == ownerId && s.ViewerId == viewerId && IsOpen(s)));

    public Task<List<CalendarShare>> FindSharedWithAsync(Guid viewerId, DateOnly today, CancellationToken ct = default)
        => Read(() => Shares.Where(s => s.ViewerId == viewerId && s.EndDate >= today
            && s.Status is CalendarShareStatus.Effective or CalendarShareStatus.Stopped).ToList());

    public Task<List<CalendarShare>> FindEffectiveFromAsync(Guid viewerId, Guid[] ownerIds, DateOnly today,
        CancellationToken ct = default)
        => Read(() => Shares.Where(s => s.ViewerId == viewerId && ownerIds.Contains(s.OwnerId)
            && s.Status == CalendarShareStatus.Effective && s.EndDate >= today && People[s.OwnerId].IsActive).ToList());

    public Task<Dictionary<Guid, Invitee>> FindInviteesAsync(Guid[] userIds, CancellationToken ct = default)
        => Read(() => userIds.Distinct().Where(People.ContainsKey)
            .ToDictionary(id => id, id => new Invitee(id, People[id].FullName, People[id].Email, People[id].IsActive)));

    public void Add(CalendarShare share) => Added.Add(share);

    private static bool IsOpen(CalendarShare share)
        => share.Status is CalendarShareStatus.Pending or CalendarShareStatus.Effective;

    private Task<T> Read<T>(Func<T> answer) => Fail is { } failure ? Task.FromException<T>(failure) : Task.FromResult(answer());
}

/// <summary>Busy places in a list, filtered by person only; the range each call asked for is kept in <see cref="Ranges"/>.</summary>
public sealed class FakeCalendarBusyQuery : ICalendarBusyQuery
{
    public List<BusyAssignment> Rows { get; } = [];
    public List<(DateTimeOffset From, DateTimeOffset To)> Ranges { get; } = [];

    public Task<List<BusyAssignment>> FindAsync(Guid[] userIds, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default)
    {
        Ranges.Add((from, to));
        return Task.FromResult(Rows.Where(r => userIds.Contains(r.UserId)).ToList());
    }
}
