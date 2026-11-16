using TrustCabinet.Application.Notes;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>The meeting notes in a list, as the use case reads and stages them. Nothing here saves.</summary>
public sealed class FakeMeetingNoteRepository : IMeetingNoteRepository
{
    /// <summary>Thrown by every call in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    public List<MeetingNote> Notes { get; } = [];

    public Guid? CurrentItemId { get; set; }

    public bool MinutesExist { get; set; }

    public HashSet<(Guid MeetingId, Guid ItemId)> AgendaItems { get; } = [];

    /// <summary>Rows the conditional update wrote, in order.</summary>
    public List<Guid> Updated { get; } = [];

    public List<string> Calls { get; } = [];

    private T Read<T>(string name, Func<T> answer)
    {
        Calls.Add(name);
        return Fail is { } failure ? throw failure : answer();
    }

    public Task<IReadOnlyList<MeetingNote>> ListAsync(Guid meetingId, HashSet<Guid> authorIds,
        CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MeetingNote>>(Read("list",
            () => Notes.Where(n => n.MeetingId == meetingId && authorIds.Contains(n.AuthorId)).ToList()));

    public Task<Guid?> FindCurrentItemAsync(Guid meetingId, CancellationToken ct)
        => Task.FromResult(Read("current-item", () => CurrentItemId));

    public Task<bool> HasMinutesAsync(Guid meetingId, CancellationToken ct)
        => Task.FromResult(Read("has-minutes", () => MinutesExist));

    public Task<bool> AgendaItemExistsAsync(Guid meetingId, Guid agendaItemId, CancellationToken ct)
        => Task.FromResult(Read("item-exists", () => AgendaItems.Contains((meetingId, agendaItemId))));

    public Task<Guid?> FindIdAsync(Guid meetingId, Guid? agendaItemId, Guid authorId, CancellationToken ct)
        => Task.FromResult(Read("find-id", () => (Guid?)Notes
            .SingleOrDefault(n => n.MeetingId == meetingId && n.AgendaItemId == agendaItemId
                && n.AuthorId == authorId)?.Id));

    public void Add(MeetingNote note)
    {
        if (Fail is { } failure) throw failure;
        Calls.Add("add");
        // As the read-back with its author: the use case maps the saved note straight away.
        note.Author ??= new User { Id = note.AuthorId, FullName = "Tác giả", Email = "Tác giả@trustcabinet.local", IsActive = true };
        Notes.Add(note);
    }

    public Task<int> TryUpdateAsync(Guid id, DateTimeOffset from, DateTimeOffset to, string body, DateTimeOffset now,
        CancellationToken ct)
        => Task.FromResult(Read("try-update", () =>
        {
            var note = Notes.SingleOrDefault(n => n.Id == id && n.UpdatedAt >= from && n.UpdatedAt <= to);
            if (note is null) return 0;
            note.Body = body;
            note.UpdatedAt = now;
            Updated.Add(id);
            return 1;
        }));

    public Task<MeetingNote> GetAsync(Guid meetingId, Guid? agendaItemId, Guid authorId, CancellationToken ct)
        => Task.FromResult(Read("get", () => Notes
            .Single(n => n.MeetingId == meetingId && n.AgendaItemId == agendaItemId && n.AuthorId == authorId)));
}

/// <summary>What the notes use case publishes outside its transaction, recorded with the commit count.</summary>
public sealed class FakeNotesChangePublisher(FakeUnitOfWork unitOfWork) : INotesChangePublisher
{
    public List<string> Calls { get; } = [];

    public List<NotesChange> Published { get; } = [];

    public Task PublishAsync(NotesChange change, CancellationToken ct)
    {
        Calls.Add($"publish:commits={unitOfWork.Commits}");
        Published.Add(change);
        return Task.CompletedTask;
    }
}
