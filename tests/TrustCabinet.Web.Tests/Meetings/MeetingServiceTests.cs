using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Infrastructure.Meetings;

namespace TrustCabinet.Web.Tests.Meetings;

public class MeetingServiceTests
{
    private sealed class CaptureLogger : ILogger<MeetingService>
    {
        public LogLevel? LastLevel { get; private set; }

        public Exception? LastException { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LastLevel = logLevel;
            LastException = exception;
        }
    }

    private readonly FakeMeetingRepository _repository = new();
    private readonly Guid _actorId = Guid.CreateVersion7();
    private readonly Guid _otherActorId = Guid.CreateVersion7();

    private MeetingService NewService()
        => new(_repository, NullLogger<MeetingService>.Instance);

    private static readonly DateTimeOffset FutureStart =
        new(DateTimeOffset.UtcNow.Date.AddDays(7).AddHours(2), TimeSpan.Zero);

    private static string Iso(DateTimeOffset value)
        => value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static MeetingInput ValidInput(
        string title = "Giao ban tuần",
        string? mode = "InPerson",
        string location = "Phòng họp A",
        string? sensitivity = "Internal",
        string description = "Nội dung",
        DateTimeOffset? start = null)
        => new(
            title,
            Iso(start ?? FutureStart),
            Iso((start ?? FutureStart).AddHours(1)),
            mode,
            location,
            null,
            sensitivity,
            description);

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
            PlannedStartTime = FutureStart,
            PlannedEndTime = FutureStart.AddHours(1),
            CreatedById = creatorId ?? _actorId,
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

        var result = await NewService().CreateAsync(input, _actorId, [new(_actorId, "Chair")]);

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
                mode: "0",
                location: new string('b', 301),
                sensitivity: "not-a-sensitivity",
                description: new string('c', 4001)),
            _actorId, [new(_actorId, "Chair")]);

        Assert.Equal(MeetingOutcome.InvalidInformation, result.Outcome);
        Assert.Equal(5, result.Errors!.Count);
        Assert.Contains("title", result.Errors.Keys);
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
            _actorId, [new(_actorId, "Chair")]);

        Assert.True(result.Succeeded);
        var meeting = Assert.Single(_repository.Meetings);
        Assert.Equal("Giao ban", meeting.Title);
        Assert.Equal("Phòng A", meeting.Location);
        Assert.Equal("Có\nxuống dòng", meeting.Description);
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Equal(_actorId, meeting.CreatedById);
        var history = Assert.Single(meeting.StatusHistory);
        Assert.Null(history.FromStatus);
        Assert.Equal(MeetingStatus.Draft, history.ToStatus);
        Assert.Equal(_actorId, history.ActorId);
    }

    [Fact]
    public async Task Create_uses_form_defaults()
    {
        var result = await NewService().CreateAsync(
            ValidInput(mode: null, sensitivity: null), _actorId, [new(_actorId, "Chair")]);

        Assert.True(result.Succeeded);
        var meeting = Assert.Single(_repository.Meetings);
        Assert.Equal(MeetingMode.InPerson, meeting.Mode);
        Assert.Equal(Sensitivity.Normal, meeting.Sensitivity);
    }

    [Fact]
    public async Task Create_accepts_the_documented_maximum_lengths()
    {
        var result = await NewService().CreateAsync(
            ValidInput(
                title: new string('t', 300),
                location: new string('l', 300),
                description: new string('d', 4000)),
            _actorId, [new(_actorId, "Chair")]);

        Assert.True(result.Succeeded);
        var meeting = Assert.Single(_repository.Meetings);
        Assert.Equal(300, meeting.Title.Length);
        Assert.Equal(300, meeting.Location.Length);
        Assert.Equal(4000, meeting.Description.Length);
    }

    [Fact]
    public async Task Create_stores_the_planned_schedule_in_utc()
    {
        var input = ValidInput() with
        {
            PlannedStartTime = "2099-03-01T09:00:00+07:00",
            PlannedEndTime = "2099-03-01T10:30:00+07:00",
            OnlineLink = "  https://meet.example.vn/x  ",
        };

        var result = await NewService().CreateAsync(input, _actorId, [new(_actorId, "Chair")]);

        Assert.True(result.Succeeded);
        var meeting = Assert.Single(_repository.Meetings);
        Assert.Equal(new DateTimeOffset(2099, 3, 1, 2, 0, 0, TimeSpan.Zero), meeting.PlannedStartTime);
        Assert.Equal(TimeSpan.Zero, meeting.PlannedStartTime.Offset);
        Assert.Equal(new DateTimeOffset(2099, 3, 1, 3, 30, 0, TimeSpan.Zero), meeting.PlannedEndTime);
        Assert.Equal("https://meet.example.vn/x", meeting.OnlineLink);
        Assert.Null(meeting.ActualStartedAt);
    }

    [Theory]
    [InlineData("2099-03-01 09:00")]
    [InlineData("2099-03-01T09:00:00")]
    [InlineData("not a time")]
    public async Task Create_rejects_a_time_without_an_offset(string start)
    {
        var result = await NewService().CreateAsync(ValidInput() with { PlannedStartTime = start }, _actorId, [new(_actorId, "Chair")]);

        Assert.Equal(MeetingOutcome.InvalidInformation, result.Outcome);
        Assert.Contains("plannedStartTime", result.Errors!.Keys);
    }

    [Fact]
    public async Task Update_of_a_draft_keeps_a_start_that_has_already_passed()
    {
        var meeting = GivenMeeting();
        var past = DateTimeOffset.UtcNow.AddDays(-1);

        var result = await NewService().UpdateAsync(meeting.Id, ValidInput(start: past), _actorId);

        Assert.True(result.Succeeded);
        Assert.False(result.Rescheduled);
        Assert.Equal(past.ToUnixTimeSeconds(), meeting.PlannedStartTime.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Create_leaves_the_approver_unassigned_until_submission()
    {
        var input = new MeetingInput(
            "Giao ban tuần",
            Iso(FutureStart),
            Iso(FutureStart.AddHours(1)),
            null,
            null,
            null,
            null,
            null);

        var result = await NewService().CreateAsync(input, _actorId, [new(_actorId, "Chair")]);

        Assert.True(result.Succeeded);
        Assert.Null(Assert.Single(_repository.Meetings).AssignedApproverId);
    }

    [Fact]
    public async Task Create_does_not_assign_or_validate_a_draft_approver()
    {
        var approverId = Guid.CreateVersion7();
        _repository.IneligibleApproverIds.Add(approverId);

        var result = await NewService().CreateAsync(ValidInput(), _actorId, [new(_actorId, "Chair")]);

        Assert.True(result.Succeeded);
        Assert.Null(Assert.Single(_repository.Meetings).AssignedApproverId);
    }

    [Fact]
    public async Task Update_changes_draft_fields_and_preserves_assignment_without_changing_lifecycle()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        meeting.AssignedApproverId = _otherActorId;
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
        Assert.Equal(MeetingStatus.Draft, saved.Status);
        Assert.Equal(_otherActorId, saved.AssignedApproverId);
        Assert.Equal(_actorId, saved.CreatedById);
        Assert.Equal(priorHistory, saved.StatusHistory);
    }

    [Fact]
    public async Task Update_uses_form_defaults()
    {
        var meeting = GivenMeeting();

        var result = await NewService().UpdateAsync(
            meeting.Id,
            ValidInput(mode: null, sensitivity: null),
            _actorId);

        Assert.True(result.Succeeded);
        var saved = Assert.Single(_repository.Meetings);
        Assert.Equal(MeetingMode.InPerson, saved.Mode);
        Assert.Equal(Sensitivity.Normal, saved.Sensitivity);
    }

    [Fact]
    public async Task Update_changes_rejected_information_without_changing_assignment_or_rejection_history()
    {
        var meeting = GivenMeeting(MeetingStatus.Rejected);
        meeting.AssignedApproverId = _otherActorId;
        var rejection = new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Rejected,
            Action = MeetingHistoryAction.Reject,
            ActorId = _otherActorId,
            Reason = "Cần bổ sung số liệu",
            OccurredAt = DateTimeOffset.UtcNow,
        };
        meeting.StatusHistory.Add(rejection);
        var service = NewService();

        var check = await service.CheckUpdateAsync(meeting.Id, _actorId);
        var result = await service.UpdateAsync(
            meeting.Id, ValidInput(title: "  Đã bổ sung số liệu  "), _actorId);

        Assert.True(check.Succeeded);
        Assert.True(result.Succeeded);
        Assert.Equal("Đã bổ sung số liệu", meeting.Title);
        Assert.Equal(MeetingStatus.Rejected, meeting.Status);
        Assert.Equal(_otherActorId, meeting.AssignedApproverId);
        Assert.Same(rejection, Assert.Single(meeting.StatusHistory));
        Assert.Equal("Cần bổ sung số liệu", rejection.Reason);
        Assert.Equal(1, _repository.SaveCount);
        Assert.Empty(_repository.Notifications);
        Assert.Empty(_repository.EmailMessages);
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.Rejected)]
    public async Task Update_returns_validation_errors_without_saving(MeetingStatus status)
    {
        var meeting = GivenMeeting(status);
        var oldTitle = meeting.Title;

        var result = await NewService().UpdateAsync(
            meeting.Id,
            ValidInput(title: ""),
            _actorId);

        Assert.Equal(MeetingOutcome.InvalidInformation, result.Outcome);
        Assert.Contains("title", result.Errors!.Keys);
        Assert.Equal(oldTitle, Assert.Single(_repository.Meetings).Title);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Theory]
    [InlineData(MeetingStatus.WaitingApproval)]
    [InlineData(MeetingStatus.Approved)]
    [InlineData(MeetingStatus.InProgress)]
    [InlineData(MeetingStatus.Ended)]
    public async Task Update_rejects_a_meeting_outside_draft_rejected_or_published(MeetingStatus status)
    {
        var meeting = GivenMeeting(status);
        var oldTitle = meeting.Title;

        var result = await NewService().UpdateAsync(
            meeting.Id,
            ValidInput(title: ""),
            _actorId);

        Assert.Equal(MeetingOutcome.NotEditable, result.Outcome);
        Assert.Equal(oldTitle, Assert.Single(_repository.Meetings).Title);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Theory]
    [InlineData(MeetingStatus.Draft, false)]
    [InlineData(MeetingStatus.Rejected, false)]
    [InlineData(MeetingStatus.Rejected, true)]
    public async Task Update_does_not_save_when_information_is_unchanged(MeetingStatus status, bool assigned)
    {
        var meeting = GivenMeeting(status);
        meeting.AssignedApproverId = assigned ? _otherActorId : null;
        meeting.UpdatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var updatedAt = meeting.UpdatedAt;

        var result = await NewService().UpdateAsync(
            meeting.Id, ValidInput(title: "  Giao ban tuần  ", location: "  Phòng họp A  "), _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal(updatedAt, meeting.UpdatedAt);
        Assert.Equal(status, meeting.Status);
        Assert.Equal(assigned ? _otherActorId : (Guid?)null, meeting.AssignedApproverId);
        Assert.Equal(0, _repository.SaveCount);
        Assert.Empty(_repository.Notifications);
        Assert.Empty(_repository.EmailMessages);
    }

    [Fact]
    public async Task Update_preserves_a_draft_assignment_when_information_is_unchanged()
    {
        var meeting = GivenMeeting();
        meeting.AssignedApproverId = _otherActorId;

        var result = await NewService().UpdateAsync(meeting.Id, ValidInput(), _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Equal(_otherActorId, meeting.AssignedApproverId);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.Rejected)]
    public async Task Get_and_update_return_the_same_not_found_for_missing_or_other_owners(MeetingStatus status)
    {
        var meeting = GivenMeeting(status);
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
        meeting.Status = MeetingStatus.Published;
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
    public async Task List_forwards_criteria_and_actor_and_returns_the_page()
    {
        var meeting = GivenMeeting();
        var query = new MeetingListQuery(
            MeetingStatus.Draft, Sensitivity.Internal, "Giao ban", null, null,
            1, 20, MeetingSortBy.Title, MeetingSortDirection.Asc);

        var result = await NewService().ListAsync(query, _actorId);

        Assert.Equal(MeetingOutcome.Success, result.Outcome);
        Assert.Same(query, _repository.LastListQuery);
        Assert.Equal(_actorId, _repository.LastListActorId);
        Assert.Equal(meeting.Id, Assert.Single(result.Page!.Items).Id);
        Assert.Equal(1, result.Page.TotalCount);
    }

    [Fact]
    public async Task List_maps_a_database_failure_to_read_failed()
    {
        var failure = new NpgsqlException("database unavailable");
        _repository.NextReadThrows = failure;
        var logger = new CaptureLogger();
        var query = new MeetingListQuery(
            null, null, null, null, null, 1, 20,
            MeetingSortBy.CreatedAt, MeetingSortDirection.Desc);

        var result = await new MeetingService(_repository, logger).ListAsync(query, _actorId);

        Assert.Equal(MeetingOutcome.ReadFailed, result.Outcome);
        Assert.Null(result.Page);
        Assert.Equal(LogLevel.Error, logger.LastLevel);
        Assert.Same(failure, logger.LastException);
    }

    [Fact]
    public async Task List_propagates_request_cancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        _repository.NextReadThrows = new OperationCanceledException(source.Token);
        var query = new MeetingListQuery(
            null, null, null, null, null, 1, 20,
            MeetingSortBy.CreatedAt, MeetingSortDirection.Desc);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => NewService().ListAsync(query, _actorId, source.Token));
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

        Assert.Equal(MeetingOutcome.Success, result.Outcome);
        Assert.Equal(expected, result.Approvers);
    }

    [Fact]
    public async Task List_eligible_approvers_maps_database_failure_to_read_failed()
    {
        _repository.NextApproverReadThrows = new NpgsqlException("database unavailable");

        var result = await NewService().ListEligibleApproversAsync();

        Assert.Equal(MeetingOutcome.ReadFailed, result.Outcome);
        Assert.Null(result.Approvers);
    }

    [Fact]
    public async Task Save_failure_is_returned_as_a_service_failure()
    {
        _repository.NextSaveThrows = new NpgsqlException("database unavailable");

        var result = await NewService().CreateAsync(ValidInput(), _actorId, [new(_actorId, "Chair")]);

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
            ValidInput(title: "Không được lưu"),
            _actorId);

        Assert.Equal(MeetingOutcome.SaveFailed, result.Outcome);
        Assert.Equal("Giao ban tuần", Assert.Single(_repository.Meetings).Title);
        Assert.Equal(1, _repository.SaveCount);
    }
}
