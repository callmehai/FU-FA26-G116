namespace TrustCabinet.Domain.Summaries;

/// <summary>
/// SCRUM-32 — the draft minutes. The content is HTML kept to a fixed allow-list (ADR-0055, MinutesHtml in the
/// infrastructure): paragraphs, three heading levels, lists, tables, rules and page breaks, bold, italic, underline,
/// strike-through, sub- and superscript, and a few styles (alignment, indents, spacing, font size, a palette of text and
/// highlight colours). The server sanitizes every write, so nothing in it can run in a browser; rows written before as
/// line-marked text are converted to HTML on read. "……" marks a blank the Secretary fills.
/// </summary>
public sealed record MinutesPerson(Guid Id, string FullName);

/// <summary>
/// <see cref="BuiltFromDraftAt"/>: the <see cref="MinutesState.DraftChangedAt"/> of the draft the content was last built
/// from, null when built without one. When the two differ, the draft has moved on since and the minutes do not show it.
/// </summary>
public sealed record MinutesView(Guid Id, int RevisionNumber, string Status, string Content, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, MinutesPerson? CreatedBy, DateTimeOffset? BuiltFromDraftAt);

/// <summary>
/// What the caller sees. A draft is the Chair's and the Secretary's until it is issued (SCRUM-33); everyone else gets
/// null. <see cref="DraftChangedAt"/> is when the AI draft that fills sections I–III last changed (written or edited),
/// null when the meeting has none, and for anyone but the Chair and the Secretary.
/// </summary>
public sealed record MinutesState(MinutesView? Minutes, bool CanManage, DateTimeOffset? DraftChangedAt);

public enum MinutesFailure
{
    None,
    NotFound,
    Inactive,
    Forbidden,
    WrongMeetingStatus,
    /// <summary>Minutes exist already and the caller did not ask to rebuild them.</summary>
    Exists,
    /// <summary>The minutes went past Draft (sent for approval or issued): they are no longer edited here.</summary>
    Locked,
    /// <summary>Someone saved the minutes after the caller loaded them.</summary>
    Stale,
    Invalid,
    /// <summary>A Secret meeting's minutes are read on screen only (BR-188), as its documents and transcripts are.</summary>
    SecretMeeting,
    ReadFailed,
    SaveFailed,
}

public sealed record MinutesOutcome(MinutesFailure Failure = MinutesFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public interface IMinutesService
{
    Task<MinutesOutcome> GetAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    /// <summary>
    /// Builds the draft from the meeting's records and its reviewed AI draft. With <paramref name="rebuild"/> it replaces
    /// a draft that exists, which throws away the Secretary's edits; the page asks first.
    /// </summary>
    Task<MinutesOutcome> CreateAsync(Guid meetingId, Guid actorId, bool rebuild, CancellationToken ct = default);
    Task<MinutesOutcome> SaveAsync(Guid minutesId, Guid actorId, string? content, DateTimeOffset? loadedAt, CancellationToken ct = default);
    /// <summary>The draft as a Word or a PDF file, drawn from the same HTML the page shows.</summary>
    Task<MinutesOutcome> ExportAsync(Guid minutesId, Guid actorId, MinutesExportFormat format, CancellationToken ct = default);
}

/// <summary>What <c>GET /api/minutes/{id}/export?format=</c> returns: Word unless PDF is asked for.</summary>
public enum MinutesExportFormat
{
    Docx,
    Pdf,
}

public sealed record MinutesFile(string FileName, byte[] Bytes, string ContentType);

public static class MinutesLimits
{
    /// <summary>Characters of HTML, as sent and as stored: HTML is wordier than the text it carries.</summary>
    public const int Content = 300_000;
}
