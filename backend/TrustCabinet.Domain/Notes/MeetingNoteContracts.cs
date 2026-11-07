using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Notes;

/// <summary>
/// SCRUM-30, UC-82 — the Secretary's running notes. One note per author per agenda item, plus one for the meeting as a
/// whole (<see cref="NoteView.AgendaItemId"/> null). The text is plain; a line may start with a mark
/// (<see cref="MeetingNoteRules.Marks"/>) that the minutes draft reads (BR-401, BR-402).
/// </summary>
public sealed record NoteAuthor(Guid Id, string FullName);

public sealed record NoteView(Guid? AgendaItemId, string Body, DateTimeOffset UpdatedAt, NoteAuthor Author);

/// <summary>
/// What the caller sees: the notes of the meeting's current Secretary, the agenda item the Chair has InProgress (where
/// new text goes, BR-397), and whether the caller may write now (BR-395, BR-396). The Chair reads with
/// <see cref="CanWrite"/> false.
/// </summary>
/// <param name="Secret">A Secret meeting: unsent text stays on the device only until the page is closed (BR-403).</param>
public sealed record NotesState(IReadOnlyList<NoteView> Notes, Guid? CurrentAgendaItemId, bool CanWrite, bool Secret);

public enum NoteFailure
{
    None,
    NotFound,
    Inactive,
    /// <summary>The current Chair, who reads the notes but does not write them (BR-395, EX-1). Anyone else who is not the
    /// Secretary gets <see cref="NotFound"/> (EX-8).</summary>
    Forbidden,
    /// <summary>The meeting is not InProgress, or it has Ended and its minutes draft exists (BR-396).</summary>
    Closed,
    /// <summary>The note was saved elsewhere after the caller read it (BR-399).</summary>
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
    /// <see cref="NoteFailure.Stale"/> and nothing is overwritten (BR-399).
    /// </summary>
    Task<NoteOutcome> SaveAsync(Guid meetingId, Guid actorId, Guid? agendaItemId, string? body, DateTimeOffset? loadedAt,
        CancellationToken ct = default);
}

public static class MeetingNoteRules
{
    /// <summary>BR-400.</summary>
    public const int MaxLength = 20_000;

    public const string Opinion = "Ý kiến";
    public const string Conclusion = "Kết luận";
    public const string Task = "Nhiệm vụ";

    /// <summary>BR-401: a line starting with one of these, then ":", is marked. Text only — nothing tracks a task.</summary>
    public static readonly IReadOnlyList<string> Marks = [Opinion, Conclusion, Task];

    /// <summary>BR-396: while the sitting runs, and after it until the minutes draft is built (the cut-off of BR-310).</summary>
    public static bool AcceptsNotes(MeetingStatus status, bool hasMinutes)
        => status == MeetingStatus.InProgress || (status == MeetingStatus.Ended && !hasMinutes);

    /// <summary>The mark of a line and the text after it, or null for an unmarked line.</summary>
    public static (string Mark, string Text)? Marked(string line)
    {
        var trimmed = line.TrimStart();
        foreach (var mark in Marks)
            if (trimmed.StartsWith(mark, StringComparison.OrdinalIgnoreCase)
                && trimmed.Length > mark.Length && trimmed[mark.Length] == ':')
                return (mark, trimmed[(mark.Length + 1)..].Trim());
        return null;
    }

    /// <summary>
    /// BR-397: what a deleted agenda item's note becomes, appended to its author's meeting-wide note so the text is not
    /// lost. Cut to <see cref="MaxLength"/> when the two together would not fit.
    /// </summary>
    public static string Fold(string general, string itemTitle, string itemBody)
    {
        var folded = string.IsNullOrWhiteSpace(general)
            ? $"{itemTitle}:\n{itemBody}"
            : $"{general.TrimEnd()}\n\n{itemTitle}:\n{itemBody}";
        return folded.Length > MaxLength ? folded[..MaxLength] : folded;
    }

    public static NoteView View(MeetingNote note) => new(note.AgendaItemId, note.Body, note.UpdatedAt,
        new(note.Author.Id, note.Author.FullName));
}
