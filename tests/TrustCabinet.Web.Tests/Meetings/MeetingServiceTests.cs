using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Infrastructure.Meetings;

namespace TrustCabinet.Web.Tests.Meetings;

public class MeetingServiceTests
{
    private readonly FakeMeetingRepository _repository = new();
    private readonly Guid _actorId = Guid.CreateVersion7();
    private readonly Guid _otherActorId = Guid.CreateVersion7();

    private MeetingService NewService()
        => new(_repository, NullLogger<MeetingService>.Instance);

    private static MeetingInput ValidInput(
        string title = "Giao ban tuần",
        string startsAt = "2026-10-01T09:00:00+07:00",
        string? endsAt = "2026-10-01T10:00:00+07:00",
        string? mode = "InPerson",
        string location = "Phòng họp A",
        string? sensitivity = "Internal",
        string description = "Nội dung",
        Guid? assignedApproverId = null)
        => new(
            title, startsAt, endsAt, mode, location, sensitivity, description,
            assignedApproverId ?? Guid.CreateVersion7());

    private Meeting GivenMeeting(MeetingStatus status = MeetingStatus.Draft, Guid? creatorId = null)
    {
        var meeting = new Meeting
        {
            Title = "Giao ban tuần",
            Description = "Nội dung",
            Status = status,
            Sensitivity = Sensitivity.Internal,
            Mode = MeetingMode.InPerson,
            Location = "Phòng họp A",
            StartsAt = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero),
            EndsAt = new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero),
            CreatedById = creatorId ?? _actorId,
            AssignedApproverId = _actorId,
        };
        _repository.Meetings.Add(meeting);
        return meeting;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_rejects_a_missing_or_blank_title(string? title)
    {
        var input = ValidInput(title: title!);

        var result = await NewService().CreateAsync(input, _actorId);

        Assert.Equal(MeetingOutcome.InvalidInformation, result.Outcome);
        Assert.Contains("title", result.Errors!.Keys);
        Assert.Empty(_repository.Meetings);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Validation_collects_field_errors_and_enforces_lengths()
    {
        var result = await NewService().CreateAsync(
            ValidInput(
                title: new string('a', 301),
                startsAt: "2026-10-01T09:00:00",
                endsAt: "2026-10-01T09:00:00+07:00",
                mode: "0",
                location: new string('b', 301),
                sensitivity: "not-a-sensitivity",
                description: new string('c', 4001)),
            _actorId);

        Assert.Equal(MeetingOutcome.InvalidInformation, result.Outcome);
        Assert.Equal(6, result.Errors!.Count);
        Assert.Contains("title", result.Errors.Keys);
        Assert.Contains("startsAt", result.Errors.Keys);
        Assert.Contains("mode", result.Errors.Keys);
        Assert.Contains("location", result.Errors.Keys);
        Assert.Contains("sensitivity", result.Errors.Keys);
        Assert.Contains("description", result.Errors.Keys);
    }

    [Theory]
    [InlineData("InPerson", "Normal")]
    [InlineData("Online", "Internal")]
    [InlineData("Hybrid", "Confidential")]
    [InlineData("InPerson", "Secret")]
    public async Task Create_accepts_each_supported_mode_and_sensitivity(string mode, string sensitivity)
    {
        var result = await NewService().CreateAsync(
            ValidInput(title: "  Giao ban  ", mode: mode, sensitivity: sensitivity,
                location: "  Phòng A  ", description: "Có\nxuống dòng"),
            _actorId);

        Assert.True(result.Succeeded);
        var meeting = Assert.Single(_repository.Meetings);
        Assert.Equal("Giao ban", meeting.Title);
        Assert.Equal("Phòng A", meeting.Location);
        Assert.Equal("Có\nxuống dòng", meeting.Description);
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Equal(_actorId, meeting.CreatedById);
        Assert.Equal(TimeSpan.Zero, meeting.StartsAt.Offset);
        var history = Assert.Single(meeting.StatusHistory);
        Assert.Null(history.FromStatus);
        Assert.Equal(MeetingStatus.Draft, history.ToStatus);
        Assert.Equal(_actorId, history.ActorId);
    }

    [Theory]
    [InlineData("2026-10-01T09:00:00+07:00", "2026-10-01T09:00:00+07:00")]
    [InlineData("2026-10-01T09:00:00+07:00", "2026-10-01T08:59:59+07:00")]
    [InlineData("2026-10-01T09:00:00+07:00", "not-a-timestamp")]
    public async Task Create_rejects_an_end_time_that_is_not_after_the_start(
        string startsAt, string endsAt)
    {
        var result = await NewService().CreateAsync(
            ValidInput(startsAt: startsAt, endsAt: endsAt), _actorId);

        Assert.Equal(MeetingOutcome.InvalidInformation, result.Outcome);
        Assert.Contains("endsAt", result.Errors!.Keys);
        Assert.Empty(_repository.Meetings);
    }

    [Fact]
    public async Task Create_allows_an_optional_end_time_and_uses_form_defaults()
    {
        var result = await NewService().CreateAsync(
            ValidInput(endsAt: null, mode: null, sensitivity: null), _actorId);

        Assert.True(result.Succeeded);
        var meeting = Assert.Single(_repository.Meetings);
        Assert.Null(meeting.EndsAt);
        Assert.Equal(MeetingMode.InPerson, meeting.Mode);
        Assert.Equal(Sensitivity.Secret, meeting.Sensitivity);
    }

    [Fact]
    public async Task Create_accepts_the_documented_maximum_lengths_and_utc_timestamps()
    {
        var result = await NewService().CreateAsync(
            ValidInput(
                title: new string('t', 300),
                startsAt: "2026-10-01T09:00:00Z",
                endsAt: "2026-10-01T10:00:00Z",
                location: new string('l', 300),
                description: new string('d', 4000)),
            _actorId);

        Assert.True(result.Succeeded);
        var meeting = Assert.Single(_repository.Meetings);
        Assert.Equal(300, meeting.Title.Length);
        Assert.Equal(300, meeting.Location.Length);
        Assert.Equal(4000, meeting.Description.Length);
        Assert.Equal(TimeSpan.Zero, meeting.StartsAt.Offset);
        Assert.Equal(TimeSpan.Zero, meeting.EndsAt!.Value.Offset);
    }

    [Fact]
    public async Task Create_rejects_a_missing_approver()
    {
        var input = new MeetingInput(
            "Giao ban tuần",
            "2026-10-01T09:00:00+07:00",
            null,
            null,
            null,
            null,
            null,
            null);

        var result = await NewService().CreateAsync(input, _actorId);

        Assert.Equal(MeetingOutcome.InvalidInformation, result.Outcome);
        Assert.Contains("assignedApproverId", result.Errors!.Keys);
        Assert.Empty(_repository.Meetings);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Create_rejects_an_approver_without_the_required_capability()
    {
        var approverId = Guid.CreateVersion7();
        _repository.IneligibleApproverIds.Add(approverId);

        var result = await NewService().CreateAsync(
            ValidInput(assignedApproverId: approverId), _actorId);

        Assert.Equal(MeetingOutcome.InvalidInformation, result.Outcome);
        Assert.Contains("assignedApproverId", result.Errors!.Keys);
        Assert.Empty(_repository.Meetings);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.Rejected)]
    public async Task Update_changes_the_editable_fields_and_preserves_status(MeetingStatus status)
    {
        var meeting = GivenMeeting(status);
        var priorHistory = meeting.StatusHistory.ToArray();

        var result = await NewService().UpdateAsync(
            meeting.Id,
            ValidInput(title: "  Giao ban mới  ", mode: "Hybrid", sensitivity: "Secret"),
            _actorId);

        Assert.True(result.Succeeded);
        var saved = Assert.Single(_repository.Meetings);
        Assert.Equal("Giao ban mới", saved.Title);
        Assert.Equal(MeetingMode.Hybrid, saved.Mode);
        Assert.Equal(Sensitivity.Secret, saved.Sensitivity);
        Assert.Equal(status, saved.Status);
        Assert.Equal(_actorId, saved.CreatedById);
        Assert.Equal(priorHistory, saved.StatusHistory);
    }

    [Fact]
    public async Task Update_allows_an_optional_end_time_and_uses_form_defaults()
    {
        var meeting = GivenMeeting();

        var result = await NewService().UpdateAsync(
            meeting.Id,
            ValidInput(endsAt: null, mode: null, sensitivity: null, assignedApproverId: _actorId),
            _actorId);

        Assert.True(result.Succeeded);
        var saved = Assert.Single(_repository.Meetings);
        Assert.Null(saved.EndsAt);
        Assert.Equal(MeetingMode.InPerson, saved.Mode);
        Assert.Equal(Sensitivity.Secret, saved.Sensitivity);
    }

    [Fact]
    public async Task Update_returns_validation_errors_without_saving()
    {
        var meeting = GivenMeeting();
        var oldTitle = meeting.Title;

        var result = await NewService().UpdateAsync(
            meeting.Id,
            ValidInput(title: "", startsAt: "bad", assignedApproverId: _actorId),
            _actorId);

        Assert.Equal(MeetingOutcome.InvalidInformation, result.Outcome);
        Assert.Contains("title", result.Errors!.Keys);
        Assert.Contains("startsAt", result.Errors.Keys);
        Assert.Equal(oldTitle, Assert.Single(_repository.Meetings).Title);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Update_rejects_an_ineligible_approver_without_saving()
    {
        var meeting = GivenMeeting();
        var invalidApproverId = Guid.CreateVersion7();
        _repository.IneligibleApproverIds.Add(invalidApproverId);

        var result = await NewService().UpdateAsync(
            meeting.Id,
            ValidInput(title: "Không được lưu", assignedApproverId: invalidApproverId),
            _actorId);

        Assert.Equal(MeetingOutcome.InvalidInformation, result.Outcome);
        Assert.Contains("assignedApproverId", result.Errors!.Keys);
        Assert.Equal("Giao ban tuần", Assert.Single(_repository.Meetings).Title);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Theory]
    [InlineData(MeetingStatus.WaitingApproval)]
    [InlineData(MeetingStatus.Approved)]
    [InlineData(MeetingStatus.Published)]
    [InlineData(MeetingStatus.InProgress)]
    [InlineData(MeetingStatus.Ended)]
    [InlineData(MeetingStatus.MinutesIssued)]
    [InlineData(MeetingStatus.Archived)]
    public async Task Update_rejects_a_meeting_outside_draft_or_rejected(MeetingStatus status)
    {
        var meeting = GivenMeeting(status);
        var oldTitle = meeting.Title;

        var result = await NewService().UpdateAsync(
            meeting.Id,
            ValidInput(title: "", startsAt: "bad"),
            _actorId);

        Assert.Equal(MeetingOutcome.NotEditable, result.Outcome);
        Assert.Equal(oldTitle, Assert.Single(_repository.Meetings).Title);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Update_does_not_save_when_information_is_unchanged()
    {
        var meeting = GivenMeeting();
        var updatedAt = meeting.UpdatedAt;

        var result = await NewService().UpdateAsync(
            meeting.Id, ValidInput(assignedApproverId: _actorId), _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal(updatedAt, meeting.UpdatedAt);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Get_and_update_return_the_same_not_found_for_missing_or_other_owners()
    {
        var meeting = GivenMeeting();
        var service = NewService();

        var missing = await service.GetAsync(Guid.CreateVersion7(), _actorId);
        var otherOwner = await service.GetAsync(meeting.Id, _otherActorId);
        var cannotUpdate = await service.UpdateAsync(meeting.Id, ValidInput(), _otherActorId);

        Assert.Equal(MeetingOutcome.NotFound, missing.Outcome);
        Assert.Equal(missing, otherOwner);
        Assert.Equal(MeetingOutcome.NotFound, cannotUpdate.Outcome);
    }

    [Fact]
    public async Task Get_allows_the_assigned_approver_and_a_meeting_participant()
    {
        var meeting = GivenMeeting();
        meeting.AssignedApproverId = _otherActorId;
        var participantId = Guid.CreateVersion7();
        meeting.Participants.Add(new MeetingParticipant
        {
            MeetingId = meeting.Id,
            UserId = participantId,
        });
        var service = NewService();

        var approverResult = await service.GetAsync(meeting.Id, _otherActorId);
        var participantResult = await service.GetAsync(meeting.Id, participantId);

        Assert.True(approverResult.Succeeded);
        Assert.Equal(meeting.Id, approverResult.Meeting!.Id);
        Assert.True(participantResult.Succeeded);
        Assert.Equal(meeting.Id, participantResult.Meeting!.Id);
    }

    [Fact]
    public async Task Get_returns_a_read_failure_when_the_repository_fails()
    {
        _repository.NextReadThrows = new NpgsqlException("database unavailable");

        var result = await NewService().GetAsync(Guid.CreateVersion7(), _actorId);

        Assert.Equal(MeetingOutcome.ReadFailed, result.Outcome);
    }

    [Fact]
    public async Task List_eligible_approvers_returns_the_repository_options()
    {
        var expected = new[]
        {
            new MeetingApproverOption(Guid.CreateVersion7(), "An Nguyễn"),
            new MeetingApproverOption(Guid.CreateVersion7(), "Bình Trần"),
        };
        _repository.EligibleApprovers.AddRange(expected);

        var result = await NewService().ListEligibleApproversAsync();

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Save_failure_is_returned_as_a_service_failure()
    {
        _repository.NextSaveThrows = new NpgsqlException("database unavailable");

        var result = await NewService().CreateAsync(ValidInput(), _actorId);

        Assert.Equal(MeetingOutcome.SaveFailed, result.Outcome);
        Assert.Empty(_repository.Meetings);
    }

    [Fact]
    public async Task Update_save_failure_is_returned_and_does_not_keep_mutated_values()
    {
        var meeting = GivenMeeting();
        _repository.NextSaveThrows = new NpgsqlException("database unavailable");

        var result = await NewService().UpdateAsync(
            meeting.Id,
            ValidInput(title: "Không được lưu", assignedApproverId: _actorId),
            _actorId);

        Assert.Equal(MeetingOutcome.SaveFailed, result.Outcome);
        Assert.Equal("Giao ban tuần", Assert.Single(_repository.Meetings).Title);
        Assert.Equal(1, _repository.SaveCount);
    }
}
