using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Recordings;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Recordings;

/// <summary>SCRUM-179 (CA-14): opening and saving the room mic's takes (ADR-0041) over the unit of work.</summary>
public sealed class RoomTakeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeRecordingRepository _recordings = new();
    private readonly FakeTranscriptRepository _transcripts = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeRecordingStorage _storage;
    private readonly FakeAuditService _audit = new();
    private readonly FakeSpeechChangePublisher _publisher;
    private readonly RecordingTestMeeting _meeting;

    public RoomTakeServiceTests()
    {
        _publisher = new(_unitOfWork);
        _storage = new(_unitOfWork);
        _meeting = new(_meetings, MeetingMode.Hybrid, Now);
    }

    private RoomTakeService Service() => new(_meetings, _recordings, _unitOfWork, _transcripts, _storage, _audit, _publisher,
        new FakeCombinedRecordings(), new FixedClock(Now), Options.Create(new RecordingOptions()), NullLogger<RoomTakeService>.Instance);

    private Task<RecordingOutcome> Begin(bool consent = true)
        => Service().BeginAsync(_meeting.Meeting.Id, _meeting.Secretary, new RoomTakeStart(consent, null));

    [Fact]
    public async Task A_take_opens_under_the_meeting_lock_in_one_transaction_then_is_announced()
    {
        var outcome = await Begin();

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        var take = Assert.Single(_recordings.Recordings);
        Assert.Equal(RecordingStatus.Pending, take.Status);
        Assert.Equal(Now, take.ConsentObtainedAt);
        Assert.Equal(take.Id, Assert.IsType<RoomTakeOpened>(outcome.Data).RecordingId);
        Assert.Equal([_meeting.Meeting.Id], _meetings.Locked);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
        Assert.Equal([AuditActions.RecordingTakeStarted], _audit.Actions);
        Assert.Equal(1, Assert.Single(_publisher.Published).CommitsBefore);
        Assert.Equal([take.StoragePath], _storage.Files.Keys);
    }

    [Fact]
    public async Task The_first_take_is_refused_until_the_room_was_told_and_no_file_is_made()
    {
        var outcome = await Begin(consent: false);

        Assert.Equal(RecordingFailure.Invalid, outcome.Failure);
        Assert.Contains("consentConfirmed", outcome.Errors!.Keys);
        Assert.Empty(_recordings.Recordings);
        Assert.Empty(_storage.Files);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task A_storage_failure_is_save_failed_and_the_new_file_is_deleted()
    {
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Begin();

        Assert.Equal(RecordingFailure.SaveFailed, outcome.Failure);
        Assert.Equal(1, _unitOfWork.Rollbacks);
        Assert.Single(_storage.Deleted);
        Assert.Empty(_storage.Files);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task A_take_opened_at_the_same_time_elsewhere_is_take_open_and_the_new_file_is_deleted()
    {
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.Unknown);

        var outcome = await Begin();

        Assert.Equal(RecordingFailure.TakeOpen, outcome.Failure);
        Assert.Equal(1, _unitOfWork.Rollbacks);
        Assert.Single(_storage.Deleted);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task A_take_the_mic_heard_nothing_in_is_dropped_with_its_file_after_the_commit()
    {
        var take = new Recording
        {
            MeetingId = _meeting.Meeting.Id, Source = RecordingSource.BrowserCapture, Status = RecordingStatus.Pending,
            FileName = "ghi-am-phong.wav", ContentType = "audio/wav", Takes = [], StoragePath = _storage.Create(".wav"), ByteSize = RecordingRules.WavHeaderBytes,
            CreatedById = _meeting.Secretary, CreatedAt = Now, UpdatedAt = Now,
        };
        _recordings.Recordings.Add(take);

        var outcome = await Service().CompleteAsync(take.Id, _meeting.Secretary);

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        Assert.Null(Assert.IsType<RoomTakeSaved>(outcome.Data).RecordingId);
        Assert.Empty(_recordings.Recordings);
        Assert.Equal([TransactionMode.None, TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal(1, _unitOfWork.Commits);
        Assert.Equal([take.StoragePath], _storage.Deleted);
        Assert.Equal([1], _storage.CommitsAtDelete);
        Assert.Equal(1, Assert.Single(_publisher.Published).CommitsBefore);
    }
}
