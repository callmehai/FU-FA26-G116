using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Summaries;

public sealed class MinutesServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingMinutesRepository _minutes = new();
    private readonly FakeMinutesSourceQuery _sources = new();
    private readonly FakeSummarySourceQuery _summarySources = new();
    private readonly FakeMeetingSummaryRepository _summaries = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeAuditLog _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeEmailMessageRepository _emails = new();
    private readonly Guid _secretary = Guid.CreateVersion7();
    private readonly Guid _chair = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();
    private readonly Guid _meeting = Guid.CreateVersion7();

    public MinutesServiceTests()
    {
        MeetingParticipant Participant(Guid userId, MeetingRole role) => new()
        {
            MeetingId = _meeting, UserId = userId, Role = role, AssignmentStatus = AssignmentStatus.Current,
            Response = InvitationResponse.Accepted,
            User = new User { Id = userId, FullName = role.ToString(), Email = "u@trustcabinet.local", IsActive = true },
        };
        _meetings.Meetings.Add(new Meeting
        {
            Id = _meeting, Title = "Họp giao ban", Status = MeetingStatus.Ended, Sensitivity = Sensitivity.Normal,
            Mode = MeetingMode.InPerson, Location = "Phòng 1", PlannedStartTime = Now.AddHours(-2),
            PlannedEndTime = Now.AddHours(-1), CreatedAt = Now, UpdatedAt = Now,
            Participants = [Participant(_secretary, MeetingRole.Secretary), Participant(_chair, MeetingRole.Chair),
                Participant(_member, MeetingRole.Member)],
        });
        _meetings.ActiveUsers.UnionWith([_secretary, _chair, _member]);
    }

    private MinutesService Service() => new(_minutes, _sources, _summarySources, _summaries, new FakeMinutesRenderer(),
        _notifications, _audit, _unitOfWork, _meetings, new FixedClock(Now), NullLogger<MinutesService>.Instance, _emails,
        Options.Create(new EmailDeliveryOptions { ApplicationBaseUrl = "https://hop.example.vn" }));

    private MeetingMinutes Revision(MinutesStatus status, int number = 1, Guid? supersedes = null)
    {
        var row = new MeetingMinutes
        {
            MeetingId = _meeting, RevisionNumber = number, Status = status, SupersedesId = supersedes, Content = "<p>Biên bản</p>",
            CreatedById = _secretary, CreatedAt = Now.AddMinutes(-10), UpdatedAt = Now.AddMinutes(-10),
        };
        _minutes.Rows.Add(row);
        return row;
    }

    [Fact]
    public async Task The_secretary_builds_the_draft_under_the_meeting_lock_in_one_transaction()
    {
        var outcome = await Service().CreateAsync(_meeting, _secretary, rebuild: false);

        Assert.Equal(MinutesFailure.None, outcome.Failure);
        var row = Assert.Single(_minutes.Rows);
        Assert.Equal(MinutesStatus.Draft, row.Status);
        Assert.Contains("BIÊN BẢN", row.Content);
        Assert.Equal([_meeting], _meetings.Locked);
        Assert.Equal(TransactionMode.ReadCommitted, _unitOfWork.Modes[0]);
        Assert.Equal((1, 0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks, _unitOfWork.Saves));
    }

    [Fact]
    public async Task A_member_who_is_neither_officer_may_not_build_the_minutes()
    {
        var outcome = await Service().CreateAsync(_meeting, _member, rebuild: false);

        Assert.Equal(MinutesFailure.Forbidden, outcome.Failure);
        Assert.Empty(_minutes.Rows);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Two_first_builds_at_once_answer_the_loser_that_minutes_exist()
    {
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.Unknown);

        var outcome = await Service().CreateAsync(_meeting, _secretary, rebuild: false);

        Assert.Equal(MinutesFailure.Exists, outcome.Failure);
        Assert.Equal(1, _unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task A_storage_failure_while_saving_the_content_is_a_failed_save()
    {
        var row = Revision(MinutesStatus.Draft);
        _minutes.Fail = new PersistenceUnavailableException();

        var outcome = await Service().SaveAsync(row.Id, _secretary, "<p>Sửa</p>", row.UpdatedAt);

        Assert.Equal(MinutesFailure.SaveFailed, outcome.Failure);
    }

    [Fact]
    public async Task The_secretary_sends_the_draft_and_the_chair_is_told()
    {
        var row = Revision(MinutesStatus.Draft);

        var outcome = await Service().SubmitAsync(row.Id, _secretary, row.UpdatedAt);

        Assert.Equal(MinutesFailure.None, outcome.Failure);
        Assert.Equal(MinutesStatus.WaitingApproval, row.Status);
        Assert.Equal([row.Id], _minutes.Locked);
        Assert.Equal(_chair, Assert.Single(_notifications.Added).UserId);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
    }

    [Fact]
    public async Task The_revision_is_locked_inside_the_transaction_after_the_meeting_row()
    {
        var row = Revision(MinutesStatus.WaitingApproval);
        var held = new List<(bool MeetingLocked, bool InTransaction)>();
        _minutes.BeforeLock = () => held.Add((_meetings.Locked.Contains(_meeting), _unitOfWork.InTransaction));

        await Service().ApproveAsync(row.Id, _chair, row.UpdatedAt);

        Assert.Equal([(true, true)], held);
        Assert.Equal(TransactionMode.ReadCommitted, Assert.Single(_unitOfWork.Modes, m => m != TransactionMode.None));
    }

    [Fact]
    public async Task The_chair_does_not_send_the_minutes_for_approval()
    {
        var row = Revision(MinutesStatus.Draft);

        var outcome = await Service().SubmitAsync(row.Id, _chair, row.UpdatedAt);

        Assert.Equal(MinutesFailure.NotSecretary, outcome.Failure);
        Assert.Equal(MinutesStatus.Draft, row.Status);
        Assert.Empty(_notifications.Added);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Approving_a_correction_issues_it_supersedes_the_earlier_text_and_records_who_issued_it()
    {
        var issued = Revision(MinutesStatus.Issued);
        var correction = Revision(MinutesStatus.WaitingApproval, 2, issued.Id);

        var outcome = await Service().ApproveAsync(correction.Id, _chair, correction.UpdatedAt);

        Assert.Equal(MinutesFailure.None, outcome.Failure);
        Assert.Equal((MinutesStatus.Issued, MinutesStatus.Superseded), (correction.Status, issued.Status));
        Assert.Equal(AuditActions.MinutesIssued, Assert.Single(_audit.Rows).Action);
        Assert.Equal(new[] { _member, _secretary }.Order(), _notifications.Added.Select(n => n.UserId).Order());
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Issuing_emails_every_active_reader_but_the_chair_before_the_one_save_of_the_issue()
    {
        _sources.ActiveUsers.UnionWith([_secretary, _chair, _member]);
        var row = Revision(MinutesStatus.WaitingApproval);

        await Service().ApproveAsync(row.Id, _chair, row.UpdatedAt);

        // PO 15/11/2026: the same readers as the bell, the Chair who issued excluded, queued in the issuing transaction.
        Assert.DoesNotContain(_chair, Assert.Single(_sources.EmailLookups));
        Assert.Equal(new[] { _member, _secretary }.Order(),
            _emails.Added.Select(e => Guid.ParseExact(e.ToAddress[..32], "N")).Order());
        Assert.All(_emails.Added, e =>
        {
            Assert.StartsWith("Biên bản cuộc họp đã ban hành: ", e.Subject);
            Assert.StartsWith($"email-minutes-issued:{row.Id:D}:", e.DeduplicationKey);
            Assert.Contains($"https://hop.example.vn/mgmt/meetings/{_meeting:D}?tab=minutes", e.Body);
            Assert.Contains("Nguyễn Chủ Toạ", e.Body);
            Assert.NotNull(e.HtmlBody);
        });
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
    }

    [Fact]
    public async Task An_issued_correction_is_emailed_as_one_and_no_active_reader_means_no_email()
    {
        _sources.ActiveUsers.Add(_member);
        var issued = Revision(MinutesStatus.Issued);
        var correction = Revision(MinutesStatus.WaitingApproval, 2, issued.Id);

        await Service().ApproveAsync(correction.Id, _chair, correction.UpdatedAt);

        Assert.StartsWith("Bản đính chính biên bản đã ban hành: ", Assert.Single(_emails.Added).Subject);

        _sources.ActiveUsers.Clear();
        _emails.Added.Clear();
        var next = Revision(MinutesStatus.WaitingApproval, 3, correction.Id);
        await Service().ApproveAsync(next.Id, _chair, next.UpdatedAt);

        Assert.Empty(_emails.Added);
    }

    [Fact]
    public async Task Returning_without_a_reason_is_refused_and_with_one_sends_it_back_to_the_secretary()
    {
        var row = Revision(MinutesStatus.WaitingApproval);

        Assert.Equal(MinutesFailure.ReasonInvalid, (await Service().ReturnAsync(row.Id, _chair, "  ", row.UpdatedAt)).Failure);
        Assert.Equal(MinutesStatus.WaitingApproval, row.Status);

        var outcome = await Service().ReturnAsync(row.Id, _chair, "Bổ sung kết luận.", row.UpdatedAt);

        Assert.Equal(MinutesFailure.None, outcome.Failure);
        Assert.Equal(MinutesStatus.Rejected, row.Status);
        Assert.Equal(_secretary, Assert.Single(_notifications.Added).UserId);
    }

    [Fact]
    public async Task A_correction_starts_from_the_issued_text_and_only_one_is_written_at_a_time()
    {
        var issued = Revision(MinutesStatus.Issued);

        Assert.Equal(MinutesFailure.None, (await Service().CorrectAsync(issued.Id, _secretary)).Failure);
        var correction = _minutes.Rows.Single(m => m.RevisionNumber == 2);
        Assert.Equal((issued.Id, issued.Content, MinutesStatus.Draft), (correction.SupersedesId!.Value, correction.Content, correction.Status));
        Assert.Empty(_notifications.Added);

        Assert.Equal(MinutesFailure.CorrectionInProgress, (await Service().CorrectAsync(issued.Id, _secretary)).Failure);
    }

    [Fact]
    public async Task Two_corrections_at_once_answer_the_loser_that_one_is_in_progress()
    {
        var issued = Revision(MinutesStatus.Issued);
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.Unknown);

        var outcome = await Service().CorrectAsync(issued.Id, _secretary);

        Assert.Equal(MinutesFailure.CorrectionInProgress, outcome.Failure);
        Assert.Equal(1, _unitOfWork.Rollbacks);
    }
}
