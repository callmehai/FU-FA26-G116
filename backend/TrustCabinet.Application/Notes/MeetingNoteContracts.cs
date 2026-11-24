using System.Text;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Notes;

/// <summary>
/// SCRUM-30, UC-28 — the Secretary's running notes. One note per author per agenda item, plus one for the meeting as a
/// whole (<see cref="NoteView.AgendaItemId"/> null). The text is plain; a line may start with a mark
/// (<see cref="MeetingNoteRules.Marks"/>) that the minutes draft reads (UC-28, UC-70).
/// </summary>
public sealed record NoteAuthor(Guid Id, string FullName);

public sealed record NoteView(Guid? AgendaItemId, string Body, DateTimeOffset UpdatedAt, NoteAuthor Author);

/// <summary>
/// What the caller sees: the notes of the meeting's current Secretary, the agenda item the Chair has InProgress (where
/// new text goes, BR-134), and whether the caller may write now (UC-55b, UC-60, UC-61). The Chair reads with
/// <see cref="CanWrite"/> false.
/// </summary>
/// <param name="Secret">A Secret meeting: unsent text stays on the device only until the page is closed (UC-28).</param>
public sealed record NotesState(IReadOnlyList<NoteView> Notes, Guid? CurrentAgendaItemId, bool CanWrite, bool Secret);

public enum NoteFailure
{
    None,
    NotFound,
    Inactive,
    /// <summary>A save by a reader of the meeting who is not its current Secretary: the Chair, or a Secretary replaced
    /// while typing (UC-55b, EX-1). Reading the notes is <see cref="NotFound"/> for anyone but the Chair (EX-8).</summary>
    Forbidden,
    /// <summary>The meeting is not InProgress, or it has Ended and its minutes draft exists (UC-60, UC-61).</summary>
    Closed,
    /// <summary>The note was saved elsewhere after the caller read it (UC-28).</summary>
    Stale,
    TooLong,
    /// <summary>The agenda item is not (or no longer) part of the meeting.</summary>
    ItemGone,
    ReadFailed,
    SaveFailed,
}

public sealed record NoteOutcome(NoteFailure Failure = NoteFailure.None, NotesState? State = null, NoteView? Note = null);

/// <summary>Only says which note changed: the Chair's and the Secretary's other windows refetch, which is reauthorized.</summary>
public sealed record NotesChange(Guid EventId, Guid MeetingId, Guid? AgendaItemId, DateTimeOffset UpdatedAt);

public interface INotesChangePublisher
{
    Task PublishAsync(NotesChange change, CancellationToken ct);
}

public interface IMeetingNoteService
{
    Task<NoteOutcome> GetAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Saves the caller's note on <paramref name="agendaItemId"/> (null: the meeting as a whole). <paramref name="loadedAt"/>
    /// is the <c>updatedAt</c> the caller read, null for a note it has not seen saved yet; a mismatch is
    /// <see cref="NoteFailure.Stale"/> and nothing is overwritten (UC-28).
    /// </summary>
    Task<NoteOutcome> SaveAsync(Guid meetingId, Guid actorId, Guid? agendaItemId, string? body, DateTimeOffset? loadedAt,
        CancellationToken ct = default);
}

/// <summary>SCRUM-30, UC-28 — the Secretary's running notes as the use case reads and writes them.</summary>
public interface IMeetingNoteRepository
{
    /// <summary>The meeting's notes by the given authors, oldest position first, with their author.</summary>
    Task<IReadOnlyList<MeetingNote>> ListAsync(Guid meetingId, HashSet<Guid> authorIds, CancellationToken ct);

    /// <summary>The meeting's item in progress; null when none is.</summary>
    Task<Guid?> FindCurrentItemAsync(Guid meetingId, CancellationToken ct);

    /// <summary>Whether the meeting's minutes draft exists (the cut-off of BR-066, BR-067, UC-60, UC-61).</summary>
    Task<bool> HasMinutesAsync(Guid meetingId, CancellationToken ct);

    /// <summary>Whether the agenda item is part of the meeting.</summary>
    Task<bool> AgendaItemExistsAsync(Guid meetingId, Guid agendaItemId, CancellationToken ct);

    /// <summary>The caller's note id; null when the caller has never saved one here.</summary>
    Task<Guid?> FindIdAsync(Guid meetingId, Guid? agendaItemId, Guid authorId, CancellationToken ct);

    /// <summary>Stages the note; written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void Add(MeetingNote note);

    /// <summary>
    /// Overwrites the note read at <paramref name="from"/> (UC-28: a save from another window that landed
    /// after it was read makes it stale, never overwritten). Returns the rows written. Runs immediately,
    /// not staged.
    /// </summary>
    Task<int> TryUpdateAsync(Guid id, DateTimeOffset from, DateTimeOffset to, string body, DateTimeOffset now,
        CancellationToken ct);

    /// <summary>The saved note with its author, read back under the meeting lock; throws when it does not exist.</summary>
    Task<MeetingNote> GetAsync(Guid meetingId, Guid? agendaItemId, Guid authorId, CancellationToken ct);
}

public static class MeetingNoteRules
{
    /// <summary>UC-28.</summary>
    public const int MaxLength = 20_000;

    public const string Opinion = "Ý kiến";
    public const string Conclusion = "Kết luận";
    public const string Task = "Nhiệm vụ";

    /// <summary>UC-28: a line starting with one of these, then ":", is marked. Text only — nothing tracks a task.</summary>
    public static readonly IReadOnlyList<string> Marks = [Opinion, Conclusion, Task];

    /// <summary>UC-60, UC-61: while the sitting runs, and after it until the minutes draft is built (the cut-off of BR-066, BR-067).</summary>
    public static bool AcceptsNotes(MeetingStatus status, bool hasMinutes)
        => status == MeetingStatus.InProgress || (status == MeetingStatus.Ended && !hasMinutes);

    /// <summary>The mark of a line and the text after it, or null for an unmarked line.</summary>
    public static (string Mark, string Text)? Marked(string line)
    {
        // NFC: a mark typed with combining accents (some Vietnamese keyboards) is the same mark. "Kết luận :" counts too.
        var trimmed = line.Normalize(NormalizationForm.FormC).TrimStart();
        foreach (var mark in Marks)
        {
            if (!trimmed.StartsWith(mark, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = trimmed[mark.Length..].TrimStart(' ', '\t');
            if (rest.StartsWith(':')) return (mark, rest[1..].Trim());
        }
        return null;
    }

    /// <summary>
    /// BR-134: what a deleted agenda item's note becomes, appended to its author's meeting-wide note so the text is not
    /// lost. Cut to <see cref="MaxLength"/> when the two together would not fit, never inside a surrogate pair.
    /// </summary>
    public static string Fold(string general, string itemTitle, string itemBody)
    {
        var folded = string.IsNullOrWhiteSpace(general)
            ? $"{itemTitle}:\n{itemBody}"
            : $"{general.TrimEnd()}\n\n{itemTitle}:\n{itemBody}";
        if (folded.Length <= MaxLength) return folded;
        var cut = char.IsHighSurrogate(folded[MaxLength - 1]) ? MaxLength - 1 : MaxLength;
        return folded[..cut];
    }

    public static NoteView View(MeetingNote note) => new(note.AgendaItemId, note.Body, note.UpdatedAt,
        new(note.Author.Id, note.Author.FullName));
}
