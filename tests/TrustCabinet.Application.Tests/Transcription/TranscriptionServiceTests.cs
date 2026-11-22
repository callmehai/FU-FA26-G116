using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Recordings;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Application.Tests.Recordings;
using TrustCabinet.Application.Transcription;
using TrustCabinet.Application.VoiceId;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Application.Tests.Transcription;

/// <summary>SCRUM-180 (CA-15): speech to text and naming its voices (SCRUM-31, SCRUM-35) over the unit of work.</summary>
public sealed class TranscriptionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeRecordingRepository _recordings = new();
    private readonly FakeTranscriptRepository _transcripts = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeRecordingStorage _storage = new();
    private readonly FakeSpeechEngine _engine = new();
    private readonly FakeVoiceIdRepository _voices = new();
    private readonly FakeVoiceprintProtector _protector = new();
    private readonly FakeAuditService _audit = new();
    private readonly FakeSpeechChangePublisher _publisher;
    private readonly RecordingTestMeeting _meeting;
    private readonly Recording _recording;

    public TranscriptionServiceTests()
    {
        _publisher = new(_unitOfWork);
        _meeting = new(_meetings, MeetingMode.InPerson, Now);
        _recording = new Recording
        {
            MeetingId = _meeting.Meeting.Id, Source = RecordingSource.Upload, Status = RecordingStatus.Available,
            FileName = "hop.mp3", ContentType = "audio/mpeg", StoragePath = _storage.Create(".mp3"),
            CreatedById = _meeting.Secretary, CreatedAt = Now.AddHours(-1), UpdatedAt = Now.AddHours(-1),
        };
        _recordings.Recordings.Add(_recording);
    }

    // A run's stale watch waits on this clock, so it looks only when a test moves the time on: on a real timer it woke
    // up in the middle of other tests' runs and added reads to what they count.
    private readonly ManualClock _clock = new(Now);

    private TranscriptionService Service() => new(_meetings, _recordings, _transcripts, _unitOfWork, _storage, _engine, _voices,
        _protector, Options.Create(new VoiceIdOptions()), _audit, _publisher, _clock, NullLogger<TranscriptionService>.Instance);

    private Transcript Queued(TranscriptStatus status = TranscriptStatus.Pending)
    {
        var transcript = new Transcript
        {
            RecordingId = _recording.Id, Recording = _recording, Status = status, CreatedAt = Now.AddMinutes(-5), UpdatedAt = Now.AddMinutes(-5),
        };
        _transcripts.Transcripts.Add(transcript);
        return transcript;
    }

    [Fact]
    public async Task A_transcript_is_queued_under_the_recording_lock_in_one_transaction()
    {
        var outcome = await Service().RetryAsync(_recording.Id, _meeting.Secretary);

        Assert.Equal(TranscriptFailure.None, outcome.Failure);
        Assert.Equal(TranscriptStatus.Pending, Assert.Single(_transcripts.Added).Status);
        Assert.Equal([_recording.Id], _recordings.Locked);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
    }

    [Fact]
    public async Task A_storage_failure_while_queueing_is_save_failed_and_rolled_back()
    {
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().RetryAsync(_recording.Id, _meeting.Secretary);

        Assert.Equal(TranscriptFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task The_machines_guess_at_a_voice_reaches_the_Secretary_only()
    {
        var transcript = Queued(TranscriptStatus.Completed);
        transcript.Segments.Add(new TranscriptSegment { TranscriptId = transcript.Id, Position = 1, Text = "Xin chào", SpeakerLabel = "SPEAKER_00" });
        transcript.Speakers.Add(new TranscriptSpeaker
        {
            TranscriptId = transcript.Id, Label = "SPEAKER_00", SuggestedUserId = _meeting.Member, SuggestionScore = 0.7,
        });

        var member = Assert.IsType<TranscriptView>((await Service().GetAsync(_recording.Id, _meeting.Member)).Data);
        var secretary = Assert.IsType<TranscriptView>((await Service().GetAsync(_recording.Id, _meeting.Secretary)).Data);

        Assert.Null(Assert.Single(member.Speakers).SuggestedUserId);
        Assert.Empty(member.People);
        Assert.Equal(_meeting.Member, Assert.Single(secretary.Speakers).SuggestedUserId);
        Assert.All(_unitOfWork.Modes, mode => Assert.Equal(TransactionMode.None, mode));
    }

    [Fact]
    public async Task A_voice_only_just_suggested_is_named_by_the_machine_too()
    {
        // ADR-0074, 22/11/2026: the room recording's three people were suggested right at 0.61–0.68 and each had to be
        // picked by hand. Every suggestion now fills the name in, and the Secretary still corrects it.
        Assert.Equal(new VoiceIdOptions().SuggestThreshold, new VoiceIdOptions().AutoAssignThreshold);
        var transcript = Queued();
        var consent = new VoiceConsent
        {
            UserId = _meeting.Member, GrantedAt = Now.AddDays(-1), NoticeVersion = VoiceIdRules.NoticeVersion, Purpose = VoiceIdRules.Purpose,
        };
        _voices.Consents.Add(consent);
        var sampleId = Guid.CreateVersion7();
        _voices.Samples.Add(new VoiceSample
        {
            Id = sampleId, UserId = _meeting.Member, ConsentId = consent.Id, EmbeddingModel = "test-model",
            Ciphertext = _protector.Protect([1f, 0f], _meeting.Member, sampleId, "test-model"),
        });
        _engine.Result = new SpeechTranscription(2000, "vi", "test", "test-model",
            [new SpeechSegment(0, 1000, "SPEAKER_00", "Xin chào")], [new SpeechSpeaker("SPEAKER_00", 1000, [0.61f, 0.79f])]);

        Assert.True(await Service().ProcessNextAsync());

        var voice = Assert.Single(transcript.Speakers);
        Assert.InRange(voice.SuggestionScore!.Value, 0.6, 0.62);
        Assert.Equal((_meeting.Member, _meeting.Member), (voice.SuggestedUserId, voice.AssignedUserId));
        Assert.Null(voice.AssignedById);
    }

    [Fact]
    public async Task A_finished_run_matches_voices_under_the_consent_lock_names_a_sure_one_and_is_announced_after_the_commit()
    {
        var transcript = Queued();
        var consent = new VoiceConsent
        {
            UserId = _meeting.Member, GrantedAt = Now.AddDays(-1), NoticeVersion = VoiceIdRules.NoticeVersion, Purpose = VoiceIdRules.Purpose,
        };
        _voices.Consents.Add(consent);
        var sampleId = Guid.CreateVersion7();
        _voices.Samples.Add(new VoiceSample
        {
            Id = sampleId, UserId = _meeting.Member, ConsentId = consent.Id, EmbeddingModel = "test-model",
            Ciphertext = _protector.Protect([1f, 0f], _meeting.Member, sampleId, "test-model"),
        });
        _engine.Result = new SpeechTranscription(2000, "vi", "test", "test-model",
            [new SpeechSegment(0, 1000, "SPEAKER_00", "Xin chào")], [new SpeechSpeaker("SPEAKER_00", 1000, [1f, 0f])]);

        Assert.True(await Service().ProcessNextAsync());

        Assert.Equal(TranscriptStatus.Completed, transcript.Status);
        Assert.Equal("Xin chào", Assert.Single(transcript.Segments).Text);
        var voice = Assert.Single(transcript.Speakers);
        Assert.Equal(_meeting.Member, voice.AssignedUserId);
        Assert.Null(voice.AssignedById);
        Assert.Equal(["share-lock consents", "read samples to match"], _voices.Steps);
        Assert.Equal([AuditActions.TranscriptSpeakerAssigned], _audit.Actions);
        Assert.Equal([_recording.Id], _recordings.Locked);
        Assert.Equal([TransactionMode.None, TransactionMode.None, TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
        Assert.Equal(1, Assert.Single(_publisher.Published).CommitsBefore);
    }

    [Fact]
    public async Task Short_lines_no_enrolled_voice_matched_are_left_out_and_the_voices_left_are_numbered_again()
    {
        var transcript = Queued();
        var consent = new VoiceConsent
        {
            UserId = _meeting.Member, GrantedAt = Now.AddDays(-1), NoticeVersion = VoiceIdRules.NoticeVersion, Purpose = VoiceIdRules.Purpose,
        };
        _voices.Consents.Add(consent);
        var sampleId = Guid.CreateVersion7();
        _voices.Samples.Add(new VoiceSample
        {
            Id = sampleId, UserId = _meeting.Member, ConsentId = consent.Id, EmbeddingModel = "test-model",
            Ciphertext = _protector.Protect([1f, 0f], _meeting.Member, sampleId, "test-model"),
        });
        _engine.Result = new SpeechTranscription(30000, "vi", "test", "test-model",
        [
            new SpeechSegment(0, 1000, "SPEAKER_00", "Ừ."),
            new SpeechSegment(1000, 5000, "SPEAKER_01", "Kính thưa các đồng chí, hôm nay chúng ta họp."),
            new SpeechSegment(5000, 6000, "SPEAKER_00", "Đồng ý."),
            new SpeechSegment(6000, 7500, "SPEAKER_02", "Đến tối là kín mịn."),
            new SpeechSegment(7500, 9000, "SPEAKER_02", "Nó có còn phải test là có nền không?"),
            new SpeechSegment(9000, 12000, "SPEAKER_02", "Tiếp theo là phần báo cáo của phòng tài chính."),
            new SpeechSegment(12000, 13000, "SPEAKER_01", "À."),
        ],
        [
            new SpeechSpeaker("SPEAKER_00", 2000, [0f, 1f]), new SpeechSpeaker("SPEAKER_01", 5000, [1f, 0f]),
            new SpeechSpeaker("SPEAKER_02", 6000, [-1f, 0f]),
        ]);

        Assert.True(await Service().ProcessNextAsync());

        // SPEAKER_00 matched nobody and said only short lines: gone. SPEAKER_01 is the member, so their short "À." goes
        // only for being an interjection; SPEAKER_02's long lines stay (a quick one of 20+ characters too), its short
        // one does not.
        Assert.Equal(
            [("SPEAKER_00", "Kính thưa các đồng chí, hôm nay chúng ta họp."), ("SPEAKER_01", "Nó có còn phải test là có nền không?"),
                ("SPEAKER_01", "Tiếp theo là phần báo cáo của phòng tài chính.")],
            transcript.Segments.OrderBy(s => s.Position).Select(s => (s.SpeakerLabel, s.Text)));
        Assert.Equal([1, 2, 3], transcript.Segments.Select(s => s.Position).Order());
        Assert.Equal(["SPEAKER_00", "SPEAKER_01"], transcript.Speakers.Select(s => s.Label).Order());
        Assert.Equal(_meeting.Member, transcript.Speakers.Single(s => s.Label == "SPEAKER_00").SuggestedUserId);
        Assert.Null(transcript.Speakers.Single(s => s.Label == "SPEAKER_01").SuggestedUserId);
    }

    [Fact]
    public async Task A_run_made_stale_by_a_newer_take_is_stopped_and_the_transcript_waits_for_the_next_one()
    {
        var transcript = Queued();
        var started = new TaskCompletionSource();
        _engine.Hold = started;
        var service = Service();
        var run = Task.Run(() => service.ProcessNextAsync());
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await _clock.WhenPendingAsync();
        // RoomTakeService appends the take and queues the whole recording again.
        transcript.Status = TranscriptStatus.Pending;
        Assert.False(run.IsCompleted);
        _clock.Advance(service.StaleCheckEvery);

        Assert.True(await run.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(_engine.Cancelled);
        Assert.Equal(TranscriptStatus.Pending, transcript.Status);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task A_run_still_current_is_not_stopped_by_its_watch()
    {
        var transcript = Queued();
        var started = new TaskCompletionSource();
        _engine.Hold = started;
        using var shutdown = new CancellationTokenSource();

        var service = Service();
        var run = Task.Run(() => service.ProcessNextAsync(shutdown.Token));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // Three looks, each finding the transcript still Processing, and the watch waiting for the next one.
        for (var look = 0; look < 3; look++)
        {
            await _clock.WhenPendingAsync();
            _clock.Advance(service.StaleCheckEvery);
        }
        await _clock.WhenPendingAsync();

        Assert.False(run.IsCompleted);
        Assert.False(_engine.Cancelled);
        await shutdown.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(TranscriptStatus.Processing, transcript.Status);
    }

    [Fact]
    public async Task An_unexpected_failure_discards_what_was_staged_and_marks_the_transcript_failed()
    {
        var transcript = Queued();
        _engine.Fail = new InvalidOperationException("worker answer unreadable");

        Assert.True(await Service().ProcessNextAsync());

        Assert.Equal(TranscriptStatus.Failed, transcript.Status);
        Assert.Equal(TranscriptionService.UnexpectedFailureMessage, transcript.ErrorMessage);
        Assert.Equal(1, _unitOfWork.Discards);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
    }

    private (Transcript Transcript, TranscriptSegment Segment, TranscriptSpeaker Voice) Spoken()
    {
        var transcript = Queued(TranscriptStatus.Completed);
        var segment = new TranscriptSegment
        {
            TranscriptId = transcript.Id, Position = 1, StartMs = 0, EndMs = 1000, Text = "Xin chào", SpeakerLabel = "SPEAKER_00",
        };
        var voice = new TranscriptSpeaker { TranscriptId = transcript.Id, Label = "SPEAKER_00", SpeechMs = 1000 };
        transcript.Segments.Add(segment);
        transcript.Speakers.Add(voice);
        return (transcript, segment, voice);
    }

    [Fact]
    public async Task Naming_a_voice_runs_under_the_recording_lock_in_one_transaction()
    {
        var (_, _, voice) = Spoken();

        var outcome = await Service().AssignSpeakerAsync(_recording.Id, "SPEAKER_00", _meeting.Member, null, _meeting.Secretary);

        Assert.Equal(TranscriptFailure.None, outcome.Failure);
        Assert.Equal((_meeting.Member, _meeting.Secretary), (voice.AssignedUserId, voice.AssignedById));
        Assert.Equal([_recording.Id], _recordings.Locked);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
        Assert.Equal([AuditActions.TranscriptSpeakerAssigned], _audit.Actions);
        // Every open Bản ghi tab of the meeting re-reads, once the name is committed.
        var (change, commitsBefore) = Assert.Single(_publisher.Published);
        Assert.Equal((_meeting.Meeting.Id, RecordingRules.TranscriptChanged, 1), (change.MeetingId, change.ChangeType, commitsBefore));
    }

    [Fact]
    public async Task Naming_a_voice_someone_else_may_not_name_tells_nobody()
    {
        Spoken();

        var outcome = await Service().AssignSpeakerAsync(_recording.Id, "SPEAKER_00", _meeting.Member, null, _meeting.Member);

        Assert.Equal(TranscriptFailure.Forbidden, outcome.Failure);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Correcting_a_line_is_announced_to_the_meeting()
    {
        var (transcript, segment, _) = Spoken();
        segment.Transcript = transcript;

        var outcome = await Service().EditSegmentAsync(segment.Id, _meeting.Secretary, "Xin chào các đồng chí");

        Assert.Equal(TranscriptFailure.None, outcome.Failure);
        var (change, _) = Assert.Single(_publisher.Published);
        Assert.Equal((_meeting.Meeting.Id, RecordingRules.TranscriptChanged), (change.MeetingId, change.ChangeType));
    }

    [Fact]
    public async Task Moving_a_line_to_a_person_gives_them_a_new_voice_under_the_recording_lock_in_one_transaction()
    {
        var (transcript, segment, voice) = Spoken();

        var outcome = await Service().AssignSegmentSpeakerAsync(segment.Id, _meeting.Member, null, null, _meeting.Secretary);

        Assert.Equal(TranscriptFailure.None, outcome.Failure);
        var created = Assert.Single(transcript.Speakers, s => s.Label != "SPEAKER_00");
        Assert.Equal(("SPEAKER_01", _meeting.Member, 1000L), (created.Label, created.AssignedUserId, created.SpeechMs));
        Assert.Equal((created.Label, 0L), (segment.SpeakerLabel, voice.SpeechMs));
        Assert.Equal([_recording.Id], _recordings.Locked);
        Assert.Equal([TransactionMode.None, TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
        Assert.Equal([AuditActions.TranscriptSegmentSpeakerAssigned], _audit.Actions);
        var (change, commitsBefore) = Assert.Single(_publisher.Published);
        Assert.Equal((RecordingRules.TranscriptChanged, 1), (change.ChangeType, commitsBefore));
    }

    [Fact]
    public async Task Moving_a_line_to_the_voice_it_already_has_writes_and_tells_nothing()
    {
        var (_, segment, _) = Spoken();

        var outcome = await Service().AssignSegmentSpeakerAsync(segment.Id, null, null, "SPEAKER_00", _meeting.Secretary);

        Assert.Equal(TranscriptFailure.None, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task The_result_for_a_recording_erased_meanwhile_is_not_written()
    {
        var transcript = Queued();
        _recording.Status = RecordingStatus.Deleted;

        Assert.True(await Service().ProcessNextAsync());

        Assert.Equal(TranscriptStatus.Processing, transcript.Status);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_publisher.Published);
    }

    // ADR-0077 — a re-run (a room recording after every take) keeps what the Secretary corrected by hand. The old run heard
    // 20 s: someone at 0–10 s, the presenter at 10–20 s, named by hand. The new run also heard the next take, 20–30 s.
    private static readonly DateTimeOffset NamedAt = Now.AddMinutes(-30);

    private Transcript Corrected()
    {
        var transcript = Queued();
        transcript.Segments.Add(new TranscriptSegment
        {
            TranscriptId = transcript.Id, Position = 1, StartMs = 0, EndMs = 10_000, Text = "Mời anh trình bày.", SpeakerLabel = "SPEAKER_00",
        });
        transcript.Segments.Add(new TranscriptSegment
        {
            TranscriptId = transcript.Id, Position = 2, StartMs = 10_000, EndMs = 20_000, Text = "Kính thưa các đồng chí.", SpeakerLabel = "SPEAKER_01",
        });
        transcript.Speakers.Add(new TranscriptSpeaker { TranscriptId = transcript.Id, Label = "SPEAKER_00", SpeechMs = 10_000 });
        transcript.Speakers.Add(new TranscriptSpeaker
        {
            TranscriptId = transcript.Id, Label = "SPEAKER_01", SpeechMs = 10_000, AssignedUserId = _meeting.Member,
            AssignedById = _meeting.Secretary, AssignedAt = NamedAt,
        });
        return transcript;
    }

    /// <summary>The new run numbers the voices the other way round: the presenter, heard first this time, is SPEAKER_00.</summary>
    private static SpeechTranscription Rerun(string embeddingModel = "", params SpeechSegment[] extra) => new(30_000, "vi", "test",
        embeddingModel,
        [
            new SpeechSegment(0, 3_000, "B", "Vâng, tôi xin phép trình bày."), new SpeechSegment(3_000, 10_000, "A", "Mời anh trình bày ạ."),
            new SpeechSegment(10_000, 20_000, "B", "Kính thưa các đồng chí."), new SpeechSegment(20_000, 30_000, "B", "Phần tiếp theo."),
            .. extra,
        ],
        [new SpeechSpeaker("A", 7_000, [0f, 1f]), new SpeechSpeaker("B", 23_000, [1f, 0f])]);

    private static TranscriptSpeaker Voice(Transcript transcript, string label) => transcript.Speakers.Single(s => s.Label == label);

    [Fact]
    public async Task A_name_given_by_hand_follows_its_voice_into_the_next_run_under_a_new_label()
    {
        var transcript = Corrected();
        _engine.Result = Rerun();

        Assert.True(await Service().ProcessNextAsync());

        Assert.Equal(TranscriptStatus.Completed, transcript.Status);
        var presenter = Voice(transcript, "SPEAKER_00");
        Assert.Equal((_meeting.Member, _meeting.Secretary, NamedAt), (presenter.AssignedUserId, presenter.AssignedById, presenter.AssignedAt));
        Assert.Null(Voice(transcript, "SPEAKER_01").AssignedUserId);
        Assert.Equal([AuditActions.TranscriptCorrectionsCarried], _audit.Actions);
    }

    [Fact]
    public async Task The_machines_name_for_a_voice_does_not_replace_the_one_given_by_hand()
    {
        var transcript = Corrected();
        // The Secretary enrolled, and the machine is sure the presenter is them.
        var consent = new VoiceConsent
        {
            UserId = _meeting.Secretary, GrantedAt = Now.AddDays(-1), NoticeVersion = VoiceIdRules.NoticeVersion, Purpose = VoiceIdRules.Purpose,
        };
        _voices.Consents.Add(consent);
        var sampleId = Guid.CreateVersion7();
        _voices.Samples.Add(new VoiceSample
        {
            Id = sampleId, UserId = _meeting.Secretary, ConsentId = consent.Id, EmbeddingModel = "test-model",
            Ciphertext = _protector.Protect([1f, 0f], _meeting.Secretary, sampleId, "test-model"),
        });
        _engine.Result = Rerun("test-model");

        Assert.True(await Service().ProcessNextAsync());

        var presenter = Voice(transcript, "SPEAKER_00");
        // Still suggested, for the Secretary to see, but the name is the one they gave.
        Assert.Equal(_meeting.Secretary, presenter.SuggestedUserId);
        Assert.Equal((_meeting.Member, _meeting.Secretary), (presenter.AssignedUserId, presenter.AssignedById));
        Assert.DoesNotContain(AuditActions.TranscriptSpeakerAssigned, _audit.Actions);
    }

    [Fact]
    public async Task A_name_the_machine_gave_someone_whose_consent_ended_is_not_carried()
    {
        var transcript = Corrected();
        // The machine had named the presenter; the member then withdrew Voice ID consent, which erased their samples.
        var presenter = Voice(transcript, "SPEAKER_01");
        (presenter.AssignedById, presenter.AssignedAt, presenter.SuggestedUserId) = (null, null, _meeting.Member);
        _voices.Consents.Add(new VoiceConsent
        {
            UserId = _meeting.Member, GrantedAt = Now.AddDays(-1), WithdrawnAt = Now.AddMinutes(-1), NoticeVersion = VoiceIdRules.NoticeVersion,
            Purpose = VoiceIdRules.Purpose,
        });
        _engine.Result = Rerun("test-model");

        Assert.True(await Service().ProcessNextAsync());

        Assert.All(transcript.Speakers, s => Assert.Null(s.AssignedUserId));
        Assert.All(transcript.Speakers, s => Assert.Null(s.SuggestedUserId));
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task A_line_edited_by_hand_keeps_its_text_on_the_new_line_at_the_same_place()
    {
        var transcript = Corrected();
        var line = transcript.Segments.Single(s => s.Position == 2);
        (line.Text, line.EditedById, line.EditedAt) = ("Kính thưa các đồng chí lãnh đạo.", _meeting.Secretary, NamedAt);
        _engine.Result = Rerun();

        Assert.True(await Service().ProcessNextAsync());

        var carried = transcript.Segments.Single(s => s.StartMs == 10_000);
        Assert.Equal(("Kính thưa các đồng chí lãnh đạo.", _meeting.Secretary, NamedAt), (carried.Text, carried.EditedById, carried.EditedAt));
        // The machine's word times were for its own words.
        Assert.Null(carried.WordStartsMs);
        Assert.Equal("Phần tiếp theo.", transcript.Segments.Single(s => s.StartMs == 20_000).Text);
    }

    [Fact]
    public async Task An_edited_line_no_new_line_matches_is_recorded_in_the_audit_log_without_its_text()
    {
        var transcript = Corrected();
        var line = transcript.Segments.Single(s => s.Position == 2);
        (line.Text, line.EditedById, line.EditedAt) = ("Nội dung đã sửa tay.", _meeting.Secretary, NamedAt);
        // This run cut the stretch differently: 10–14 s and 14–20 s, neither close to the old line.
        _engine.Result = new SpeechTranscription(30_000, "vi", "test", "",
            [
                new SpeechSegment(0, 10_000, "A", "Mời anh trình bày ạ."), new SpeechSegment(10_000, 14_000, "B", "Kính thưa các đồng chí."),
                new SpeechSegment(14_000, 20_000, "B", "Hôm nay chúng ta họp."),
            ],
            [new SpeechSpeaker("A", 10_000, [0f, 1f]), new SpeechSpeaker("B", 10_000, [1f, 0f])]);

        Assert.True(await Service().ProcessNextAsync());

        Assert.DoesNotContain(transcript.Segments, s => s.EditedAt is not null);
        Assert.Equal([AuditActions.TranscriptCorrectionsCarried], _audit.Actions);
        var details = JsonSerializer.Serialize(Assert.Single(_audit.Details));
        using var json = JsonDocument.Parse(details);
        var unplaced = Assert.Single(json.RootElement.GetProperty("UnplacedEdits").EnumerateArray());
        Assert.Equal((10_000L, 20_000L), (unplaced.GetProperty("StartMs").GetInt64(), unplaced.GetProperty("EndMs").GetInt64()));
        Assert.Equal(0, json.RootElement.GetProperty("Edits").GetInt32());
        // The name still follows the voice; the text itself stays out of the log, which outlives the recording.
        Assert.Equal(_meeting.Member, Voice(transcript, "SPEAKER_01").AssignedUserId);
        Assert.DoesNotContain("sửa tay", details);
    }

    [Fact]
    public async Task A_line_moved_to_a_person_by_hand_is_moved_again_and_kept_even_when_short()
    {
        var transcript = Corrected();
        // The Secretary moved a short "Đồng ý." to themselves, which made them a voice of their own.
        transcript.Segments.Add(new TranscriptSegment
        {
            TranscriptId = transcript.Id, Position = 3, StartMs = 25_000, EndMs = 26_000, Text = "Đồng ý.", SpeakerLabel = "SPEAKER_02",
            SpeakerAssignedById = _meeting.Secretary, SpeakerAssignedAt = NamedAt,
        });
        transcript.Speakers.Add(new TranscriptSpeaker
        {
            TranscriptId = transcript.Id, Label = "SPEAKER_02", SpeechMs = 1_000, AssignedUserId = _meeting.Secretary,
            AssignedById = _meeting.Secretary, AssignedAt = NamedAt,
        });
        _engine.Result = Rerun("", new SpeechSegment(25_100, 26_000, "A", "Đồng ý."));

        Assert.True(await Service().ProcessNextAsync());

        var moved = transcript.Segments.Single(s => s.StartMs == 25_100);
        var theirs = Voice(transcript, moved.SpeakerLabel);
        Assert.Equal(("SPEAKER_02", _meeting.Secretary, 900L), (theirs.Label, theirs.AssignedUserId, theirs.SpeechMs));
        Assert.Equal((_meeting.Secretary, NamedAt), (moved.SpeakerAssignedById, moved.SpeakerAssignedAt));
        Assert.Equal(7_000 - 900L, Voice(transcript, "SPEAKER_01").SpeechMs);
    }
}
