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

    private TranscriptionService Service() => new(_meetings, _recordings, _transcripts, _unitOfWork, _storage, _engine, _voices,
        _protector, Options.Create(new VoiceIdOptions()), _audit, _publisher, new FixedClock(Now),
        NullLogger<TranscriptionService>.Instance);

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
}
