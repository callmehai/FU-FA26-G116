using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Reports;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Reports;

/// <summary>The report query (supervisor review 22/11/2026) and the manager figures of the meeting report.</summary>
public sealed class ReportQueryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    private readonly FakeMeetingReportQuery _query = new();
    private readonly FakeReportFactQuery _facts = new();
    private readonly FakeMeetingReportRenderer _renderer = new();
    private readonly FakeAuditService _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Guid _actor = Guid.CreateVersion7();
    private readonly Guid _huong = Guid.CreateVersion7();
    private readonly Guid _an = Guid.CreateVersion7();

    private ReportService Service() => new(_query, _facts, _renderer, _audit, _unitOfWork, new FixedClock(Now),
        NullLogger<ReportService>.Instance);

    private ReportMeetingRow Meeting(int month, int day, MeetingStatus status = MeetingStatus.Ended, Guid? chair = null,
        string? chairName = null, bool manages = false, bool onRoster = true, int invitees = 0, int present = 0, int marked = 0,
        double hours = 2, Sensitivity sensitivity = Sensitivity.Normal, string title = "Họp")
    {
        var start = new DateTimeOffset(2026, month, day, 9, 0, 0, Vietnam);
        var ended = status == MeetingStatus.Ended;
        var row = new ReportMeetingRow(Guid.CreateVersion7(), title, start, status, MeetingMode.InPerson, sensitivity,
            ended ? start : null, ended ? start.AddHours(hours) : null, _actor, "Người tạo", chair, chairName,
            invitees, present, marked, manages, onRoster);
        _facts.Meetings.Add(row);
        return row;
    }

    private static ReportQueryInput Input(string entity, string? groupBy = null, string? sort = null, string? top = null,
        string from = "2026-09", string to = "2026-11")
        => new(entity, from, to, groupBy, sort, top);

    [Fact]
    public async Task Groups_meetings_by_chair_biggest_first_and_cuts_to_the_top()
    {
        Meeting(9, 2, chair: _huong, chairName: "Hương");
        Meeting(10, 2, chair: _huong, chairName: "Hương");
        Meeting(10, 9, MeetingStatus.Cancelled, chair: _huong, chairName: "Hương");
        Meeting(11, 3, chair: _an, chairName: "An");
        Meeting(11, 4, MeetingStatus.Published);

        var outcome = await Service().QueryAsync(_actor, Input("meetings", "chair", "count", "2"));

        Assert.Equal(ReportFailure.None, outcome.Failure);
        var table = outcome.Table!;
        Assert.Equal(3, table.Groups);
        Assert.Equal(["Hương", "An"], table.Rows.Select(r => r.Label));
        Assert.Equal([1, 2], table.Rows.Select(r => r.Rank));
        Assert.Equal(_huong.ToString(), table.Rows[0].Key);
        Assert.Equal(3, table.Rows[0].Values["count"]);
        Assert.Equal(2, table.Rows[0].Values["ended"]);
        Assert.Equal(1, table.Rows[0].Values["cancelled"]);
        Assert.Equal(4, table.Rows[0].Values["hours"]);
        // The total covers every meeting, not only the two groups shown.
        Assert.Equal(5, table.Total["count"]);
        Assert.Equal(["count", "ended", "cancelled", "hours", "attendance"], table.Columns.Select(c => c.Key));
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task A_month_table_lists_every_month_of_the_period_in_order_and_filters()
    {
        Meeting(9, 2, sensitivity: Sensitivity.Secret);
        Meeting(11, 3, sensitivity: Sensitivity.Secret);
        Meeting(11, 4);

        var outcome = await Service().QueryAsync(_actor, Input("meetings", "month") with { Sensitivity = "Secret" });

        var table = outcome.Table!;
        Assert.Equal(["2026-09", "2026-10", "2026-11"], table.Rows.Select(r => r.Key));
        Assert.Equal(["09/2026", "10/2026", "11/2026"], table.Rows.Select(r => r.Label));
        Assert.Equal([1, 0, 1], table.Rows.Select(r => r.Values["count"]));
        Assert.Equal("group", table.Sort);
        Assert.Equal([new ReportFilter("sensitivity", "Mức độ", "Secret", "Mật")], table.Filters);
    }

    [Fact]
    public async Task Attendance_is_averaged_over_ended_meetings_that_had_it_taken()
    {
        Meeting(10, 1, invitees: 4, present: 3, marked: 4);
        Meeting(10, 2, invitees: 5, present: 0, marked: 0);
        Meeting(10, 3, MeetingStatus.Published, invitees: 2, present: 2, marked: 2);

        var table = (await Service().QueryAsync(_actor, Input("meetings", "status"))).Table!;

        Assert.Equal(0.75, table.Total["attendance"]);
        Assert.Null(table.Rows.Single(r => r.Key == "Published").Values["attendance"]);
    }

    [Fact]
    public async Task Draft_votes_count_only_for_the_chair_and_secretaries()
    {
        var mine = Meeting(10, 1, manages: true);
        var theirs = Meeting(10, 2);
        _facts.Votes.AddRange(
        [
            new ReportVoteRow(mine.Id, VotingStatus.Draft, null, false, 0, 0),
            new ReportVoteRow(theirs.Id, VotingStatus.Draft, null, false, 0, 0),
            new ReportVoteRow(theirs.Id, VotingStatus.Closed, VoteConclusion.Passed, true, 5, 4),
            new ReportVoteRow(theirs.Id, VotingStatus.Closed, VoteConclusion.NotPassed, false, 5, 2),
        ]);

        var table = (await Service().QueryAsync(_actor, Input("votes", "conclusion"))).Table!;

        Assert.Equal(3, table.Total["count"]);
        Assert.Equal(1, table.Total["passed"]);
        Assert.Equal(1, table.Total["notPassed"]);
        // 6 ballots of 10 eligible; the draft has none of either.
        Assert.Equal(0.6, table.Total["turnout"]);
        Assert.Equal("Chưa kết luận", table.Rows.Single(r => r.Key == "None").Label);
        Assert.Equal(["meetings", "votes"], _facts.Calls);
    }

    [Fact]
    public async Task Minutes_not_issued_count_only_for_the_chair_and_secretaries_and_turnaround_starts_at_the_first_issue()
    {
        var mine = Meeting(10, 1, manages: true);
        var theirs = Meeting(10, 2);
        var ended = theirs.ActualEndedAt!.Value;
        _facts.Minutes.AddRange(
        [
            new ReportMinutesRow(mine.Id, MinutesStatus.WaitingApproval, _actor, "Thư ký", null),
            new ReportMinutesRow(theirs.Id, MinutesStatus.Draft, _actor, "Thư ký", null),
            new ReportMinutesRow(theirs.Id, MinutesStatus.Superseded, _actor, "Thư ký", ended.AddHours(10)),
            new ReportMinutesRow(theirs.Id, MinutesStatus.Issued, _actor, "Thư ký", ended.AddHours(50)),
        ]);

        var table = (await Service().QueryAsync(_actor, Input("minutes", "minutesStatus"))).Table!;

        Assert.Equal(3, table.Total["count"]);
        Assert.Equal(2, table.Total["issued"]);
        Assert.Equal(1, table.Total["waiting"]);
        Assert.Equal(10, table.Total["turnaround"]);
        Assert.DoesNotContain(table.Rows, r => r.Key == "Draft");
    }

    [Fact]
    public async Task Recordings_count_only_where_the_caller_is_on_the_roster_and_a_combined_one_adds_no_hours()
    {
        var mine = Meeting(10, 1);
        var approverOnly = Meeting(10, 2, onRoster: false);
        _facts.Recordings.AddRange(
        [
            new ReportRecordingRow(mine.Id, RecordingSource.BrowserCapture, RecordingStatus.Available, 3_600_000, TranscriptStatus.Completed),
            new ReportRecordingRow(mine.Id, RecordingSource.Combined, RecordingStatus.Available, 3_600_000, null),
            new ReportRecordingRow(mine.Id, RecordingSource.Upload, RecordingStatus.Available, 1_800_000, TranscriptStatus.Failed),
            new ReportRecordingRow(approverOnly.Id, RecordingSource.Upload, RecordingStatus.Available, 3_600_000, TranscriptStatus.Completed),
        ]);

        var table = (await Service().QueryAsync(_actor, Input("recordings", "transcript"))).Table!;

        Assert.Equal(3, table.Total["count"]);
        Assert.Equal(1.5, table.Total["hours"]);
        Assert.Equal(1, table.Total["transcribed"]);
        Assert.Equal(["Completed", "Failed", "None"], table.Rows.OrderBy(r => r.Key).Select(r => r.Key));
    }

    [Fact]
    public async Task Participants_rank_people_by_absences_with_the_rate_over_times_marked()
    {
        var first = Meeting(10, 1);
        var second = Meeting(10, 2);
        var guest = Guid.CreateVersion7();
        _facts.Participants.AddRange(
        [
            new ReportParticipantRow(first.Id, _an.ToString(), "An", MeetingRole.Member, AttendanceStatus.Absent, InvitationResponse.Accepted),
            new ReportParticipantRow(second.Id, _an.ToString(), "An", MeetingRole.Member, AttendanceStatus.Present, InvitationResponse.Accepted),
            new ReportParticipantRow(first.Id, _huong.ToString(), "Hương", MeetingRole.Chair, AttendanceStatus.Present, InvitationResponse.Accepted),
            new ReportParticipantRow(second.Id, _huong.ToString(), "Hương", MeetingRole.Chair, AttendanceStatus.Unknown, InvitationResponse.Accepted),
            new ReportParticipantRow(first.Id, $"guest:{guest}", "Khách A", MeetingRole.Guest, AttendanceStatus.Present, InvitationResponse.Pending),
        ]);

        var table = (await Service().QueryAsync(_actor, Input("participants", "person", "absent", "1"))).Table!;

        var top = Assert.Single(table.Rows);
        Assert.Equal(("An", 1d, 0.5), (top.Label, top.Values["absent"]!.Value, top.Values["rate"]!.Value));
        Assert.Equal(3, table.Groups);
        var byRole = (await Service().QueryAsync(_actor, Input("participants", "role") with { Attendance = "Present" })).Table!;
        // One present each: ties keep the order the roles are listed in.
        Assert.Equal(["Chair", "Member", "Guest"], byRole.Rows.Select(r => r.Key));
        Assert.Equal(3, byRole.Total["count"]);
    }

    [Fact]
    public async Task Documents_group_by_the_kind_of_file()
    {
        var meeting = Meeting(10, 1);
        _facts.Documents.AddRange(
        [
            new ReportDocumentRow(meeting.Id, _actor, "Thư ký", "Ke-hoach.PDF", 2_000_000, 12, 3, 4),
            new ReportDocumentRow(meeting.Id, _actor, "Thư ký", "Du-toan.xlsx", 500_000, null, 0, 1),
            new ReportDocumentRow(meeting.Id, _huong, "Hương", "To-trinh.docx", 300_000, null, 1, 0),
        ]);

        var table = (await Service().QueryAsync(_actor, Input("documents", "fileType", "size"))).Table!;

        Assert.Equal(["pdf", "excel", "word"], table.Rows.Select(r => r.Key));
        Assert.Equal(2, table.Rows[0].Values["size"]);
        Assert.Equal(12, table.Total["pages"]);
        Assert.Equal(4, table.Total["comments"]);
        Assert.Equal(5, table.Total["reads"]);
    }

    [Theory]
    [InlineData("tasks", null, null, null, null)]
    [InlineData("meetings", "person", null, null, null)]
    [InlineData("meetings", "month", "turnout", null, null)]
    [InlineData("meetings", "month", null, "0", null)]
    [InlineData("meetings", "month", null, "501", null)]
    [InlineData("meetings", "month", null, "ten", null)]
    [InlineData("meetings", "month", null, null, "Present")]
    [InlineData("participants", "person", null, null, "Here")]
    public async Task A_parameter_the_table_does_not_have_reads_nothing(string entity, string? groupBy, string? sort,
        string? top, string? attendance)
    {
        var outcome = await Service().QueryAsync(_actor, Input(entity, groupBy, sort, top) with { Attendance = attendance });

        Assert.Equal(ReportFailure.InvalidParameter, outcome.Failure);
        Assert.Empty(_facts.Calls);
        Assert.Empty(_unitOfWork.Modes);
    }

    [Fact]
    public async Task A_bad_period_is_told_before_a_bad_parameter()
    {
        var outcome = await Service().QueryAsync(_actor, Input("tasks", from: "2026-12", to: "2026-01"));

        Assert.Equal(ReportFailure.InvalidPeriod, outcome.Failure);
        Assert.Empty(_facts.Calls);
    }

    [Fact]
    public async Task No_table_named_reads_meetings_by_month()
    {
        Meeting(10, 1);

        var table = (await Service().QueryAsync(_actor, new ReportQueryInput(From: "2026-10", To: "2026-10"))).Table!;

        Assert.Equal(("meetings", "month"), (table.Entity, table.GroupBy));
        Assert.Equal(1, table.Total["count"]);
    }

    [Fact]
    public async Task A_store_failure_answers_read_failed()
    {
        _facts.Fail = new PersistenceUnavailableException();

        var outcome = await Service().QueryAsync(_actor, Input("votes"));

        Assert.Equal(ReportFailure.ReadFailed, outcome.Failure);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task Export_renders_the_table_and_records_what_was_asked()
    {
        Meeting(10, 1);

        var outcome = await Service().ExportQueryAsync(_actor, Input("votes", "conclusion") with { Status = "Ended" });

        Assert.Equal(ReportFailure.None, outcome.Failure);
        Assert.Equal("bao-cao-bieu-quyet_2026-09_2026-11.xlsx", outcome.File!.FileName);
        Assert.Equal("votes", Assert.Single(_renderer.ReceivedTables).Entity);
        Assert.Equal([AuditActions.ReportExported], _audit.Actions);
    }

    [Fact]
    public async Task Export_without_the_audit_row_gives_no_file()
    {
        _audit.Fail = new PersistenceUnavailableException();

        var outcome = await Service().ExportQueryAsync(_actor, Input("meetings"));

        Assert.Equal(ReportFailure.ExportFailed, outcome.Failure);
        Assert.Null(outcome.File);
    }

    [Fact]
    public async Task Export_of_a_bad_parameter_records_nothing()
    {
        var outcome = await Service().ExportQueryAsync(_actor, Input("meetings", "nobody"));

        Assert.Equal(ReportFailure.InvalidParameter, outcome.Failure);
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task The_meeting_report_carries_the_manager_figures_of_what_the_caller_may_see()
    {
        var mine = Meeting(10, 1, manages: true);
        var theirs = Meeting(10, 2, onRoster: false);
        _facts.Votes.AddRange(
        [
            new ReportVoteRow(theirs.Id, VotingStatus.Closed, VoteConclusion.Passed, false, 3, 3),
            new ReportVoteRow(theirs.Id, VotingStatus.Closed, VoteConclusion.Deferred, false, 3, 3),
            new ReportVoteRow(theirs.Id, VotingStatus.Open, null, false, 3, 1),
        ]);
        _facts.Minutes.AddRange(
        [
            new ReportMinutesRow(mine.Id, MinutesStatus.WaitingApproval, _actor, "Thư ký", null),
            new ReportMinutesRow(theirs.Id, MinutesStatus.WaitingApproval, _actor, "Thư ký", null),
            new ReportMinutesRow(theirs.Id, MinutesStatus.Issued, _actor, "Thư ký", theirs.ActualEndedAt!.Value.AddHours(2)),
            new ReportMinutesRow(theirs.Id, MinutesStatus.Superseded, _actor, "Thư ký", theirs.ActualEndedAt!.Value.AddHours(1)),
        ]);
        _facts.Recordings.AddRange(
        [
            new ReportRecordingRow(mine.Id, RecordingSource.BrowserCapture, RecordingStatus.Available, 5_400_000, TranscriptStatus.Completed),
            new ReportRecordingRow(theirs.Id, RecordingSource.Upload, RecordingStatus.Available, 3_600_000, TranscriptStatus.Completed),
        ]);

        var report = (await Service().GetMeetingReportAsync(_actor, "2026-10", "2026-10")).Report!;

        Assert.Equal(new VoteFigure(2, 1, 0, 1), report.Votes);
        Assert.Equal(new MinutesFigure(1, 1), report.Minutes);
        Assert.Equal(new RecordingFigure(1, 1.5, 1.5), report.Recordings);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }
}
