using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Application.Summaries;

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
/// The approval facts are SCRUM-33: who sent the revision for approval, who approved it, who returned it and why. The
/// return reason, the submitter and the returner are the Chair's and the Secretary's alone; a reader sees who approved
/// the issued text and when. <see cref="SupersedesId"/> is the revision a correction replaces, null on the first.
/// </summary>
public sealed record MinutesView(Guid Id, int RevisionNumber, string Status, string Content, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, MinutesPerson? CreatedBy, DateTimeOffset? BuiltFromDraftAt,
    Guid? SupersedesId = null, MinutesPerson? SubmittedBy = null, DateTimeOffset? SubmittedAt = null,
    MinutesPerson? ApprovedBy = null, DateTimeOffset? ApprovedAt = null, DateTimeOffset? IssuedAt = null,
    MinutesPerson? ReturnedBy = null, DateTimeOffset? ReturnedAt = null, string? RejectionReason = null);

/// <summary>One line of the minutes' history (UC-58): every revision for the Chair and the Secretary, only the Issued and
/// Superseded ones for anyone else (BR-412).</summary>
public sealed record MinutesRevision(Guid Id, int RevisionNumber, string Status, DateTimeOffset? IssuedAt);

/// <summary>
/// What the caller sees. The Chair and the Secretary get the latest revision whatever its state; everyone else who may
/// read the meeting gets the latest issued revision, or null while none is (BR-386, BR-412). <see cref="DraftChangedAt"/>
/// is when the AI draft that fills sections I–III last changed (written or edited), null when the meeting has none, and
/// for anyone but the Chair and the Secretary. <see cref="History"/> is newest first, filtered the same way.
/// <see cref="CanSubmit"/> and <see cref="CanApprove"/> say which of the two officers the caller is — the current Secretary
/// sends the minutes for approval (BR-404), the current Chair approves or returns them (BR-406) — so the screen offers the
/// right buttons; the state of the minutes and the meeting still decides whether the action goes through.
/// </summary>
public sealed record MinutesState(MinutesView? Minutes, bool CanManage, DateTimeOffset? DraftChangedAt,
    IReadOnlyList<MinutesRevision>? History = null, bool CanSubmit = false, bool CanApprove = false);

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
    /// <summary>The Chair asked to submit: only the Secretary sends the minutes for approval (BR-404).</summary>
    NotSecretary,
    /// <summary>The minutes are sent for approval only after the meeting has ended (BR-405).</summary>
    MeetingNotEnded,
    /// <summary>The Secretary asked to approve or return: only the Chair does (BR-406).</summary>
    NotChair,
    /// <summary>Approve or return on a revision that is not waiting for approval.</summary>
    NotWaiting,
    /// <summary>The return reason is empty or longer than <see cref="MinutesLimits.ReturnReason"/> (BR-407).</summary>
    ReasonInvalid,
    /// <summary>A correction asked of a revision that is not the issued one.</summary>
    NotIssued,
    /// <summary>The issued revision already has a correction that is not issued yet (BR-410).</summary>
    CorrectionInProgress,
    /// <summary>A correction is rebuilt from the template: it starts from the issued text and is corrected by hand (BR-413).</summary>
    CorrectionRebuild,
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
    /// <summary>One revision, for opening a superseded one from the history (UC-58).</summary>
    Task<MinutesOutcome> GetRevisionAsync(Guid minutesId, Guid actorId, CancellationToken ct = default);
    /// <summary>UC-61: the Secretary sends a Draft or Rejected revision to the Chair once the meeting has ended.</summary>
    Task<MinutesOutcome> SubmitAsync(Guid minutesId, Guid actorId, DateTimeOffset? loadedAt, CancellationToken ct = default);
    /// <summary>UC-64: the Chair approves the revision waiting for approval, which issues it at once.</summary>
    Task<MinutesOutcome> ApproveAsync(Guid minutesId, Guid actorId, DateTimeOffset? loadedAt, CancellationToken ct = default);
    /// <summary>UC-65: the Chair sends the revision waiting for approval back to the Secretary with a reason.</summary>
    Task<MinutesOutcome> ReturnAsync(Guid minutesId, Guid actorId, string? reason, DateTimeOffset? loadedAt,
        CancellationToken ct = default);
    /// <summary>UC-83: a new Draft revision that starts from the issued text of <paramref name="minutesId"/>.</summary>
    Task<MinutesOutcome> CorrectAsync(Guid minutesId, Guid actorId, CancellationToken ct = default);
}

/// <summary>What <c>GET /api/minutes/{id}/export?format=</c> returns: Word unless PDF is asked for.</summary>
public enum MinutesExportFormat
{
    Docx,
    Pdf,
}

public sealed record MinutesFile(string FileName, byte[] Bytes, string ContentType);

/// <summary>The revisions of each meeting's minutes, <c>meeting_minutes</c>.</summary>
public interface IMeetingMinutesRepository
{
    /// <summary>The meeting's highest revision, tracked; null when the meeting has no minutes.</summary>
    Task<MeetingMinutes?> FindLatestAsync(Guid meetingId, CancellationToken ct);

    /// <summary>One revision, untracked; null when there is none.</summary>
    Task<MeetingMinutes?> FindAsync(Guid minutesId, CancellationToken ct);

    /// <summary>One revision, tracked; throws when there is none.</summary>
    Task<MeetingMinutes> GetAsync(Guid minutesId, CancellationToken ct);

    /// <summary>One revision untracked, with its author, submitter, approver and returner; null when there is none.</summary>
    Task<MeetingMinutes?> FindWithPeopleAsync(Guid minutesId, CancellationToken ct);

    /// <summary>The meeting a revision belongs to, untracked; null when there is no such revision.</summary>
    Task<Guid?> FindMeetingIdAsync(Guid minutesId, CancellationToken ct);

    /// <summary>
    /// Takes an exclusive lock on the one revision row until the current transaction ends, then returns it tracked; null,
    /// and no lock, when there is none. Callers lock the meeting row first.
    /// </summary>
    Task<MeetingMinutes?> LockAsync(Guid minutesId, CancellationToken ct);

    /// <summary>Whether the meeting has a revision numbered above <paramref name="revisionNumber"/>.</summary>
    Task<bool> HasLaterRevisionAsync(Guid meetingId, int revisionNumber, CancellationToken ct);

    /// <summary>
    /// The meeting's revisions, newest first, untracked: all of them for one of the two officers
    /// (<paramref name="manages"/>), only the Issued and Superseded ones for anyone else (BR-412).
    /// </summary>
    Task<IReadOnlyList<MinutesRevisionRow>> ListRevisionsAsync(Guid meetingId, bool manages, CancellationToken ct);

    /// <summary>
    /// Stores the content when the revision is a Draft or Rejected and was last saved between <paramref name="from"/> and
    /// <paramref name="to"/>. Runs immediately, not staged; returns the rows changed (0 when someone saved it since).
    /// </summary>
    Task<int> SaveContentAsync(Guid minutesId, string content, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now,
        CancellationToken ct);

    void Add(MeetingMinutes minutes);
}

public sealed record MinutesRevisionRow(Guid Id, int RevisionNumber, MinutesStatus Status, DateTimeOffset? IssuedAt);

/// <summary>A current participant as the minutes list them.</summary>
public sealed record MinutesAttendee(MeetingRole Role, AttendanceStatus Attendance, string FullName);

/// <summary>A Guest whose invitation stands, under the name they gave or were invited by.</summary>
public sealed record MinutesGuest(string Name, AttendanceStatus Attendance);

/// <summary>What the minutes read of a meeting's people and notes, untracked.</summary>
public interface IMinutesSourceQuery
{
    /// <summary>The current participants, by role and then name.</summary>
    Task<IReadOnlyList<MinutesAttendee>> ListAttendeesAsync(Guid meetingId, CancellationToken ct);

    Task<IReadOnlyList<MinutesGuest>> ListGuestsAsync(Guid meetingId, CancellationToken ct);

    /// <summary>The non-empty notes of these authors, oldest first.</summary>
    Task<IReadOnlyList<MinutesNote>> ListNotesAsync(Guid meetingId, IReadOnlyCollection<Guid> authorIds, CancellationToken ct);

    /// <summary>Which of these users are active.</summary>
    Task<IReadOnlyList<Guid>> ListActiveUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>The active accounts among <paramref name="readerIds"/>, whom the "minutes issued" email goes to.</summary>
    Task<IReadOnlyList<MinutesEmailRecipient>> ListEmailRecipientsAsync(Guid[] readerIds, CancellationToken ct);

    /// <summary>
    /// Whom the "minutes issued" email names: the Chair who issued it, then the organizer with their address. Both
    /// accounts must exist; one read each, in that order.
    /// </summary>
    Task<MinutesIssuedNames> GetIssuedEmailNamesAsync(Guid chairId, Guid organizerId, CancellationToken ct);
}

/// <summary>One active reader the "minutes issued" email is addressed to.</summary>
public sealed record MinutesEmailRecipient(Guid Id, string FullName, string Email);

/// <summary>The people a "minutes issued" email names.</summary>
public sealed record MinutesIssuedNames(string Chair, string Organizer, string OrganizerEmail);

/// <summary>
/// The minutes' HTML allow-list (ADR-0055) and the Word and PDF files drawn from it; the parser and the PDF engine are
/// the infrastructure project's.
/// </summary>
public interface IMinutesRenderer
{
    /// <summary>Line-marked text, as the template writes it, as HTML.</summary>
    string FromLines(string lineText);

    /// <summary>Nested deeper than any document the editor writes; refused before it is walked.</summary>
    bool TooDeep(string html);

    /// <summary>Only the allow-list of the HTML.</summary>
    string Sanitize(string html);

    /// <summary>Whether the document has any text, not only empty paragraphs.</summary>
    bool HasText(string html);

    /// <summary>Stored content as the page shows it: sanitized HTML, or line-marked text converted.</summary>
    string Normalize(string content);

    byte[] BuildPdf(string content);

    byte[] BuildDocx(string content);
}
