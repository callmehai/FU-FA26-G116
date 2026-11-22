using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Recordings;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Recordings;

/// <summary>SCRUM-179 (CA-14): recordings kept, refused and erased (SCRUM-29) over the unit of work.</summary>
public sealed class RecordingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeRecordingRepository _recordings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeRecordingStorage _storage;
    private readonly FakeAuditService _audit = new();
    private readonly FakeAudioTranscoder _transcoder = new();
    private readonly FakeSpeechChangePublisher _publisher;
    private readonly RecordingTestMeeting _meeting;

    public RecordingServiceTests()
    {
        _publisher = new(_unitOfWork);
        _storage = new(_unitOfWork);
        _meeting = new(_meetings, MeetingMode.InPerson, Now);
    }

    private RecordingService Service() => new(_meetings, _recordings, _unitOfWork, _storage, new FakeRecordingSigner(), _audit,
        _publisher, new FakeCombinedRecordings(), _transcoder, new FixedClock(Now), Options.Create(new RecordingOptions()),
        NullLogger<RecordingService>.Instance);

    private RecordingUpload Upload(bool consent = true)
        => new("hop.mp3", "audio/mpeg", _storage.Create(".mp3"), 1000, RecordingSource.Upload, consent, null, 5000);

    [Fact]
    public async Task An_upload_is_kept_with_its_purpose_and_retention_in_one_transaction_then_announced()
    {
        var upload = Upload();

        var outcome = await Service().AddAsync(_meeting.Meeting.Id, _meeting.Secretary, upload);

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        var recording = Assert.Single(_recordings.Recordings);
        Assert.Equal(RecordingRules.DefaultPurpose, recording.ConsentPurpose);
        Assert.Equal(Now, recording.ConsentObtainedAt);
        Assert.Equal(Now.AddDays(180), recording.RetainUntil);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
        Assert.Equal([AuditActions.RecordingUploaded], _audit.Actions);
        Assert.Equal(1, Assert.Single(_publisher.Published).CommitsBefore);
        Assert.Empty(_storage.Deleted);
    }

    private Recording Stored(string contentType)
    {
        var recording = new Recording
        {
            MeetingId = _meeting.Meeting.Id, Source = RecordingSource.Upload, Status = RecordingStatus.Available,
            FileName = "hop", ContentType = contentType, StoragePath = _storage.Create(".bin"), CreatedById = _meeting.Secretary,
        };
        _recordings.Add(recording);
        return recording;
    }

    [Fact]
    public async Task A_download_that_cannot_be_made_mp3_is_refused_before_the_link_and_the_log()
    {
        // ADR-0069: no ffmpeg, so a WAV cannot leave as MP3: refused where the screen can say so, nothing logged.
        _transcoder.Available = false;
        var wav = Stored("audio/wav");
        Assert.Equal(RecordingFailure.Mp3Unavailable, (await Service().DownloadAsync(wav.Id, _meeting.Secretary)).Failure);
        Assert.Empty(_audit.Actions);

        // A file already MP3 leaves as it is, so it needs no converter.
        var checks = _transcoder.Checks;
        var mp3 = Stored("audio/mpeg");
        Assert.Equal(RecordingFailure.None, (await Service().DownloadAsync(mp3.Id, _meeting.Secretary)).Failure);
        Assert.Equal(checks, _transcoder.Checks);
        Assert.Equal([AuditActions.RecordingDownloaded], _audit.Actions);
    }

    [Fact]
    public async Task An_upload_without_consent_is_refused_and_its_bytes_deleted()
    {
        var upload = Upload(consent: false);

        var outcome = await Service().AddAsync(_meeting.Meeting.Id, _meeting.Secretary, upload);

        Assert.Equal(RecordingFailure.Invalid, outcome.Failure);
        Assert.Contains("consentConfirmed", outcome.Errors!.Keys);
        Assert.Empty(_recordings.Recordings);
        Assert.Equal([upload.StoragePath], _storage.Deleted);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task A_storage_failure_is_save_failed_and_leaves_no_bytes_behind()
    {
        var upload = Upload();
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().AddAsync(_meeting.Meeting.Id, _meeting.Secretary, upload);

        Assert.Equal(RecordingFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Equal([upload.StoragePath], _storage.Deleted);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task An_expired_recording_is_marked_erased_with_its_transcript_and_its_file_deleted_after_the_commit()
    {
        var recording = new Recording
        {
            MeetingId = _meeting.Meeting.Id, Source = RecordingSource.Upload, Status = RecordingStatus.Available,
            FileName = "hop.mp3", StoragePath = _storage.Create(".mp3"), ContentType = "audio/mpeg", ByteSize = 1000,
            RetainUntil = Now.AddDays(-1), CreatedById = _meeting.Secretary, CreatedAt = Now.AddDays(-181), UpdatedAt = Now.AddDays(-181),
        };
        _recordings.Recordings.Add(recording);

        var erased = await Service().EraseExpiredAsync();

        Assert.Equal(1, erased);
        Assert.Equal(RecordingStatus.Deleted, recording.Status);
        Assert.Equal(Now, recording.DeletedAt);
        Assert.Equal([recording.Id], _recordings.Locked);
        Assert.Equal([recording.Id], _recordings.TranscriptsDeleted);
        Assert.Equal([AuditActions.RecordingErased], _audit.Actions);
        Assert.Equal([TransactionMode.None, TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal(1, _unitOfWork.Commits);
        Assert.Equal([recording.StoragePath], _storage.Deleted);
        Assert.Equal([1], _storage.CommitsAtDelete);
    }

    [Fact]
    public async Task A_recording_still_kept_is_not_erased()
    {
        _recordings.Recordings.Add(new Recording
        {
            MeetingId = _meeting.Meeting.Id, Status = RecordingStatus.Available, StoragePath = "kept.mp3",
            FileName = "hop.mp3", ContentType = "audio/mpeg",
            RetainUntil = Now.AddDays(1),
        });

        Assert.Equal(0, await Service().EraseExpiredAsync());
        Assert.Empty(_storage.Deleted);
    }
}
