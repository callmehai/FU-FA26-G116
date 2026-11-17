using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// Agenda items, guests and participants in lists. What the services stage or lock is recorded; nothing here
/// saves. It cannot tell whether a transaction is open, so the "lock needs the caller's transaction" guard of the
/// real <c>LockMeetingAsync</c> is left to the integration tests.
/// </summary>
public sealed class FakeAgendaRepository : IAgendaRepository
{
    public List<Meeting> Meetings { get; } = [];
    public List<AgendaItem> Items { get; } = [];
    public List<MeetingGuestInvitation> Guests { get; } = [];
    public List<MeetingParticipant> Participations { get; } = [];
    public bool HasMinutes { get; set; }

    public List<AgendaItem> Added { get; } = [];
    public List<AgendaItem> Removed { get; } = [];

    /// <summary>Meetings locked by <see cref="LockMeetingAsync"/>, in order.</summary>
    public List<Guid> Locked { get; } = [];

    /// <summary>Thrown by every read in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    public Task<IReadOnlyList<AgendaItem>> ListAsync(Guid meetingId, bool tracked, CancellationToken ct)
        => Read<IReadOnlyList<AgendaItem>>(() => Items.Where(i => i.MeetingId == meetingId)
            .OrderBy(i => i.Position).ThenBy(i => i.Id).ToList());

    public Task<IReadOnlyList<MeetingGuestInvitation>> CurrentGuestsAsync(Guid meetingId, CancellationToken ct)
        => Read<IReadOnlyList<MeetingGuestInvitation>>(() =>
            Guests.Where(g => g.MeetingId == meetingId && g.RevokedAt == null).ToList());

    public Task<bool> HasMinutesAsync(Guid meetingId, CancellationToken ct) => Read(() => HasMinutes);

    public void Add(AgendaItem item)
    {
        Added.Add(item);
        Items.Add(item);
    }

    public Task RemoveAsync(AgendaItem item, DateTimeOffset now, CancellationToken ct)
        => Read(() =>
        {
            Removed.Add(item);
            return Items.Remove(item);
        });

    public Task<Meeting?> LockMeetingAsync(Guid meetingId, CancellationToken ct)
        => Read(() =>
        {
            Locked.Add(meetingId);
            return Meetings.SingleOrDefault(m => m.Id == meetingId);
        });

    public Task<AgendaItem?> FindCurrentAsync(Guid meetingId, CancellationToken ct)
        => Read(() => Items.SingleOrDefault(i => i.MeetingId == meetingId && i.Status == AgendaItemStatus.InProgress));

    public Task<AgendaItem?> FindAsync(Guid meetingId, Guid itemId, CancellationToken ct)
        => Read(() => Items.SingleOrDefault(i => i.Id == itemId && i.MeetingId == meetingId));

    public Task<IReadOnlyList<MeetingParticipant>> ListParticipationsAsync(Guid meetingId, Guid userId, CancellationToken ct)
        => Read<IReadOnlyList<MeetingParticipant>>(() =>
            Participations.Where(p => p.MeetingId == meetingId && p.UserId == userId).ToList());

    public Task<MeetingGuestInvitation?> FindCurrentGuestAsync(Guid meetingId, Guid invitationId, CancellationToken ct)
        => Read(() => Guests.SingleOrDefault(g => g.Id == invitationId && g.MeetingId == meetingId && g.RevokedAt == null));

    /// <summary>The name <see cref="GetUserFullNameAsync"/> answers for any account.</summary>
    public string UserFullName { get; set; } = "Trần Tổ Chức";

    public Task<string> GetUserFullNameAsync(Guid userId, CancellationToken ct) => Read(() => UserFullName);

    private Task<T> Read<T>(Func<T> answer) => Fail is { } failure ? Task.FromException<T>(failure) : Task.FromResult(answer());
}

/// <summary>The meeting's documents as the agenda sees them; links made are recorded.</summary>
public sealed class FakeAgendaDocumentRepository : IAgendaDocumentRepository
{
    public List<AgendaDocumentOption> Documents { get; } = [];
    public bool CanAssociate { get; set; } = true;
    public List<(Guid ItemId, IReadOnlyList<Guid> DocumentIds)> Associated { get; } = [];

    public Task<IReadOnlyList<AgendaDocumentOption>> ListAsync(Guid meetingId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<AgendaDocumentOption>>(Documents);

    public Task<bool> CanAssociateAsync(Guid meetingId, Guid? itemId, IReadOnlyList<Guid> documentIds, CancellationToken ct)
        => Task.FromResult(CanAssociate);

    public Task AssociateAsync(Guid meetingId, Guid itemId, IReadOnlyList<Guid> documentIds, CancellationToken ct)
    {
        Associated.Add((itemId, documentIds));
        return Task.CompletedTask;
    }
}

/// <summary>
/// What AgendaService does outside its transaction (the realtime event, the presenter notice) and the guest check,
/// recorded in one ordered list so a test can see each ran after the commit.
/// </summary>
public sealed class FakeAgendaEffects(FakeUnitOfWork unitOfWork) : IAgendaChangePublisher, IAgendaPresenterNotifier,
    IAgendaGuestAccess
{
    public List<string> Calls { get; } = [];
    public List<AgendaChange> Published { get; } = [];
    public List<AgendaPresenterAssignment> Assignments { get; } = [];
    public HashSet<Guid> GuestSessionsAllowed { get; } = [];

    public Task PublishAsync(AgendaChange change, CancellationToken ct)
    {
        Calls.Add($"publish:{change.ChangeType}:commits={unitOfWork.Commits}");
        Published.Add(change);
        return Task.CompletedTask;
    }

    public Task NotifyAsync(AgendaPresenterAssignment assignment, CancellationToken ct)
    {
        Calls.Add($"notify:commits={unitOfWork.Commits}");
        Assignments.Add(assignment);
        return Task.CompletedTask;
    }

    public Task<bool> CanReadAsync(MeetingGuestSession session, CancellationToken ct)
        => Task.FromResult(GuestSessionsAllowed.Contains(session.SessionId));

    public Task<IReadOnlyList<MeetingGuestSession>> ListReadersAsync(Guid meetingId, CancellationToken ct)
        => throw new NotSupportedException();
}

/// <summary>
/// A scope factory over fixed instances, for a use case that opens a scope of its own. It counts the scopes opened
/// and disposed; an unknown type resolves to null, so <c>GetRequiredService</c> throws as the real one does.
/// </summary>
public sealed class FakeServiceScopeFactory(params object[] services) : IServiceScopeFactory
{
    public int Created { get; private set; }
    public int Disposed { get; private set; }

    public IServiceScope CreateScope()
    {
        Created++;
        return new Scope(this);
    }

    private sealed class Scope(FakeServiceScopeFactory factory)
        : IServiceScope, IAsyncDisposable, IServiceProvider
    {
        public IServiceProvider ServiceProvider => this;

        public object? GetService(Type serviceType) => factory.Find(serviceType);

        public void Dispose() => factory.Disposed++;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private object? Find(Type serviceType) => services.FirstOrDefault(serviceType.IsInstanceOfType);
}
