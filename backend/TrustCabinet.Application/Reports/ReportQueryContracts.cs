using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Reports;

/// <summary>
/// One meeting the caller may read (<see cref="TrustCabinet.Domain.Meetings.MeetingReadPolicy"/>, applied in the query),
/// whose planned start is in the period, with what every report table groups or counts by. Never leaves the server
/// as such: a report table names a meeting only when it is grouped by meeting, which the caller could open anyway.
/// <see cref="ViewerManages"/>: the caller is an active Chair or Secretary of it (who also sees draft votes and minutes
/// not yet issued). <see cref="ViewerOnRoster"/>: the caller is an active participant (who may list its recordings).
/// <see cref="Invitees"/>, <see cref="Present"/> and <see cref="Marked"/> count current internal participants and guests
/// whose invitation stands, as BR-151 does. Untracked.
/// </summary>
public sealed record ReportMeetingRow(
    Guid Id,
    string Title,
    DateTimeOffset PlannedStart,
    MeetingStatus Status,
    MeetingMode Mode,
    Sensitivity Sensitivity,
    DateTimeOffset? ActualStartedAt,
    DateTimeOffset? ActualEndedAt,
    Guid CreatedById,
    string CreatedByName,
    Guid? ChairId,
    string? ChairName,
    int Invitees,
    int Present,
    int Marked,
    bool ViewerManages,
    bool ViewerOnRoster);

/// <summary>
/// One current participant (not removed) or one guest whose invitation stands. A guest's
/// <see cref="PersonKey"/> is "guest:" and the invitation id — never the email (PO 01/11/2026).
/// </summary>
public sealed record ReportParticipantRow(Guid MeetingId, string PersonKey, string PersonName, MeetingRole Role,
    AttendanceStatus Attendance, InvitationResponse Response);

/// <summary>One vote of a meeting, with its ballots cast (receipts) and how many could vote.</summary>
public sealed record ReportVoteRow(Guid MeetingId, VotingStatus Status, VoteConclusion? Conclusion, bool IsAnonymous,
    int EligibleCount, int Ballots);

/// <summary>One document attached to a meeting and not removed from it.</summary>
public sealed record ReportDocumentRow(Guid MeetingId, Guid AddedById, string AddedByName, string FileName, long ByteSize,
    int? PageCount, int Comments, int ReadConfirmations);

/// <summary>One revision of a meeting's minutes.</summary>
public sealed record ReportMinutesRow(Guid MeetingId, MinutesStatus Status, Guid CreatedById, string CreatedByName,
    DateTimeOffset? IssuedAt);

/// <summary>One recording of a meeting that is not deleted, with the state of its transcript if it has one.</summary>
public sealed record ReportRecordingRow(Guid MeetingId, RecordingSource Source, RecordingStatus Status, long DurationMs,
    TranscriptStatus? Transcript);

/// <summary>
/// The reads behind the report query (supervisor review 22/11/2026): one per main table, untracked. Every read is limited
/// to meetings <paramref name="actorId"/> may read under <see cref="TrustCabinet.Domain.Meetings.MeetingReadPolicy"/> whose
/// planned start is in [start, end) — the same filter as <see cref="IMeetingReportQuery"/>, applied before anything
/// leaves the database. What a reader of a meeting may see of its votes, minutes and recordings is decided by
/// <see cref="ReportService"/> from <see cref="ReportMeetingRow.ViewerManages"/> and
/// <see cref="ReportMeetingRow.ViewerOnRoster"/>. Pass UTC instants.
/// </summary>
public interface IReportFactQuery
{
    Task<List<ReportMeetingRow>> ListMeetingsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    Task<List<ReportParticipantRow>> ListParticipantsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    Task<List<ReportVoteRow>> ListVotesAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    Task<List<ReportDocumentRow>> ListDocumentsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    Task<List<ReportMinutesRow>> ListMinutesAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    Task<List<ReportRecordingRow>> ListRecordingsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
}

/// <summary>
/// <c>GET /api/reports/query</c>: which table to read (<see cref="Entity"/>), the period, what to group by, how to sort,
/// how many groups to keep, and one optional filter per column that has a fixed list of values. A filter left empty
/// does not filter. Every value is checked by <see cref="ReportCatalog"/>; the names are the query string's.
/// </summary>
public sealed record ReportQueryInput(
    string? Entity = null,
    string? From = null,
    string? To = null,
    string? GroupBy = null,
    string? Sort = null,
    string? Top = null,
    string? Status = null,
    string? Mode = null,
    string? Sensitivity = null,
    string? Role = null,
    string? Attendance = null,
    string? Response = null,
    string? Conclusion = null,
    string? VoteStatus = null,
    string? Ballot = null,
    string? FileType = null,
    string? MinutesStatus = null,
    string? Source = null,
    string? Transcript = null)
{
    /// <summary>The filters by their key, as the catalogue names its columns.</summary>
    public IReadOnlyDictionary<string, string?> Filters => new Dictionary<string, string?>
    {
        ["status"] = Status, ["mode"] = Mode, ["sensitivity"] = Sensitivity, ["role"] = Role,
        ["attendance"] = Attendance, ["response"] = Response, ["conclusion"] = Conclusion, ["voteStatus"] = VoteStatus,
        ["ballot"] = Ballot, ["fileType"] = FileType, ["minutesStatus"] = MinutesStatus, ["source"] = Source,
        ["transcript"] = Transcript,
    };
}

/// <summary>A figure of the table: <see cref="Format"/> is <c>count</c>, <c>percent</c> (a fraction), <c>hours</c> or <c>megabytes</c>.</summary>
public sealed record ReportColumn(string Key, string Label, string Format);

/// <summary>A filter the table was read with, with its value in words.</summary>
public sealed record ReportFilter(string Key, string Label, string Value, string ValueLabel);

/// <summary>
/// One group. <see cref="Key"/> is the value grouped on: a month "yyyy-MM", an enum name, a user id, "guest:" and an
/// invitation id, or a meeting id. <see cref="Values"/> has one entry per column; null when there is nothing to
/// average.
/// </summary>
public sealed record ReportRow(int Rank, string Key, string Label, IReadOnlyDictionary<string, double?> Values);

/// <summary>
/// The answer to one report query. <see cref="Groups"/> is how many groups there were before <see cref="Top"/> cut the
/// list; <see cref="Total"/> is every column over all the rows that passed the filters, not only the groups shown.
/// </summary>
public sealed record ReportTable(
    string Entity,
    string EntityLabel,
    string GroupBy,
    string GroupLabel,
    string Sort,
    int? Top,
    string From,
    string To,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ReportFilter> Filters,
    IReadOnlyList<ReportColumn> Columns,
    int Groups,
    IReadOnlyDictionary<string, double?> Total,
    IReadOnlyList<ReportRow> Rows);
