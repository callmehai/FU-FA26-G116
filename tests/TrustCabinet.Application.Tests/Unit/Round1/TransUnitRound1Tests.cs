using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Recordings;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Application.Users;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Application.Transcription;
using TrustCabinet.Application.VoiceId;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Transcription;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Application.Tests.Unit.Round1;

/// <summary>Round 1 (Report 5.1) unit tests for FE-10 transcription, summary and AI, tester Pham Gia Khanh.
/// Each test covers one UTC of cases/FE-10/unit: Trait UTC names Class.Method/UTCnnn,
/// DisplayName repeats it for the trx matcher, and the asserts cover the Expected
/// triple (Return / Exception / Log Message).</summary>
public sealed class TransUnitRound1Tests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    private static (FakeMeetingRepository Meetings, Meeting Meeting, Guid Chair, Guid Secretary, Guid Member) NewMeeting(
        MeetingMode mode = MeetingMode.InPerson, MeetingStatus status = MeetingStatus.Ended,
        Sensitivity sensitivity = Sensitivity.Normal)
    {
        var meetings = new FakeMeetingRepository();
        var chair = Guid.CreateVersion7();
        var secretary = Guid.CreateVersion7();
        var member = Guid.CreateVersion7();
        var meeting = new Meeting
        {
            Title = "Họp giao ban", Mode = mode, Status = status, Sensitivity = sensitivity, CreatedById = chair,
            PlannedStartTime = Now.AddHours(-2), PlannedEndTime = Now.AddHours(-1), ActualStartedAt = Now.AddHours(-2),
        };
        foreach (var (userId, role) in new[] { (chair, MeetingRole.Chair), (secretary, MeetingRole.Secretary), (member, MeetingRole.Member) })
        {
            meeting.Participants.Add(new MeetingParticipant
            {
                MeetingId = meeting.Id, UserId = userId, Role = role, AssignmentStatus = AssignmentStatus.Current,
                Response = InvitationResponse.Accepted,
                User = new User { Id = userId, FullName = $"{role}", Email = $"{userId}@trustcabinet.local", IsActive = true },
            });
            meetings.ActiveUsers.Add(userId);
        }
        meetings.Meetings.Add(meeting);
        return (meetings, meeting, chair, secretary, member);
    }

    private sealed record TransSvc(T1Meetings Meetings, FakeRecordingRepository Recordings,
        FakeTranscriptRepository Transcripts, FakeUnitOfWork Uow, FakeRecordingStorage Files, T1Storage Storage,
        FakeSpeechEngine Engine, FakeVoiceIdRepository Voices, FakeVoiceprintProtector Protector, FakeAuditService Audit,
        FakeSpeechChangePublisher Publisher, T1Logger<TranscriptionService> Log, TranscriptionService Service,
        Meeting Meeting, Guid Chair, Guid Secretary, Guid Member, Recording Recording);

    private static TransSvc TranscriptionSvc(MeetingMode mode = MeetingMode.InPerson, MeetingStatus status = MeetingStatus.InProgress)
    {
        var (inner, meeting, chair, secretary, member) = NewMeeting(mode, status);
        var meetings = new T1Meetings(inner);
        var recordings = new FakeRecordingRepository();
        var transcripts = new FakeTranscriptRepository();
        var uow = new FakeUnitOfWork();
        var files = new FakeRecordingStorage(uow);
        var storage = new T1Storage(files);
        var engine = new FakeSpeechEngine();
        var voices = new FakeVoiceIdRepository();
        var protector = new FakeVoiceprintProtector();
        var audit = new FakeAuditService();
        var publisher = new FakeSpeechChangePublisher(uow);
        var log = new T1Logger<TranscriptionService>();
        var service = new TranscriptionService(meetings, recordings, transcripts, uow, storage, engine, voices,
            protector, Options.Create(new VoiceIdOptions()), audit, publisher, new FixedClock(Now), log);
        var recording = new Recording
        {
            MeetingId = meeting.Id, Source = RecordingSource.Upload, Status = RecordingStatus.Available,
            FileName = "hop.mp3", ContentType = "audio/mpeg", StoragePath = files.Create(".mp3"),
            CreatedById = secretary, CreatedAt = Now.AddHours(-1), UpdatedAt = Now.AddHours(-1),
        };
        recordings.Add(recording);
        return new(meetings, recordings, transcripts, uow, files, storage, engine, voices, protector, audit,
            publisher, log, service, meeting, chair, secretary, member, recording);
    }

    private static Transcript QueuedTranscript(TransSvc f, TranscriptStatus status = TranscriptStatus.Pending)
    {
        var transcript = new Transcript
        {
            RecordingId = f.Recording.Id, Recording = f.Recording, Status = status,
            CreatedAt = Now.AddMinutes(-5), UpdatedAt = Now.AddMinutes(-5),
        };
        f.Transcripts.Transcripts.Add(transcript);
        return transcript;
    }

    private static Transcript SpokenTranscript(TransSvc f)
    {
        var transcript = QueuedTranscript(f, TranscriptStatus.Completed);
        var only = new TranscriptSegment
        {
            TranscriptId = transcript.Id, Position = 1, StartMs = 0, EndMs = 1000, Text = "Xin chào", SpeakerLabel = "SPEAKER_00",
        };
        only.Transcript = transcript;
        transcript.Segments.Add(only);
        transcript.Speakers.Add(new TranscriptSpeaker { TranscriptId = transcript.Id, Label = "SPEAKER_00", SpeechMs = 1000 });
        return transcript;
    }

    [Fact(DisplayName = "TranscriptionService.ProcessNextAsync/UTC001: a finished run names a sure voice at once")]
    [Trait("UTC", "TranscriptionService.ProcessNextAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task ProcessNext_a_finished_run_names_a_sure_voice_at_once()
    {
        var f = TranscriptionSvc();
        var transcript = QueuedTranscript(f);
        var consent = new VoiceConsent
        {
            UserId = f.Member, GrantedAt = Now.AddDays(-1), NoticeVersion = VoiceIdRules.NoticeVersion, Purpose = VoiceIdRules.Purpose,
        };
        f.Voices.Consents.Add(consent);
        var sampleId = Guid.CreateVersion7();
        f.Voices.Samples.Add(new VoiceSample
        {
            Id = sampleId, UserId = f.Member, ConsentId = consent.Id, EmbeddingModel = "test-model",
            Ciphertext = f.Protector.Protect([1f, 0f], f.Member, sampleId, "test-model"),
        });
        f.Engine.Result = new SpeechTranscription(2000, "vi", "test", "test-model",
            [new SpeechSegment(0, 1000, "SPEAKER_00", "Xin chào")], [new SpeechSpeaker("SPEAKER_00", 1000, [1f, 0f])]);

        Assert.True(await f.Service.ProcessNextAsync());

        Assert.Equal(TranscriptStatus.Completed, transcript.Status);
        Assert.Equal("Xin chào", Assert.Single(transcript.Segments).Text);
        Assert.Equal("self-hosted:test", transcript.Provider);
        Assert.Equal("vi", transcript.Language);
        Assert.Equal(100, transcript.ProgressPercent);
        Assert.Equal(f.Member, Assert.Single(transcript.Speakers).AssignedUserId);
        Assert.Equal([AuditActions.TranscriptSpeakerAssigned], f.Audit.Actions);
        Assert.Equal(1, Assert.Single(f.Publisher.Published).CommitsBefore);
    }

    [Fact(DisplayName = "TranscriptionService.ProcessNextAsync/UTC002: an empty queue claims nothing")]
    [Trait("UTC", "TranscriptionService.ProcessNextAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task ProcessNext_an_empty_queue_claims_nothing()
    {
        var f = TranscriptionSvc();

        Assert.False(await f.Service.ProcessNextAsync());

        Assert.Equal([TransactionMode.None], f.Uow.Modes);
        Assert.Empty(f.Publisher.Published);
    }

    [Fact(DisplayName = "TranscriptionService.ProcessNextAsync/UTC003: a recording erased meanwhile is not written")]
    [Trait("UTC", "TranscriptionService.ProcessNextAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task ProcessNext_a_recording_erased_meanwhile_is_not_written()
    {
        var f = TranscriptionSvc();
        var transcript = QueuedTranscript(f);
        f.Recording.Status = RecordingStatus.Deleted;

        Assert.True(await f.Service.ProcessNextAsync());

        Assert.Equal(TranscriptStatus.Processing, transcript.Status);
        Assert.Equal("", transcript.ErrorMessage);
        Assert.Equal(0, f.Uow.Saves);
        Assert.Empty(f.Publisher.Published);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Bản ghi đã bị xoá"));
    }

    [Fact(DisplayName = "TranscriptionService.ProcessNextAsync/UTC004: a missing audio file fails the transcript")]
    [Trait("UTC", "TranscriptionService.ProcessNextAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task ProcessNext_a_missing_audio_file_fails_the_transcript()
    {
        var f = TranscriptionSvc();
        var transcript = QueuedTranscript(f);
        f.Storage.MissingOnOpen = true;

        Assert.True(await f.Service.ProcessNextAsync());

        Assert.Equal(TranscriptStatus.Failed, transcript.Status);
        Assert.Equal("Tệp âm thanh không còn trên máy chủ.", transcript.ErrorMessage);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Tệp âm thanh không còn"));
        Assert.Single(f.Publisher.Published);
    }

    [Fact(DisplayName = "TranscriptionService.EditSegmentAsync/UTC001: the Secretary corrects a line")]
    [Trait("UTC", "TranscriptionService.EditSegmentAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task EditSegment_the_secretary_corrects_a_line()
    {
        var f = TranscriptionSvc();
        var transcript = SpokenTranscript(f);
        transcript.Segments.First().WordStartsMs = [0, 500];
        var segment = transcript.Segments.First();

        var outcome = await f.Service.EditSegmentAsync(segment.Id, f.Secretary, "câu đã sửa");

        Assert.Equal(TranscriptFailure.None, outcome.Failure);
        Assert.Equal("câu đã sửa", segment.Text);
        Assert.Equal(f.Secretary, segment.EditedById);
        Assert.Equal(Now, segment.EditedAt);
        Assert.Null(segment.WordStartsMs);
        Assert.Equal([AuditActions.TranscriptSegmentEdited], f.Audit.Actions);
        Assert.Equal([TransactionMode.None], f.Uow.Modes);
        Assert.Equal(1, f.Uow.Saves);
    }

    [Fact(DisplayName = "TranscriptionService.EditSegmentAsync/UTC002: a blank correction is invalid")]
    [Trait("UTC", "TranscriptionService.EditSegmentAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task EditSegment_a_blank_correction_is_invalid()
    {
        var f = TranscriptionSvc();
        var segment = SpokenTranscript(f).Segments.First();

        var outcome = await f.Service.EditSegmentAsync(segment.Id, f.Secretary, "   ");

        Assert.Equal(TranscriptFailure.Invalid, outcome.Failure);
        Assert.Equal(["Nội dung đoạn phải có từ 1 đến 8000 ký tự."], outcome.Errors!["text"]);
        Assert.Equal("Xin chào", segment.Text);
        Assert.Equal(0, f.Uow.Saves);
    }

    [Fact(DisplayName = "TranscriptionService.EditSegmentAsync/UTC003: a text at the limit is accepted")]
    [Trait("UTC", "TranscriptionService.EditSegmentAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task EditSegment_a_text_at_the_limit_is_accepted()
    {
        var f = TranscriptionSvc();
        var segment = SpokenTranscript(f).Segments.First();

        var outcome = await f.Service.EditSegmentAsync(segment.Id, f.Secretary, new string('x', 8000));

        Assert.Equal(TranscriptFailure.None, outcome.Failure);
        Assert.Equal(8000, segment.Text.Length);
        Assert.Equal([AuditActions.TranscriptSegmentEdited], f.Audit.Actions);
    }

    [Fact(DisplayName = "TranscriptionService.EditSegmentAsync/UTC004: a Member may not correct a line")]
    [Trait("UTC", "TranscriptionService.EditSegmentAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task EditSegment_a_member_may_not_correct_a_line()
    {
        var f = TranscriptionSvc();
        var segment = SpokenTranscript(f).Segments.First();

        var outcome = await f.Service.EditSegmentAsync(segment.Id, f.Member, "câu đã sửa");

        Assert.Equal(TranscriptFailure.Forbidden, outcome.Failure);
        Assert.Equal("Xin chào", segment.Text);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "TranscriptionService.EditSegmentAsync/UTC005: editing nothing is not found")]
    [Trait("UTC", "TranscriptionService.EditSegmentAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task EditSegment_editing_nothing_is_not_found()
    {
        var f = TranscriptionSvc();

        var outcome = await f.Service.EditSegmentAsync(Guid.CreateVersion7(), f.Secretary, "câu đã sửa");

        Assert.Equal(TranscriptFailure.NotFound, outcome.Failure);
        Assert.Equal(0, f.Uow.Saves);
    }

    [Fact(DisplayName = "TranscriptionService.EditSegmentAsync/UTC006: a save failure rolls the edit back")]
    [Trait("UTC", "TranscriptionService.EditSegmentAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task EditSegment_a_save_failure_rolls_the_edit_back()
    {
        var f = TranscriptionSvc();
        var segment = SpokenTranscript(f).Segments.First();
        f.Uow.FailNextSave = new PersistenceUnavailableException();

        var outcome = await f.Service.EditSegmentAsync(segment.Id, f.Secretary, "câu đã sửa");

        Assert.Equal(TranscriptFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 0), (f.Uow.Commits, f.Uow.Rollbacks));
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to edit transcript segment"));
    }

    [Fact(DisplayName = "TranscriptionService.AssignSpeakerAsync/UTC001: naming a voice runs under the recording lock")]
    [Trait("UTC", "TranscriptionService.AssignSpeakerAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task AssignSpeaker_naming_a_voice_runs_under_the_recording_lock()
    {
        var f = TranscriptionSvc();
        var voice = SpokenTranscript(f).Speakers.First();

        var outcome = await f.Service.AssignSpeakerAsync(f.Recording.Id, "SPEAKER_00", f.Member, null, f.Secretary);

        Assert.Equal(TranscriptFailure.None, outcome.Failure);
        Assert.Equal((f.Member, f.Secretary), (voice.AssignedUserId, voice.AssignedById));
        Assert.Equal([f.Recording.Id], f.Recordings.Locked);
        Assert.Equal([TransactionMode.ReadCommitted], f.Uow.Modes);
        Assert.Equal((1, 1), (f.Uow.Commits, f.Uow.Saves));
        Assert.Equal([AuditActions.TranscriptSpeakerAssigned], f.Audit.Actions);
    }

    [Fact(DisplayName = "TranscriptionService.AssignSpeakerAsync/UTC002: an unknown voice is not found")]
    [Trait("UTC", "TranscriptionService.AssignSpeakerAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task AssignSpeaker_an_unknown_voice_is_not_found()
    {
        var f = TranscriptionSvc();
        SpokenTranscript(f);

        var outcome = await f.Service.AssignSpeakerAsync(f.Recording.Id, "SPEAKER_09", f.Member, null, f.Secretary);

        Assert.Equal(TranscriptFailure.NotFound, outcome.Failure);
        Assert.Equal(0, f.Uow.Saves);
    }

    [Fact(DisplayName = "TranscriptionService.AssignSpeakerAsync/UTC003: a voice is named as one roster person")]
    [Trait("UTC", "TranscriptionService.AssignSpeakerAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task AssignSpeaker_a_voice_is_named_as_one_roster_person()
    {
        var f = TranscriptionSvc();
        SpokenTranscript(f);
        var outsider = Guid.CreateVersion7();

        var outcome = await f.Service.AssignSpeakerAsync(f.Recording.Id, "SPEAKER_00", outsider, null, f.Secretary);

        Assert.Equal(TranscriptFailure.InvalidSpeaker, outcome.Failure);
        Assert.Equal(0, f.Uow.Saves);
    }

    [Fact(DisplayName = "TranscriptionService.AssignSpeakerAsync/UTC004: a Member may not name a voice")]
    [Trait("UTC", "TranscriptionService.AssignSpeakerAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task AssignSpeaker_a_member_may_not_name_a_voice()
    {
        var f = TranscriptionSvc();
        SpokenTranscript(f);

        var outcome = await f.Service.AssignSpeakerAsync(f.Recording.Id, "SPEAKER_00", f.Member, null, f.Member);

        Assert.Equal(TranscriptFailure.Forbidden, outcome.Failure);
        Assert.Empty(f.Audit.Actions);
    }

    private static Transcript MovedTranscript(TransSvc f)
    {
        var transcript = QueuedTranscript(f, TranscriptStatus.Completed);
        transcript.Segments.Add(new TranscriptSegment
        {
            TranscriptId = transcript.Id, Position = 1, StartMs = 0, EndMs = 1000, Text = "Xin chào", SpeakerLabel = "SPEAKER_01",
        });
        transcript.Speakers.Add(new TranscriptSpeaker
        {
            TranscriptId = transcript.Id, Label = "SPEAKER_00", SpeechMs = 2000, AssignedUserId = f.Member,
            AssignedById = f.Secretary, AssignedAt = Now,
        });
        transcript.Speakers.Add(new TranscriptSpeaker { TranscriptId = transcript.Id, Label = "SPEAKER_01", SpeechMs = 0 });
        return transcript;
    }

    [Fact(DisplayName = "TranscriptionService.AssignSegmentSpeakerAsync/UTC001: a line moves to the person voice")]
    [Trait("UTC", "TranscriptionService.AssignSegmentSpeakerAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task AssignSegmentSpeaker_a_line_moves_to_the_person_voice()
    {
        var f = TranscriptionSvc();
        var transcript = MovedTranscript(f);
        var segment = transcript.Segments.First();
        var voice00 = transcript.Speakers.First();
        var voice01 = transcript.Speakers.Skip(1).First();

        var outcome = await f.Service.AssignSegmentSpeakerAsync(segment.Id, f.Member, null, null, f.Secretary);

        Assert.Equal(TranscriptFailure.None, outcome.Failure);
        Assert.Equal("SPEAKER_00", segment.SpeakerLabel);
        Assert.Equal(3000, voice00.SpeechMs);
        Assert.Equal(0, voice01.SpeechMs);
        Assert.NotNull(outcome.Data);
        var data = outcome.Data!;
        Assert.Equal("SPEAKER_00", data.GetType().GetProperty("speakerLabel")!.GetValue(data));
        Assert.Equal(false, data.GetType().GetProperty("newVoice")!.GetValue(data));
        Assert.Equal([AuditActions.TranscriptSegmentSpeakerAssigned], f.Audit.Actions);
        Assert.Equal([f.Recording.Id], f.Recordings.Locked);
    }

    [Fact(DisplayName = "TranscriptionService.AssignSegmentSpeakerAsync/UTC002: a line is said by one voice")]
    [Trait("UTC", "TranscriptionService.AssignSegmentSpeakerAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task AssignSegmentSpeaker_a_line_is_said_by_one_voice()
    {
        var f = TranscriptionSvc();
        var segment = MovedTranscript(f).Segments.First();

        var outcome = await f.Service.AssignSegmentSpeakerAsync(segment.Id, f.Member, null, "SPEAKER_00", f.Secretary);

        Assert.Equal(TranscriptFailure.InvalidSpeaker, outcome.Failure);
        Assert.Equal("SPEAKER_01", segment.SpeakerLabel);
    }

    [Fact(DisplayName = "TranscriptionService.AssignSegmentSpeakerAsync/UTC003: another recording label is not a voice")]
    [Trait("UTC", "TranscriptionService.AssignSegmentSpeakerAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task AssignSegmentSpeaker_another_recording_label_is_not_a_voice()
    {
        var f = TranscriptionSvc();
        var segment = MovedTranscript(f).Segments.First();

        var outcome = await f.Service.AssignSegmentSpeakerAsync(segment.Id, null, null, "SPEAKER_09", f.Secretary);

        Assert.Equal(TranscriptFailure.NotFound, outcome.Failure);
        Assert.Equal("SPEAKER_01", segment.SpeakerLabel);
    }

    [Fact(DisplayName = "TranscriptionService.AssignSegmentSpeakerAsync/UTC004: with no free label the move is refused")]
    [Trait("UTC", "TranscriptionService.AssignSegmentSpeakerAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task AssignSegmentSpeaker_with_no_free_label_the_move_is_refused()
    {
        var f = TranscriptionSvc();
        var transcript = MovedTranscript(f);
        transcript.Speakers.Clear();
        for (var i = 0; i < 100; i++)
            transcript.Speakers.Add(new TranscriptSpeaker
            {
                TranscriptId = transcript.Id, Label = $"SPEAKER_{i:00}", SpeechMs = 100, AssignedUserId = Guid.CreateVersion7(),
            });
        var segment = transcript.Segments.First();

        var outcome = await f.Service.AssignSegmentSpeakerAsync(segment.Id, f.Member, null, null, f.Secretary);

        Assert.Equal(TranscriptFailure.InvalidSpeaker, outcome.Failure);
        Assert.Equal("SPEAKER_01", segment.SpeakerLabel);
    }

    [Fact(DisplayName = "TranscriptionService.AssignSegmentSpeakerAsync/UTC005: a Member may not move a line")]
    [Trait("UTC", "TranscriptionService.AssignSegmentSpeakerAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task AssignSegmentSpeaker_a_member_may_not_move_a_line()
    {
        var f = TranscriptionSvc();
        var segment = MovedTranscript(f).Segments.First();

        var outcome = await f.Service.AssignSegmentSpeakerAsync(segment.Id, f.Member, null, null, f.Member);

        Assert.Equal(TranscriptFailure.Forbidden, outcome.Failure);
        Assert.Equal("SPEAKER_01", segment.SpeakerLabel);
    }

    [Fact(DisplayName = "TranscriptionService.ExportAsync/UTC001: exports the finished transcript as a text file")]
    [Trait("UTC", "TranscriptionService.ExportAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Export_exports_the_finished_transcript_as_a_text_file()
    {
        var f = TranscriptionSvc();
        var transcript = SpokenTranscript(f);
        transcript.Segments.Add(new TranscriptSegment
        {
            TranscriptId = transcript.Id, Position = 2, StartMs = 1000, EndMs = 2000, Text = "Vâng", SpeakerLabel = "SPEAKER_00",
        });
        transcript.Speakers.First().AssignedUserId = f.Member;

        var outcome = await f.Service.ExportAsync(f.Recording.Id, f.Chair);

        Assert.Equal(TranscriptFailure.None, outcome.Failure);
        var file = Assert.IsType<ExportedText>(outcome.Data);
        Assert.StartsWith("ban-go-bang-", file.FileName);
        Assert.EndsWith(".txt", file.FileName);
        Assert.Contains("Xin chào", file.Text);
        Assert.Contains("Vâng", file.Text);
        Assert.Equal([AuditActions.TranscriptExported], f.Audit.Actions);
    }

    [Fact(DisplayName = "TranscriptionService.ExportAsync/UTC002: a Member may not export")]
    [Trait("UTC", "TranscriptionService.ExportAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Export_a_member_may_not_export()
    {
        var f = TranscriptionSvc();
        SpokenTranscript(f);

        var outcome = await f.Service.ExportAsync(f.Recording.Id, f.Member);

        Assert.Equal(TranscriptFailure.ExportForbidden, outcome.Failure);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "TranscriptionService.ExportAsync/UTC003: a Secret transcript never downloads")]
    [Trait("UTC", "TranscriptionService.ExportAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Export_a_secret_transcript_never_downloads()
    {
        var f = TranscriptionSvc(MeetingMode.InPerson, MeetingStatus.InProgress);
        f.Meeting.Sensitivity = Sensitivity.Secret;
        SpokenTranscript(f);

        var outcome = await f.Service.ExportAsync(f.Recording.Id, f.Chair);

        Assert.Equal(TranscriptFailure.SecretMeeting, outcome.Failure);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "TranscriptionService.ExportAsync/UTC004: an unfinished transcript is not ready")]
    [Trait("UTC", "TranscriptionService.ExportAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Export_an_unfinished_transcript_is_not_ready()
    {
        var f = TranscriptionSvc();
        QueuedTranscript(f, TranscriptStatus.Processing);

        var outcome = await f.Service.ExportAsync(f.Recording.Id, f.Chair);

        Assert.Equal(TranscriptFailure.NotReady, outcome.Failure);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "TranscriptionService.ExportAsync/UTC005: an empty transcript exports a header only")]
    [Trait("UTC", "TranscriptionService.ExportAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Export_an_empty_transcript_exports_a_header_only()
    {
        var f = TranscriptionSvc();
        QueuedTranscript(f, TranscriptStatus.Completed);

        var outcome = await f.Service.ExportAsync(f.Recording.Id, f.Chair);

        Assert.Equal(TranscriptFailure.None, outcome.Failure);
        Assert.Contains("Họp giao ban", Assert.IsType<ExportedText>(outcome.Data).Text);
        Assert.Equal([AuditActions.TranscriptExported], f.Audit.Actions);
    }

    [Fact(DisplayName = "TranscriptionService.ExportAsync/UTC006: a read failure is read failed with an error log")]
    [Trait("UTC", "TranscriptionService.ExportAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Export_a_read_failure_is_read_failed_with_an_error_log()
    {
        var f = TranscriptionSvc();
        SpokenTranscript(f);
        f.Meetings.FailReads = true;

        var outcome = await f.Service.ExportAsync(f.Recording.Id, f.Chair);

        Assert.Equal(TranscriptFailure.ReadFailed, outcome.Failure);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to export the transcript"));
    }

/// <summary>Captures log records so a test can assert the Log Message line of a UTC.</summary>
internal sealed class T1Logger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Error)> Entries { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => true;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format)
        => Entries.Add((level, format(state, error), error));
}

/// <summary>FakeRecordingStorage whose open can miss, for the missing-audio UTC. Local to Round 1 (FE-10).</summary>
internal sealed class T1Storage(FakeRecordingStorage inner) : IRecordingStorage
{
    public bool MissingOnOpen { get; set; }
    public Task<(string StoragePath, long ByteSize)> SaveAsync(Stream content, string extension, long maxBytes, CancellationToken ct)
        => inner.SaveAsync(content, extension, maxBytes, ct);
    public string Create(string extension) => inner.Create(extension);
    public Task<long> WriteAtAsync(string storagePath, long offset, Stream content, long maxBytes, CancellationToken ct)
        => inner.WriteAtAsync(storagePath, offset, content, maxBytes, ct);
    public void Truncate(string storagePath, long length) => inner.Truncate(storagePath, length);
    public void SealWav(string storagePath) => inner.SealWav(storagePath);
    public Stream OpenRead(string storagePath) => MissingOnOpen
        ? throw new FileNotFoundException("gone", storagePath) : inner.OpenRead(storagePath);
    public void Delete(string storagePath) => inner.Delete(storagePath);
    public IEnumerable<StoredAudioFile> ListWrittenBefore(DateTimeOffset cutoff) => inner.ListWrittenBefore(cutoff);
    public Task<string> ConcatAsync(string first, string second, long skip, CancellationToken ct) => inner.ConcatAsync(first, second, skip, ct);
    public Task<string> CutAsync(string source, long from, long length, CancellationToken ct) => inner.CutAsync(source, from, length, ct);
}

/// <summary>FakeMeetingRepository with a failing read switch for the ReadFailed UTCs. Local to Round 1 (FE-10).</summary>
internal sealed class T1Meetings(FakeMeetingRepository inner) : IMeetingRepository
{
    public bool FailReads { get; set; }
    public List<Guid> Locked => inner.Locked;
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

    private sealed record SumSvc(FakeMeetingSummaryRepository Summaries, FakeSummarySourceQuery Sources, FakeUnitOfWork Uow,
        FakeMeetingRepository Meetings, FakeLanguageModelProvider Models, FakeLanguageModel Local, FakeMeetingTranscriptQuery Transcript,
        T1Logger<SummaryService> Log, SummaryService Service, Meeting Meeting, Guid Chair, Guid Secretary, Guid Member);

    private static SumSvc SummarySvc(MeetingStatus status = MeetingStatus.Ended)
    {
        var (meetings, meeting, chair, secretary, member) = NewMeeting(MeetingMode.InPerson, status);
        var summaries = new FakeMeetingSummaryRepository();
        var sources = new FakeSummarySourceQuery();
        var uow = new FakeUnitOfWork();
        var local = new FakeLanguageModel("qwen-local");
        var models = new FakeLanguageModelProvider(local);
        var transcript = new FakeMeetingTranscriptQuery();
        var log = new T1Logger<SummaryService>();
        var service = new SummaryService(summaries, sources, uow, meetings, models, new MeetingTranscriptReader(transcript),
            new FixedClock(Now), log, Options.Create(new LanguageModelOptions { ApiParallelism = 1 }));
        return new(summaries, sources, uow, meetings, models, local, transcript, log, service, meeting, chair, secretary, member);
    }

    private static MeetingSummary ReadyDraft(SumSvc f, SummaryStatus status = SummaryStatus.Ready, bool reviewed = true)
    {
        var draft = new SummaryDraft("Mục tiêu.", "Thảo luận.", "Kết luận.", []);
        var row = new MeetingSummary
        {
            MeetingId = f.Meeting.Id, Status = status,
            Content = JsonSerializer.Serialize(draft, SummaryService.Json),
            RequestedById = f.Secretary, RequestedAt = Now, Model = "qwen-local", GeneratedAt = Now,
            ReviewedById = reviewed ? f.Secretary : null, ReviewedAt = reviewed ? Now : null,
        };
        f.Summaries.Rows.Add(row);
        return row;
    }

    private static SummaryEdit ValidEdit() => new("Mục tiêu mới.", "Thảo luận mới.", "Kết luận mới.");

    [Fact(DisplayName = "SummaryService.RequestAsync/UTC001: the Chair queues a draft and reads it back")]
    [Trait("UTC", "SummaryService.RequestAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Request_the_chair_queues_a_draft_and_reads_it_back()
    {
        var f = SummarySvc();

        var outcome = await f.Service.RequestAsync(f.Meeting.Id, f.Chair);

        Assert.Equal(SummaryFailure.None, outcome.Failure);
        var row = Assert.Single(f.Summaries.Rows);
        Assert.Equal(SummaryStatus.Queued, row.Status);
        Assert.Equal(f.Chair, row.RequestedById);
        Assert.Equal(Now, row.RequestedAt);
        Assert.Equal("Queued", Assert.IsType<SummaryState>(outcome.Data).Summary!.Status);
        Assert.Equal(1, f.Uow.Saves);
    }

    [Fact(DisplayName = "SummaryService.RequestAsync/UTC002: a Member may not ask for a draft")]
    [Trait("UTC", "SummaryService.RequestAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Request_a_member_may_not_ask_for_a_draft()
    {
        var f = SummarySvc();

        var outcome = await f.Service.RequestAsync(f.Meeting.Id, f.Member);

        Assert.Equal(SummaryFailure.Forbidden, outcome.Failure);
        Assert.Empty(f.Summaries.Rows);
    }

    [Fact(DisplayName = "SummaryService.RequestAsync/UTC003: a Draft meeting has no draft yet")]
    [Trait("UTC", "SummaryService.RequestAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Request_a_draft_meeting_has_no_draft_yet()
    {
        var f = SummarySvc(MeetingStatus.Draft);

        var outcome = await f.Service.RequestAsync(f.Meeting.Id, f.Chair);

        Assert.Equal(SummaryFailure.WrongMeetingStatus, outcome.Failure);
        Assert.Empty(f.Summaries.Rows);
    }

    [Fact(DisplayName = "SummaryService.RequestAsync/UTC004: a waiting draft is not queued twice")]
    [Trait("UTC", "SummaryService.RequestAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Request_a_waiting_draft_is_not_queued_twice()
    {
        var f = SummarySvc();
        f.Summaries.Rows.Add(new MeetingSummary { MeetingId = f.Meeting.Id, Status = SummaryStatus.Queued, RequestedById = f.Chair, RequestedAt = Now });

        var outcome = await f.Service.RequestAsync(f.Meeting.Id, f.Chair);

        Assert.Equal(SummaryFailure.Busy, outcome.Failure);
        Assert.Single(f.Summaries.Rows);
    }

    [Fact(DisplayName = "SummaryService.RequestAsync/UTC005: an unconfigured AI answers unavailable")]
    [Trait("UTC", "SummaryService.RequestAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Request_an_unconfigured_ai_answers_unavailable()
    {
        var f = SummarySvc();
        f.Local.Configured = false;

        var outcome = await f.Service.RequestAsync(f.Meeting.Id, f.Chair);

        Assert.Equal(SummaryFailure.AiUnavailable, outcome.Failure);
        Assert.Empty(f.Summaries.Rows);
    }

    [Fact(DisplayName = "SummaryService.RequestAsync/UTC006: a save failure is save failed with an error log")]
    [Trait("UTC", "SummaryService.RequestAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Request_a_save_failure_is_save_failed_with_an_error_log()
    {
        var f = SummarySvc();
        f.Uow.FailNextSave = new PersistenceUnavailableException();

        var outcome = await f.Service.RequestAsync(f.Meeting.Id, f.Chair);

        Assert.Equal(SummaryFailure.SaveFailed, outcome.Failure);
        Assert.Equal(0, f.Uow.Commits);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Could not queue the summary"));
    }

    [Fact(DisplayName = "SummaryService.RequestAsync/UTC007: a meeting with no sentences has nothing to summarise")]
    [Trait("UTC", "SummaryService.RequestAsync/UTC007")]
    [Trait("Trait", "UTC")]
    public async Task Request_a_meeting_with_no_sentences_has_nothing_to_summarise()
    {
        var f = SummarySvc();
        f.Sources.Sentences = 0;

        var outcome = await f.Service.RequestAsync(f.Meeting.Id, f.Chair);

        Assert.Equal(SummaryFailure.NothingSaid, outcome.Failure);
        Assert.Empty(f.Summaries.Rows);
    }

    [Fact(DisplayName = "SummaryService.EditAsync/UTC001: the Chair corrects the draft and clears the review")]
    [Trait("UTC", "SummaryService.EditAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Edit_the_chair_corrects_the_draft_and_clears_the_review()
    {
        var f = SummarySvc();
        var row = ReadyDraft(f);

        var outcome = await f.Service.EditAsync(f.Meeting.Id, f.Chair, ValidEdit());

        Assert.Equal(SummaryFailure.None, outcome.Failure);
        Assert.Contains("Mục tiêu mới.", row.Content);
        Assert.Equal(f.Chair, row.EditedById);
        Assert.Equal(Now, row.EditedAt);
        Assert.Null(row.ReviewedById);
        Assert.Null(row.ReviewedAt);
        Assert.Equal("Mục tiêu mới.", Assert.IsType<SummaryState>(outcome.Data).Summary!.Draft!.Objectives);
    }

    [Fact(DisplayName = "SummaryService.EditAsync/UTC002: editing without a draft is not drafted")]
    [Trait("UTC", "SummaryService.EditAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Edit_editing_without_a_draft_is_not_drafted()
    {
        var f = SummarySvc();

        var outcome = await f.Service.EditAsync(f.Meeting.Id, f.Chair, ValidEdit());

        Assert.Equal(SummaryFailure.NotDrafted, outcome.Failure);
        Assert.Equal(0, f.Uow.Saves);
    }

    [Fact(DisplayName = "SummaryService.EditAsync/UTC003: editing while the AI writes is busy")]
    [Trait("UTC", "SummaryService.EditAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Edit_editing_while_the_ai_writes_is_busy()
    {
        var f = SummarySvc();
        ReadyDraft(f, SummaryStatus.Running, reviewed: false);

        var outcome = await f.Service.EditAsync(f.Meeting.Id, f.Chair, ValidEdit());

        Assert.Equal(SummaryFailure.Busy, outcome.Failure);
    }

    [Fact(DisplayName = "SummaryService.EditAsync/UTC004: a blank section is invalid")]
    [Trait("UTC", "SummaryService.EditAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Edit_a_blank_section_is_invalid()
    {
        var f = SummarySvc();
        var row = ReadyDraft(f);

        var outcome = await f.Service.EditAsync(f.Meeting.Id, f.Chair, new SummaryEdit("   ", "Thảo luận.", "Kết luận."));

        Assert.Equal(SummaryFailure.Invalid, outcome.Failure);
        Assert.True(outcome.Errors!.ContainsKey("objectives"));
        Assert.Contains("Mục tiêu.", row.Content);
    }

    [Fact(DisplayName = "SummaryService.EditAsync/UTC005: sections at the limits are accepted")]
    [Trait("UTC", "SummaryService.EditAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Edit_sections_at_the_limits_are_accepted()
    {
        var f = SummarySvc();
        var row = ReadyDraft(f);

        var outcome = await f.Service.EditAsync(f.Meeting.Id, f.Chair,
            new SummaryEdit(new string('x', 2000), new string('y', 12000), new string('z', 8000)));

        Assert.Equal(SummaryFailure.None, outcome.Failure);
        Assert.Equal(2000, Assert.IsType<SummaryState>(outcome.Data).Summary!.Draft!.Objectives.Length);
        Assert.Equal(Now, row.EditedAt);
    }

    [Fact(DisplayName = "SummaryService.EditAsync/UTC006: a Member may not correct the draft")]
    [Trait("UTC", "SummaryService.EditAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Edit_a_member_may_not_correct_the_draft()
    {
        var f = SummarySvc();
        ReadyDraft(f);

        var outcome = await f.Service.EditAsync(f.Meeting.Id, f.Member, ValidEdit());

        Assert.Equal(SummaryFailure.Forbidden, outcome.Failure);
        Assert.Equal(0, f.Uow.Saves);
    }

    [Fact(DisplayName = "SummaryService.ReviewAsync/UTC001: the Secretary reviews the draft for the readers")]
    [Trait("UTC", "SummaryService.ReviewAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Review_the_secretary_reviews_the_draft_for_the_readers()
    {
        var f = SummarySvc();
        var row = ReadyDraft(f);

        var outcome = await f.Service.ReviewAsync(f.Meeting.Id, f.Secretary);

        Assert.Equal(SummaryFailure.None, outcome.Failure);
        Assert.Equal(f.Secretary, row.ReviewedById);
        Assert.Equal(Now, row.ReviewedAt);
        Assert.NotNull(Assert.IsType<SummaryState>(outcome.Data).Summary);
    }

    [Fact(DisplayName = "SummaryService.ReviewAsync/UTC002: reviewing without a draft is not drafted")]
    [Trait("UTC", "SummaryService.ReviewAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Review_reviewing_without_a_draft_is_not_drafted()
    {
        var f = SummarySvc();

        var outcome = await f.Service.ReviewAsync(f.Meeting.Id, f.Secretary);

        Assert.Equal(SummaryFailure.NotDrafted, outcome.Failure);
    }

    [Fact(DisplayName = "SummaryService.ReviewAsync/UTC003: reviewing while the AI writes is busy")]
    [Trait("UTC", "SummaryService.ReviewAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Review_reviewing_while_the_ai_writes_is_busy()
    {
        var f = SummarySvc();
        ReadyDraft(f, SummaryStatus.Running, reviewed: false);

        var outcome = await f.Service.ReviewAsync(f.Meeting.Id, f.Secretary);

        Assert.Equal(SummaryFailure.Busy, outcome.Failure);
        Assert.Null(f.Summaries.Rows[0].ReviewedById);
    }

    [Fact(DisplayName = "SummaryService.ReviewAsync/UTC004: a Member may not review the draft")]
    [Trait("UTC", "SummaryService.ReviewAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Review_a_member_may_not_review_the_draft()
    {
        var f = SummarySvc();
        ReadyDraft(f, SummaryStatus.Ready, reviewed: false);

        var outcome = await f.Service.ReviewAsync(f.Meeting.Id, f.Member);

        Assert.Equal(SummaryFailure.Forbidden, outcome.Failure);
        Assert.Null(f.Summaries.Rows[0].ReviewedById);
    }

    [Fact(DisplayName = "SummaryService.ProcessNextAsync/UTC001: the worker writes the draft with the local model")]
    [Trait("UTC", "SummaryService.ProcessNextAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task ProcessNext_the_worker_writes_the_draft_with_the_local_model()
    {
        var f = SummarySvc();
        f.Summaries.Rows.Add(new MeetingSummary
        {
            MeetingId = f.Meeting.Id, Status = SummaryStatus.Queued, RequestedById = f.Chair, RequestedAt = Now,
        });

        Assert.True(await f.Service.ProcessNextAsync());

        var row = Assert.Single(f.Summaries.Rows);
        Assert.Equal(SummaryStatus.Ready, row.Status);
        Assert.Equal("qwen-local", row.Model);
        Assert.Contains("Rà soát", row.Content);
        Assert.Equal((0, 0), (f.Uow.Commits, f.Uow.Saves));
    }

    [Fact(DisplayName = "SummaryService.ProcessNextAsync/UTC002: an empty queue runs nothing")]
    [Trait("UTC", "SummaryService.ProcessNextAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task ProcessNext_an_empty_queue_runs_nothing()
    {
        var f = SummarySvc();

        Assert.False(await f.Service.ProcessNextAsync());

        Assert.Empty(f.Summaries.Rows);
    }

    [Fact(DisplayName = "SummaryService.ProcessNextAsync/UTC004: a refusing model fails the draft with its message")]
    [Trait("UTC", "SummaryService.ProcessNextAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task ProcessNext_a_refusing_model_fails_the_draft_with_its_message()
    {
        var f = SummarySvc();
        f.Summaries.Rows.Add(new MeetingSummary
        {
            MeetingId = f.Meeting.Id, Status = SummaryStatus.Queued, RequestedById = f.Chair, RequestedAt = Now,
        });
        f.Local.Fail = new LanguageModelException("Máy AI không trả lời.");

        Assert.True(await f.Service.ProcessNextAsync());

        var row = Assert.Single(f.Summaries.Rows);
        Assert.Equal(SummaryStatus.Failed, row.Status);
        Assert.Equal("Máy AI không trả lời.", row.ErrorMessage);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("could not draft"));
    }

    [Fact(DisplayName = "SummaryService.ProcessNextAsync/UTC005: a throwing model fails the draft as unwritten")]
    [Trait("UTC", "SummaryService.ProcessNextAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task ProcessNext_a_throwing_model_fails_the_draft_as_unwritten()
    {
        var f = SummarySvc();
        f.Summaries.Rows.Add(new MeetingSummary
        {
            MeetingId = f.Meeting.Id, Status = SummaryStatus.Queued, RequestedById = f.Chair, RequestedAt = Now,
        });
        f.Local.Fail = new InvalidOperationException("boom");

        Assert.True(await f.Service.ProcessNextAsync());

        var row = Assert.Single(f.Summaries.Rows);
        Assert.Equal(SummaryStatus.Failed, row.Status);
        Assert.Equal("Không viết được dự thảo. Vui lòng thử lại.", row.ErrorMessage);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error);
    }

    private sealed record VoiceSvc(FakeVoiceIdRepository Repository, FakeUnitOfWork Uow, FakeVoiceEmbedder Embedder,
        FakeVoiceprintProtector Protector, FakeAuditService Audit, T1Logger<VoiceIdService> Log, VoiceIdService Service, Guid User);

    private static VoiceSvc MakeVoiceSvc()
    {
        var repository = new FakeVoiceIdRepository();
        var uow = new FakeUnitOfWork();
        var embedder = new FakeVoiceEmbedder();
        var protector = new FakeVoiceprintProtector();
        var audit = new FakeAuditService();
        var clock = new FixedClock(Now);
        var log = new T1Logger<VoiceIdService>();
        var service = new VoiceIdService(repository, uow, new VoiceIdErasure(repository, uow, audit, clock), embedder,
            protector, audit, clock, Options.Create(new VoiceIdOptions { Controller = "Văn phòng" }), log);
        return new(repository, uow, embedder, protector, audit, log, service, Guid.CreateVersion7());
    }

    private static VoiceConsent Consented(VoiceSvc f, string notice = VoiceIdRules.NoticeVersion)
    {
        var consent = new VoiceConsent { UserId = f.User, GrantedAt = Now.AddDays(-1), NoticeVersion = notice, Purpose = VoiceIdRules.Purpose };
        f.Repository.Consents.Add(consent);
        return consent;
    }

    private static VoiceSample OwnSample(VoiceSvc f, VoiceConsent consent)
    {
        var sample = new VoiceSample
        {
            UserId = f.User, ConsentId = consent.Id, EmbeddingModel = "test-model", Ciphertext = [0xE0],
            CreatedAt = Now, UpdatedAt = Now,
        };
        f.Repository.Samples.Add(sample);
        return sample;
    }

    [Fact(DisplayName = "VoiceIdService.GrantAsync/UTC001: agreeing records the consent and its audit")]
    [Trait("UTC", "VoiceIdService.GrantAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Grant_agreeing_records_the_consent_and_its_audit()
    {
        var f = MakeVoiceSvc();

        var outcome = await f.Service.GrantAsync(f.User, "1.4");

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        Assert.Equal("1.4", Assert.Single(f.Repository.Consents).NoticeVersion);
        Assert.Equal([AuditActions.VoiceConsentGranted], f.Audit.Actions);
        Assert.Equal((1, 1), (f.Uow.Commits, f.Uow.Saves));
        Assert.NotNull(Assert.IsType<VoiceProfile>(outcome.Data).Consent);
    }

    [Fact(DisplayName = "VoiceIdService.GrantAsync/UTC002: renewing keeps the samples")]
    [Trait("UTC", "VoiceIdService.GrantAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Grant_renewing_keeps_the_samples()
    {
        var f = MakeVoiceSvc();
        var consent = Consented(f, "1.2");
        OwnSample(f, consent);

        var outcome = await f.Service.GrantAsync(f.User, "1.4");

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        Assert.Equal("1.4", Assert.Single(f.Repository.Consents).NoticeVersion);
        Assert.Single(f.Repository.Samples);
        Assert.Equal([AuditActions.VoiceConsentRenewed], f.Audit.Actions);
    }

    [Fact(DisplayName = "VoiceIdService.GrantAsync/UTC003: an old notice version is invalid")]
    [Trait("UTC", "VoiceIdService.GrantAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Grant_an_old_notice_version_is_invalid()
    {
        var f = MakeVoiceSvc();

        var outcome = await f.Service.GrantAsync(f.User, "1.2");

        Assert.Equal(VoiceFailure.Invalid, outcome.Failure);
        Assert.Equal(["Nội dung thông báo đã thay đổi. Hãy tải lại trang và đọc lại trước khi đồng ý."], outcome.Errors!["noticeVersion"]);
        Assert.Empty(f.Repository.Consents);
    }

    [Fact(DisplayName = "VoiceIdService.GrantAsync/UTC004: a missing notice version is refused")]
    [Trait("UTC", "VoiceIdService.GrantAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Grant_a_missing_notice_version_is_refused()
    {
        var f = MakeVoiceSvc();

        var outcome = await f.Service.GrantAsync(f.User, "");

        Assert.Equal(VoiceFailure.Invalid, outcome.Failure);
        Assert.True(outcome.Errors!.ContainsKey("noticeVersion"));
        Assert.Empty(f.Repository.Consents);
    }

    [Fact(DisplayName = "VoiceIdService.GrantAsync/UTC005: two agreements at once answer the loser as agreed")]
    [Trait("UTC", "VoiceIdService.GrantAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Grant_two_agreements_at_once_answer_the_loser_as_agreed()
    {
        var f = MakeVoiceSvc();
        f.Uow.FailNextSave = new UniqueConflictException(UniqueConflict.Unknown);

        var outcome = await f.Service.GrantAsync(f.User, "1.4");

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        Assert.IsType<VoiceProfile>(outcome.Data);
        Assert.Equal(1, f.Uow.Rollbacks);
    }

    [Fact(DisplayName = "VoiceIdService.AddSampleAsync/UTC001: a sample is kept encrypted under the consent lock")]
    [Trait("UTC", "VoiceIdService.AddSampleAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task AddSample_a_sample_is_kept_encrypted_under_the_consent_lock()
    {
        var f = MakeVoiceSvc();
        Consented(f);

        var outcome = await f.Service.AddSampleAsync(f.User, new MemoryStream([1, 2, 3]), "audio/webm");

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        var sample = Assert.Single(f.Repository.Samples);
        Assert.Equal([0xE0], sample.Ciphertext);
        Assert.Equal([1, 2, 3, 0xA0], sample.AudioCiphertext);
        Assert.Equal([AuditActions.VoiceSampleAdded], f.Audit.Actions);
        Assert.Equal((1, 1), (f.Uow.Commits, f.Uow.Saves));
    }

    [Fact(DisplayName = "VoiceIdService.AddSampleAsync/UTC002: a sample needs consent before the worker hears it")]
    [Trait("UTC", "VoiceIdService.AddSampleAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task AddSample_a_sample_needs_consent_before_the_worker_hears_it()
    {
        var f = MakeVoiceSvc();

        var outcome = await f.Service.AddSampleAsync(f.User, new MemoryStream([1, 2, 3]), "audio/webm");

        Assert.Equal(VoiceFailure.NoConsent, outcome.Failure);
        Assert.Equal(0, f.Embedder.Calls);
        Assert.Empty(f.Repository.Samples);
    }

    [Fact(DisplayName = "VoiceIdService.AddSampleAsync/UTC003: the sixth own sample is refused")]
    [Trait("UTC", "VoiceIdService.AddSampleAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task AddSample_the_sixth_own_sample_is_refused()
    {
        var f = MakeVoiceSvc();
        var consent = Consented(f);
        for (var i = 0; i < 5; i++) OwnSample(f, consent);

        var outcome = await f.Service.AddSampleAsync(f.User, new MemoryStream([1, 2, 3]), "audio/webm");

        Assert.Equal(VoiceFailure.LimitReached, outcome.Failure);
        Assert.Equal(0, f.Embedder.Calls);
        Assert.Equal(5, f.Repository.Samples.Count);
    }

    [Fact(DisplayName = "VoiceIdService.AddSampleAsync/UTC004: a too-short sample is rejected with guidance")]
    [Trait("UTC", "VoiceIdService.AddSampleAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task AddSample_a_too_short_sample_is_rejected_with_guidance()
    {
        var f = MakeVoiceSvc();
        Consented(f);
        f.Embedder.Fail = new VoiceSampleRejectedException("too_short");

        var outcome = await f.Service.AddSampleAsync(f.User, new MemoryStream([1, 2, 3]), "audio/webm");

        Assert.Equal(VoiceFailure.Rejected, outcome.Failure);
        Assert.Equal(["Giọng mẫu quá ngắn — cần ít nhất 6 giây có tiếng nói. Hãy đọc hết đoạn văn mẫu."], outcome.Errors!["file"]);
        Assert.Empty(f.Repository.Samples);
    }

    [Fact(DisplayName = "VoiceIdService.AddSampleAsync/UTC005: a down worker is engine unavailable with a warning")]
    [Trait("UTC", "VoiceIdService.AddSampleAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task AddSample_a_down_worker_is_engine_unavailable_with_a_warning()
    {
        var f = MakeVoiceSvc();
        Consented(f);
        f.Embedder.Fail = new SpeechEngineException("down");

        var outcome = await f.Service.AddSampleAsync(f.User, new MemoryStream([1, 2, 3]), "audio/webm");

        Assert.Equal(VoiceFailure.EngineUnavailable, outcome.Failure);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Warning);
        Assert.Empty(f.Repository.Samples);
    }

    [Fact(DisplayName = "VoiceIdService.AddSampleAsync/UTC006: the fifth sample is stored, a sixth would be refused")]
    [Trait("UTC", "VoiceIdService.AddSampleAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task AddSample_the_fifth_sample_is_stored_a_sixth_would_be_refused()
    {
        var f = MakeVoiceSvc();
        var consent = Consented(f);
        for (var i = 0; i < 4; i++) OwnSample(f, consent);

        var outcome = await f.Service.AddSampleAsync(f.User, new MemoryStream([1, 2, 3]), "audio/webm");

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        Assert.Equal(5, f.Repository.Samples.Count);
        Assert.Equal([AuditActions.VoiceSampleAdded], f.Audit.Actions);
    }

    [Fact(DisplayName = "VoiceIdService.DeleteSampleAsync/UTC001: deleting removes the sample and audits it")]
    [Trait("UTC", "VoiceIdService.DeleteSampleAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task DeleteSample_deleting_removes_the_sample_and_audits_it()
    {
        var f = MakeVoiceSvc();
        var sample = OwnSample(f, Consented(f));

        var outcome = await f.Service.DeleteSampleAsync(f.User, sample.Id);

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        Assert.Empty(f.Repository.Samples);
        Assert.Equal([AuditActions.VoiceSampleDeleted], f.Audit.Actions);
    }

    [Fact(DisplayName = "VoiceIdService.DeleteSampleAsync/UTC002: a secretary-taken sample is the person own too")]
    [Trait("UTC", "VoiceIdService.DeleteSampleAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task DeleteSample_a_secretary_taken_sample_is_the_person_own_too()
    {
        var f = MakeVoiceSvc();
        var consent = Consented(f);
        var sample = new VoiceSample
        {
            UserId = f.User, ConsentId = consent.Id, EmbeddingModel = "test-model", Ciphertext = [0xE0],
            CollectedById = Guid.CreateVersion7(), MeetingId = Guid.CreateVersion7(), CreatedAt = Now, UpdatedAt = Now,
        };
        f.Repository.Samples.Add(sample);

        var outcome = await f.Service.DeleteSampleAsync(f.User, sample.Id);

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        Assert.Empty(f.Repository.Samples);
        Assert.Equal([AuditActions.VoiceSampleDeleted], f.Audit.Actions);
    }

    [Fact(DisplayName = "VoiceIdService.DeleteSampleAsync/UTC003: someone else sample reads as not found")]
    [Trait("UTC", "VoiceIdService.DeleteSampleAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task DeleteSample_someone_else_sample_reads_as_not_found()
    {
        var f = MakeVoiceSvc();
        var other = Guid.CreateVersion7();
        f.Repository.Samples.Add(new VoiceSample
        {
            UserId = other, ConsentId = Guid.CreateVersion7(), EmbeddingModel = "test-model", Ciphertext = [0xE0],
            CreatedAt = Now, UpdatedAt = Now,
        });

        var outcome = await f.Service.DeleteSampleAsync(f.User, f.Repository.Samples[0].Id);

        Assert.Equal(VoiceFailure.NotFound, outcome.Failure);
        Assert.Single(f.Repository.Samples);
    }

    [Fact(DisplayName = "VoiceIdService.DeleteSampleAsync/UTC004: deleting nothing removes nothing")]
    [Trait("UTC", "VoiceIdService.DeleteSampleAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task DeleteSample_deleting_nothing_removes_nothing()
    {
        var f = MakeVoiceSvc();
        Consented(f);

        var outcome = await f.Service.DeleteSampleAsync(f.User, Guid.CreateVersion7());

        Assert.Equal(VoiceFailure.NotFound, outcome.Failure);
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "VoiceIdService.SampleAudioAsync/UTC001: the owner hears back their kept recording")]
    [Trait("UTC", "VoiceIdService.SampleAudioAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task SampleAudio_the_owner_hears_back_their_kept_recording()
    {
        var f = MakeVoiceSvc();
        var sample = OwnSample(f, Consented(f));
        sample.AudioCiphertext = [7, 0xA0];
        sample.AudioContentType = "audio/webm";

        var audio = await f.Service.SampleAudioAsync(f.User, sample.Id);

        Assert.Equal([7], Assert.IsType<VoiceSampleAudio>(audio).Bytes);
        Assert.Equal("audio/webm", Assert.IsType<VoiceSampleAudio>(audio).ContentType);
    }

    [Fact(DisplayName = "VoiceIdService.SampleAudioAsync/UTC002: someone else sample reads as not found")]
    [Trait("UTC", "VoiceIdService.SampleAudioAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task SampleAudio_someone_else_sample_reads_as_not_found()
    {
        var f = MakeVoiceSvc();
        var other = Guid.CreateVersion7();
        var foreign = new VoiceSample
        {
            UserId = other, ConsentId = Guid.CreateVersion7(), EmbeddingModel = "test-model", Ciphertext = [0xE0],
            AudioCiphertext = [7, 0xA0], AudioContentType = "audio/webm", CreatedAt = Now, UpdatedAt = Now,
        };
        f.Repository.Samples.Add(foreign);

        Assert.Null(await f.Service.SampleAudioAsync(f.User, foreign.Id));
    }

    [Fact(DisplayName = "VoiceIdService.SampleAudioAsync/UTC003: a sample without a kept recording is silent")]
    [Trait("UTC", "VoiceIdService.SampleAudioAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task SampleAudio_a_sample_without_a_kept_recording_is_silent()
    {
        var f = MakeVoiceSvc();
        var sample = OwnSample(f, Consented(f));

        Assert.Null(await f.Service.SampleAudioAsync(f.User, sample.Id));
    }

    [Fact(DisplayName = "VoiceIdService.WithdrawAsync/UTC001: withdrawing erases every sample and suggestion")]
    [Trait("UTC", "VoiceIdService.WithdrawAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Withdraw_withdrawing_erases_every_sample_and_suggestion()
    {
        var f = MakeVoiceSvc();
        var consent = Consented(f);
        OwnSample(f, consent);

        var outcome = await f.Service.WithdrawAsync(f.User);

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        Assert.Empty(f.Repository.Samples);
        Assert.NotNull(consent.WithdrawnAt);
        Assert.Null(Assert.IsType<VoiceProfile>(outcome.Data).Consent);
        Assert.Equal([AuditActions.VoiceConsentWithdrawn], f.Audit.Actions);
        Assert.Equal((1, 1), (f.Uow.Commits, f.Uow.Saves));
    }

    [Fact(DisplayName = "VoiceIdService.WithdrawAsync/UTC002: without consent there is nothing to erase")]
    [Trait("UTC", "VoiceIdService.WithdrawAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Withdraw_without_consent_there_is_nothing_to_erase()
    {
        var f = MakeVoiceSvc();

        var outcome = await f.Service.WithdrawAsync(f.User);

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        Assert.Null(Assert.IsType<VoiceProfile>(outcome.Data).Consent);
    }

    [Fact(DisplayName = "VoiceIdService.WithdrawAsync/UTC003: an erase failure is save failed with an error log")]
    [Trait("UTC", "VoiceIdService.WithdrawAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Withdraw_an_erase_failure_is_save_failed_with_an_error_log()
    {
        var f = MakeVoiceSvc();
        Consented(f);
        f.Uow.FailNextSave = new PersistenceUnavailableException();

        var outcome = await f.Service.WithdrawAsync(f.User);

        Assert.Equal(VoiceFailure.SaveFailed, outcome.Failure);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to withdraw Voice ID"));
    }

    private sealed record CollectSvc(FakeVoiceIdRepository Repository, FakeNotificationRepository Notifications,
        FakeUnitOfWork Uow, FakeVoiceEmbedder Embedder, FakeVoiceprintProtector Protector, FakeAuditService Audit,
        ICallerAccountQuery Callers, T1Logger<VoiceCollectionService> Log, VoiceCollectionService Service,
        Meeting Meeting, Guid Secretary, Guid Member);

    private static CollectSvc CollectionSvc(MeetingStatus status = MeetingStatus.Published, ICallerAccountQuery? callers = null)
    {
        var (inner, meeting, _, secretary, member) = NewMeeting(MeetingMode.InPerson, status);
        var repository = new FakeVoiceIdRepository();
        repository.Meetings.Add(meeting);
        var notifications = new FakeNotificationRepository();
        var uow = new FakeUnitOfWork();
        var embedder = new FakeVoiceEmbedder();
        var protector = new FakeVoiceprintProtector();
        var audit = new FakeAuditService();
        var accounts = callers ?? new ListCallerAccounts();
        var log = new T1Logger<VoiceCollectionService>();
        var service = new VoiceCollectionService(repository, notifications, uow, embedder, protector, audit,
            new FixedClock(Now), accounts, log);
        return new(repository, notifications, uow, embedder, protector, audit, accounts, log, service, meeting, secretary, member);
    }

    private static VoiceConsent CollectionConsent(CollectSvc f, string notice = VoiceIdRules.CollectionNoticeVersion)
    {
        var consent = new VoiceConsent
        {
            UserId = f.Member, GrantedAt = Now.AddDays(-1), NoticeVersion = notice, Purpose = VoiceIdRules.Purpose,
        };
        f.Repository.Consents.Add(consent);
        return consent;
    }

    private static VoiceSample ProxySample(CollectSvc f, VoiceConsent consent, int count, Guid? collector = null)
    {
        var sample = new VoiceSample
        {
            UserId = f.Member, ConsentId = consent.Id, EmbeddingModel = "test-model", Ciphertext = [0xE0],
            AudioCiphertext = [7, 0xA0], AudioContentType = "audio/webm",
            CollectedById = collector ?? f.Secretary, CollectedBy = new User
            {
                Id = collector ?? f.Secretary, FullName = "Thư ký", Email = "tk@trustcabinet.local", IsActive = true,
            },
            MeetingId = f.Meeting.Id, SpeechMs = 3000, CreatedAt = Now.AddMinutes(-count), UpdatedAt = Now,
        };
        f.Repository.Samples.Add(sample);
        return sample;
    }

    private static Task<VoiceOutcome> Collect(CollectSvc f, Guid? actor = null) => f.Service.AddAsync(
        f.Meeting.Id, actor ?? f.Secretary, f.Member, new MemoryStream([1, 2, 3]), "audio/webm");

    [Fact(DisplayName = "VoiceCollectionService.AddAsync/UTC001: a sample and the notice are kept together")]
    [Trait("UTC", "VoiceCollectionService.AddAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Collect_a_sample_and_the_notice_are_kept_together()
    {
        var f = CollectionSvc();
        CollectionConsent(f);
        ProxySample(f, f.Repository.Consents[0], 1);
        ProxySample(f, f.Repository.Consents[0], 2);

        var outcome = await Collect(f);

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        Assert.Equal(3, f.Repository.Samples.Count);
        var added = f.Repository.Samples[^1];
        Assert.Equal((f.Member, f.Secretary, (Guid?)f.Meeting.Id), (added.UserId, added.CollectedById, added.MeetingId));
        var notice = Assert.Single(f.Notifications.Added);
        Assert.Equal((f.Member, NotificationType.VoiceSampleCollected), (notice.UserId, notice.Type));
        Assert.Equal([AuditActions.VoiceSampleAdded], f.Audit.Actions);
        Assert.Equal((1, 1), (f.Uow.Commits, f.Uow.Saves));
    }

    [Fact(DisplayName = "VoiceCollectionService.AddAsync/UTC002: without consent no audio reaches the worker")]
    [Trait("UTC", "VoiceCollectionService.AddAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Collect_without_consent_no_audio_reaches_the_worker()
    {
        var f = CollectionSvc();

        var outcome = await Collect(f);

        Assert.Equal(VoiceFailure.NoConsent, outcome.Failure);
        Assert.Equal(0, f.Embedder.Calls);
        Assert.Empty(f.Notifications.Added);
    }

    [Fact(DisplayName = "VoiceCollectionService.AddAsync/UTC003: five samples by the secretary is the limit")]
    [Trait("UTC", "VoiceCollectionService.AddAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Collect_five_samples_by_the_secretary_is_the_limit()
    {
        var f = CollectionSvc();
        var consent = CollectionConsent(f);
        for (var i = 0; i < 5; i++) ProxySample(f, consent, i);

        var outcome = await Collect(f);

        Assert.Equal(VoiceFailure.LimitReached, outcome.Failure);
        Assert.Equal(0, f.Embedder.Calls);
        Assert.Equal(5, f.Repository.Samples.Count);
    }

    [Fact(DisplayName = "VoiceCollectionService.AddAsync/UTC004: an Ended meeting collects nothing more")]
    [Trait("UTC", "VoiceCollectionService.AddAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Collect_an_ended_meeting_collects_nothing_more()
    {
        var collectorRole = Guid.CreateVersion7();
        var accounts = new CollectorAccounts(
        [
            new User { Id = Guid.CreateVersion7(), FullName = "Thư ký", Email = "tk@trustcabinet.local", IsActive = true, RoleId = collectorRole },
        ], collectorRole);
        var f = CollectionSvc(MeetingStatus.Ended, accounts);
        var secretary = f.Repository.Meetings[0].Participants
            .Single(p => p.Role == MeetingRole.Secretary);
        accounts.Users[0].Id = secretary.UserId;
        CollectionConsent(f);

        var outcome = await f.Service.AddAsync(f.Meeting.Id, secretary.UserId, f.Member, new MemoryStream([1, 2, 3]), "audio/webm");

        Assert.Equal(VoiceFailure.NotCollectable, outcome.Failure);
        Assert.Equal(0, f.Embedder.Calls);
        Assert.Empty(f.Repository.Samples);
    }

    [Fact(DisplayName = "VoiceCollectionService.AddAsync/UTC005: a too-short sample is rejected with guidance")]
    [Trait("UTC", "VoiceCollectionService.AddAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Collect_a_too_short_sample_is_rejected_with_guidance()
    {
        var f = CollectionSvc();
        CollectionConsent(f);
        f.Embedder.Fail = new VoiceSampleRejectedException("too_short");

        var outcome = await Collect(f);

        Assert.Equal(VoiceFailure.Rejected, outcome.Failure);
        Assert.Equal(["Giọng mẫu quá ngắn — cần ít nhất 6 giây có tiếng nói. Hãy đọc hết đoạn văn mẫu."], outcome.Errors!["file"]);
        Assert.Empty(f.Repository.Samples);
    }

    [Fact(DisplayName = "VoiceCollectionService.AddAsync/UTC006: a down worker is engine unavailable with a warning")]
    [Trait("UTC", "VoiceCollectionService.AddAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Collect_a_down_worker_is_engine_unavailable_with_a_warning()
    {
        var f = CollectionSvc();
        CollectionConsent(f);
        f.Embedder.Fail = new SpeechEngineException("down");

        var outcome = await Collect(f);

        Assert.Equal(VoiceFailure.EngineUnavailable, outcome.Failure);
        Assert.Contains(f.Log.Entries, e => e.Level == LogLevel.Warning);
        Assert.Empty(f.Repository.Samples);
    }

    [Fact(DisplayName = "VoiceCollectionService.AudioAsync/UTC001: hearing a sample is audited before its bytes")]
    [Trait("UTC", "VoiceCollectionService.AudioAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Audio_hearing_a_sample_is_audited_before_its_bytes()
    {
        var f = CollectionSvc();
        var sample = ProxySample(f, CollectionConsent(f), 1);

        var audio = await f.Service.AudioAsync(f.Meeting.Id, f.Secretary, sample.Id);

        Assert.Equal([7], Assert.IsType<VoiceSampleAudio>(audio).Bytes);
        Assert.Equal([AuditActions.VoiceSamplePlayed], f.Audit.Actions);
        Assert.Equal(1, f.Uow.Saves);
    }

    [Fact(DisplayName = "VoiceCollectionService.AudioAsync/UTC002: a secretary hears only their own meeting samples")]
    [Trait("UTC", "VoiceCollectionService.AudioAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Audio_a_secretary_hears_only_their_own_meeting_samples()
    {
        var f = CollectionSvc();
        var sample = ProxySample(f, CollectionConsent(f), 1);
        sample.MeetingId = Guid.CreateVersion7();

        Assert.Null(await f.Service.AudioAsync(f.Meeting.Id, f.Secretary, sample.Id));
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "VoiceCollectionService.AudioAsync/UTC003: a non-secretary hears nothing")]
    [Trait("UTC", "VoiceCollectionService.AudioAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Audio_a_non_secretary_hears_nothing()
    {
        var f = CollectionSvc();
        var sample = ProxySample(f, CollectionConsent(f), 1);

        Assert.Null(await f.Service.AudioAsync(f.Meeting.Id, f.Member, sample.Id));
        Assert.Empty(f.Audit.Actions);
    }

    [Fact(DisplayName = "VoiceCollectionService.CanCollect/UTC001: a Published gathering may take samples")]
    [Trait("UTC", "VoiceCollectionService.CanCollect/UTC001")]
    [Trait("Trait", "UTC")]
    public void CanCollect_a_published_gathering_may_take_samples()
    {
        Assert.True(VoiceCollectionService.CanCollect(MeetingStatus.Published));
    }

    [Fact(DisplayName = "VoiceCollectionService.CanCollect/UTC002: an Ended gathering is over")]
    [Trait("UTC", "VoiceCollectionService.CanCollect/UTC002")]
    [Trait("Trait", "UTC")]
    public void CanCollect_an_ended_gathering_is_over()
    {
        Assert.False(VoiceCollectionService.CanCollect(MeetingStatus.Ended));
    }

    [Fact(DisplayName = "VoiceCollectionService.CanCollect/UTC003: a Cancelled gathering takes nothing")]
    [Trait("UTC", "VoiceCollectionService.CanCollect/UTC003")]
    [Trait("Trait", "UTC")]
    public void CanCollect_a_cancelled_gathering_takes_nothing()
    {
        Assert.False(VoiceCollectionService.CanCollect(MeetingStatus.Cancelled));
    }

    [Fact(DisplayName = "VoiceCollectionService.DeleteAsync/UTC001: deleting a sample taken here removes it")]
    [Trait("UTC", "VoiceCollectionService.DeleteAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Delete_deleting_a_sample_taken_here_removes_it()
    {
        var f = CollectionSvc();
        var sample = ProxySample(f, CollectionConsent(f), 1);

        var outcome = await f.Service.DeleteAsync(f.Meeting.Id, f.Secretary, sample.Id);

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        Assert.Empty(f.Repository.Samples);
        Assert.Equal([AuditActions.VoiceSampleDeleted], f.Audit.Actions);
        Assert.Equal(1, f.Uow.Saves);
    }

    [Fact(DisplayName = "VoiceCollectionService.DeleteAsync/UTC002: a self-recorded sample is out of reach")]
    [Trait("UTC", "VoiceCollectionService.DeleteAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Delete_a_self_recorded_sample_is_out_of_reach()
    {
        var f = CollectionSvc();
        var consent = CollectionConsent(f);
        var own = new VoiceSample
        {
            UserId = f.Member, ConsentId = consent.Id, EmbeddingModel = "test-model", Ciphertext = [0xE0],
            CreatedAt = Now, UpdatedAt = Now,
        };
        f.Repository.Samples.Add(own);

        var outcome = await f.Service.DeleteAsync(f.Meeting.Id, f.Secretary, own.Id);

        Assert.Equal(VoiceFailure.NotFound, outcome.Failure);
        Assert.Single(f.Repository.Samples);
    }

    [Fact(DisplayName = "VoiceCollectionService.DeleteAsync/UTC003: a non-secretary deletes nothing")]
    [Trait("UTC", "VoiceCollectionService.DeleteAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Delete_a_non_secretary_deletes_nothing()
    {
        var f = CollectionSvc();
        var sample = ProxySample(f, CollectionConsent(f), 1);

        var outcome = await f.Service.DeleteAsync(f.Meeting.Id, f.Member, sample.Id);

        Assert.Equal(VoiceFailure.NotFound, outcome.Failure);
        Assert.Single(f.Repository.Samples);
    }
}

/// <summary>Accounts with one capability-holding role, for the Ended-meeting UTC. Local to Round 1 (FE-10).</summary>
internal sealed class CollectorAccounts(List<User> users, Guid collectorRole) : ICallerAccountQuery
{
    public List<User> Users { get; } = users;
    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Users.SingleOrDefault(u => u.Id == id));
    public Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(Guid roleId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyCollection<Capability>>(roleId == collectorRole ? [Capability.VoiceCollector] : []);
}
