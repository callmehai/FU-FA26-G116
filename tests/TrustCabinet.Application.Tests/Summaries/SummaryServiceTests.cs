using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Summaries;

public sealed class SummaryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingSummaryRepository _summaries = new();
    private readonly FakeSummarySourceQuery _sources = new();
    private readonly FakeMeetingTranscriptQuery _transcript = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeLanguageModel _local = new("qwen-local");
    private readonly FakeLanguageModelProvider _models;
    private readonly Guid _secretary = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();
    private readonly Guid _meeting = Guid.CreateVersion7();

    public SummaryServiceTests()
    {
        _models = new FakeLanguageModelProvider(_local);
        MeetingParticipant Participant(Guid userId, MeetingRole role) => new()
        {
            MeetingId = _meeting, UserId = userId, Role = role, AssignmentStatus = AssignmentStatus.Current,
            Response = InvitationResponse.Accepted,
            User = new User { Id = userId, FullName = role.ToString(), Email = "u@trustcabinet.local", IsActive = true },
        };
        _meetings.Meetings.Add(new Meeting
        {
            Id = _meeting, Title = "Họp giao ban", Status = MeetingStatus.Ended, Sensitivity = Sensitivity.Normal,
            PlannedStartTime = Now.AddHours(-2), PlannedEndTime = Now.AddHours(-1), CreatedAt = Now, UpdatedAt = Now,
            Participants = [Participant(_secretary, MeetingRole.Secretary), Participant(_member, MeetingRole.Member)],
        });
        _meetings.ActiveUsers.UnionWith([_secretary, _member]);
    }

    private SummaryService Service() => new(_summaries, _sources, _unitOfWork, _meetings, _models,
        new MeetingTranscriptReader(_transcript), new FixedClock(Now), NullLogger<SummaryService>.Instance,
        Options.Create(new LanguageModelOptions { ApiParallelism = 1 }));

    [Fact]
    public async Task The_secretary_queues_a_draft_and_reads_it_back_as_queued()
    {
        var outcome = await Service().RequestAsync(_meeting, _secretary);

        Assert.Equal(SummaryFailure.None, outcome.Failure);
        var row = Assert.Single(_summaries.Rows);
        Assert.Equal(SummaryStatus.Queued, row.Status);
        Assert.Equal(_secretary, row.RequestedById);
        Assert.Equal("Queued", Assert.IsType<SummaryState>(outcome.Data).Summary!.Status);
        Assert.Equal(1, _unitOfWork.Saves);
        Assert.All(_unitOfWork.Modes, mode => Assert.Equal(TransactionMode.None, mode));
    }

    [Fact]
    public async Task A_member_who_is_neither_officer_may_not_ask_for_a_draft()
    {
        var outcome = await Service().RequestAsync(_meeting, _member);

        Assert.Equal(SummaryFailure.Forbidden, outcome.Failure);
        Assert.Empty(_summaries.Rows);
    }

    [Fact]
    public async Task A_meeting_still_running_has_no_draft_yet()
    {
        // Khánh 19/11/2026: a draft summarises the whole meeting, so only once it has ended.
        _meetings.Meetings[0].Status = MeetingStatus.InProgress;

        var outcome = await Service().RequestAsync(_meeting, _secretary);

        Assert.Equal(SummaryFailure.WrongMeetingStatus, outcome.Failure);
        Assert.Empty(_summaries.Rows);
    }

    [Fact]
    public async Task A_meeting_where_nothing_was_said_has_nothing_to_summarise()
    {
        _sources.Sentences = 0;

        var outcome = await Service().RequestAsync(_meeting, _secretary);

        Assert.Equal(SummaryFailure.NothingSaid, outcome.Failure);
        Assert.Empty(_summaries.Rows);
    }

    [Fact]
    public async Task Two_requests_at_once_answer_the_loser_as_busy()
    {
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.Unknown);

        var outcome = await Service().RequestAsync(_meeting, _secretary);

        Assert.Equal(SummaryFailure.Busy, outcome.Failure);
    }

    [Fact]
    public async Task Any_other_storage_failure_while_queueing_is_a_failed_save()
    {
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().RequestAsync(_meeting, _secretary);

        Assert.Equal(SummaryFailure.SaveFailed, outcome.Failure);
    }

    [Fact]
    public async Task A_storage_failure_while_reading_is_a_failed_read()
    {
        _summaries.Fail = new PersistenceUnavailableException();

        var outcome = await Service().GetAsync(_meeting, _secretary);

        Assert.Equal(SummaryFailure.ReadFailed, outcome.Failure);
    }

    [Fact]
    public async Task The_worker_writes_the_draft_with_the_in_house_harness_for_the_in_house_model()
    {
        await Service().RequestAsync(_meeting, _secretary);

        Assert.True(await Service().ProcessNextAsync());

        var row = Assert.Single(_summaries.Rows);
        Assert.Equal(SummaryStatus.Ready, row.Status);
        Assert.Equal("qwen-local", row.Model);
        Assert.Contains("Rà soát tiến độ.", row.Content);
        Assert.DoesNotContain(_local.Systems, s => s.Contains(SummaryWriter.ApiSpellingRule.Trim(), StringComparison.Ordinal));
        Assert.False(await Service().ProcessNextAsync());
    }

    [Fact]
    public async Task A_remote_model_writes_the_draft_with_the_hosted_harness()
    {
        var api = new FakeLanguageModel("deepseek-v4", remote: true);
        _models.Model = api;
        await Service().RequestAsync(_meeting, _secretary);

        Assert.True(await Service().ProcessNextAsync());

        Assert.Equal("deepseek-v4", Assert.Single(_summaries.Rows).Model);
        Assert.Contains(api.Systems, s => s.Contains(SummaryWriter.ApiSpellingRule.Trim(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_model_that_cannot_answer_leaves_the_draft_failed_with_its_message()
    {
        await Service().RequestAsync(_meeting, _secretary);
        _local.Fail = new LanguageModelException("Máy AI không trả lời.");

        Assert.True(await Service().ProcessNextAsync());

        var row = Assert.Single(_summaries.Rows);
        Assert.Equal(SummaryStatus.Failed, row.Status);
        Assert.Equal("Máy AI không trả lời.", row.ErrorMessage);
    }

    [Fact]
    public async Task A_restart_puts_an_interrupted_draft_back_in_the_queue()
    {
        _summaries.Rows.Add(new MeetingSummary { MeetingId = _meeting, Status = SummaryStatus.Running });

        Assert.Equal(1, await Service().RequeueInterruptedAsync());
        Assert.Equal(SummaryStatus.Queued, _summaries.Rows[0].Status);
    }
}
