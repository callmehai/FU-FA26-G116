using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Unit.Round1;

/// <summary>Round 1 unit tests for FE-12 (Minutes): one test per kept UTC of the Minutes.* sheets.</summary>
public sealed class MinUnitRound1Tests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 2, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingMinutesRepository _minutes = new();
    private readonly FakeMinutesSourceQuery _sources = new();
    private readonly FakeSummarySourceQuery _summarySources = new();
    private readonly FakeMeetingSummaryRepository _summaries = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeAuditLog _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeEmailMessageRepository _emails = new();
    private readonly FakeRound1Logger<MinutesService> _logger = new();
    private readonly FakeMeetingChangePublisher _changes = new();
    private readonly Guid _secretary = Guid.CreateVersion7();
    private readonly Guid _chair = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();
    private readonly Guid _meeting = Guid.CreateVersion7();

    public MinUnitRound1Tests()
    {
        MeetingParticipant Participant(Guid userId, MeetingRole role) => new()
        {
            MeetingId = _meeting, UserId = userId, Role = role, AssignmentStatus = AssignmentStatus.Current,
            Response = InvitationResponse.Accepted,
            User = new User { Id = userId, FullName = role.ToString(), Email = "u@trustcabinet.local", IsActive = true },
        };
        _meetings.Meetings.Add(new Meeting
        {
            Id = _meeting, Title = "Họp tổng kết", Status = MeetingStatus.Ended, Sensitivity = Sensitivity.Normal,
            Mode = MeetingMode.InPerson, Location = "Phòng 2", PlannedStartTime = Now.AddHours(-2),
            PlannedEndTime = Now.AddHours(-1), CreatedAt = Now, UpdatedAt = Now,
            Participants = [Participant(_secretary, MeetingRole.Secretary), Participant(_chair, MeetingRole.Chair),
                Participant(_member, MeetingRole.Member)],
        });
        _meetings.ActiveUsers.UnionWith([_secretary, _chair, _member]);
    }

    private MinutesService Service() => new(_minutes, _sources, _summarySources, _summaries, new FakeMinutesRenderer(),
        _notifications, _audit, _unitOfWork, _meetings, new FixedClock(Now), _logger, _emails,
        Options.Create(new EmailDeliveryOptions { ApplicationBaseUrl = "https://hop.example.vn" }), _changes);

    private MeetingMinutes Revision(MinutesStatus status, int number = 1, Guid? supersedes = null)
    {
        var row = new MeetingMinutes
        {
            MeetingId = _meeting, RevisionNumber = number, Status = status, SupersedesId = supersedes,
            Content = "<p>Biên bản</p>", CreatedById = _secretary, CreatedAt = Now.AddMinutes(-10),
            UpdatedAt = Now.AddMinutes(-10),
        };
        _minutes.Rows.Add(row);
        return row;
    }

    // MinutesService.CreateAsync (Minutes.Create/UTC001-UTC006)

    [Fact(DisplayName = "MinutesService.CreateAsync/UTC001 secretary builds the draft from records")]
    [Trait("UTC", "MinutesService.CreateAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Create_BuildsDraftFromRecords()
    {
        var outcome = await Service().CreateAsync(_meeting, _secretary, rebuild: false);

        Assert.Equal(MinutesFailure.None, outcome.Failure);
        var row = Assert.Single(_minutes.Rows);
        Assert.Equal((MinutesStatus.Draft, 1), (row.Status, row.RevisionNumber));
        Assert.Contains("BIÊN BẢN", row.Content);
        Assert.Null(row.BuiltFromDraftAt);
        Assert.IsType<MinutesState>(outcome.Data);
        Assert.Equal([_meeting], _changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.CreateAsync/UTC002 member who is neither officer is refused")]
    [Trait("UTC", "MinutesService.CreateAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Create_MemberForbidden()
    {
        var outcome = await Service().CreateAsync(_meeting, _member, rebuild: false);

        Assert.Equal(MinutesFailure.Forbidden, outcome.Failure);
        Assert.Empty(_minutes.Rows);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.CreateAsync/UTC003 meeting still running has no draft yet")]
    [Trait("UTC", "MinutesService.CreateAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Create_RunningMeetingRefused()
    {
        _meetings.Meetings[0].Status = MeetingStatus.InProgress;

        var outcome = await Service().CreateAsync(_meeting, _secretary, rebuild: false);

        Assert.Equal(MinutesFailure.WrongMeetingStatus, outcome.Failure);
        Assert.Empty(_minutes.Rows);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.CreateAsync/UTC004 existing draft is kept without rebuild")]
    [Trait("UTC", "MinutesService.CreateAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Create_ExistingDraftKept()
    {
        Revision(MinutesStatus.Draft);

        var outcome = await Service().CreateAsync(_meeting, _secretary, rebuild: false);

        Assert.Equal(MinutesFailure.Exists, outcome.Failure);
        Assert.Single(_minutes.Rows);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.CreateAsync/UTC005 sent revision is refused even with rebuild")]
    [Trait("UTC", "MinutesService.CreateAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Create_SentRevisionLocked()
    {
        Revision(MinutesStatus.WaitingApproval);

        var outcome = await Service().CreateAsync(_meeting, _secretary, rebuild: true);

        Assert.Equal(MinutesFailure.Locked, outcome.Failure);
        Assert.Single(_minutes.Rows);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.CreateAsync/UTC006 storage failure while building is a failed save")]
    [Trait("UTC", "MinutesService.CreateAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Create_SaveFailureLogged()
    {
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().CreateAsync(_meeting, _secretary, rebuild: false);

        Assert.Equal(MinutesFailure.SaveFailed, outcome.Failure);
        var record = Assert.Single(_logger.Records);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Contains("Could not build the minutes", record.Message);
        Assert.Empty(_changes.MinutesChanged);
    }

    // MinutesService.SaveAsync (Minutes.Save/UTC001-UTC006)

    [Fact(DisplayName = "MinutesService.SaveAsync/UTC001 secretary saves the draft content")]
    [Trait("UTC", "MinutesService.SaveAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Save_StoresFilteredContent()
    {
        var row = Revision(MinutesStatus.Draft);

        var outcome = await Service().SaveAsync(row.Id, _secretary, "<p>Biên bản họp.</p>", row.UpdatedAt);

        Assert.Equal(MinutesFailure.None, outcome.Failure);
        Assert.Equal(("<p>Biên bản họp.</p>", Now), (row.Content, row.UpdatedAt));
        Assert.IsType<MinutesState>(outcome.Data);
        Assert.Equal([_meeting], _changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.SaveAsync/UTC002 member learns nothing about a draft")]
    [Trait("UTC", "MinutesService.SaveAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Save_MemberSeesNothing()
    {
        var row = Revision(MinutesStatus.Draft);

        var outcome = await Service().SaveAsync(row.Id, _member, "<p>A.</p>", row.UpdatedAt);

        Assert.Equal(MinutesFailure.NotFound, outcome.Failure);
        Assert.Equal("<p>Biên bản</p>", row.Content);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.SaveAsync/UTC003 sent revision is no longer edited here")]
    [Trait("UTC", "MinutesService.SaveAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Save_SentRevisionLocked()
    {
        var row = Revision(MinutesStatus.WaitingApproval);

        var outcome = await Service().SaveAsync(row.Id, _secretary, "<p>A.</p>", row.UpdatedAt);

        Assert.Equal(MinutesFailure.Locked, outcome.Failure);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.SaveAsync/UTC004 content over 300000 characters is refused")]
    [Trait("UTC", "MinutesService.SaveAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Save_OverlongContentRefused()
    {
        var row = Revision(MinutesStatus.Draft);

        var outcome = await Service().SaveAsync(row.Id, _secretary, new string('x', 300001), row.UpdatedAt);

        Assert.Equal(MinutesFailure.Invalid, outcome.Failure);
        var message = Assert.Single(outcome.Errors!["content"]);
        Assert.StartsWith("Nhập nội dung biên bản, tối đa 300", message);
        Assert.Equal("<p>Biên bản</p>", row.Content);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.SaveAsync/UTC005 save over a newer save is stale")]
    [Trait("UTC", "MinutesService.SaveAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Save_StaleCopyDiscarded()
    {
        var row = Revision(MinutesStatus.Draft);

        var outcome = await Service().SaveAsync(row.Id, _secretary, "<p>A.</p>", row.UpdatedAt.AddMinutes(-5));

        Assert.Equal(MinutesFailure.Stale, outcome.Failure);
        Assert.Equal("<p>Biên bản</p>", row.Content);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.SaveAsync/UTC006 storage failure while saving is a failed save")]
    [Trait("UTC", "MinutesService.SaveAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Save_SaveFailureLogged()
    {
        var row = Revision(MinutesStatus.Draft);
        _minutes.Fail = new PersistenceUnavailableException();

        var outcome = await Service().SaveAsync(row.Id, _secretary, "<p>A.</p>", row.UpdatedAt);

        Assert.Equal(MinutesFailure.SaveFailed, outcome.Failure);
        var record = Assert.Single(_logger.Records);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Contains("Could not save minutes", record.Message);
        Assert.Empty(_changes.MinutesChanged);
    }

    // MinutesService.SubmitAsync (Minutes.Submit/UTC001-UTC006)

    [Fact(DisplayName = "MinutesService.SubmitAsync/UTC001 secretary sends the draft and chair is told")]
    [Trait("UTC", "MinutesService.SubmitAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Submit_SendsDraftToChair()
    {
        var row = Revision(MinutesStatus.Draft);

        var outcome = await Service().SubmitAsync(row.Id, _secretary, row.UpdatedAt);

        Assert.Equal(MinutesFailure.None, outcome.Failure);
        Assert.Equal((MinutesStatus.WaitingApproval, _secretary, Now),
            (row.Status, row.SubmittedById, row.SubmittedAt!.Value));
        Assert.Equal(_chair, Assert.Single(_notifications.Added).UserId);
        Assert.Equal([_meeting], _changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.SubmitAsync/UTC002 chair does not send the minutes")]
    [Trait("UTC", "MinutesService.SubmitAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Submit_ChairRefused()
    {
        var row = Revision(MinutesStatus.Draft);

        var outcome = await Service().SubmitAsync(row.Id, _chair, row.UpdatedAt);

        Assert.Equal(MinutesFailure.NotSecretary, outcome.Failure);
        Assert.Equal(MinutesStatus.Draft, row.Status);
        Assert.Empty(_notifications.Added);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.SubmitAsync/UTC003 draft of a running meeting is not sent")]
    [Trait("UTC", "MinutesService.SubmitAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Submit_RunningMeetingRefused()
    {
        _meetings.Meetings[0].Status = MeetingStatus.InProgress;
        var row = Revision(MinutesStatus.Draft);

        var outcome = await Service().SubmitAsync(row.Id, _secretary, row.UpdatedAt);

        Assert.Equal(MinutesFailure.MeetingNotEnded, outcome.Failure);
        Assert.Equal(MinutesStatus.Draft, row.Status);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.SubmitAsync/UTC004 sent revision is not sent again")]
    [Trait("UTC", "MinutesService.SubmitAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Submit_SentRevisionLocked()
    {
        var row = Revision(MinutesStatus.WaitingApproval);

        var outcome = await Service().SubmitAsync(row.Id, _secretary, row.UpdatedAt);

        Assert.Equal(MinutesFailure.Locked, outcome.Failure);
        Assert.Empty(_notifications.Added);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.SubmitAsync/UTC005 submit over a newer save is stale")]
    [Trait("UTC", "MinutesService.SubmitAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Submit_StaleCopyDiscarded()
    {
        var row = Revision(MinutesStatus.Draft);

        var outcome = await Service().SubmitAsync(row.Id, _secretary, row.UpdatedAt.AddMinutes(-5));

        Assert.Equal(MinutesFailure.Stale, outcome.Failure);
        Assert.Equal(MinutesStatus.Draft, row.Status);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.SubmitAsync/UTC006 storage failure while submitting is a failed save")]
    [Trait("UTC", "MinutesService.SubmitAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Submit_SaveFailureLogged()
    {
        var row = Revision(MinutesStatus.Draft);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().SubmitAsync(row.Id, _secretary, row.UpdatedAt);

        Assert.Equal(MinutesFailure.SaveFailed, outcome.Failure);
        var record = Assert.Single(_logger.Records);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Contains("Could not submit minutes", record.Message);
        // The service mutates the tracked row before the save fails; the real
        // transaction rolls that back, so the fake can only show no commit.
        Assert.Equal(0, _unitOfWork.Commits);
        Assert.Empty(_changes.MinutesChanged);
    }

    // MinutesService.ApproveAsync (Minutes.Approve/UTC001-UTC006)

    [Fact(DisplayName = "MinutesService.ApproveAsync/UTC001 chair issues the waiting revision")]
    [Trait("UTC", "MinutesService.ApproveAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Approve_IssuesWithEvidenceAndEmails()
    {
        _sources.ActiveUsers.UnionWith([_secretary, _member]);
        var row = Revision(MinutesStatus.WaitingApproval);

        var outcome = await Service().ApproveAsync(row.Id, _chair, row.UpdatedAt);

        Assert.Equal(MinutesFailure.None, outcome.Failure);
        Assert.Equal((MinutesStatus.Issued, _chair, Now, Now), (row.Status, row.ApprovedById, row.ApprovedAt!.Value, row.IssuedAt!.Value));
        Assert.Equal(AuditActions.MinutesIssued, Assert.Single(_audit.Rows).Action);
        Assert.Equal(new[] { _member, _secretary }.Order(), _notifications.Added.Select(n => n.UserId).Order());
        Assert.Equal(new[] { _member, _secretary }.Order(),
            _emails.Added.Select(e => Guid.ParseExact(e.ToAddress[..32], "N")).Order());
        Assert.Equal([_meeting], _changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.ApproveAsync/UTC002 issuing a correction supersedes the earlier text")]
    [Trait("UTC", "MinutesService.ApproveAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Approve_CorrectionSupersedesIssued()
    {
        _sources.ActiveUsers.UnionWith([_secretary, _member]);
        var issued = Revision(MinutesStatus.Issued);
        var correction = Revision(MinutesStatus.WaitingApproval, 2, issued.Id);

        var outcome = await Service().ApproveAsync(correction.Id, _chair, correction.UpdatedAt);

        Assert.Equal(MinutesFailure.None, outcome.Failure);
        Assert.Equal((MinutesStatus.Issued, MinutesStatus.Superseded), (correction.Status, issued.Status));
        Assert.Equal(2, _emails.Added.Count);
        Assert.All(_emails.Added, e =>
            Assert.StartsWith("Bản đính chính biên bản đã ban hành: ", e.Subject));
        Assert.Equal([_meeting], _changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.ApproveAsync/UTC003 secretary may not approve")]
    [Trait("UTC", "MinutesService.ApproveAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Approve_SecretaryRefused()
    {
        var row = Revision(MinutesStatus.WaitingApproval);

        var outcome = await Service().ApproveAsync(row.Id, _secretary, row.UpdatedAt);

        Assert.Equal(MinutesFailure.NotChair, outcome.Failure);
        Assert.Equal(MinutesStatus.WaitingApproval, row.Status);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.ApproveAsync/UTC004 draft is not waiting for approval")]
    [Trait("UTC", "MinutesService.ApproveAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Approve_DraftNotWaiting()
    {
        var row = Revision(MinutesStatus.Draft);

        var outcome = await Service().ApproveAsync(row.Id, _chair, row.UpdatedAt);

        Assert.Equal(MinutesFailure.NotWaiting, outcome.Failure);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.ApproveAsync/UTC005 approve over a newer save is stale")]
    [Trait("UTC", "MinutesService.ApproveAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Approve_StaleCopyDiscarded()
    {
        var row = Revision(MinutesStatus.WaitingApproval);

        var outcome = await Service().ApproveAsync(row.Id, _chair, row.UpdatedAt.AddMinutes(-5));

        Assert.Equal(MinutesFailure.Stale, outcome.Failure);
        Assert.Equal(MinutesStatus.WaitingApproval, row.Status);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.ApproveAsync/UTC006 storage failure while issuing is a failed save")]
    [Trait("UTC", "MinutesService.ApproveAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Approve_SaveFailureLogged()
    {
        var row = Revision(MinutesStatus.WaitingApproval);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().ApproveAsync(row.Id, _chair, row.UpdatedAt);

        Assert.Equal(MinutesFailure.SaveFailed, outcome.Failure);
        var record = Assert.Single(_logger.Records);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Contains("Could not approve minutes", record.Message);
        // The service mutates the tracked row before the save fails; the real
        // transaction rolls that back, so the fake can only show no commit.
        Assert.Equal(0, _unitOfWork.Commits);
        Assert.Empty(_changes.MinutesChanged);
    }

    // MinutesService.ReturnAsync (Minutes.Return/UTC001-UTC006)

    [Fact(DisplayName = "MinutesService.ReturnAsync/UTC001 chair returns the revision with a reason")]
    [Trait("UTC", "MinutesService.ReturnAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Return_SendsBackWithReason()
    {
        var row = Revision(MinutesStatus.WaitingApproval);

        var outcome = await Service().ReturnAsync(row.Id, _chair, "Bổ sung kết luận của mục 2.", row.UpdatedAt);

        Assert.Equal(MinutesFailure.None, outcome.Failure);
        Assert.Equal((MinutesStatus.Rejected, "Bổ sung kết luận của mục 2.", _chair),
            (row.Status, row.RejectionReason, row.ReturnedById));
        Assert.Equal(_secretary, Assert.Single(_notifications.Added).UserId);
        Assert.Equal([_meeting], _changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.ReturnAsync/UTC002 secretary may not return")]
    [Trait("UTC", "MinutesService.ReturnAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Return_SecretaryRefused()
    {
        var row = Revision(MinutesStatus.WaitingApproval);

        var outcome = await Service().ReturnAsync(row.Id, _secretary, "Bổ sung kết luận.", row.UpdatedAt);

        Assert.Equal(MinutesFailure.NotChair, outcome.Failure);
        Assert.Equal(MinutesStatus.WaitingApproval, row.Status);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.ReturnAsync/UTC003 draft is not waiting for approval")]
    [Trait("UTC", "MinutesService.ReturnAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Return_DraftNotWaiting()
    {
        var row = Revision(MinutesStatus.Draft);

        var outcome = await Service().ReturnAsync(row.Id, _chair, "Bổ sung kết luận.", row.UpdatedAt);

        Assert.Equal(MinutesFailure.NotWaiting, outcome.Failure);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.ReturnAsync/UTC004 blank reason is refused")]
    [Trait("UTC", "MinutesService.ReturnAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Return_BlankReasonRefused()
    {
        var row = Revision(MinutesStatus.WaitingApproval);

        var outcome = await Service().ReturnAsync(row.Id, _chair, "   ", row.UpdatedAt);

        Assert.Equal(MinutesFailure.ReasonInvalid, outcome.Failure);
        Assert.Equal(MinutesStatus.WaitingApproval, row.Status);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.ReturnAsync/UTC005 reason over 2000 characters is refused")]
    [Trait("UTC", "MinutesService.ReturnAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Return_OverlongReasonRefused()
    {
        var row = Revision(MinutesStatus.WaitingApproval);

        var outcome = await Service().ReturnAsync(row.Id, _chair, new string('l', 2001), row.UpdatedAt);

        Assert.Equal(MinutesFailure.ReasonInvalid, outcome.Failure);
        Assert.Equal(MinutesStatus.WaitingApproval, row.Status);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.ReturnAsync/UTC006 return over a newer save is stale")]
    [Trait("UTC", "MinutesService.ReturnAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Return_StaleCopyDiscarded()
    {
        var row = Revision(MinutesStatus.WaitingApproval);

        var outcome = await Service().ReturnAsync(row.Id, _chair, "Bổ sung kết luận.", row.UpdatedAt.AddMinutes(-5));

        Assert.Equal(MinutesFailure.Stale, outcome.Failure);
        Assert.Equal(MinutesStatus.WaitingApproval, row.Status);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    // MinutesService.CorrectAsync (Minutes.Correct/UTC001-UTC006)

    [Fact(DisplayName = "MinutesService.CorrectAsync/UTC001 secretary opens a correction from the issued text")]
    [Trait("UTC", "MinutesService.CorrectAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Correct_OpensDraftCorrection()
    {
        var issued = Revision(MinutesStatus.Issued);

        var outcome = await Service().CorrectAsync(issued.Id, _secretary);

        Assert.Equal(MinutesFailure.None, outcome.Failure);
        var correction = _minutes.Rows.Single(m => m.RevisionNumber == 2);
        Assert.Equal((issued.Id, issued.Content, MinutesStatus.Draft),
            (correction.SupersedesId!.Value, correction.Content, correction.Status));
        Assert.Empty(_notifications.Added);
        Assert.Equal([_meeting], _changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.CorrectAsync/UTC002 member is told the issued text is not theirs to write")]
    [Trait("UTC", "MinutesService.CorrectAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Correct_MemberForbidden()
    {
        var issued = Revision(MinutesStatus.Issued);

        var outcome = await Service().CorrectAsync(issued.Id, _member);

        Assert.Equal(MinutesFailure.Forbidden, outcome.Failure);
        Assert.Single(_minutes.Rows);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.CorrectAsync/UTC003 draft revision has nothing to correct")]
    [Trait("UTC", "MinutesService.CorrectAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Correct_DraftNotIssued()
    {
        var row = Revision(MinutesStatus.Draft);

        var outcome = await Service().CorrectAsync(row.Id, _secretary);

        Assert.Equal(MinutesFailure.NotIssued, outcome.Failure);
        Assert.Single(_minutes.Rows);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.CorrectAsync/UTC004 second correction while one is open is refused")]
    [Trait("UTC", "MinutesService.CorrectAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Correct_SecondCorrectionRefused()
    {
        var issued = Revision(MinutesStatus.Issued);
        Revision(MinutesStatus.Draft, 2, issued.Id);

        var outcome = await Service().CorrectAsync(issued.Id, _secretary);

        Assert.Equal(MinutesFailure.CorrectionInProgress, outcome.Failure);
        Assert.Equal(2, _minutes.Rows.Count);
        Assert.Empty(_changes.MinutesChanged);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.CorrectAsync/UTC005 simultaneous corrections keep the first one")]
    [Trait("UTC", "MinutesService.CorrectAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Correct_ConcurrentCorrectionWarned()
    {
        var issued = Revision(MinutesStatus.Issued);
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.Unknown);

        var outcome = await Service().CorrectAsync(issued.Id, _secretary);

        Assert.Equal(MinutesFailure.CorrectionInProgress, outcome.Failure);
        var record = Assert.Single(_logger.Records);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("Concurrent minutes correct", record.Message);
        Assert.Empty(_changes.MinutesChanged);
    }

    [Fact(DisplayName = "MinutesService.CorrectAsync/UTC006 storage failure while correcting is a failed save")]
    [Trait("UTC", "MinutesService.CorrectAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Correct_SaveFailureLogged()
    {
        var issued = Revision(MinutesStatus.Issued);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().CorrectAsync(issued.Id, _secretary);

        Assert.Equal(MinutesFailure.SaveFailed, outcome.Failure);
        var record = Assert.Single(_logger.Records);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Contains("Could not correct minutes", record.Message);
        Assert.Empty(_changes.MinutesChanged);
    }

    // MinutesService.ExportAsync (Minutes.Export/UTC001-UTC005)

    [Fact(DisplayName = "MinutesService.ExportAsync/UTC001 officer downloads the issued revision as Word")]
    [Trait("UTC", "MinutesService.ExportAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Export_OfficerDownloadsDocx()
    {
        var row = Revision(MinutesStatus.Issued);

        var outcome = await Service().ExportAsync(row.Id, _secretary, MinutesExportFormat.Docx);

        Assert.Equal(MinutesFailure.None, outcome.Failure);
        var file = Assert.IsType<MinutesFile>(outcome.Data);
        Assert.Equal("Biên bản - Họp tổng kết.docx", file.FileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", file.ContentType);
        Assert.NotEmpty(file.Bytes);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.ExportAsync/UTC002 unknown revision downloads nothing")]
    [Trait("UTC", "MinutesService.ExportAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Export_UnknownIdNotFound()
    {
        var outcome = await Service().ExportAsync(Guid.CreateVersion7(), _secretary, MinutesExportFormat.Docx);

        Assert.Equal(MinutesFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.Data);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.ExportAsync/UTC003 reader may not download a draft")]
    [Trait("UTC", "MinutesService.ExportAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Export_DraftHiddenFromReader()
    {
        var row = Revision(MinutesStatus.Draft);

        var outcome = await Service().ExportAsync(row.Id, _member, MinutesExportFormat.Docx);

        Assert.Equal(MinutesFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.Data);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.ExportAsync/UTC004 secret minutes stay on screen only")]
    [Trait("UTC", "MinutesService.ExportAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Export_SecretMeetingRefused()
    {
        _meetings.Meetings[0].Sensitivity = Sensitivity.Secret;
        var row = Revision(MinutesStatus.Issued);

        var outcome = await Service().ExportAsync(row.Id, _chair, MinutesExportFormat.Docx);

        Assert.Equal(MinutesFailure.SecretMeeting, outcome.Failure);
        Assert.Null(outcome.Data);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.ExportAsync/UTC005 long title is cut to 120 characters")]
    [Trait("UTC", "MinutesService.ExportAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Export_LongTitleTruncated()
    {
        _meetings.Meetings[0].Title = new string('T', 150);
        var row = Revision(MinutesStatus.Issued);

        var outcome = await Service().ExportAsync(row.Id, _secretary, MinutesExportFormat.Docx);

        Assert.Equal(MinutesFailure.None, outcome.Failure);
        var file = Assert.IsType<MinutesFile>(outcome.Data);
        Assert.Equal(125, file.FileName.Length);
        Assert.StartsWith("Biên bản - ", file.FileName);
        Assert.EndsWith(".docx", file.FileName);
        Assert.Empty(_logger.Records);
    }

    // MinutesService.GetRevisionAsync (Minutes.GetRevision/UTC001-UTC004)

    [Fact(DisplayName = "MinutesService.GetRevisionAsync/UTC001 officer opens a superseded revision")]
    [Trait("UTC", "MinutesService.GetRevisionAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task GetRevision_OfficerSeesSuperseded()
    {
        var first = Revision(MinutesStatus.Superseded);
        Revision(MinutesStatus.Issued, 2, first.Id);

        var outcome = await Service().GetRevisionAsync(first.Id, _secretary);

        Assert.Equal(MinutesFailure.None, outcome.Failure);
        var state = Assert.IsType<MinutesState>(outcome.Data);
        Assert.Equal(first.Id, state.Minutes!.Id);
        Assert.Equal(2, state.History!.Count);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.GetRevisionAsync/UTC002 reader sees the issued text without officer fields")]
    [Trait("UTC", "MinutesService.GetRevisionAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task GetRevision_ReaderSeesMaskedIssued()
    {
        var first = Revision(MinutesStatus.Superseded);
        var second = Revision(MinutesStatus.Issued, 2, first.Id);

        var outcome = await Service().GetRevisionAsync(second.Id, _member);

        Assert.Equal(MinutesFailure.None, outcome.Failure);
        var state = Assert.IsType<MinutesState>(outcome.Data);
        Assert.Equal(second.Id, state.Minutes!.Id);
        Assert.Null(state.Minutes.SubmittedBy);
        Assert.Null(state.Minutes.RejectionReason);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.GetRevisionAsync/UTC003 unissued correction stays hidden from readers")]
    [Trait("UTC", "MinutesService.GetRevisionAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task GetRevision_DraftCorrectionHidden()
    {
        var first = Revision(MinutesStatus.Issued);
        var correction = Revision(MinutesStatus.Draft, 2, first.Id);

        var outcome = await Service().GetRevisionAsync(correction.Id, _member);

        Assert.Equal(MinutesFailure.NotFound, outcome.Failure);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "MinutesService.GetRevisionAsync/UTC005 storage failure while reading is a failed read")]
    [Trait("UTC", "MinutesService.GetRevisionAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task GetRevision_ReadFailureLogged()
    {
        var row = Revision(MinutesStatus.Issued);
        _summaries.Fail = new PersistenceUnavailableException();

        var outcome = await Service().GetRevisionAsync(row.Id, _secretary);

        Assert.Equal(MinutesFailure.ReadFailed, outcome.Failure);
        var record = Assert.Single(_logger.Records);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Contains("Could not read minutes", record.Message);
    }

    [Fact(DisplayName = "MinutesService.GetRevisionAsync/UTC004 unknown revision reads nothing")]
    [Trait("UTC", "MinutesService.GetRevisionAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task GetRevision_UnknownIdNotFound()
    {
        var outcome = await Service().GetRevisionAsync(Guid.CreateVersion7(), _secretary);

        Assert.Equal(MinutesFailure.NotFound, outcome.Failure);
        Assert.Empty(_logger.Records);
    }

    // MinutesTemplate.Build (MinutesTemplate.Build/UTC001-UTC008)

    private static readonly TimeZoneInfo Hanoi = SummaryWriter.FindHanoi();

    private static MinutesRecords TemplateRecords(
        string title = "Họp quý 3",
        DateTimeOffset? startedAt = null,
        DateTimeOffset? endedAt = null,
        string place = "Phòng A",
        string? chair = "Nguyễn An",
        string? secretary = "Lê Chi",
        IReadOnlyList<MinutesPersonLine>? people = null,
        IReadOnlyList<AgendaFacts>? agenda = null,
        IReadOnlyList<string>? votes = null,
        IReadOnlyList<MinutesNote>? notes = null) => new(title,
            startedAt ?? new DateTimeOffset(2026, 11, 10, 2, 0, 0, TimeSpan.Zero), endedAt, place, chair, secretary,
            people ?? [new("Nguyễn An", "Chủ trì", false), new("Trần Bình", "Thành viên", false)],
            agenda ?? [], votes ?? [], notes);

    [Fact(DisplayName = "MinutesTemplate.Build/UTC001 full records render the fixed template")]
    [Trait("UTC", "MinutesTemplate.Build/UTC001")]
    [Trait("Trait", "UTC")]
    public void Build_FullRecordsRender()
    {
        var text = MinutesTemplate.Build(TemplateRecords(), null, Hanoi);

        Assert.Contains("# BIÊN BẢN", text);
        Assert.Contains("09 giờ 00 phút, ngày 10/11/2026.", text);
        Assert.Contains("**Địa điểm:** Phòng A.", text);
        Assert.Contains("02 người có mặt, gồm:", text);
        Assert.Contains("- Nguyễn An, Chủ trì;", text);
        Assert.Contains("- Trần Bình, Thành viên.", text);
        Assert.EndsWith("\n", text);
    }

    [Fact(DisplayName = "MinutesTemplate.Build/UTC002 vertical bar in a name does not split the column")]
    [Trait("UTC", "MinutesTemplate.Build/UTC002")]
    [Trait("Trait", "UTC")]
    public void Build_PipeInNameNeutralized()
    {
        var text = MinutesTemplate.Build(TemplateRecords(secretary: "Lê | Chi"), null, Hanoi);

        Assert.Contains("Lê / Chi", text);
    }

    [Fact(DisplayName = "MinutesTemplate.Build/UTC003 agenda items render under section II")]
    [Trait("UTC", "MinutesTemplate.Build/UTC003")]
    [Trait("Trait", "UTC")]
    public void Build_AgendaRendersInSectionTwo()
    {
        var item1 = Guid.CreateVersion7();
        var agenda = new[]
        {
            new AgendaFacts(item1, "Báo cáo", "Phạm Dũng", null, null, null),
            new AgendaFacts(Guid.CreateVersion7(), "Khác", null, null, null, null),
        };

        var text = MinutesTemplate.Build(TemplateRecords(agenda: agenda), null, Hanoi);

        Assert.Contains("### 1. Báo cáo", text);
        Assert.Contains("Phạm Dũng trình bày. ……", text);
        Assert.Contains("### 2. Khác", text);
    }

    [Fact(DisplayName = "MinutesTemplate.Build/UTC004 conclusions and vote lines render under section III")]
    [Trait("UTC", "MinutesTemplate.Build/UTC004")]
    [Trait("Trait", "UTC")]
    public void Build_ConclusionsAndVotesRender()
    {
        var agenda = new[] { new AgendaFacts(Guid.CreateVersion7(), "Ngân sách", null, "Duyệt ngân sách", null, null) };

        var text = MinutesTemplate.Build(TemplateRecords(agenda: agenda,
            votes: ["Chọn địa điểm: Thông qua", "Mua máy: Hoãn"]), null, Hanoi);

        Assert.Contains("### 1. Kết luận của chủ trì", text);
        Assert.Contains("- Ngân sách: Duyệt ngân sách", text);
        Assert.Contains("Kết quả biểu quyết:", text);
        Assert.Contains("- Chọn địa điểm: Thông qua", text);
    }

    [Fact(DisplayName = "MinutesTemplate.Build/UTC005 secretary notes are routed by their marks")]
    [Trait("UTC", "MinutesTemplate.Build/UTC005")]
    [Trait("Trait", "UTC")]
    public void Build_NotesRoutedByMarks()
    {
        var item = Guid.CreateVersion7();
        var agenda = new[] { new AgendaFacts(item, "Báo cáo", null, null, null, null) };
        var notes = new[]
        {
            new MinutesNote(item, "Ghi nhận tiến độ"),
            new MinutesNote(item, "Kết luận: Duyệt"),
            new MinutesNote(item, "Nhiệm vụ: Gửi báo cáo"),
        };

        var text = MinutesTemplate.Build(TemplateRecords(agenda: agenda, notes: notes), null, Hanoi);

        Assert.Contains("Ghi nhận tiến độ", text);
        Assert.Contains("- Báo cáo: Duyệt", text);
        Assert.Contains("- Báo cáo: Gửi báo cáo", text);
    }

    [Fact(DisplayName = "MinutesTemplate.Build/UTC006 reviewed draft replaces the records")]
    [Trait("UTC", "MinutesTemplate.Build/UTC006")]
    [Trait("Trait", "UTC")]
    public void Build_DraftReplacesRecords()
    {
        var agenda = new[] { new AgendaFacts(Guid.CreateVersion7(), "Báo cáo", null, null, null, null) };
        var draft = new SummaryDraft("Mục tiêu A", "1. Báo cáo\nNội dung thảo luận",
            "1. Kết luận của chủ trì\nDuyệt", []);

        var text = MinutesTemplate.Build(TemplateRecords(agenda: agenda), draft, Hanoi);

        Assert.Contains("Mục tiêu A", text);
        Assert.Contains("### 1. Báo cáo", text);
        Assert.Contains("Nội dung thảo luận", text);
        Assert.Contains("### 1. Kết luận của chủ trì", text);
    }

    [Fact(DisplayName = "MinutesTemplate.Build/UTC007 hostile draft text stays a plain line")]
    [Trait("UTC", "MinutesTemplate.Build/UTC007")]
    [Trait("Trait", "UTC")]
    public void Build_HostileDraftNeutralized()
    {
        var draft = new SummaryDraft("", "# BIÊN BẢN GIẢ\n@@ A | B\n**đậm**", "", []);

        var text = MinutesTemplate.Build(TemplateRecords(), draft, Hanoi);

        Assert.Contains("BIÊN BẢN GIẢ", text);
        Assert.DoesNotContain("# BIÊN BẢN GIẢ", text);
        Assert.Contains("A | B", text);
        Assert.DoesNotContain("**đậm**", text);
        Assert.Contains("đậm", text);
    }

    [Fact(DisplayName = "MinutesTemplate.Build/UTC008 only short numbered lines become sub-headings")]
    [Trait("UTC", "MinutesTemplate.Build/UTC008")]
    [Trait("Trait", "UTC")]
    public void Build_NumberedLineLengthRule()
    {
        var draft = new SummaryDraft("", $"1. {new string('x', 152)}\n1. {new string('y', 149)}", "", []);

        var text = MinutesTemplate.Build(TemplateRecords(), draft, Hanoi);

        Assert.Contains($"1. {new string('x', 152)}", text);
        Assert.DoesNotContain($"### 1. {new string('x', 152)}", text);
        Assert.Contains($"### 1. {new string('y', 149)}", text);
    }

    // Notes.Discussion and Notes.Read are the private reader inside MinutesTemplate. They cannot be called, so each UTC
    // drives MinutesTemplate.Build with no draft and reads the section the reader feeds.

    private static readonly Guid ReportItem = Guid.CreateVersion7();
    private static readonly Guid BudgetItem = Guid.CreateVersion7();

    private static IReadOnlyList<AgendaFacts> TwoItems() =>
    [
        new(ReportItem, "Báo cáo", null, null, null, null),
        new(BudgetItem, "Ngân sách", null, null, null, null),
    ];

    private static string BuildWithNotes(params MinutesNote[] notes)
        => MinutesTemplate.Build(TemplateRecords(agenda: TwoItems(), notes: notes), null, Hanoi).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>The text between two headings of the built minutes.</summary>
    private static string Between(string text, string from, string to)
    {
        var start = text.IndexOf(from, StringComparison.Ordinal) + from.Length;
        return text[start..text.IndexOf(to, start, StringComparison.Ordinal)];
    }

    private static string SectionTwo(string text) => Between(text, SummaryWriter.DiscussionHeading, SummaryWriter.ConclusionsHeading);

    [Fact(DisplayName = "Notes.Discussion/UTC001: the meeting-wide note comes before the first item")]
    [Trait("UTC", "Notes.Discussion/UTC001")]
    [Trait("Trait", "UTC")]
    public void NotesDiscussion_MeetingWideNote_OpensSectionTwo()
    {
        var section = SectionTwo(BuildWithNotes(new MinutesNote(null, "Mở đầu")));

        Assert.Equal("Mở đầu", section[..section.IndexOf("### 1.", StringComparison.Ordinal)].Trim());
    }

    [Fact(DisplayName = "Notes.Discussion/UTC002: unmarked lines of an item stay under it in note order")]
    [Trait("UTC", "Notes.Discussion/UTC002")]
    [Trait("Trait", "UTC")]
    public void NotesDiscussion_UnmarkedLines_StayUnderTheirItemInOrder()
    {
        var section = SectionTwo(BuildWithNotes(new MinutesNote(ReportItem, "Dòng một\nDòng hai")));

        var item = Between(section, "### 1. Báo cáo", "### 2. Ngân sách");
        Assert.Equal(["Dòng một", "Dòng hai"], item.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    [Fact(DisplayName = "Notes.Discussion/UTC003: a marked line leaves the item's discussion for section III")]
    [Trait("UTC", "Notes.Discussion/UTC003")]
    [Trait("Trait", "UTC")]
    public void NotesDiscussion_OnlyMarkedLine_LeavesTheItemEmpty()
    {
        var text = BuildWithNotes(new MinutesNote(ReportItem, "Kết luận: Duyệt"));

        var item = Between(SectionTwo(text), "### 1. Báo cáo", "### 2. Ngân sách");
        Assert.DoesNotContain("Duyệt", item);
        Assert.Contains("- Báo cáo: Duyệt", text);
    }

    [Fact(DisplayName = "Notes.Discussion/UTC004: a note on an unknown item shows nowhere")]
    [Trait("UTC", "Notes.Discussion/UTC004")]
    [Trait("Trait", "UTC")]
    public void NotesDiscussion_UnknownItem_ReadsEmpty()
    {
        var text = BuildWithNotes(new MinutesNote(Guid.CreateVersion7(), "Ghi chú lạc"));

        Assert.DoesNotContain("Ghi chú lạc", text);
        Assert.Contains($"### 1. Báo cáo\n{MinutesTemplate.Blank}", text.Replace("\r\n", "\n"));
    }

    [Fact(DisplayName = "Notes.Discussion/UTC005: with no meeting-wide note section II starts at the first item")]
    [Trait("UTC", "Notes.Discussion/UTC005")]
    [Trait("Trait", "UTC")]
    public void NotesDiscussion_NoMeetingWideNote_SectionStartsAtFirstItem()
    {
        var section = SectionTwo(BuildWithNotes(new MinutesNote(ReportItem, "Ý kiến riêng")));

        Assert.StartsWith("### 1. Báo cáo", section.Trim());
    }

    [Fact(DisplayName = "Notes.Read/UTC001: meeting-wide note first, then items in agenda order")]
    [Trait("UTC", "Notes.Read/UTC001")]
    [Trait("Trait", "UTC")]
    public void NotesRead_OutOfOrderNotes_AreSortedForTheTemplate()
    {
        var section = SectionTwo(BuildWithNotes(new MinutesNote(BudgetItem, "Chốt giá"), new MinutesNote(null, "Mở đầu")));

        Assert.True(section.IndexOf("Mở đầu", StringComparison.Ordinal) < section.IndexOf("### 1. Báo cáo", StringComparison.Ordinal));
        Assert.Contains("Chốt giá", Between(section + "\n#END", "### 2. Ngân sách", "#END"));
    }

    [Fact(DisplayName = "Notes.Read/UTC002: a Kết luận line becomes a conclusion prefixed with its item")]
    [Trait("UTC", "Notes.Read/UTC002")]
    [Trait("Trait", "UTC")]
    public void NotesRead_ConclusionLine_GoesToSectionThree()
    {
        var text = BuildWithNotes(new MinutesNote(ReportItem, "Kết luận: Duyệt"));

        Assert.Contains("- Báo cáo: Duyệt", Between(text, SummaryWriter.ChairHeading, SummaryWriter.TasksHeading));
        Assert.DoesNotContain("Kết luận", Between(SectionTwo(text), "### 1. Báo cáo", "### 2. Ngân sách"));
    }

    [Fact(DisplayName = "Notes.Read/UTC003: a Nhiệm vụ line of the meeting-wide note has no item prefix")]
    [Trait("UTC", "Notes.Read/UTC003")]
    [Trait("Trait", "UTC")]
    public void NotesRead_TaskLine_GoesUnderTasksWithoutPrefix()
    {
        var text = BuildWithNotes(new MinutesNote(null, "Nhiệm vụ: Gửi biên bản"));

        var tasks = text[text.IndexOf(SummaryWriter.TasksHeading, StringComparison.Ordinal)..];
        Assert.Contains("\n- Gửi biên bản", tasks);
        Assert.DoesNotContain(": Gửi biên bản", tasks);
    }

    [Fact(DisplayName = "Notes.Read/UTC004: a mark with nothing after the colon stays a discussion line")]
    [Trait("UTC", "Notes.Read/UTC004")]
    [Trait("Trait", "UTC")]
    public void NotesRead_EmptyConclusion_StaysInTheItem()
    {
        var text = BuildWithNotes(new MinutesNote(ReportItem, "Kết luận:"));

        Assert.Contains("Kết luận:", Between(SectionTwo(text), "### 1. Báo cáo", "### 2. Ngân sách"));
        Assert.Equal(MinutesTemplate.Blank, Between(text, SummaryWriter.ChairHeading, SummaryWriter.TasksHeading).Replace("###", "").Trim());
    }

    [Fact(DisplayName = "Notes.Read/UTC005: a note on an item no longer in the agenda is skipped")]
    [Trait("UTC", "Notes.Read/UTC005")]
    [Trait("Trait", "UTC")]
    public void NotesRead_StrayItemNote_IsSkipped()
    {
        var text = BuildWithNotes(new MinutesNote(Guid.CreateVersion7(), "Kết luận: Không còn mục"), new MinutesNote(Guid.CreateVersion7(), "Dòng lạc"));

        Assert.DoesNotContain("Không còn mục", text);
        Assert.DoesNotContain("Dòng lạc", text);
    }

    [Fact(DisplayName = "Notes.Read/UTC006: heading and signature marks in a note line are stripped")]
    [Trait("UTC", "Notes.Read/UTC006")]
    [Trait("Trait", "UTC")]
    public void NotesRead_HeadingAndSignatureMarks_AreStripped()
    {
        var text = BuildWithNotes(new MinutesNote(ReportItem, "## Tiêu đề\n@@ x | y")).Replace("\r\n", "\n");

        Assert.Contains("\nTiêu đề\n", text);
        Assert.Contains("\nx | y\n", text);
        Assert.DoesNotContain("## Tiêu đề", text);
        Assert.DoesNotContain("@@ x", text);
    }

    /// <summary>Captures what the service logs, so a test can assert the log line of its UTC.</summary>
    private sealed class FakeRound1Logger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Records { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoScope.Instance;

        private sealed class NoScope : IDisposable
        {
            public static readonly NoScope Instance = new();

            public void Dispose()
            {
            }
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Records.Add((logLevel, formatter(state, exception)));
    }

    /// <summary>Records the realtime minutes-changed pushes the service emits after a committed write.</summary>
    private sealed class FakeMeetingChangePublisher : IMeetingChangePublisher
    {
        public List<Guid> MinutesChanged { get; } = [];

        public Task MeetingChangedAsync(Guid meetingId, CancellationToken ct = default) => Task.CompletedTask;

        public Task MinutesChangedAsync(Guid meetingId, CancellationToken ct = default)
        {
            MinutesChanged.Add(meetingId);
            return Task.CompletedTask;
        }

        public Task CommentsChangedAsync(Guid meetingId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
