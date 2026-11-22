using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Recordings;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Application.Transcription;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Application.Tests.Recordings;

/// <summary>Round 1 (Report 5.1) unit tests for FE-09 recordings, tester Pham Gia Khanh.
/// Each test covers one UTC of cases/FE-09/unit: Trait UTC names Class.Method/UTCnnn,
/// DisplayName repeats it for the trx matcher, and the asserts cover the Expected
/// triple (Return / Exception / Log Message).</summary>
public sealed class RecUnitRound1Tests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    private static (R1Meetings Meetings, Meeting Meeting, Guid Chair, Guid Secretary, Guid Member) NewMeeting(
        MeetingMode mode = MeetingMode.InPerson, MeetingStatus status = MeetingStatus.InProgress,
        Sensitivity sensitivity = Sensitivity.Normal)
    {
        var inner = new FakeMeetingRepository();
        var chair = Guid.CreateVersion7();
        var secretary = Guid.CreateVersion7();
        var member = Guid.CreateVersion7();
        var meeting = new Meeting
        {
            Title = "Họp giao ban", Mode = mode, Status = status, Sensitivity = sensitivity, CreatedById = chair,
            PlannedStartTime = Now.AddHours(-1), PlannedEndTime = Now.AddHours(1), ActualStartedAt = Now.AddHours(-1),
        };
        foreach (var (userId, role) in new[] { (chair, MeetingRole.Chair), (secretary, MeetingRole.Secretary), (member, MeetingRole.Member) })
        {
            meeting.Participants.Add(new MeetingParticipant
            {
                MeetingId = meeting.Id, UserId = userId, Role = role, AssignmentStatus = AssignmentStatus.Current,
                Response = InvitationResponse.Accepted,
                User = new User { Id = userId, FullName = $"{role}", Email = $"{userId}@trustcabinet.local", IsActive = true },
            });
            inner.ActiveUsers.Add(userId);
        }
        inner.Meetings.Add(meeting);
        return (new R1Meetings(inner), meeting, chair, secretary, member);
    }

    private sealed record RecSvc(R1Meetings Meetings, FakeRecordingRepository Recordings, FakeUnitOfWork Uow,
        FakeRecordingStorage Files, R1Storage Storage, FakeAuditService Audit, FakeSpeechChangePublisher Publisher,
        FakeCombinedRecordings Combined, FakeAudioTranscoder Transcoder, R1Logger<RecordingService> Log,
        RecordingService Service, Meeting Meeting, Guid Chair, Guid Secretary, Guid Member);

    private static RecSvc RecordingSvc(MeetingMode mode = MeetingMode.InPerson,
        MeetingStatus status = MeetingStatus.InProgress, Sensitivity sensitivity = Sensitivity.Normal)
    {
        var (meetings, meeting, chair, secretary, member) = NewMeeting(mode, status, sensitivity);
        var recordings = new FakeRecordingRepository();
        var uow = new FakeUnitOfWork();
        var files = new FakeRecordingStorage(uow);
        var storage = new R1Storage(files);
        var audit = new FakeAuditService();
        var publisher = new FakeSpeechChangePublisher(uow);
        var combined = new FakeCombinedRecordings();
        var transcoder = new FakeAudioTranscoder();
        var log = new R1Logger<RecordingService>();
        var service = new RecordingService(meetings, recordings, uow, storage, new FakeRecordingSigner(), audit,
            publisher, combined, transcoder, new FixedClock(Now), Options.Create(new RecordingOptions()), log);
        return new(meetings, recordings, uow, files, storage, audit, publisher, combined, transcoder, log, service,
            meeting, chair, secretary, member);
    }

    private static RecordingUpload ValidUpload(string path) => new("hop.mp3", "audio/mpeg", path, 5_000_000,
        RecordingSource.Upload, true, "  Họp HĐQT  ", 3_600_000);

    private static Recording StoredUpload(RecSvc f, string contentType = "audio/mpeg")
    {
        var recording = new Recording
        {
            MeetingId = f.Meeting.Id, Source = RecordingSource.Upload, Status = RecordingStatus.Available,
            FileName = "hop", ContentType = contentType, StoragePath = f.Files.Create(".bin"),
            CreatedById = f.Secretary, CreatedAt = Now, UpdatedAt = Now,
        };
        f.Recordings.Add(recording);
        return recording;
    }

    [Fact(DisplayName = "RecordingService.AddAsync/UTC001: keeps an upload with its purpose and retention")]
    [Trait("UTC", "RecordingService.AddAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Add_keeps_an_upload_with_its_purpose_and_retention()
    {
        var f = RecordingSvc();
        var path = f.Files.Create(".mp3");

        var outcome = await f.Service.AddAsync(f.Meeting.Id, f.Secretary, ValidUpload(path));

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        var recording = Assert.Single(f.Recordings.Recordings);
        Assert.Equal("Họp HĐQT", recording.ConsentPurpose);
        Assert.Equal(Now, recording.ConsentObtainedAt);
        Assert.Equal(Now.AddDays(180), recording.RetainUntil);
        Assert.Equal(f.Secretary, recording.CreatedById);
        Assert.Equal([AuditActions.RecordingUploaded], f.Audit.Actions);
        Assert.Equal(1, Assert.Single(f.Publisher.Published).CommitsBefore);
        Assert.Contains(path, f.Files.Files.Keys);
    }

    [Fact(DisplayName = "RecordingService.AddAsync/UTC002: refuses an upload without consent and deletes its bytes")]
    [Trait("UTC", "RecordingService.AddAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Add_refuses_an_upload_without_consent_and_deletes_its_bytes()
    {
        var f = RecordingSvc();
        var path = f.Files.Create(".mp3");

        var outcome = await f.Service.AddAsync(f.Meeting.Id, f.Secretary, ValidUpload(path) with { ConsentConfirmed = false });

        Assert.Equal(RecordingFailure.Invalid, outcome.Failure);
        Assert.Contains("consentConfirmed", outcome.Errors!.Keys);
        Assert.Empty(f.Recordings.Recordings);
        Assert.Equal([path], f.Files.Deleted);
        Assert.Empty(f.Publisher.Published);
    }

    [Fact(DisplayName = "RecordingService.AddAsync/UTC003: the Chair may not upload")]
    [Trait("UTC", "RecordingService.AddAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Add_the_chair_may_not_upload()
    {
        var f = RecordingSvc();
        var path = f.Files.Create(".mp3");

        var outcome = await f.Service.AddAsync(f.Meeting.Id, f.Chair, ValidUpload(path));

        Assert.Equal(RecordingFailure.Forbidden, outcome.Failure);
        Assert.Empty(f.Recordings.Recordings);
        Assert.Equal([path], f.Files.Deleted);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "RecordingService.AddAsync/UTC004: an online meeting has no room mic")]
    [Trait("UTC", "RecordingService.AddAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Add_an_online_meeting_has_no_room_mic()
    {
        var f = RecordingSvc(MeetingMode.Online);
        var path = f.Files.Create(".mp3");

        var outcome = await f.Service.AddAsync(f.Meeting.Id, f.Secretary, ValidUpload(path));

        Assert.Equal(RecordingFailure.NoRoom, outcome.Failure);
        Assert.Equal([path], f.Files.Deleted);
        Assert.Empty(f.Publisher.Published);
    }

    [Fact(DisplayName = "RecordingService.AddAsync/UTC005: a hybrid meeting takes no uploaded file")]
    [Trait("UTC", "RecordingService.AddAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Add_a_hybrid_meeting_takes_no_uploaded_file()
    {
        var f = RecordingSvc(MeetingMode.Hybrid);
        var path = f.Files.Create(".mp3");

        var outcome = await f.Service.AddAsync(f.Meeting.Id, f.Secretary, ValidUpload(path));

        Assert.Equal(RecordingFailure.HybridLiveOnly, outcome.Failure);
        Assert.Equal([path], f.Files.Deleted);
        Assert.Empty(f.Publisher.Published);
    }

    [Fact(DisplayName = "RecordingService.AddAsync/UTC006: a Published meeting is locked for uploads")]
    [Trait("UTC", "RecordingService.AddAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Add_a_published_meeting_is_locked_for_uploads()
    {
        var f = RecordingSvc(MeetingMode.InPerson, MeetingStatus.Published);
        var path = f.Files.Create(".mp3");

        var outcome = await f.Service.AddAsync(f.Meeting.Id, f.Secretary, ValidUpload(path));

        Assert.Equal(RecordingFailure.Locked, outcome.Failure);
        Assert.Equal([path], f.Files.Deleted);
        Assert.Empty(f.Recordings.Recordings);
    }

    [Fact(DisplayName = "RecordingService.AddAsync/UTC007: a storage failure is save failed and leaves no bytes")]
    [Trait("UTC", "RecordingService.AddAsync/UTC007")]
    [Trait("Trait", "UTC")]
    public async Task Add_a_storage_failure_is_save_failed_and_leaves_no_bytes()
    {
        var f = RecordingSvc();
        var path = f.Files.Create(".mp3");
        f.Uow.FailNextSave = new PersistenceUnavailableException();

        var outcome = await f.Service.AddAsync(f.Meeting.Id, f.Secretary, ValidUpload(path));

        Assert.Equal(RecordingFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 1), (f.Uow.Commits, f.Uow.Rollbacks));
        Assert.Equal([path], f.Files.Deleted);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to store recording"));
    }

    [Fact(DisplayName = "RecordingService.DownloadAsync/UTC001: hands out a signed download link for an MP3")]
    [Trait("UTC", "RecordingService.DownloadAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Download_hands_out_a_signed_download_link_for_an_mp3()
    {
        var f = RecordingSvc();
        var recording = StoredUpload(f);

        var outcome = await f.Service.DownloadAsync(recording.Id, f.Chair);

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        Assert.StartsWith("signed/", Assert.IsType<RecordingPlayback>(outcome.Data).Url);
        Assert.Equal([AuditActions.RecordingDownloaded], f.Audit.Actions);
        Assert.Equal([TransactionMode.None, TransactionMode.None], f.Uow.Modes);
    }

    [Fact(DisplayName = "RecordingService.DownloadAsync/UTC002: a Member may not download")]
    [Trait("UTC", "RecordingService.DownloadAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Download_a_member_may_not_download()
    {
        var f = RecordingSvc();
        var recording = StoredUpload(f);

        var outcome = await f.Service.DownloadAsync(recording.Id, f.Member);

        Assert.Equal(RecordingFailure.ExportForbidden, outcome.Failure);
        Assert.Empty(f.Audit.Actions);
        Assert.Equal([TransactionMode.None, TransactionMode.None], f.Uow.Modes);
    }

    [Fact(DisplayName = "RecordingService.DownloadAsync/UTC003: a Secret meeting never downloads")]
    [Trait("UTC", "RecordingService.DownloadAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Download_a_secret_meeting_never_downloads()
    {
        var f = RecordingSvc(MeetingMode.InPerson, MeetingStatus.InProgress, Sensitivity.Secret);
        var recording = StoredUpload(f);

        var outcome = await f.Service.DownloadAsync(recording.Id, f.Chair);

        Assert.Equal(RecordingFailure.SecretMeeting, outcome.Failure);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "RecordingService.DownloadAsync/UTC004: the role check comes before the Secret check")]
    [Trait("UTC", "RecordingService.DownloadAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Download_the_role_check_comes_before_the_secret_check()
    {
        var f = RecordingSvc(MeetingMode.InPerson, MeetingStatus.InProgress, Sensitivity.Secret);
        var recording = StoredUpload(f);

        var outcome = await f.Service.DownloadAsync(recording.Id, f.Member);

        Assert.Equal(RecordingFailure.ExportForbidden, outcome.Failure);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "RecordingService.DownloadAsync/UTC005: a read failure is read failed with an error log")]
    [Trait("UTC", "RecordingService.DownloadAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Download_a_read_failure_is_read_failed_with_an_error_log()
    {
        var f = RecordingSvc();
        var recording = StoredUpload(f);
        f.Meetings.FailReads = true;

        var outcome = await f.Service.DownloadAsync(recording.Id, f.Chair);

        Assert.Equal(RecordingFailure.ReadFailed, outcome.Failure);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to sign a download"));
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "RecordingService.DownloadAsync/UTC006: a WAV the server cannot make MP3 is refused with MSG482")]
    [Trait("UTC", "RecordingService.DownloadAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Download_a_wav_the_server_cannot_make_mp3_is_refused()
    {
        var f = RecordingSvc();
        var recording = StoredUpload(f, "audio/wav");
        f.Transcoder.Available = false;

        var outcome = await f.Service.DownloadAsync(recording.Id, f.Chair);

        Assert.Equal(RecordingFailure.Mp3Unavailable, outcome.Failure);
        Assert.Empty(f.Audit.Actions);
        Assert.Equal([TransactionMode.None, TransactionMode.None], f.Uow.Modes);
    }

    [Fact(DisplayName = "RecordingService.EraseAsync/UTC001: erasing removes the row, its transcript and its file")]
    [Trait("UTC", "RecordingService.EraseAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Erase_erasing_removes_the_row_its_transcript_and_its_file()
    {
        var f = RecordingSvc();
        var recording = StoredUpload(f);

        var outcome = await f.Service.EraseAsync(recording.Id, f.Secretary);

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        Assert.Equal(recording.Id, outcome.RecordingId);
        Assert.Equal(RecordingStatus.Deleted, recording.Status);
        Assert.Equal(Now, recording.DeletedAt);
        Assert.Equal([recording.Id], f.Recordings.TranscriptsDeleted);
        Assert.Equal([recording.StoragePath], f.Files.Deleted);
        Assert.Equal([AuditActions.RecordingErased], f.Audit.Actions);
        Assert.Equal(1, Assert.Single(f.Publisher.Published).CommitsBefore);
    }

    [Fact(DisplayName = "RecordingService.EraseAsync/UTC002: erasing an erased recording changes nothing")]
    [Trait("UTC", "RecordingService.EraseAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Erase_erasing_an_erased_recording_changes_nothing()
    {
        var f = RecordingSvc();
        var recording = StoredUpload(f);
        recording.Status = RecordingStatus.Deleted;

        var outcome = await f.Service.EraseAsync(recording.Id, f.Secretary);

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        Assert.Equal(recording.Id, outcome.RecordingId);
        Assert.Empty(f.Audit.Actions);
        Assert.Empty(f.Files.Deleted);
        Assert.Empty(f.Publisher.Published);
    }

    [Fact(DisplayName = "RecordingService.EraseAsync/UTC003: the combined recording erases without being rebuilt")]
    [Trait("UTC", "RecordingService.EraseAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Erase_the_combined_recording_erases_without_being_rebuilt()
    {
        var f = RecordingSvc(MeetingMode.Hybrid, MeetingStatus.Ended);
        var recording = new Recording
        {
            MeetingId = f.Meeting.Id, Source = RecordingSource.Combined, Status = RecordingStatus.Available,
            FileName = "ban-ghi-tong-hop.wav", ContentType = "audio/wav", StoragePath = f.Files.Create(".wav"),
            CreatedById = f.Secretary, CreatedAt = Now, UpdatedAt = Now,
        };
        f.Recordings.Add(recording);

        var outcome = await f.Service.EraseAsync(recording.Id, f.Secretary);

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        Assert.Equal(RecordingStatus.Deleted, recording.Status);
        Assert.Empty(f.Combined.Removed);
        Assert.Equal([AuditActions.RecordingErased], f.Audit.Actions);
        Assert.Equal([recording.StoragePath], f.Files.Deleted);
    }

    [Fact(DisplayName = "RecordingService.EraseAsync/UTC004: the Chair may not erase")]
    [Trait("UTC", "RecordingService.EraseAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Erase_the_chair_may_not_erase()
    {
        var f = RecordingSvc();
        var recording = StoredUpload(f);

        var outcome = await f.Service.EraseAsync(recording.Id, f.Chair);

        Assert.Equal(RecordingFailure.Forbidden, outcome.Failure);
        Assert.Equal(RecordingStatus.Available, recording.Status);
        Assert.Empty(f.Files.Deleted);
    }

    [Fact(DisplayName = "RecordingService.EraseAsync/UTC005: a streaming take is not touched")]
    [Trait("UTC", "RecordingService.EraseAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Erase_a_streaming_take_is_not_touched()
    {
        var f = RecordingSvc();
        var recording = new Recording
        {
            MeetingId = f.Meeting.Id, Source = RecordingSource.BrowserCapture, Status = RecordingStatus.Pending,
            FileName = "ghi-am-phong.wav", ContentType = "audio/wav", StoragePath = f.Files.Create(".wav"),
            Takes = [], CreatedById = f.Secretary, CreatedAt = Now, UpdatedAt = Now,
        };
        f.Recordings.Add(recording);

        var outcome = await f.Service.EraseAsync(recording.Id, f.Secretary);

        Assert.Equal(RecordingFailure.TakeOpen, outcome.Failure);
        Assert.Equal(RecordingStatus.Pending, recording.Status);
        Assert.Empty(f.Files.Deleted);
    }

    [Fact(DisplayName = "RecordingService.EraseAsync/UTC006: a save failure keeps the file and logs an error")]
    [Trait("UTC", "RecordingService.EraseAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Erase_a_save_failure_keeps_the_file_and_logs_an_error()
    {
        var f = RecordingSvc();
        var recording = StoredUpload(f);
        f.Uow.FailNextSave = new PersistenceUnavailableException();

        var outcome = await f.Service.EraseAsync(recording.Id, f.Secretary);

        Assert.Equal(RecordingFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 1), (f.Uow.Commits, f.Uow.Rollbacks));
        Assert.Empty(f.Files.Deleted);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to erase recording"));
    }

    [Fact(DisplayName = "RecordingRules.Export/UTC001: the Chair may export")]
    [Trait("UTC", "RecordingRules.Export/UTC001")]
    [Trait("Trait", "UTC")]
    public void Export_the_chair_may_export()
    {
        var (_, meeting, chair, _, _) = NewMeeting();

        Assert.Equal(RecordingFailure.None, RecordingRules.Export(meeting, chair));
    }

    [Fact(DisplayName = "RecordingRules.Export/UTC002: a Member may not export")]
    [Trait("UTC", "RecordingRules.Export/UTC002")]
    [Trait("Trait", "UTC")]
    public void Rules_a_member_may_not_export()
    {
        var (_, meeting, _, _, member) = NewMeeting();

        Assert.Equal(RecordingFailure.ExportForbidden, RecordingRules.Export(meeting, member));
    }

    [Fact(DisplayName = "RecordingRules.Export/UTC003: a Secret meeting never exports")]
    [Trait("UTC", "RecordingRules.Export/UTC003")]
    [Trait("Trait", "UTC")]
    public void Rules_a_secret_meeting_never_exports()
    {
        var (_, meeting, chair, _, _) = NewMeeting(MeetingMode.InPerson, MeetingStatus.InProgress, Sensitivity.Secret);

        Assert.Equal(RecordingFailure.SecretMeeting, RecordingRules.Export(meeting, chair));
    }

    [Fact(DisplayName = "RecordingRules.Export/UTC004: the role check comes before the Secret check")]
    [Trait("UTC", "RecordingRules.Export/UTC004")]
    [Trait("Trait", "UTC")]
    public void Export_the_role_check_comes_before_the_secret_check()
    {
        var (_, meeting, _, _, member) = NewMeeting(MeetingMode.InPerson, MeetingStatus.InProgress, Sensitivity.Secret);

        Assert.Equal(RecordingFailure.ExportForbidden, RecordingRules.Export(meeting, member));
    }

    [Fact(DisplayName = "RecordingService.PlaybackAsync/UTC001: plays an Available recording with a signed link")]
    [Trait("UTC", "RecordingService.PlaybackAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Playback_plays_an_available_recording_with_a_signed_link()
    {
        var f = RecordingSvc(MeetingMode.InPerson, MeetingStatus.Published);
        var recording = StoredUpload(f);

        var outcome = await f.Service.PlaybackAsync(recording.Id, f.Member);

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        Assert.StartsWith("signed/", Assert.IsType<RecordingPlayback>(outcome.Data).Url);
        Assert.Equal([TransactionMode.None, TransactionMode.None], f.Uow.Modes);
    }

    [Fact(DisplayName = "RecordingService.PlaybackAsync/UTC002: a deactivated account hears nothing")]
    [Trait("UTC", "RecordingService.PlaybackAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Playback_a_deactivated_account_hears_nothing()
    {
        var f = RecordingSvc();
        var recording = StoredUpload(f);

        var outcome = await f.Service.PlaybackAsync(recording.Id, Guid.CreateVersion7());

        Assert.Equal(RecordingFailure.Inactive, outcome.Failure);
        Assert.Equal([TransactionMode.None, TransactionMode.None], f.Uow.Modes);
    }

    [Fact(DisplayName = "RecordingService.PlaybackAsync/UTC003: an erased recording is not played")]
    [Trait("UTC", "RecordingService.PlaybackAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Playback_an_erased_recording_is_not_played()
    {
        var f = RecordingSvc();
        var recording = StoredUpload(f);
        recording.Status = RecordingStatus.Deleted;

        var outcome = await f.Service.PlaybackAsync(recording.Id, f.Secretary);

        Assert.Equal(RecordingFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.Data);
    }

    [Fact(DisplayName = "RecordingService.PlaybackAsync/UTC004: nobody hears a streaming take")]
    [Trait("UTC", "RecordingService.PlaybackAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Playback_nobody_hears_a_streaming_take()
    {
        var f = RecordingSvc();
        var recording = new Recording
        {
            MeetingId = f.Meeting.Id, Source = RecordingSource.BrowserCapture, Status = RecordingStatus.Pending,
            FileName = "ghi-am-phong.wav", ContentType = "audio/wav", StoragePath = f.Files.Create(".wav"),
            Takes = [], CreatedById = f.Secretary, CreatedAt = Now, UpdatedAt = Now,
        };
        f.Recordings.Add(recording);

        var outcome = await f.Service.PlaybackAsync(recording.Id, f.Secretary);

        Assert.Equal(RecordingFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.Data);
    }

    [Fact(DisplayName = "RecordingService.PlaybackAsync/UTC005: only Available recordings are played")]
    [Trait("UTC", "RecordingService.PlaybackAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Playback_only_available_recordings_are_played()
    {
        var f = RecordingSvc();
        var recording = StoredUpload(f);
        recording.Status = RecordingStatus.Failed;

        var outcome = await f.Service.PlaybackAsync(recording.Id, f.Member);

        Assert.Equal(RecordingFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.Data);
    }

    [Fact(DisplayName = "RecordingService.PlaybackAsync/UTC006: a read failure is read failed with an error log")]
    [Trait("UTC", "RecordingService.PlaybackAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Playback_a_read_failure_is_read_failed_with_an_error_log()
    {
        var f = RecordingSvc();
        var recording = StoredUpload(f);
        f.Meetings.FailReads = true;

        var outcome = await f.Service.PlaybackAsync(recording.Id, f.Member);

        Assert.Equal(RecordingFailure.ReadFailed, outcome.Failure);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to sign playback"));
    }

    private sealed record TakeSvc(R1Meetings Meetings, FakeRecordingRepository Recordings, FakeTranscriptRepository Transcripts,
        FakeUnitOfWork Uow, FakeRecordingStorage Files, R1Storage Storage, FakeAuditService Audit, FakeSpeechChangePublisher Publisher,
        FakeCombinedRecordings Combined, R1Logger<RoomTakeService> Log, RoomTakeService Service,
        Meeting Meeting, Guid Chair, Guid Secretary, Guid Member);

    private static TakeSvc MakeTakeSvc(MeetingMode mode = MeetingMode.InPerson, MeetingStatus status = MeetingStatus.InProgress)
    {
        var (meetings, meeting, chair, secretary, member) = NewMeeting(mode, status);
        var recordings = new FakeRecordingRepository();
        var transcripts = new FakeTranscriptRepository();
        var uow = new FakeUnitOfWork();
        var files = new FakeRecordingStorage(uow);
        var storage = new R1Storage(files);
        var audit = new FakeAuditService();
        var publisher = new FakeSpeechChangePublisher(uow);
        var combined = new FakeCombinedRecordings();
        var log = new R1Logger<RoomTakeService>();
        var service = new RoomTakeService(meetings, recordings, uow, transcripts, storage, audit, publisher,
            combined, new FixedClock(Now), Options.Create(new RecordingOptions()), log);
        return new(meetings, recordings, transcripts, uow, files, storage, audit, publisher, combined, log, service,
            meeting, chair, secretary, member);
    }

    private static Recording PendingTake(TakeSvc f, long byteSize = 0, Guid? maker = null)
    {
        var take = new Recording
        {
            MeetingId = f.Meeting.Id, Source = RecordingSource.BrowserCapture, Status = RecordingStatus.Pending,
            FileName = "ghi-am-phong.wav", ContentType = "audio/wav", StoragePath = f.Files.Create(".wav"),
            ByteSize = byteSize, Takes = [], CreatedById = maker ?? f.Secretary, CreatedAt = Now, UpdatedAt = Now,
        };
        var buf = new byte[byteSize];
        f.Files.Files[take.StoragePath] = buf;
        f.Recordings.Add(take);
        return take;
    }

    private static byte[] WavPart(int size)
    {
        var part = new byte[size];
        CombinedRecordingRules.WavHeader().CopyTo(part, 0);
        return part;
    }

    [Fact(DisplayName = "RoomTakeService.AppendAsync/UTC001: the first part lands at offset zero")]
    [Trait("UTC", "RoomTakeService.AppendAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Append_the_first_part_lands_at_offset_zero()
    {
        var f = MakeTakeSvc();
        var take = PendingTake(f);

        var outcome = await f.Service.AppendAsync(take.Id, f.Secretary, 1, new MemoryStream(WavPart(320_000)), 320_000, false);

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        Assert.Equal(take.Id, outcome.RecordingId);
        Assert.Equal(320_000, take.ByteSize);
        Assert.Equal(9_998, take.DurationMs);
        Assert.Equal((1, 1), (f.Uow.Commits, f.Uow.Saves));
    }

    [Fact(DisplayName = "RoomTakeService.AppendAsync/UTC002: an empty first part is refused without a header")]
    [Trait("UTC", "RoomTakeService.AppendAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Append_an_empty_first_part_is_refused_without_a_header()
    {
        var f = MakeTakeSvc();
        var take = PendingTake(f);

        var outcome = await f.Service.AppendAsync(take.Id, f.Secretary, 1, new MemoryStream([]), null, true);

        Assert.Equal(RecordingFailure.BadPart, outcome.Failure);
        Assert.Equal(0, take.ByteSize);
        Assert.Equal(0, f.Uow.Saves);
    }

    [Fact(DisplayName = "RoomTakeService.AppendAsync/UTC003: only the final part may be shorter")]
    [Trait("UTC", "RoomTakeService.AppendAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Append_only_the_final_part_may_be_shorter()
    {
        var f = MakeTakeSvc();
        var take = PendingTake(f);

        var outcome = await f.Service.AppendAsync(take.Id, f.Secretary, 2, new MemoryStream([]), 100_000, false);

        Assert.Equal(RecordingFailure.BadPart, outcome.Failure);
        Assert.Equal(0, take.ByteSize);
        Assert.Equal(0, f.Uow.Saves);
    }

    [Fact(DisplayName = "RoomTakeService.AppendAsync/UTC004: a part past a hole is refused")]
    [Trait("UTC", "RoomTakeService.AppendAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Append_a_part_past_a_hole_is_refused()
    {
        var f = MakeTakeSvc();
        var take = PendingTake(f, 640_000);

        var outcome = await f.Service.AppendAsync(take.Id, f.Secretary, 4, new MemoryStream(WavPart(320_000)), null, false);

        Assert.Equal(RecordingFailure.BadPart, outcome.Failure);
        Assert.Equal(640_000, take.ByteSize);
        Assert.Equal(0, f.Uow.Saves);
    }

    [Fact(DisplayName = "RoomTakeService.AppendAsync/UTC005: a part past the size limit is too large")]
    [Trait("UTC", "RoomTakeService.AppendAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Append_a_part_past_the_size_limit_is_too_large()
    {
        var f = MakeTakeSvc();
        var take = PendingTake(f, 314_560_000);

        var outcome = await f.Service.AppendAsync(take.Id, f.Secretary, 984, new MemoryStream(WavPart(320_000)), null, false);

        Assert.Equal(RecordingFailure.TooLarge, outcome.Failure);
        Assert.Equal(314_560_000, take.ByteSize);
        Assert.Equal(0, f.Uow.Saves);
    }

    [Fact(DisplayName = "RoomTakeService.AppendAsync/UTC006: a final part may reach exactly the limit")]
    [Trait("UTC", "RoomTakeService.AppendAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Append_a_final_part_may_reach_exactly_the_limit()
    {
        var f = MakeTakeSvc();
        var take = PendingTake(f, 314_560_000);

        var outcome = await f.Service.AppendAsync(take.Id, f.Secretary, 984, new MemoryStream(new byte[12_800]), 12_800, true);

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        Assert.Equal(314_572_800, take.ByteSize);
        Assert.Equal((1, 1), (f.Uow.Commits, f.Uow.Saves));
    }

    [Fact(DisplayName = "RoomTakeService.AppendAsync/UTC007: a saved take takes no more parts")]
    [Trait("UTC", "RoomTakeService.AppendAsync/UTC007")]
    [Trait("Trait", "UTC")]
    public async Task Append_a_saved_take_takes_no_more_parts()
    {
        var f = MakeTakeSvc();
        var take = PendingTake(f);
        take.Status = RecordingStatus.Available;

        var outcome = await f.Service.AppendAsync(take.Id, f.Secretary, 2, new MemoryStream(WavPart(320_000)), null, false);

        Assert.Equal(RecordingFailure.TakeClosed, outcome.Failure);
        Assert.Equal(0, take.ByteSize);
    }

    [Fact(DisplayName = "RoomTakeService.AppendAsync/UTC008: a write failure is save failed with an error log")]
    [Trait("UTC", "RoomTakeService.AppendAsync/UTC008")]
    [Trait("Trait", "UTC")]
    public async Task Append_a_write_failure_is_save_failed_with_an_error_log()
    {
        var f = MakeTakeSvc();
        var take = PendingTake(f);
        f.Uow.FailNextSave = new PersistenceUnavailableException();

        var outcome = await f.Service.AppendAsync(take.Id, f.Secretary, 1, new MemoryStream(WavPart(320_000)), 320_000, false);

        Assert.Equal(RecordingFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 1), (f.Uow.Commits, f.Uow.Rollbacks));
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to write part"));
    }

    [Fact(DisplayName = "RoomTakeService.BeginAsync/UTC001: a take opens under the meeting lock in one transaction")]
    [Trait("UTC", "RoomTakeService.BeginAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Begin_a_take_opens_under_the_meeting_lock_in_one_transaction()
    {
        var f = MakeTakeSvc();

        var outcome = await f.Service.BeginAsync(f.Meeting.Id, f.Secretary, new RoomTakeStart(true, "Họp giao ban"));

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        var opened = Assert.IsType<RoomTakeOpened>(outcome.Data);
        Assert.Equal(320_000, opened.PartSize);
        Assert.Equal(314_572_800, opened.MaxBytes);
        var take = Assert.Single(f.Recordings.Recordings);
        Assert.Equal(RecordingStatus.Pending, take.Status);
        Assert.Equal(RecordingSource.BrowserCapture, take.Source);
        Assert.Equal("audio/wav", take.ContentType);
        Assert.StartsWith("ghi-am-phong-", take.FileName);
        Assert.Equal("Họp giao ban", take.ConsentPurpose);
        Assert.Equal(Now.AddDays(180), take.RetainUntil);
        Assert.Equal([f.Meeting.Id], f.Meetings.Locked);
        Assert.Equal([TransactionMode.ReadCommitted], f.Uow.Modes);
        Assert.Equal([AuditActions.RecordingTakeStarted], f.Audit.Actions);
        Assert.Equal(1, Assert.Single(f.Publisher.Published).CommitsBefore);
        Assert.Single(f.Files.Files);
    }

    [Fact(DisplayName = "RoomTakeService.BeginAsync/UTC002: the first take needs the room to be told")]
    [Trait("UTC", "RoomTakeService.BeginAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Begin_the_first_take_needs_the_room_to_be_told()
    {
        var f = MakeTakeSvc();

        var outcome = await f.Service.BeginAsync(f.Meeting.Id, f.Secretary, new RoomTakeStart(false, null));

        Assert.Equal(RecordingFailure.Invalid, outcome.Failure);
        Assert.Contains("consentConfirmed", outcome.Errors!.Keys);
        Assert.Empty(f.Recordings.Recordings);
        Assert.Empty(f.Files.Files);
        Assert.Empty(f.Publisher.Published);
    }

    [Fact(DisplayName = "RoomTakeService.BeginAsync/UTC003: the Chair may not open a take")]
    [Trait("UTC", "RoomTakeService.BeginAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Begin_the_chair_may_not_open_a_take()
    {
        var f = MakeTakeSvc();

        var outcome = await f.Service.BeginAsync(f.Meeting.Id, f.Chair, new RoomTakeStart(true, null));

        Assert.Equal(RecordingFailure.Forbidden, outcome.Failure);
        Assert.Empty(f.Recordings.Recordings);
        Assert.Empty(f.Files.Files);
    }

    [Fact(DisplayName = "RoomTakeService.BeginAsync/UTC004: an online meeting has no room mic")]
    [Trait("UTC", "RoomTakeService.BeginAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Begin_an_online_meeting_has_no_room_mic()
    {
        var f = MakeTakeSvc(MeetingMode.Online);

        var outcome = await f.Service.BeginAsync(f.Meeting.Id, f.Secretary, new RoomTakeStart(true, null));

        Assert.Equal(RecordingFailure.NoRoom, outcome.Failure);
        Assert.Empty(f.Recordings.Recordings);
    }

    [Fact(DisplayName = "RoomTakeService.BeginAsync/UTC005: a Published meeting is not running")]
    [Trait("UTC", "RoomTakeService.BeginAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Begin_a_published_meeting_is_not_running()
    {
        var f = MakeTakeSvc(MeetingMode.InPerson, MeetingStatus.Published);

        var outcome = await f.Service.BeginAsync(f.Meeting.Id, f.Secretary, new RoomTakeStart(true, null));

        Assert.Equal(RecordingFailure.NotRunning, outcome.Failure);
        Assert.Empty(f.Recordings.Recordings);
    }

    [Fact(DisplayName = "RoomTakeService.BeginAsync/UTC006: two devices opening at once leave one take open")]
    [Trait("UTC", "RoomTakeService.BeginAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Begin_two_devices_opening_at_once_leave_one_take_open()
    {
        var f = MakeTakeSvc();
        f.Uow.FailNextSave = new UniqueConflictException(UniqueConflict.Unknown);

        var outcome = await f.Service.BeginAsync(f.Meeting.Id, f.Secretary, new RoomTakeStart(true, null));

        Assert.Equal(RecordingFailure.TakeOpen, outcome.Failure);
        Assert.Equal(1, f.Uow.Rollbacks);
        Assert.Single(f.Files.Deleted);
        Assert.Empty(f.Files.Files);
        Assert.Empty(f.Publisher.Published);
    }

    private static Recording RoomRecording(TakeSvc f, long byteSize, int takeCount, long takeMs, bool sparse = false)
    {
        var takes = Enumerable.Range(0, takeCount).Select(i => new RecordingTake(i * takeMs, takeMs, Now)).ToList();
        var recording = new Recording
        {
            MeetingId = f.Meeting.Id, Source = RecordingSource.BrowserCapture, Status = RecordingStatus.Available,
            FileName = "ghi-am-phong.wav", ContentType = "audio/wav", StoragePath = f.Files.Create(".wav"),
            ByteSize = byteSize, DurationMs = RecordingRules.PcmMs(byteSize), Takes = takes,
            ConsentObtainedAt = Now, ConsentPurpose = "Họp giao ban", CreatedById = f.Secretary, CreatedAt = Now, UpdatedAt = Now,
        };
        f.Files.Files[recording.StoragePath] = sparse ? [] : new byte[byteSize];
        f.Recordings.Add(recording);
        return recording;
    }

    [Fact(DisplayName = "RoomTakeService.CompleteAsync/UTC001: the first take becomes the room recording")]
    [Trait("UTC", "RoomTakeService.CompleteAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Complete_the_first_take_becomes_the_room_recording()
    {
        var f = MakeTakeSvc();
        var take = PendingTake(f, 320_044);

        var outcome = await f.Service.CompleteAsync(take.Id, f.Secretary);

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        var saved = Assert.IsType<RoomTakeSaved>(outcome.Data);
        Assert.Equal(take.Id, saved.RecordingId);
        Assert.False(saved.Appended);
        Assert.Equal(RecordingStatus.Available, take.Status);
        Assert.Equal(320_044, take.ByteSize);
        Assert.Equal(10_000, take.DurationMs);
        Assert.Single(take.Takes!);
        Assert.Single(f.Transcripts.Added);
        Assert.Equal([AuditActions.RecordingTakeSaved], f.Audit.Actions);
        Assert.Equal(1, Assert.Single(f.Publisher.Published).CommitsBefore);
    }

    [Fact(DisplayName = "RoomTakeService.CompleteAsync/UTC002: a take is appended to the room recording")]
    [Trait("UTC", "RoomTakeService.CompleteAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Complete_a_take_is_appended_to_the_room_recording()
    {
        var f = MakeTakeSvc();
        var room = RoomRecording(f, 960_044, 2, 15_000);
        var roomPath = room.StoragePath;
        var take = PendingTake(f, 160_044);
        var takePath = take.StoragePath;
        var transcript = new Transcript { RecordingId = room.Id, Status = TranscriptStatus.Completed, CreatedAt = Now, UpdatedAt = Now };
        f.Transcripts.Transcripts.Add(transcript);

        var outcome = await f.Service.CompleteAsync(take.Id, f.Secretary);

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        var saved = Assert.IsType<RoomTakeSaved>(outcome.Data);
        Assert.Equal(room.Id, saved.RecordingId);
        Assert.True(saved.Appended);
        Assert.Equal(1_120_044, room.ByteSize);
        Assert.Equal(35_000, room.DurationMs);
        Assert.Equal(3, room.Takes!.Count);
        Assert.Equal(TranscriptStatus.Pending, transcript.Status);
        Assert.DoesNotContain(take.Id, f.Recordings.Recordings.Select(r => r.Id));
        Assert.Equal([roomPath, takePath], f.Files.Deleted);
        Assert.Equal([AuditActions.RecordingTakeSaved], f.Audit.Actions);
        Assert.Equal(1, Assert.Single(f.Publisher.Published).CommitsBefore);
    }

    [Fact(DisplayName = "RoomTakeService.CompleteAsync/UTC003: a take past a full room starts its own recording")]
    [Trait("UTC", "RoomTakeService.CompleteAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Complete_a_take_past_a_full_room_starts_its_own_recording()
    {
        var f = MakeTakeSvc();
        RoomRecording(f, 314_000_064, 2, 15_000, sparse: true);
        var take = PendingTake(f, 572_812);

        var outcome = await f.Service.CompleteAsync(take.Id, f.Secretary);

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        var saved = Assert.IsType<RoomTakeSaved>(outcome.Data);
        Assert.Equal(take.Id, saved.RecordingId);
        Assert.False(saved.Appended);
        Assert.Equal(RecordingStatus.Available, take.Status);
        Assert.Single(f.Transcripts.Added);
        Assert.Equal([AuditActions.RecordingTakeSaved], f.Audit.Actions);
        Assert.Empty(f.Files.Deleted);
    }

    [Fact(DisplayName = "RoomTakeService.CompleteAsync/UTC004: the Chair may not save a take")]
    [Trait("UTC", "RoomTakeService.CompleteAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Complete_the_chair_may_not_save_a_take()
    {
        var f = MakeTakeSvc();
        var take = PendingTake(f, 320_044);

        var outcome = await f.Service.CompleteAsync(take.Id, f.Chair);

        Assert.Equal(RecordingFailure.Forbidden, outcome.Failure);
        Assert.Equal(RecordingStatus.Pending, take.Status);
        Assert.Empty(f.Transcripts.Added);
    }

    [Fact(DisplayName = "RoomTakeService.CompleteAsync/UTC005: a Deleted recording is already closed")]
    [Trait("UTC", "RoomTakeService.CompleteAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Complete_a_deleted_recording_is_already_closed()
    {
        var f = MakeTakeSvc();
        var take = PendingTake(f, 320_044);
        take.Status = RecordingStatus.Deleted;

        var outcome = await f.Service.CompleteAsync(take.Id, f.Secretary);

        Assert.Equal(RecordingFailure.TakeClosed, outcome.Failure);
        Assert.Equal(RecordingStatus.Deleted, take.Status);
    }

    [Fact(DisplayName = "RoomTakeService.CompleteAsync/UTC006: a join failure keeps the take pending")]
    [Trait("UTC", "RoomTakeService.CompleteAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Complete_a_join_failure_keeps_the_take_pending()
    {
        var f = MakeTakeSvc();
        var room = RoomRecording(f, 960_044, 2, 15_000);
        var take = PendingTake(f, 160_044);
        f.Storage.ThrowSeal = true;

        var outcome = await f.Service.CompleteAsync(take.Id, f.Secretary);

        Assert.Equal(RecordingFailure.SaveFailed, outcome.Failure);
        Assert.Equal(RecordingStatus.Pending, take.Status);
        Assert.Equal(2, room.Takes!.Count);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to save take"));
    }

    private static (Recording Recording, Transcript Transcript) TakeWithTranscript(TakeSvc f)
    {
        var recording = new Recording
        {
            MeetingId = f.Meeting.Id, Source = RecordingSource.BrowserCapture, Status = RecordingStatus.Available,
            FileName = "ghi-am-phong.wav", ContentType = "audio/wav", StoragePath = f.Files.Create(".wav"),
            ByteSize = 736_044, DurationMs = 23_000,
            Takes = [new(0, 10_000, Now), new(10_000, 5_000, Now), new(15_000, 8_000, Now)],
            CreatedById = f.Secretary, CreatedAt = Now, UpdatedAt = Now,
        };
        f.Files.Files[recording.StoragePath] = new byte[736_044];
        f.Recordings.Add(recording);
        var transcript = new Transcript { RecordingId = recording.Id, Status = TranscriptStatus.Completed, CreatedAt = Now, UpdatedAt = Now };
        transcript.Segments.Add(new TranscriptSegment { TranscriptId = transcript.Id, Position = 1, StartMs = 1_000, EndMs = 4_000, Text = "A", SpeakerLabel = "S1" });
        transcript.Segments.Add(new TranscriptSegment { TranscriptId = transcript.Id, Position = 2, StartMs = 11_000, EndMs = 14_000, Text = "B", SpeakerLabel = "S2" });
        transcript.Segments.Add(new TranscriptSegment { TranscriptId = transcript.Id, Position = 3, StartMs = 16_000, EndMs = 20_000, Text = "C", SpeakerLabel = "S1" });
        transcript.Speakers.Add(new TranscriptSpeaker { TranscriptId = transcript.Id, Label = "S1", SpeechMs = 7_000 });
        transcript.Speakers.Add(new TranscriptSpeaker { TranscriptId = transcript.Id, Label = "S2", SpeechMs = 3_000 });
        f.Transcripts.Transcripts.Add(transcript);
        return (recording, transcript);
    }

    [Fact(DisplayName = "RoomTakeService.DeleteTakeAsync/UTC001: cutting a take moves the later lines up")]
    [Trait("UTC", "RoomTakeService.DeleteTakeAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task DeleteTake_cutting_a_take_moves_the_later_lines_up()
    {
        var f = MakeTakeSvc();
        var (recording, transcript) = TakeWithTranscript(f);
        var previous = recording.StoragePath;

        var outcome = await f.Service.DeleteTakeAsync(recording.Id, 2, f.Secretary);

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        Assert.Equal(recording.Id, outcome.RecordingId);
        Assert.Equal(576_044, recording.ByteSize);
        Assert.Equal(18_000, recording.DurationMs);
        Assert.Equal(2, recording.Takes!.Count);
        Assert.Equal(10_000, recording.Takes[1].OffsetMs);
        var kept = transcript.Segments.OrderBy(s => s.StartMs).ToList();
        Assert.Equal(2, kept.Count);
        Assert.Equal((11_000L, 15_000L), (kept[1].StartMs, kept[1].EndMs));
        Assert.DoesNotContain(transcript.Speakers, s => s.Label == "S2");
        Assert.Equal([AuditActions.RecordingTakeDeleted], f.Audit.Actions);
        Assert.Equal([previous], f.Files.Deleted);
        Assert.Equal(1, Assert.Single(f.Publisher.Published).CommitsBefore);
    }

    [Fact(DisplayName = "RoomTakeService.DeleteTakeAsync/UTC002: a line running past the take start ends there")]
    [Trait("UTC", "RoomTakeService.DeleteTakeAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task DeleteTake_a_line_running_past_the_take_start_ends_there()
    {
        var f = MakeTakeSvc();
        var (recording, transcript) = TakeWithTranscript(f);
        transcript.Segments.Clear();
        transcript.Segments.Add(new TranscriptSegment { TranscriptId = transcript.Id, Position = 1, StartMs = 8_000, EndMs = 12_000, Text = "X", SpeakerLabel = "S1" });

        var outcome = await f.Service.DeleteTakeAsync(recording.Id, 2, f.Secretary);

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        Assert.Equal(10_000, transcript.Segments.Single().EndMs);
        Assert.Equal(8_000, transcript.Segments.Single().StartMs);
    }

    [Fact(DisplayName = "RoomTakeService.DeleteTakeAsync/UTC003: a transcript still running is remade from the shorter file")]
    [Trait("UTC", "RoomTakeService.DeleteTakeAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task DeleteTake_a_transcript_still_running_is_remade_from_the_shorter_file()
    {
        var f = MakeTakeSvc();
        var (recording, transcript) = TakeWithTranscript(f);
        transcript.Status = TranscriptStatus.Processing;
        transcript.ProgressPercent = 50;

        var outcome = await f.Service.DeleteTakeAsync(recording.Id, 2, f.Secretary);

        Assert.Equal(RecordingFailure.None, outcome.Failure);
        Assert.Equal(TranscriptStatus.Pending, transcript.Status);
        Assert.Equal(0, transcript.ProgressPercent);
        // The last finished run's lines are cut as well (ADR-0077): the run that replaces them finds the Secretary's
        // corrections on the shorter file's clock.
        Assert.Equal([(1_000L, "A"), (11_000L, "C")], transcript.Segments.OrderBy(s => s.StartMs).Select(s => (s.StartMs, s.Text)));
        Assert.Equal([AuditActions.RecordingTakeDeleted], f.Audit.Actions);
    }

    [Fact(DisplayName = "RoomTakeService.DeleteTakeAsync/UTC004: the last take is erased whole instead")]
    [Trait("UTC", "RoomTakeService.DeleteTakeAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task DeleteTake_the_last_take_is_erased_whole_instead()
    {
        var f = MakeTakeSvc();
        var (recording, _) = TakeWithTranscript(f);
        recording.Takes = [new(0, 23_000, Now)];

        var outcome = await f.Service.DeleteTakeAsync(recording.Id, 1, f.Secretary);

        Assert.Equal(RecordingFailure.LastTake, outcome.Failure);
        Assert.Equal(736_044, recording.ByteSize);
        Assert.Empty(f.Files.Deleted);
    }

    [Fact(DisplayName = "RoomTakeService.DeleteTakeAsync/UTC005: the Chair may not cut a take")]
    [Trait("UTC", "RoomTakeService.DeleteTakeAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task DeleteTake_the_chair_may_not_cut_a_take()
    {
        var f = MakeTakeSvc();
        var (recording, _) = TakeWithTranscript(f);

        var outcome = await f.Service.DeleteTakeAsync(recording.Id, 2, f.Chair);

        Assert.Equal(RecordingFailure.Forbidden, outcome.Failure);
        Assert.Equal(736_044, recording.ByteSize);
    }

    [Fact(DisplayName = "RoomTakeService.DeleteTakeAsync/UTC006: a save failure keeps the original file")]
    [Trait("UTC", "RoomTakeService.DeleteTakeAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task DeleteTake_a_save_failure_keeps_the_original_file()
    {
        var f = MakeTakeSvc();
        var (recording, _) = TakeWithTranscript(f);
        var previous = recording.StoragePath;
        f.Uow.FailNextSave = new PersistenceUnavailableException();

        var outcome = await f.Service.DeleteTakeAsync(recording.Id, 2, f.Secretary);

        Assert.Equal(RecordingFailure.SaveFailed, outcome.Failure);
        Assert.Contains(previous, f.Files.Files.Keys);
        Assert.Equal(736_044, f.Files.Files[previous].Length);
        Assert.Single(f.Files.Deleted);
        Assert.NotEqual(previous, f.Files.Deleted.Single());
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to erase take"));
    }

    private sealed record CombinedSvc(FakeRecordingRepository Recordings, FakeSpeechClipRepository Clips,
        FakeMeetingRepository Meetings, FakeUnitOfWork Uow, FakeRecordingStorage Files, R1Storage Storage,
        FakeSpeechChangePublisher Publisher, FakeAuditService Audit, R1Logger<CombinedRecordingService> Log,
        CombinedRecordingService Service);

    private static CombinedSvc MakeCombinedSvc()
    {
        var recordings = new FakeRecordingRepository();
        var clips = new FakeSpeechClipRepository();
        var meetings = new FakeMeetingRepository();
        var uow = new FakeUnitOfWork();
        var files = new FakeRecordingStorage(uow);
        var storage = new R1Storage(files);
        var publisher = new FakeSpeechChangePublisher(uow);
        var audit = new FakeAuditService();
        var log = new R1Logger<CombinedRecordingService>();
        var service = new CombinedRecordingService(recordings, clips, meetings, uow, storage, publisher, audit,
            new FixedClock(Now), Options.Create(new RecordingOptions()), log);
        return new(recordings, clips, meetings, uow, files, storage, publisher, audit, log, service);
    }

    private static void HybridSources(CombinedSvc f, Guid meetingId, DateTimeOffset takeStart, long takeMs, int sentences, DateTimeOffset changed, bool withAudio = true)
    {
        f.Recordings.CombinableMeetings.Add(new CombinableMeeting(meetingId, MeetingMode.Hybrid));
        var roomPath = f.Files.Create(".wav");
        var roomBytes = withAudio ? new byte[RecordingRules.WavHeaderBytes + (int)(takeMs / 1000 * RecordingRules.RoomBytesPerSecond)] : CombinedRecordingRules.WavHeader();
        CombinedRecordingRules.WavHeader().CopyTo(roomBytes, 0);
        f.Files.Files[roomPath] = roomBytes;
        f.Recordings.Recordings.Add(new Recording
        {
            MeetingId = meetingId, Source = RecordingSource.BrowserCapture, Status = RecordingStatus.Available,
            FileName = "ghi-am-phong.wav", ContentType = "audio/wav", StoragePath = roomPath,
            ByteSize = withAudio ? roomBytes.Length : 44, DurationMs = takeMs, Takes = [new RecordingTake(0, takeMs, takeStart)],
            ConsentObtainedAt = takeStart, ConsentPurpose = "Lập biên bản", RetainUntil = null,
            CreatedById = Guid.CreateVersion7(), CreatedAt = takeStart, UpdatedAt = changed,
        });
        for (var i = 0; i < sentences; i++)
        {
            var clipPath = f.Files.Create(".wav");
            var clipBytes = withAudio ? new byte[RecordingRules.WavHeaderBytes + 5 * RecordingRules.RoomBytesPerSecond] : CombinedRecordingRules.WavHeader();
            CombinedRecordingRules.WavHeader().CopyTo(clipBytes, 0);
            f.Files.Files[clipPath] = clipBytes;
            f.Clips.Mixable.Add(new CombinedClip(takeStart.AddMilliseconds(500 + i * 1000), 5_000, clipPath, Now));
        }
        f.Clips.LastChange = changed;
    }

    [Fact(DisplayName = "CombinedRecordingService.BuildNextAsync/UTC001: the room and the sentences mix into one recording")]
    [Trait("UTC", "CombinedRecordingService.BuildNextAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task BuildNext_the_room_and_the_sentences_mix_into_one_recording()
    {
        var f = MakeCombinedSvc();
        var meetingId = Guid.CreateVersion7();
        var takeStart = new DateTimeOffset(2026, 11, 14, 3, 5, 0, TimeSpan.Zero);
        var changed = new DateTimeOffset(2026, 11, 18, 5, 0, 0, TimeSpan.Zero);
        HybridSources(f, meetingId, takeStart, 60_000, 3, changed);

        Assert.True(await f.Service.BuildNextAsync());

        var combined = Assert.Single(f.Recordings.Recordings, r => r.Source == RecordingSource.Combined);
        Assert.Equal(RecordingStatus.Available, combined.Status);
        Assert.Equal("audio/wav", combined.ContentType);
        Assert.Equal("ban-ghi-tong-hop-14-11-2026-1005.wav", combined.FileName);
        Assert.Equal(60_000, combined.DurationMs);
        Assert.Equal(44 + 1_920_000, combined.ByteSize);
        Assert.Equal("Lập biên bản", combined.ConsentPurpose);
        Assert.Equal(Now.AddDays(180), combined.RetainUntil);
        Assert.Equal(changed, combined.UpdatedAt);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("Built the combined recording")
            && e.Message.Contains("1 room takes") && e.Message.Contains("3 online sentences"));
        Assert.Equal(1, Assert.Single(f.Publisher.Published).CommitsBefore);
    }

    [Fact(DisplayName = "CombinedRecordingService.BuildNextAsync/UTC002: a streaming take waits for its save")]
    [Trait("UTC", "CombinedRecordingService.BuildNextAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task BuildNext_a_streaming_take_waits_for_its_save()
    {
        var f = MakeCombinedSvc();
        var meetingId = Guid.CreateVersion7();
        HybridSources(f, meetingId, new DateTimeOffset(2026, 11, 14, 3, 5, 0, TimeSpan.Zero), 60_000, 1,
            new DateTimeOffset(2026, 11, 18, 5, 0, 0, TimeSpan.Zero));
        f.Recordings.Recordings.Add(new Recording
        {
            MeetingId = meetingId, Source = RecordingSource.BrowserCapture, Status = RecordingStatus.Pending,
            FileName = "take.wav", ContentType = "audio/wav", StoragePath = f.Files.Create(".wav"), Takes = [],
        });

        Assert.False(await f.Service.BuildNextAsync());

        Assert.DoesNotContain(f.Recordings.Recordings, r => r.Source == RecordingSource.Combined);
        Assert.Empty(f.Publisher.Published);
    }

    [Fact(DisplayName = "CombinedRecordingService.BuildNextAsync/UTC003: a hybrid meeting needs both sides")]
    [Trait("UTC", "CombinedRecordingService.BuildNextAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task BuildNext_a_hybrid_meeting_needs_both_sides()
    {
        var f = MakeCombinedSvc();
        var meetingId = Guid.CreateVersion7();
        f.Recordings.CombinableMeetings.Add(new CombinableMeeting(meetingId, MeetingMode.Hybrid));
        f.Clips.Mixable.Add(new CombinedClip(Now.AddHours(-3), 5_000, f.Files.Create(".wav"), Now.AddHours(-3)));
        f.Clips.LastChange = Now.AddHours(-3);

        Assert.False(await f.Service.BuildNextAsync());

        Assert.Empty(f.Recordings.Recordings);
        Assert.Empty(f.Publisher.Published);
    }

    [Fact(DisplayName = "CombinedRecordingService.BuildNextAsync/UTC004: a change exactly at the settle limit still builds")]
    [Trait("UTC", "CombinedRecordingService.BuildNextAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task BuildNext_a_change_exactly_at_the_settle_limit_still_builds()
    {
        var f = MakeCombinedSvc();
        var meetingId = Guid.CreateVersion7();
        var changed = Now.AddMinutes(-1);
        HybridSources(f, meetingId, changed.AddMinutes(-5), 60_000, 1, changed);

        Assert.True(await f.Service.BuildNextAsync());

        var combined = Assert.Single(f.Recordings.Recordings, r => r.Source == RecordingSource.Combined);
        Assert.Equal(changed, combined.UpdatedAt);
        Assert.Equal(1, Assert.Single(f.Publisher.Published).CommitsBefore);
    }

    [Fact(DisplayName = "CombinedRecordingService.BuildNextAsync/UTC005: an erased combined recording is never rebuilt")]
    [Trait("UTC", "CombinedRecordingService.BuildNextAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task BuildNext_an_erased_combined_recording_is_never_rebuilt()
    {
        var f = MakeCombinedSvc();
        var meetingId = Guid.CreateVersion7();
        HybridSources(f, meetingId, new DateTimeOffset(2026, 11, 14, 3, 5, 0, TimeSpan.Zero), 60_000, 1,
            new DateTimeOffset(2026, 11, 18, 5, 0, 0, TimeSpan.Zero));
        f.Recordings.Recordings.Add(new Recording
        {
            MeetingId = meetingId, Source = RecordingSource.Combined, Status = RecordingStatus.Deleted,
            FileName = "ban-ghi-tong-hop.wav", ContentType = "audio/wav", StoragePath = "erased.wav", CreatedAt = Now.AddMinutes(-30),
        });

        Assert.False(await f.Service.BuildNextAsync());

        Assert.Equal(0, f.Uow.Saves);
        Assert.Empty(f.Publisher.Published);
    }

    [Fact(DisplayName = "CombinedRecordingService.BuildNextAsync/UTC006: a track past twelve hours fails the build")]
    [Trait("UTC", "CombinedRecordingService.BuildNextAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task BuildNext_a_track_past_twelve_hours_fails_the_build()
    {
        var f = MakeCombinedSvc();
        var meetingId = Guid.CreateVersion7();
        var start = new DateTimeOffset(2026, 11, 14, 3, 5, 0, TimeSpan.Zero);
        var changed = new DateTimeOffset(2026, 11, 18, 5, 0, 0, TimeSpan.Zero);
        HybridSources(f, meetingId, start, 13 * 3_600_000L, 1, changed, withAudio: false);

        Assert.True(await f.Service.BuildNextAsync());

        Assert.DoesNotContain(f.Recordings.Recordings, r => r.Source == RecordingSource.Combined);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Could not build the combined recording"));
        Assert.False(await f.Service.BuildNextAsync());
    }

    private sealed record ClipSvc(R1Meetings Meetings, R1Clips Clips, FakeSpeechClipRepository ClipsInner, FakeUnitOfWork Uow,
        FakeRecordingStorage Files, R1Storage Storage, FakeSpeechEngine Engine, FakeAuditService Audit,
        FakeSpeechChangePublisher Publisher, R1Logger<SpeechClipService> Log, SpeechClipService Service,
        Meeting Meeting, Guid Chair, Guid Secretary, Guid Member);

    private static ClipSvc MakeClipSvc(MeetingMode mode = MeetingMode.Online, MeetingStatus status = MeetingStatus.InProgress)
    {
        var (meetings, meeting, chair, secretary, member) = NewMeeting(mode, status);
        var clipsInner = new FakeSpeechClipRepository();
        var clips = new R1Clips(clipsInner);
        var uow = new FakeUnitOfWork();
        var files = new FakeRecordingStorage(uow);
        var storage = new R1Storage(files);
        var engine = new FakeSpeechEngine();
        var audit = new FakeAuditService();
        var publisher = new FakeSpeechChangePublisher(uow);
        var log = new R1Logger<SpeechClipService>();
        var service = new SpeechClipService(meetings, clips, uow, engine, new SpeechClipGate(), publisher, audit,
            new FixedClock(Now), Options.Create(new RecordingOptions()), storage, new FakeRecordingSigner(),
            new FakeCombinedRecordings(), log);
        return new(meetings, clips, clipsInner, uow, files, storage, engine, audit, publisher, log, service,
            meeting, chair, secretary, member);
    }

    private static SpeechClipUpload ClipUpload(long durationMs = 5_000) =>
        new(Guid.CreateVersion7(), Now.AddSeconds(-2), durationMs, "audio/wav", [1, 2, 3]);

    [Fact(DisplayName = "SpeechClipService.AddAsync/UTC001: a sentence is stored with its audio and announced")]
    [Trait("UTC", "SpeechClipService.AddAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Add_a_sentence_is_stored_with_its_audio_and_announced()
    {
        var f = MakeClipSvc();
        var upload = ClipUpload();

        var outcome = await f.Service.AddAsync(f.Meeting.Id, f.Member, upload);

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        var clip = Assert.Single(f.ClipsInner.Added);
        Assert.Equal("Xin chào", clip.Text);
        Assert.Equal(upload.StartedAt, clip.StartedAt);
        Assert.Equal(5_000, clip.DurationMs);
        Assert.Contains(clip.AudioPath!, f.Files.Files.Keys);
        Assert.Equal(("ClipAdded", 1), (Assert.Single(f.Publisher.Published).Change.ChangeType, Assert.Single(f.Publisher.Published).CommitsBefore));
    }

    [Fact(DisplayName = "SpeechClipService.AddAsync/UTC002: a zero-length sentence is invalid")]
    [Trait("UTC", "SpeechClipService.AddAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Add_a_zero_length_sentence_is_invalid()
    {
        var f = MakeClipSvc();

        var outcome = await f.Service.AddAsync(f.Meeting.Id, f.Member, ClipUpload(0));

        Assert.Equal(SpeechClipFailure.Invalid, outcome.Failure);
        Assert.Equal(["Một câu dài từ 1 ms đến 60 giây."], outcome.Errors!["durationMs"]);
        Assert.Empty(f.ClipsInner.Added);
        Assert.Empty(f.Files.Files);
    }

    [Fact(DisplayName = "SpeechClipService.AddAsync/UTC003: a sentence of exactly sixty seconds is accepted")]
    [Trait("UTC", "SpeechClipService.AddAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Add_a_sentence_of_exactly_sixty_seconds_is_accepted()
    {
        var f = MakeClipSvc();

        var outcome = await f.Service.AddAsync(f.Meeting.Id, f.Member, ClipUpload(60_000));

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        Assert.Equal(60_000, Assert.Single(f.ClipsInner.Added).DurationMs);
        Assert.Equal(1, Assert.Single(f.Publisher.Published).CommitsBefore);
    }

    [Fact(DisplayName = "SpeechClipService.AddAsync/UTC004: a sentence past the late window is not captured")]
    [Trait("UTC", "SpeechClipService.AddAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Add_a_sentence_past_the_late_window_is_not_captured()
    {
        var f = MakeClipSvc(MeetingMode.Online, MeetingStatus.Ended);
        f.Meeting.ActualEndedAt = Now.AddDays(-8);

        var outcome = await f.Service.AddAsync(f.Meeting.Id, f.Member, ClipUpload());

        Assert.Equal(SpeechClipFailure.NotCapturing, outcome.Failure);
        Assert.Empty(f.ClipsInner.Added);
        Assert.Empty(f.Files.Files);
        Assert.Equal(0, f.Uow.Saves);
        Assert.Empty(f.Publisher.Published);
    }

    [Fact(DisplayName = "SpeechClipService.AddAsync/UTC005: a worker failure is engine unavailable with a warning")]
    [Trait("UTC", "SpeechClipService.AddAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Add_a_worker_failure_is_engine_unavailable_with_a_warning()
    {
        var f = MakeClipSvc();
        f.Engine.Fail = new SpeechEngineException("worker down");

        var outcome = await f.Service.AddAsync(f.Meeting.Id, f.Member, ClipUpload());

        Assert.Equal(SpeechClipFailure.EngineUnavailable, outcome.Failure);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("could not transcribe"));
        Assert.Empty(f.ClipsInner.Added);
        Assert.Empty(f.Files.Files);
    }

    [Fact(DisplayName = "SpeechClipService.AddAsync/UTC006: a store failure deletes the written audio")]
    [Trait("UTC", "SpeechClipService.AddAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Add_a_store_failure_deletes_the_written_audio()
    {
        var f = MakeClipSvc();
        f.Uow.FailNextSave = new PersistenceUnavailableException();

        var outcome = await f.Service.AddAsync(f.Meeting.Id, f.Member, ClipUpload());

        Assert.Equal(SpeechClipFailure.SaveFailed, outcome.Failure);
        Assert.Equal(1, f.Uow.Rollbacks);
        Assert.Single(f.Files.Deleted);
        Assert.Empty(f.Files.Files);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to store a speech clip"));
    }

    private static MeetingGuestSession GuestSession(ClipSvc f, Guid invitationId, bool admitted, bool notice)
    {
        var session = new MeetingGuestSession(f.Meeting.Id, invitationId, Guid.CreateVersion7());
        if (admitted) f.ClipsInner.AdmittedSessions.Add(session.SessionId);
        if (notice) f.ClipsInner.ConfirmedNotices.Add(session.SessionId);
        return session;
    }

    private static SpeechClip GuestClip(ClipSvc f, Guid invitationId, bool audio, bool excluded)
    {
        var clip = new SpeechClip
        {
            MeetingId = f.Meeting.Id, GuestInvitationId = invitationId, ClientClipId = Guid.CreateVersion7(),
            StartedAt = Now.AddSeconds(-5), DurationMs = 2000, Text = "Xin chào",
            CreatedAt = Now, UpdatedAt = Now, ExcludedAt = excluded ? Now : null,
            AudioPath = audio ? f.Files.Create(".wav") : null, AudioContentType = audio ? "audio/wav" : null,
        };
        f.ClipsInner.Clips.Add(clip);
        return clip;
    }

    [Fact(DisplayName = "SpeechClipService.AddForGuestAsync/UTC001: a guest sentence is kept under the invitation")]
    [Trait("UTC", "SpeechClipService.AddForGuestAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task AddForGuest_a_guest_sentence_is_kept_under_the_invitation()
    {
        var f = MakeClipSvc();
        var invitation = Guid.CreateVersion7();
        var guest = GuestSession(f, invitation, admitted: true, notice: true);

        var outcome = await f.Service.AddForGuestAsync(guest, f.Meeting.Id, ClipUpload());

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        var clip = Assert.Single(f.ClipsInner.Added);
        Assert.Equal(invitation, clip.GuestInvitationId);
        Assert.True(Assert.IsType<SpeechClipView>(outcome.Data).SpeakerIsGuest);
        Assert.Equal(1, Assert.Single(f.Publisher.Published).CommitsBefore);
    }

    [Fact(DisplayName = "SpeechClipService.AddForGuestAsync/UTC002: an empty guest sentence is invalid")]
    [Trait("UTC", "SpeechClipService.AddForGuestAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task AddForGuest_an_empty_guest_sentence_is_invalid()
    {
        var f = MakeClipSvc();
        var guest = GuestSession(f, Guid.CreateVersion7(), admitted: true, notice: true);

        var outcome = await f.Service.AddForGuestAsync(guest, f.Meeting.Id, ClipUpload() with { Audio = [] });

        Assert.Equal(SpeechClipFailure.Invalid, outcome.Failure);
        Assert.Contains("audio", outcome.Errors!.Keys);
        Assert.Empty(f.ClipsInner.Added);
    }

    [Fact(DisplayName = "SpeechClipService.AddForGuestAsync/UTC003: a guest without a confirmed notice is refused")]
    [Trait("UTC", "SpeechClipService.AddForGuestAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task AddForGuest_a_guest_without_a_confirmed_notice_is_refused()
    {
        var f = MakeClipSvc();
        var guest = GuestSession(f, Guid.CreateVersion7(), admitted: true, notice: false);

        var outcome = await f.Service.AddForGuestAsync(guest, f.Meeting.Id, ClipUpload());

        Assert.Equal(SpeechClipFailure.NoticeRequired, outcome.Failure);
        Assert.Empty(f.ClipsInner.Added);
        Assert.Empty(f.Publisher.Published);
    }

    [Fact(DisplayName = "SpeechClipService.AddForGuestAsync/UTC004: revoked between check and store is refused and the audio deleted")]
    [Trait("UTC", "SpeechClipService.AddForGuestAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task AddForGuest_revoked_between_check_and_store_is_refused_and_the_audio_deleted()
    {
        var f = MakeClipSvc();
        var guest = GuestSession(f, Guid.CreateVersion7(), admitted: true, notice: true);
        // The first admission check (before the sentence is transcribed) passes; the one inside the store finds it revoked.
        f.ClipsInner.RevokeAdmissionAfterChecks = 1;

        var outcome = await f.Service.AddForGuestAsync(guest, f.Meeting.Id, ClipUpload());

        Assert.Equal(SpeechClipFailure.NotCapturing, outcome.Failure);
        Assert.Empty(f.ClipsInner.Added);
        Assert.Empty(f.Files.Files);
        Assert.Empty(f.Publisher.Published);
    }

    private static SpeechClip MemberClip(ClipSvc f, Guid speaker, bool audio, bool excluded = false)
    {
        var clip = new SpeechClip
        {
            MeetingId = f.Meeting.Id, SpeakerId = speaker, ClientClipId = Guid.CreateVersion7(),
            StartedAt = Now.AddSeconds(-5), DurationMs = 5000, Text = "Xin chào",
            CreatedAt = Now, UpdatedAt = Now, ExcludedAt = excluded ? Now : null,
            AudioPath = audio ? f.Files.Create(".wav") : null, AudioContentType = audio ? "audio/wav" : null,
        };
        f.ClipsInner.Clips.Add(clip);
        return clip;
    }

    [Fact(DisplayName = "SpeechClipService.DeleteAsync/UTC001: the speaker takes back their sentence")]
    [Trait("UTC", "SpeechClipService.DeleteAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Delete_the_speaker_takes_back_their_sentence()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: true);

        var outcome = await f.Service.DeleteAsync(clip.Id, f.Member);

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        Assert.Empty(f.ClipsInner.Clips);
        Assert.Equal([clip.AudioPath!], f.Files.Deleted);
        Assert.Empty(f.Files.Files);
        Assert.Equal([AuditActions.SpeechClipDeleted], f.Audit.Actions);
        Assert.Equal(1, Assert.Single(f.Publisher.Published).CommitsBefore);
    }

    [Fact(DisplayName = "SpeechClipService.DeleteAsync/UTC002: the Secretary removes another member sentence")]
    [Trait("UTC", "SpeechClipService.DeleteAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Delete_the_secretary_removes_another_member_sentence()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: true);

        var outcome = await f.Service.DeleteAsync(clip.Id, f.Secretary);

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        Assert.Empty(f.ClipsInner.Clips);
        Assert.Equal([clip.AudioPath!], f.Files.Deleted);
        Assert.Equal([AuditActions.SpeechClipDeleted], f.Audit.Actions);
    }

    [Fact(DisplayName = "SpeechClipService.DeleteAsync/UTC003: a stranger to the sentence is forbidden")]
    [Trait("UTC", "SpeechClipService.DeleteAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Delete_a_stranger_to_the_sentence_is_forbidden()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: true);
        var other = Guid.CreateVersion7();
        f.Meeting.Participants.Add(new MeetingParticipant
        {
            MeetingId = f.Meeting.Id, UserId = other, Role = MeetingRole.Member, AssignmentStatus = AssignmentStatus.Current,
            Response = InvitationResponse.Accepted,
            User = new User { Id = other, FullName = "Other", Email = $"{other}@trustcabinet.local", IsActive = true },
        });
        f.Meetings.ActiveUsers.Add(other);

        var outcome = await f.Service.DeleteAsync(clip.Id, other);

        Assert.Equal(SpeechClipFailure.Forbidden, outcome.Failure);
        Assert.Single(f.ClipsInner.Clips);
        Assert.Empty(f.Files.Deleted);
    }

    [Fact(DisplayName = "SpeechClipService.DeleteAsync/UTC004: deleting nothing is not found")]
    [Trait("UTC", "SpeechClipService.DeleteAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Delete_deleting_nothing_is_not_found()
    {
        var f = MakeClipSvc();

        var outcome = await f.Service.DeleteAsync(Guid.CreateVersion7(), f.Member);

        Assert.Equal(SpeechClipFailure.NotFound, outcome.Failure);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "SpeechClipService.DeleteAsync/UTC005: a deactivated speaker is inactive")]
    [Trait("UTC", "SpeechClipService.DeleteAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Delete_a_deactivated_speaker_is_inactive()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: true);
        var ghost = Guid.CreateVersion7();

        var outcome = await f.Service.DeleteAsync(clip.Id, ghost);

        Assert.Equal(SpeechClipFailure.Inactive, outcome.Failure);
        Assert.Single(f.ClipsInner.Clips);
    }

    [Fact(DisplayName = "SpeechClipService.DeleteAsync/UTC006: a save failure keeps the row and its audio")]
    [Trait("UTC", "SpeechClipService.DeleteAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Delete_a_save_failure_keeps_the_row_and_its_audio()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: true);
        f.Uow.FailNextSave = new PersistenceUnavailableException();

        var outcome = await f.Service.DeleteAsync(clip.Id, f.Member);

        Assert.Equal(SpeechClipFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 1), (f.Uow.Commits, f.Uow.Rollbacks));
        Assert.Contains(clip.AudioPath!, f.Files.Files.Keys);
        Assert.Empty(f.Files.Deleted);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to delete speech clip"));
    }

    [Fact(DisplayName = "SpeechClipService.DeleteForGuestAsync/UTC001: a guest takes back their own sentence")]
    [Trait("UTC", "SpeechClipService.DeleteForGuestAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task DeleteForGuest_a_guest_takes_back_their_own_sentence()
    {
        var f = MakeClipSvc();
        var invitation = Guid.CreateVersion7();
        var guest = GuestSession(f, invitation, admitted: true, notice: true);
        var clip = GuestClip(f, invitation, audio: true, excluded: false);

        var outcome = await f.Service.DeleteForGuestAsync(guest, clip.Id);

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        Assert.Empty(f.ClipsInner.Clips);
        Assert.Equal([clip.AudioPath!], f.Files.Deleted);
        Assert.Equal([AuditActions.SpeechClipDeleted], f.Audit.Actions);
        Assert.Equal(1, Assert.Single(f.Publisher.Published).CommitsBefore);
    }

    [Fact(DisplayName = "SpeechClipService.DeleteForGuestAsync/UTC002: someone else sentence does not exist for a guest")]
    [Trait("UTC", "SpeechClipService.DeleteForGuestAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task DeleteForGuest_someone_else_sentence_does_not_exist_for_a_guest()
    {
        var f = MakeClipSvc();
        var guest = GuestSession(f, Guid.CreateVersion7(), admitted: true, notice: true);
        var clip = GuestClip(f, Guid.CreateVersion7(), audio: true, excluded: false);

        var outcome = await f.Service.DeleteForGuestAsync(guest, clip.Id);

        Assert.Equal(SpeechClipFailure.NotFound, outcome.Failure);
        Assert.Single(f.ClipsInner.Clips);
    }

    [Fact(DisplayName = "SpeechClipService.DeleteForGuestAsync/UTC003: an expired session deletes nothing")]
    [Trait("UTC", "SpeechClipService.DeleteForGuestAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task DeleteForGuest_an_expired_session_deletes_nothing()
    {
        var f = MakeClipSvc();
        var invitation = Guid.CreateVersion7();
        var guest = GuestSession(f, invitation, admitted: false, notice: false);
        var clip = GuestClip(f, invitation, audio: true, excluded: false);

        var outcome = await f.Service.DeleteForGuestAsync(guest, clip.Id);

        Assert.Equal(SpeechClipFailure.NotFound, outcome.Failure);
        Assert.Single(f.ClipsInner.Clips);
    }

    [Fact(DisplayName = "SpeechClipService.DeleteForGuestAsync/UTC004: a save failure keeps the guest row and audio")]
    [Trait("UTC", "SpeechClipService.DeleteForGuestAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task DeleteForGuest_a_save_failure_keeps_the_guest_row_and_audio()
    {
        var f = MakeClipSvc();
        var invitation = Guid.CreateVersion7();
        var guest = GuestSession(f, invitation, admitted: true, notice: true);
        var clip = GuestClip(f, invitation, audio: true, excluded: false);
        f.Uow.FailNextSave = new PersistenceUnavailableException();

        var outcome = await f.Service.DeleteForGuestAsync(guest, clip.Id);

        Assert.Equal(SpeechClipFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 1), (f.Uow.Commits, f.Uow.Rollbacks));
        Assert.Contains(clip.AudioPath!, f.Files.Files.Keys);
        Assert.Empty(f.Files.Deleted);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("for a guest") && e.Message.Contains("Failed to delete speech clip"));
    }

    [Fact(DisplayName = "SpeechClipService.EditAsync/UTC001: the Secretary corrects the wording")]
    [Trait("UTC", "SpeechClipService.EditAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Edit_the_secretary_corrects_the_wording()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: false);

        var outcome = await f.Service.EditAsync(clip.Id, f.Secretary, "cách nói đã sửa");

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        Assert.Equal("cách nói đã sửa", clip.Text);
        Assert.Equal(f.Secretary, clip.EditedById);
        Assert.Equal(Now, clip.EditedAt);
        Assert.Equal([AuditActions.SpeechClipEdited], f.Audit.Actions);
        Assert.Equal(1, Assert.Single(f.Publisher.Published).CommitsBefore);
    }

    [Fact(DisplayName = "SpeechClipService.EditAsync/UTC002: a Member may not correct a sentence")]
    [Trait("UTC", "SpeechClipService.EditAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Edit_a_member_may_not_correct_a_sentence()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: false);

        var outcome = await f.Service.EditAsync(clip.Id, f.Member, "cách nói đã sửa");

        Assert.Equal(SpeechClipFailure.Forbidden, outcome.Failure);
        Assert.Equal("Xin chào", clip.Text);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "SpeechClipService.EditAsync/UTC003: text past 8000 characters is invalid")]
    [Trait("UTC", "SpeechClipService.EditAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Edit_text_past_8000_characters_is_invalid()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: false);

        var outcome = await f.Service.EditAsync(clip.Id, f.Secretary, new string('x', 8001));

        Assert.Equal(SpeechClipFailure.Invalid, outcome.Failure);
        Assert.Equal(["Nội dung từ 1 đến 8000 ký tự."], outcome.Errors!["text"]);
        Assert.Equal("Xin chào", clip.Text);
    }

    [Fact(DisplayName = "SpeechClipService.EditAsync/UTC004: editing nothing is not found")]
    [Trait("UTC", "SpeechClipService.EditAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Edit_editing_nothing_is_not_found()
    {
        var f = MakeClipSvc();

        var outcome = await f.Service.EditAsync(Guid.CreateVersion7(), f.Secretary, "cách nói đã sửa");

        Assert.Equal(SpeechClipFailure.NotFound, outcome.Failure);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "SpeechClipService.EditAsync/UTC005: a deactivated editor is inactive")]
    [Trait("UTC", "SpeechClipService.EditAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Edit_a_deactivated_editor_is_inactive()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: false);

        var outcome = await f.Service.EditAsync(clip.Id, Guid.CreateVersion7(), "cách nói đã sửa");

        Assert.Equal(SpeechClipFailure.Inactive, outcome.Failure);
        Assert.Equal("Xin chào", clip.Text);
    }

    [Fact(DisplayName = "SpeechClipService.EditAsync/UTC006: a save failure keeps the old wording")]
    [Trait("UTC", "SpeechClipService.EditAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Edit_a_save_failure_keeps_the_old_wording()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: false);
        f.Uow.FailNextSave = new PersistenceUnavailableException();

        var outcome = await f.Service.EditAsync(clip.Id, f.Secretary, "cách nói đã sửa");

        Assert.Equal(SpeechClipFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 1), (f.Uow.Commits, f.Uow.Rollbacks));
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to edit speech clip"));
    }

    [Fact(DisplayName = "SpeechClipService.ExportAsync/UTC001: exports kept sentences with guest labels")]
    [Trait("UTC", "SpeechClipService.ExportAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Export_exports_kept_sentences_with_guest_labels()
    {
        var f = MakeClipSvc();
        f.ClipsInner.Add(MemberClip(f, f.Member, audio: false));
        var invitation = Guid.CreateVersion7();
        f.ClipsInner.Add(GuestClip(f, invitation, audio: false, excluded: false));
        f.ClipsInner.Add(MemberClip(f, f.Member, audio: false));
        f.ClipsInner.Clips[^1].ExcludedAt = Now;
        f.ClipsInner.Rows[^1] = f.ClipsInner.Rows[^1] with { ExcludedAt = Now };

        var outcome = await f.Service.ExportAsync(f.Meeting.Id, f.Chair);

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        var file = Assert.IsType<ExportedText>(outcome.Data);
        Assert.Contains("Xin chào", file.Text);
        Assert.Contains("(Khách)", file.Text);
        Assert.Equal(2, file.Text.Split("Xin chào").Length - 1);
        Assert.Equal([AuditActions.SpeechClipsExported], f.Audit.Actions);
    }

    [Fact(DisplayName = "SpeechClipService.ExportAsync/UTC002: a Member may not export")]
    [Trait("UTC", "SpeechClipService.ExportAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task ClipExport_a_member_may_not_export()
    {
        var f = MakeClipSvc();

        var outcome = await f.Service.ExportAsync(f.Meeting.Id, f.Member);

        Assert.Equal(SpeechClipFailure.ExportForbidden, outcome.Failure);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "SpeechClipService.ExportAsync/UTC003: a Secret meeting never exports")]
    [Trait("UTC", "SpeechClipService.ExportAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task ClipExport_a_secret_meeting_never_exports()
    {
        var f = MakeClipSvc(MeetingMode.Online, MeetingStatus.InProgress);
        f.Meeting.Sensitivity = Sensitivity.Secret;

        var outcome = await f.Service.ExportAsync(f.Meeting.Id, f.Chair);

        Assert.Equal(SpeechClipFailure.SecretMeeting, outcome.Failure);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "SpeechClipService.ExportAsync/UTC004: an empty record exports a header only")]
    [Trait("UTC", "SpeechClipService.ExportAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Export_an_empty_record_exports_a_header_only()
    {
        var f = MakeClipSvc();

        var outcome = await f.Service.ExportAsync(f.Meeting.Id, f.Chair);

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        Assert.NotEmpty(Assert.IsType<ExportedText>(outcome.Data).Text);
        Assert.Equal([AuditActions.SpeechClipsExported], f.Audit.Actions);
    }

    [Fact(DisplayName = "SpeechClipService.ExportAsync/UTC005: an outsider reads nothing")]
    [Trait("UTC", "SpeechClipService.ExportAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Export_an_outsider_reads_nothing()
    {
        var f = MakeClipSvc();
        var outsider = Guid.CreateVersion7();
        f.Meetings.ActiveUsers.Add(outsider);

        var outcome = await f.Service.ExportAsync(f.Meeting.Id, outsider);

        Assert.Equal(SpeechClipFailure.NotFound, outcome.Failure);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "SpeechClipService.ExportAsync/UTC006: a read failure is read failed with an error log")]
    [Trait("UTC", "SpeechClipService.ExportAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Export_a_read_failure_is_read_failed_with_an_error_log()
    {
        var f = MakeClipSvc();
        f.Meetings.FailReads = true;

        var outcome = await f.Service.ExportAsync(f.Meeting.Id, f.Chair);

        Assert.Equal(SpeechClipFailure.ReadFailed, outcome.Failure);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to export the speech clips"));
    }

    [Fact(DisplayName = "SpeechClipService.PlaybackAsync/UTC001: hears one sentence with a signed link")]
    [Trait("UTC", "SpeechClipService.PlaybackAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Playback_hears_one_sentence_with_a_signed_link()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: true);

        var outcome = await f.Service.PlaybackAsync(clip.Id, f.Member);

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        Assert.StartsWith("signed/", Assert.IsType<RecordingPlayback>(outcome.Data).Url);
        Assert.Equal([TransactionMode.None, TransactionMode.None], f.Uow.Modes);
    }

    [Fact(DisplayName = "SpeechClipService.PlaybackAsync/UTC002: an excluded sentence does not exist for others")]
    [Trait("UTC", "SpeechClipService.PlaybackAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Playback_an_excluded_sentence_does_not_exist_for_others()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: true, excluded: true);

        var outcome = await f.Service.PlaybackAsync(clip.Id, f.Member);

        Assert.Equal(SpeechClipFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.Data);
    }

    [Fact(DisplayName = "SpeechClipService.PlaybackAsync/UTC003: a deactivated listener is inactive")]
    [Trait("UTC", "SpeechClipService.PlaybackAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Playback_a_deactivated_listener_is_inactive()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: true);

        var outcome = await f.Service.PlaybackAsync(clip.Id, Guid.CreateVersion7());

        Assert.Equal(SpeechClipFailure.Inactive, outcome.Failure);
        Assert.Null(outcome.Data);
    }

    [Fact(DisplayName = "SpeechClipService.PlaybackAsync/UTC004: a sentence without audio is not heard")]
    [Trait("UTC", "SpeechClipService.PlaybackAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Playback_a_sentence_without_audio_is_not_heard()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: false);

        var outcome = await f.Service.PlaybackAsync(clip.Id, f.Member);

        Assert.Equal(SpeechClipFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.Data);
    }

    [Fact(DisplayName = "SpeechClipService.PlaybackAsync/UTC005: a read failure is read failed with an error log")]
    [Trait("UTC", "SpeechClipService.PlaybackAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task ClipPlayback_a_read_failure_is_read_failed_with_an_error_log()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: true);
        f.Clips.FailFind = new PersistenceUnavailableException();

        var outcome = await f.Service.PlaybackAsync(clip.Id, f.Member);

        Assert.Equal(SpeechClipFailure.ReadFailed, outcome.Failure);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to sign playback for speech clip"));
    }

    [Fact(DisplayName = "SpeechClipService.PlaybackForGuestAsync/UTC001: a guest hears their own sentence")]
    [Trait("UTC", "SpeechClipService.PlaybackForGuestAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task PlaybackForGuest_a_guest_hears_their_own_sentence()
    {
        var f = MakeClipSvc();
        var invitation = Guid.CreateVersion7();
        var guest = GuestSession(f, invitation, admitted: true, notice: true);
        var clip = GuestClip(f, invitation, audio: true, excluded: false);

        var outcome = await f.Service.PlaybackForGuestAsync(guest, clip.Id);

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        Assert.StartsWith("signed/", Assert.IsType<RecordingPlayback>(outcome.Data).Url);
    }

    [Fact(DisplayName = "SpeechClipService.PlaybackForGuestAsync/UTC002: someone else sentence does not exist for a guest")]
    [Trait("UTC", "SpeechClipService.PlaybackForGuestAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task PlaybackForGuest_someone_else_sentence_does_not_exist_for_a_guest()
    {
        var f = MakeClipSvc();
        var guest = GuestSession(f, Guid.CreateVersion7(), admitted: true, notice: true);
        var clip = GuestClip(f, Guid.CreateVersion7(), audio: true, excluded: false);

        var outcome = await f.Service.PlaybackForGuestAsync(guest, clip.Id);

        Assert.Equal(SpeechClipFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.Data);
    }

    [Fact(DisplayName = "SpeechClipService.PlaybackForGuestAsync/UTC003: only officers hear an excluded sentence")]
    [Trait("UTC", "SpeechClipService.PlaybackForGuestAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task PlaybackForGuest_only_officers_hear_an_excluded_sentence()
    {
        var f = MakeClipSvc();
        var invitation = Guid.CreateVersion7();
        var guest = GuestSession(f, invitation, admitted: true, notice: true);
        var clip = GuestClip(f, invitation, audio: true, excluded: true);

        var outcome = await f.Service.PlaybackForGuestAsync(guest, clip.Id);

        Assert.Equal(SpeechClipFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.Data);
    }

    [Fact(DisplayName = "SpeechClipService.PlaybackForGuestAsync/UTC004: a sentence without audio is not heard")]
    [Trait("UTC", "SpeechClipService.PlaybackForGuestAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task PlaybackForGuest_a_sentence_without_audio_is_not_heard()
    {
        var f = MakeClipSvc();
        var invitation = Guid.CreateVersion7();
        var guest = GuestSession(f, invitation, admitted: true, notice: true);
        var clip = GuestClip(f, invitation, audio: false, excluded: false);

        var outcome = await f.Service.PlaybackForGuestAsync(guest, clip.Id);

        Assert.Equal(SpeechClipFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.Data);
    }

    [Fact(DisplayName = "SpeechClipService.PlaybackForGuestAsync/UTC005: a read failure is read failed with an error log")]
    [Trait("UTC", "SpeechClipService.PlaybackForGuestAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task PlaybackForGuest_a_read_failure_is_read_failed_with_an_error_log()
    {
        var f = MakeClipSvc();
        var invitation = Guid.CreateVersion7();
        var guest = GuestSession(f, invitation, admitted: true, notice: true);
        var clip = GuestClip(f, invitation, audio: true, excluded: false);
        f.Clips.FailFind = new PersistenceUnavailableException();

        var outcome = await f.Service.PlaybackForGuestAsync(guest, clip.Id);

        Assert.Equal(SpeechClipFailure.ReadFailed, outcome.Failure);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("for a guest"));
    }

    [Fact(DisplayName = "SpeechClipService.SetExclusionAsync/UTC001: the Chair takes a sentence out of the record")]
    [Trait("UTC", "SpeechClipService.SetExclusionAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task SetExclusion_the_chair_takes_a_sentence_out_of_the_record()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: false);

        var outcome = await f.Service.SetExclusionAsync(clip.Id, f.Chair, true, "Leak");

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        Assert.Equal(true, outcome.Data);
        Assert.NotNull(clip.ExcludedAt);
        Assert.Equal(SpeechClipExclusion.Leak, clip.ExclusionReason);
        Assert.Equal([AuditActions.SpeechClipExcluded], f.Audit.Actions);
        Assert.Equal("ClipEdited", Assert.Single(f.Publisher.Published).Change.ChangeType);
    }

    [Fact(DisplayName = "SpeechClipService.SetExclusionAsync/UTC002: the Secretary puts a sentence back")]
    [Trait("UTC", "SpeechClipService.SetExclusionAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task SetExclusion_the_secretary_puts_a_sentence_back()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: false);
        clip.ExcludedAt = Now;
        clip.ExclusionReason = SpeechClipExclusion.Leak;

        var outcome = await f.Service.SetExclusionAsync(clip.Id, f.Secretary, false, null);

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        Assert.Equal(false, outcome.Data);
        Assert.Null(clip.ExcludedAt);
        Assert.Null(clip.ExclusionReason);
        Assert.Equal([AuditActions.SpeechClipRestored], f.Audit.Actions);
    }

    [Fact(DisplayName = "SpeechClipService.SetExclusionAsync/UTC003: a Member may not take sentences out")]
    [Trait("UTC", "SpeechClipService.SetExclusionAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task SetExclusion_a_member_may_not_take_sentences_out()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: false);

        var outcome = await f.Service.SetExclusionAsync(clip.Id, f.Member, true, "Other");

        Assert.Equal(SpeechClipFailure.Forbidden, outcome.Failure);
        Assert.Null(clip.ExcludedAt);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "SpeechClipService.SetExclusionAsync/UTC004: an unknown reason is invalid")]
    [Trait("UTC", "SpeechClipService.SetExclusionAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task SetExclusion_an_unknown_reason_is_invalid()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: false);

        var outcome = await f.Service.SetExclusionAsync(clip.Id, f.Chair, true, "Noise");

        Assert.Equal(SpeechClipFailure.Invalid, outcome.Failure);
        Assert.Equal(["Lý do phải là Leak hoặc Other."], outcome.Errors!["reason"]);
        Assert.Null(clip.ExcludedAt);
    }

    [Fact(DisplayName = "SpeechClipService.SetExclusionAsync/UTC005: excluding nothing is not found")]
    [Trait("UTC", "SpeechClipService.SetExclusionAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task SetExclusion_excluding_nothing_is_not_found()
    {
        var f = MakeClipSvc();

        var outcome = await f.Service.SetExclusionAsync(Guid.CreateVersion7(), f.Chair, true, "Leak");

        Assert.Equal(SpeechClipFailure.NotFound, outcome.Failure);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "SpeechClipService.SetExclusionAsync/UTC006: a save failure keeps the sentence in the record")]
    [Trait("UTC", "SpeechClipService.SetExclusionAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task SetExclusion_a_save_failure_keeps_the_sentence_in_the_record()
    {
        var f = MakeClipSvc();
        var clip = MemberClip(f, f.Member, audio: false);
        f.Uow.FailNextSave = new PersistenceUnavailableException();

        var outcome = await f.Service.SetExclusionAsync(clip.Id, f.Chair, true, "Other");

        Assert.Equal(SpeechClipFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 1), (f.Uow.Commits, f.Uow.Rollbacks));
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to change the exclusion of speech clip"));
    }
}

/// <summary>Captures log records so a test can assert the Log Message line of a UTC.</summary>
internal sealed class R1Logger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Error)> Entries { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => true;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format)
        => Entries.Add((level, format(state, error), error));
}

/// <summary>FakeRecordingStorage plus the operations the shared fake lacks: joining,
/// cutting, a missing file on open, and a failing seal. Local to Round 1 (FE-09).</summary>
internal sealed class R1Storage(FakeRecordingStorage inner) : IRecordingStorage
{
    public bool ThrowConcat { get; set; }
    public bool ThrowSeal { get; set; }
    public bool MissingOnOpen { get; set; }

    public Task<(string StoragePath, long ByteSize)> SaveAsync(Stream content, string extension, long maxBytes, CancellationToken ct)
        => inner.SaveAsync(content, extension, maxBytes, ct);
    public string Create(string extension) => inner.Create(extension);
    public Task<long> WriteAtAsync(string storagePath, long offset, Stream content, long maxBytes, CancellationToken ct)
        => inner.WriteAtAsync(storagePath, offset, content, maxBytes, ct);
    public void Truncate(string storagePath, long length) => inner.Truncate(storagePath, length);
    public void SealWav(string storagePath)
    {
        if (ThrowSeal) throw new IOException("disk full");
        inner.SealWav(storagePath);
    }
    public Stream OpenRead(string storagePath) => MissingOnOpen
        ? throw new FileNotFoundException("gone", storagePath) : inner.OpenRead(storagePath);
    public void Delete(string storagePath) => inner.Delete(storagePath);
    public IEnumerable<StoredAudioFile> ListWrittenBefore(DateTimeOffset cutoff) => inner.ListWrittenBefore(cutoff);
    public Task<string> ConcatAsync(string first, string second, long skip, CancellationToken ct)
    {
        if (ThrowConcat) throw new IOException("disk full");
        var a = inner.Files[first];
        var b = inner.Files[second];
        var path = inner.Create(".wav");
        var buf = new byte[a.Length + Math.Max(0, b.Length - skip)];
        a.CopyTo(buf, 0);
        b.AsSpan((int)skip).CopyTo(buf.AsSpan(a.Length));
        inner.Files[path] = buf;
        return Task.FromResult(path);
    }
    public Task<string> CutAsync(string source, long from, long length, CancellationToken ct)
    {
        var f = inner.Files[source];
        var path = inner.Create(".wav");
        var buf = new byte[f.Length - length];
        f.AsSpan(0, (int)from).CopyTo(buf);
        f.AsSpan((int)(from + length)).CopyTo(buf.AsSpan((int)from));
        inner.Files[path] = buf;
        return Task.FromResult(path);
    }
}

/// <summary>FakeSpeechClipRepository plus FindPlayback / guest reads and a failing
/// find. Local to Round 1 (FE-09).</summary>
internal sealed class R1Clips(FakeSpeechClipRepository inner) : ISpeechClipRepository
{
    public Exception? FailFind { get; set; }
    public Task<IReadOnlyList<ClipRow>> ListRowsAsync(Guid meetingId, CancellationToken ct) => inner.ListRowsAsync(meetingId, ct);
    public Task<ClipRow?> FindRowByClientIdAsync(Guid meetingId, Guid? speakerId, Guid? guestInvitationId, Guid clientClipId, CancellationToken ct)
        => inner.FindRowByClientIdAsync(meetingId, speakerId, guestInvitationId, clientClipId, ct);
    public Task<ClipRow?> FindLastRowStartedBetweenAsync(Guid meetingId, Guid? speakerId, Guid? guestInvitationId, DateTimeOffset after, DateTimeOffset before, CancellationToken ct)
        => inner.FindLastRowStartedBetweenAsync(meetingId, speakerId, guestInvitationId, after, before, ct);
    public Task<SpeechClip?> FindAsync(Guid clipId, CancellationToken ct) => inner.FindAsync(clipId, ct);
    public Task<bool> IsGuestSessionAdmittedAsync(Guid sessionId, Guid invitationId, Guid meetingId, DateTimeOffset now, CancellationToken ct)
        => inner.IsGuestSessionAdmittedAsync(sessionId, invitationId, meetingId, now, ct);
    public Task<bool> HasGuestConfirmedNoticeAsync(Guid sessionId, CancellationToken ct) => inner.HasGuestConfirmedNoticeAsync(sessionId, ct);
    public Task<IReadOnlyList<string>> ListAudioCreatedBeforeAsync(DateTimeOffset cutoff, CancellationToken ct) => inner.ListAudioCreatedBeforeAsync(cutoff, ct);
    public Task<int> DeleteCreatedBeforeAsync(DateTimeOffset cutoff, CancellationToken ct) => inner.DeleteCreatedBeforeAsync(cutoff, ct);
    public Task<IReadOnlyList<CombinedClip>> ListMixableAsync(Guid meetingId, CancellationToken ct) => inner.ListMixableAsync(meetingId, ct);
    public Task<DateTimeOffset?> FindLastChangeAsync(Guid meetingId, CancellationToken ct) => inner.FindLastChangeAsync(meetingId, ct);
    public void Add(SpeechClip clip) => inner.Add(clip);
    public void Remove(SpeechClip clip) => inner.Remove(clip);
    public Task<IReadOnlyList<ClipRow>> ListRowsChangedSinceAsync(Guid meetingId, DateTimeOffset after, CancellationToken ct) => inner.ListRowsChangedSinceAsync(meetingId, after, ct);
    public Task<IReadOnlyList<ClipRow>> ListRowsStartedBetweenAsync(Guid meetingId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) => inner.ListRowsStartedBetweenAsync(meetingId, from, to, ct);
    public Task<IReadOnlyList<ClipRow>> ListGuestRowsAsync(Guid meetingId, Guid invitationId, DateTimeOffset? since, CancellationToken ct) => inner.ListGuestRowsAsync(meetingId, invitationId, since, ct);
    public Task<SpeechClip?> FindGuestClipAsync(Guid clipId, Guid meetingId, Guid invitationId, CancellationToken ct)
        => FailFind is { } f ? Task.FromException<SpeechClip?>(f)
            : Task.FromResult(inner.Clips.SingleOrDefault(c => c.Id == clipId && c.MeetingId == meetingId && c.GuestInvitationId == invitationId));
    public Task<bool> HasGuestAudioAsync(Guid clipId, Guid meetingId, Guid invitationId, CancellationToken ct)
        => FailFind is { } f ? Task.FromException<bool>(f) : Task.FromResult(
            inner.Clips.SingleOrDefault(c => c.Id == clipId && c.MeetingId == meetingId && c.GuestInvitationId == invitationId) is { AudioPath: not null, ExcludedAt: null });
    public Task<ClipPlayback?> FindPlaybackAsync(Guid clipId, CancellationToken ct)
        => FailFind is { } f ? Task.FromException<ClipPlayback?>(f) : Task.FromResult(
            inner.Clips.SingleOrDefault(c => c.Id == clipId) is { } c ? new ClipPlayback(c.MeetingId, c.AudioPath, c.ExcludedAt) : null);
    public Task<SpeechClipAudio?> FindAudioAsync(Guid clipId, CancellationToken ct) => inner.FindAudioAsync(clipId, ct);
}

/// <summary>FakeMeetingRepository with a failing read switch for the ReadFailed UTCs. Local to Round 1 (FE-09).</summary>
internal sealed class R1Meetings(FakeMeetingRepository inner) : IMeetingRepository
{
    public bool FailReads { get; set; }
    public HashSet<(Guid MeetingId, Guid UserId)> Accessible => inner.Accessible;
    public List<Meeting> Meetings => inner.Meetings;
    public HashSet<(Guid UserId, Capability Capability)> Capabilities => inner.Capabilities;
    public List<Guid> Locked => inner.Locked;
    public List<MeetingStatusHistory> History => inner.History;
    public List<Notification> Notifications => inner.Notifications;
    public List<Meeting> Added => inner.Added;
    public List<Guid> LockedUsers => inner.LockedUsers;
    public HashSet<Guid> ActiveUsers => inner.ActiveUsers;
    public Task<Meeting?> FindAccessibleAsync(Guid id, Guid actorId, CancellationToken ct = default) => inner.FindAccessibleAsync(id, actorId, ct);
    public Task<Meeting?> FindWorkflowAsync(Guid id, bool forUpdate = false, CancellationToken ct = default) => FailReads
        ? Task.FromException<Meeting?>(new PersistenceUnavailableException()) : inner.FindWorkflowAsync(id, forUpdate, ct);
    public Task<Meeting?> LockOwnedAsync(Guid id, Guid creatorId, CancellationToken ct = default) => inner.LockOwnedAsync(id, creatorId, ct);
    public Task<IReadOnlyList<Guid>> ListOverdueAsync(DateTimeOffset cutoff, int take, CancellationToken ct = default) => inner.ListOverdueAsync(cutoff, take, ct);
    public Task<IReadOnlyList<Guid>> ListOverrunAsync(DateTimeOffset cutoff, int take, CancellationToken ct = default) => inner.ListOverrunAsync(cutoff, take, ct);
    public Task<bool> IsActiveUserWithCapabilityAsync(Guid userId, Capability capability, CancellationToken ct = default) => inner.IsActiveUserWithCapabilityAsync(userId, capability, ct);
    public Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct = default) => FailReads
        ? Task.FromException<bool>(new PersistenceUnavailableException()) : inner.IsUserActiveAsync(userId, ct);
    public Task<string?> FindUserDisplayNameAsync(Guid userId, CancellationToken ct = default) => inner.FindUserDisplayNameAsync(userId, ct);
    public Task AddNotificationsAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct = default) => inner.AddNotificationsAsync(notifications, ct);
    public Task AddHistoryAsync(MeetingStatusHistory history, CancellationToken ct = default) => inner.AddHistoryAsync(history, ct);
    public Task<IReadOnlyList<User>> LockParticipantUsersAsync(Guid[] userIds, CancellationToken ct = default) => inner.LockParticipantUsersAsync(userIds, ct);
    public Task AddAsync(Meeting meeting, CancellationToken ct = default) => inner.AddAsync(meeting, ct);
    public Task<MeetingHistoryPage?> FindHistoryPageAsync(Guid meetingId, int page, int pageSize, CancellationToken ct = default) => inner.FindHistoryPageAsync(meetingId, page, pageSize, ct);
    public Task<MeetingListPage> FindAuthorizedPageAsync(Guid actorId, MeetingListQuery query, CancellationToken ct = default) => inner.FindAuthorizedPageAsync(actorId, query, ct);
    public Task<Meeting?> FindOwnedAsync(Guid id, Guid creatorId, CancellationToken ct = default) => inner.FindOwnedAsync(id, creatorId, ct);
    public Task<IReadOnlyList<MeetingApproverOption>> ListEligibleApproversAsync(CancellationToken ct = default) => inner.ListEligibleApproversAsync(ct);
    public Task<bool> IsEligibleApproverAsync(Guid id, CancellationToken ct = default) => inner.IsEligibleApproverAsync(id, ct);
}
