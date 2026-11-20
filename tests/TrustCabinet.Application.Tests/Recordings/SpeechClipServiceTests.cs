using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Recordings;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Application.Transcription;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Recordings;

/// <summary>SCRUM-179 (CA-14): the per-mic transcript (SCRUM-109) stored and erased over the unit of work.</summary>
public sealed class SpeechClipServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeSpeechClipRepository _clips = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeRecordingStorage _storage = new();
    private readonly FakeSpeechChangePublisher _publisher;

    public SpeechClipServiceTests() => _publisher = new(_unitOfWork);

    private readonly FakeSpeechEngine _engine = new();

    private SpeechClipService Service() => new(_meetings, _clips, _unitOfWork, _engine, new SpeechClipGate(),
        _publisher, new FakeAuditService(), new FixedClock(Now), Options.Create(new RecordingOptions()), _storage,
        new FakeRecordingSigner(), new FakeCombinedRecordings(), NullLogger<SpeechClipService>.Instance);

    private static SpeechClipUpload Upload() => new(Guid.CreateVersion7(), Now.AddSeconds(-2), 1500, "audio/wav", [1, 2, 3]);

    private Task<SpeechClipOutcome> Add(RecordingTestMeeting meeting, SpeechClipUpload upload)
        => Service().AddAsync(meeting.Meeting.Id, meeting.Member, upload);

    [Fact]
    public async Task A_sentence_is_stored_with_its_audio_in_one_transaction_then_announced()
    {
        var meeting = new RecordingTestMeeting(_meetings, MeetingMode.Online, Now);

        var outcome = await Add(meeting, Upload());

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        var clip = Assert.Single(_clips.Added);
        Assert.Equal(meeting.Member, clip.SpeakerId);
        Assert.Equal("Xin chào", clip.Text);
        Assert.Equal(clip.Id, Assert.IsType<SpeechClipView>(outcome.Data).Id);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
        var (change, commitsBefore) = Assert.Single(_publisher.Published);
        Assert.Equal(("ClipAdded", 1), (change.ChangeType, commitsBefore));
        Assert.True(_storage.Files.ContainsKey(clip.AudioPath!));
        Assert.Empty(_storage.Deleted);
    }

    [Fact]
    public async Task A_sentence_from_a_meeting_held_in_one_room_is_refused_before_it_is_transcribed()
    {
        var meeting = new RecordingTestMeeting(_meetings, MeetingMode.InPerson, Now);

        var outcome = await Add(meeting, Upload());

        Assert.Equal(SpeechClipFailure.NotCapturing, outcome.Failure);
        Assert.Empty(_clips.Added);
        Assert.Empty(_storage.Files);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task A_storage_failure_is_save_failed_and_the_audio_is_deleted()
    {
        var meeting = new RecordingTestMeeting(_meetings, MeetingMode.Online, Now);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Add(meeting, Upload());

        Assert.Equal(SpeechClipFailure.SaveFailed, outcome.Failure);
        Assert.Equal(1, _unitOfWork.Rollbacks);
        Assert.Single(_storage.Deleted);
        Assert.Empty(_storage.Files);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task The_speakers_previous_sentence_is_read_ahead_and_saved_punctuated_again_with_the_new_one()
    {
        var meeting = new RecordingTestMeeting(_meetings, MeetingMode.Online, Now);
        var previous = Previous(meeting, "Kế hoạch này.", edited: false);
        _engine.Result = new SpeechTranscription(1000, "vi", "test", "test",
            [new SpeechSegment(0, 1000, null, "cần thêm thời gian.")], [], Context: "Kế hoạch này");

        var outcome = await Add(meeting, Upload());

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        Assert.Equal("Kế hoạch này.", Assert.Single(_engine.Contexts));
        Assert.Equal(("Kế hoạch này", Now), (previous.Text, previous.UpdatedAt));
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
    }

    [Fact]
    public async Task A_previous_sentence_the_secretary_corrected_is_neither_read_ahead_nor_changed()
    {
        var meeting = new RecordingTestMeeting(_meetings, MeetingMode.Online, Now);
        var previous = Previous(meeting, "Kế hoạch này.", edited: true);

        await Add(meeting, Upload());

        Assert.Null(Assert.Single(_engine.Contexts));
        Assert.Equal("Kế hoạch này.", previous.Text);
    }

    /// <summary>The member's sentence that ended a second before <see cref="Upload"/>'s began, as a row and tracked.</summary>
    private SpeechClip Previous(RecordingTestMeeting meeting, string text, bool edited)
    {
        var started = Now.AddSeconds(-5);
        var clip = new SpeechClip
        {
            MeetingId = meeting.Meeting.Id, SpeakerId = meeting.Member, StartedAt = started, DurationMs = 2000, Text = text,
            CreatedAt = started, UpdatedAt = started, EditedAt = edited ? started : null,
        };
        _clips.Clips.Add(clip);
        _clips.Rows.Add(new ClipRow(clip.Id, meeting.Member, "Thành viên", started, 2000, text, started, clip.EditedAt, null, [],
            null, null, null, false, null, false));
        return clip;
    }

    [Fact]
    public async Task The_same_sentence_sent_twice_at_once_answers_with_the_copy_stored_first()
    {
        var meeting = new RecordingTestMeeting(_meetings, MeetingMode.Online, Now);
        var copy = new ClipRow(Guid.CreateVersion7(), meeting.Member, "Thành viên", Now.AddSeconds(-2), 1500, "Xin chào",
            Now, null, null, [], null, null, null, true, null, false);
        _clips.ConcurrentCopy = copy;
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.Unknown);

        var outcome = await Add(meeting, Upload());

        Assert.Equal(SpeechClipFailure.None, outcome.Failure);
        Assert.Equal(copy.Id, Assert.IsType<SpeechClipView>(outcome.Data).Id);
        Assert.Equal((1, 1), (_unitOfWork.Rollbacks, _unitOfWork.Discards));
        Assert.Single(_storage.Deleted);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Sentences_past_their_retention_are_deleted_with_their_audio()
    {
        var old = new SpeechClip { AudioPath = _storage.Create(".wav"), CreatedAt = Now.AddDays(-181) };
        var kept = new SpeechClip { AudioPath = _storage.Create(".wav"), CreatedAt = Now.AddDays(-1) };
        _clips.Clips.AddRange([old, kept]);

        var erased = await Service().EraseExpiredAsync();

        Assert.Equal(1, erased);
        Assert.Equal([kept], _clips.Clips);
        Assert.Equal([old.AudioPath], _storage.Deleted);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }
}
