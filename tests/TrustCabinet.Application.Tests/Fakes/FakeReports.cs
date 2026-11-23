using TrustCabinet.Application.Reports;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>The meeting report reads in memory. Each call filters nothing; the test seeds only what the case needs.</summary>
public sealed class FakeMeetingReportQuery : IMeetingReportQuery
{
    public List<MeetingReportGroup> Groups { get; } = [];
    public List<MeetingReportEndedRow> EndedRows { get; } = [];

    /// <summary>Every call, in order: <c>groups</c>, <c>ended</c>.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Thrown by every call, in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    public Task<List<MeetingReportGroup>> ListGroupsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default)
        => Read("groups", () => Groups.ToList());

    public Task<List<MeetingReportEndedRow>> ListEndedRowsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default)
        => Read("ended", () => EndedRows.ToList());

    private Task<T> Read<T>(string name, Func<T> answer)
    {
        Calls.Add(name);
        return Fail is { } failure ? Task.FromException<T>(failure) : Task.FromResult(answer());
    }
}

/// <summary>The Excel export as fixed bytes, keeping what it was asked to render.</summary>
public sealed class FakeMeetingReportRenderer : IMeetingReportRenderer
{
    public byte[] Bytes { get; set; } = [1, 2, 3];
    public List<MeetingReport> Received { get; } = [];
    public List<ReportTable> ReceivedTables { get; } = [];

    public byte[] Build(MeetingReport report)
    {
        Received.Add(report);
        return Bytes;
    }

    public byte[] Build(ReportTable table)
    {
        ReceivedTables.Add(table);
        return Bytes;
    }
}

/// <summary>The report query's rows in memory. Filters nothing: the test seeds what the caller may read.</summary>
public sealed class FakeReportFactQuery : IReportFactQuery
{
    public List<ReportMeetingRow> Meetings { get; } = [];
    public List<ReportParticipantRow> Participants { get; } = [];
    public List<ReportVoteRow> Votes { get; } = [];
    public List<ReportDocumentRow> Documents { get; } = [];
    public List<ReportMinutesRow> Minutes { get; } = [];
    public List<ReportRecordingRow> Recordings { get; } = [];

    /// <summary>Every call, in order, by table: <c>meetings</c>, <c>participants</c>, <c>votes</c>…</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Thrown by every call, in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    public Task<List<ReportMeetingRow>> ListMeetingsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
        => Read("meetings", Meetings);

    public Task<List<ReportParticipantRow>> ListParticipantsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
        => Read("participants", Participants);

    public Task<List<ReportVoteRow>> ListVotesAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
        => Read("votes", Votes);

    public Task<List<ReportDocumentRow>> ListDocumentsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
        => Read("documents", Documents);

    public Task<List<ReportMinutesRow>> ListMinutesAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
        => Read("minutes", Minutes);

    public Task<List<ReportRecordingRow>> ListRecordingsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
        => Read("recordings", Recordings);

    private Task<List<T>> Read<T>(string name, List<T> rows)
    {
        Calls.Add(name);
        return Fail is { } failure ? Task.FromException<List<T>>(failure) : Task.FromResult(rows.ToList());
    }
}
