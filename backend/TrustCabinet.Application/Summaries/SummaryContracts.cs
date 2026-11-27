using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Summaries;

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
/// <param name="TranscriptChangedAt">
/// The latest change to what a draft draws on — a clip or a transcript landing, a line corrected or left out, a voice
/// named — as AI-eCabinet's <c>transcript_changed_at</c>: a draft made before it is out of date. Null without any.
/// </param>
public sealed record SummaryReadiness(int Sentences, int UnnamedVoices, int Transcribing, int FailedTranscripts,
    int AgendaItems, int AgendaItemsTimed, int ClosedVotes, DateTimeOffset? TranscriptChangedAt);

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
    /// <summary>The meeting has not ended (Khánh 19/11/2026): a draft summarises a whole meeting, not half of one.</summary>
    WrongMeetingStatus,
    /// <summary>Not one sentence recorded or transcribed: the AI would have nothing said to summarise.</summary>
    NothingSaid,
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

/// <summary>The AI draft of each meeting: at most one row a meeting, <c>meeting_summaries</c>.</summary>
public interface IMeetingSummaryRepository
{
    /// <summary>The meeting's row, tracked when <paramref name="tracked"/>; null when there is none.</summary>
    Task<MeetingSummary?> FindByMeetingAsync(Guid meetingId, bool tracked, CancellationToken ct);

    /// <summary>The meeting's row untracked, with who asked for, reviewed and edited it; null when there is none.</summary>
    Task<MeetingSummary?> FindWithPeopleAsync(Guid meetingId, CancellationToken ct);

    void Add(MeetingSummary summary);

    /// <summary>
    /// Stores the Chair's or the Secretary's corrections and clears the review, unless the row is Queued or Running.
    /// Runs immediately, not staged; returns the rows changed (0 when the AI writes it again).
    /// </summary>
    Task<int> SaveEditAsync(Guid id, string content, Guid actorId, DateTimeOffset now, CancellationToken ct);

    /// <summary>Marks the draft reviewed, unless the row is Queued or Running. Runs immediately, not staged; returns the rows changed.</summary>
    Task<int> MarkReviewedAsync(Guid id, Guid actorId, DateTimeOffset now, CancellationToken ct);

    /// <summary>The oldest Queued draft, untracked; null when none waits.</summary>
    Task<QueuedSummary?> FindNextQueuedAsync(CancellationToken ct);

    /// <summary>Turns the row from Queued to Running, so only one worker writes it. Runs immediately, not staged; returns the rows changed.</summary>
    Task<int> ClaimAsync(Guid id, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Replaces the draft whole and marks it Ready, clearing the review and the edit. Runs immediately, not staged; returns
    /// the rows changed.
    /// </summary>
    Task<int> StoreDraftAsync(Guid id, string content, string model, DateTimeOffset generatedAt, DateTimeOffset now,
        CancellationToken ct);

    /// <summary>Marks the row Failed with the message for the Secretary. Runs immediately, not staged; returns the rows changed.</summary>
    Task<int> MarkFailedAsync(Guid id, string error, DateTimeOffset now, CancellationToken ct);

    /// <summary>Turns every Running row back to Queued, after a restart. Runs immediately, not staged; returns the rows changed.</summary>
    Task<int> RequeueRunningAsync(CancellationToken ct);
}

public sealed record QueuedSummary(Guid Id, Guid MeetingId);

/// <summary>A closed vote as the draft and the minutes write it: counts per option and the Chair's conclusion, never ballots.</summary>
public sealed record ClosedVote(string Title, bool IsAnonymous, VoteConclusion? Conclusion, string ConclusionNote,
    IReadOnlyList<ClosedVoteOption> Options);

public sealed record ClosedVoteOption(string Label, int BallotCount);

/// <summary>What the AI draft and the minutes read of a meeting's records, untracked.</summary>
public interface ISummarySourceQuery
{
    Task<string> FindTitleAsync(Guid meetingId, CancellationToken ct);

    /// <summary>The agenda in order, with each item's presenter, conclusion and actual times.</summary>
    Task<IReadOnlyList<AgendaFacts>> ListAgendaAsync(Guid meetingId, CancellationToken ct);

    /// <summary>The names of the documents not removed, oldest first.</summary>
    Task<IReadOnlyList<string>> ListDocumentNamesAsync(Guid meetingId, CancellationToken ct);

    /// <summary>The closed votes in the order they closed, each option's count in the option's order.</summary>
    Task<IReadOnlyList<ClosedVote>> ListClosedVotesAsync(Guid meetingId, CancellationToken ct);

    /// <summary>What a draft would read: each sentence kept from a microphone and each line of a finished transcript.</summary>
    Task<int> CountSentencesAsync(Guid meetingId, CancellationToken ct);

    /// <summary>AI-eCabinet's minutes readiness: what a draft asked for now would draw on, and what it would miss.</summary>
    Task<SummaryReadiness> ReadinessAsync(Guid meetingId, CancellationToken ct);
}
