namespace TrustCabinet.Domain.Summaries;

/// <summary>
/// SCRUM-36 — the AI draft of a meeting's content, AI-eCabinet's "Dự thảo": three plain-text sections under the headings
/// of the minutes template (Decree 30's Mẫu 1.4), which the Chair or the Secretary edits as text and which fill the
/// minutes once reviewed. Stored as JSON in <c>meeting_summaries.content</c>.
/// <list type="bullet">
/// <item><see cref="Objectives"/> — I. Mục tiêu và phạm vi cuộc họp.</item>
/// <item><see cref="Discussion"/> — II. Nội dung thảo luận và đánh giá: one "1. &lt;nội dung&gt;" part per agenda item.</item>
/// <item><see cref="Conclusions"/> — III. Kết luận và phân công thực hiện: the Chair's conclusions, the vote results
/// (copied from the data, never written by the model) and the tasks as said in the meeting. Only words in the minutes:
/// TrustCabinet tracks no task.</item>
/// </list>
/// </summary>
public sealed record SummaryDraft(string Objectives, string Discussion, string Conclusions, IReadOnlyList<SummarySource> Sources);

/// <summary>
/// Where part of section II came from: the stretch of the meeting between the moment its agenda item was started and
/// the moment it was closed, so the Secretary can check it against the transcript (SCRUM-36: "dẫn chiếu tới đoạn tương
/// ứng trong bản gỡ băng").
/// </summary>
public sealed record SummarySource(Guid? AgendaItemId, string Title, DateTimeOffset? From, DateTimeOffset? To, int Lines);

/// <summary>The Chair's or the Secretary's corrections to the three sections.</summary>
public sealed record SummaryEdit(string? Objectives, string? Discussion, string? Conclusions);

/// <summary>
/// What could make a draft wrong, shown before asking for one (AI-eCabinet's minutes readiness): voices nobody named yet
/// read as "Người nói N", and recordings still being transcribed or failed are left out.
/// </summary>
public sealed record SummaryReadiness(int Sentences, int UnnamedVoices, int Transcribing, int FailedTranscripts,
    int AgendaItems, int AgendaItemsTimed, int ClosedVotes);

public sealed record SummaryPerson(Guid Id, string FullName);

public sealed record SummaryView(string Status, SummaryDraft? Draft, string Model, DateTimeOffset? GeneratedAt,
    DateTimeOffset RequestedAt, SummaryPerson? RequestedBy, string ErrorMessage, SummaryPerson? ReviewedBy,
    DateTimeOffset? ReviewedAt, SummaryPerson? EditedBy, DateTimeOffset? EditedAt);

/// <summary>
/// What the caller sees. <see cref="Summary"/> is null when there is none yet, and for someone who is not the Chair or
/// the Secretary until the draft is reviewed: an unreviewed AI text never reaches the other participants.
/// </summary>
public sealed record SummaryState(SummaryView? Summary, bool CanManage, bool AiReady, SummaryReadiness? Readiness);

public enum SummaryFailure
{
    None,
    NotFound,
    Inactive,
    Forbidden,
    /// <summary>The meeting has not started, or was cancelled: there is nothing to draft from yet.</summary>
    WrongMeetingStatus,
    /// <summary>A draft is already queued or being written.</summary>
    Busy,
    /// <summary>No finished draft to edit or review.</summary>
    NotDrafted,
    Invalid,
    /// <summary>No AI server is configured, so a request would only fail later.</summary>
    AiUnavailable,
    ReadFailed,
    SaveFailed,
}

public sealed record SummaryOutcome(SummaryFailure Failure = SummaryFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public interface ISummaryService
{
    Task<SummaryOutcome> GetAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    /// <summary>Queues a draft, or a new one in place of the last; the last stays readable until the new one is done.</summary>
    Task<SummaryOutcome> RequestAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    Task<SummaryOutcome> EditAsync(Guid meetingId, Guid actorId, SummaryEdit edit, CancellationToken ct = default);
    /// <summary>The Chair or the Secretary vouches for the draft: the other participants may read it from now on.</summary>
    Task<SummaryOutcome> ReviewAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    /// <summary>Writes the oldest queued draft; false when there was none.</summary>
    Task<bool> ProcessNextAsync(CancellationToken ct = default);
    Task<int> RequeueInterruptedAsync(CancellationToken ct = default);
}

public static class SummaryLimits
{
    public const int Objectives = 2000;
    public const int Discussion = 12000;
    public const int Conclusions = 8000;
}
