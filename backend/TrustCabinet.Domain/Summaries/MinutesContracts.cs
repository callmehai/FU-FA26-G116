namespace TrustCabinet.Domain.Summaries;

/// <summary>
/// SCRUM-32 — the draft minutes. The text is plain, with a few marks the page and the Word export read (never HTML, so
/// nothing in it can run in a browser):
/// <list type="bullet">
/// <item><c>@@ left | right</c> — a row of two columns; consecutive rows form one borderless table (the header, the
/// signature blocks).</item>
/// <item><c># </c> — the centred title ("BIÊN BẢN"); <c>^ </c> — a centred line.</item>
/// <item><c>## </c> — a section heading (I, II, III); <c>### </c> — a bold sub-heading.</item>
/// <item><c>- </c> — a listed line; <c>~ </c> — a small line flush left ("Nơi nhận").</item>
/// <item>Inside any line, <c>**bold**</c> and <c>__italic__</c>.</item>
/// </list>
/// Anything else is a justified paragraph. "……" marks a blank the Secretary fills.
/// </summary>
public sealed record MinutesPerson(Guid Id, string FullName);

public sealed record MinutesView(Guid Id, int RevisionNumber, string Status, string Content, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, MinutesPerson? CreatedBy);

/// <summary>
/// What the caller sees. A draft is the Chair's and the Secretary's until it is issued (SCRUM-33); everyone else gets
/// null. <see cref="SummaryReviewed"/> says whether the reviewed AI draft can fill sections I–III.
/// </summary>
public sealed record MinutesState(MinutesView? Minutes, bool CanManage, bool SummaryReviewed);

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
    /// <summary>The draft as a Word file, built from the same marks the page reads.</summary>
    Task<MinutesOutcome> ExportAsync(Guid minutesId, Guid actorId, CancellationToken ct = default);
}

public sealed record MinutesFile(string FileName, byte[] Bytes);

public static class MinutesLimits
{
    public const int Content = 100_000;
}
