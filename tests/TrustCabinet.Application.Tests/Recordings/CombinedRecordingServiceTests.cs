using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Recordings;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Recordings;

/// <summary>SCRUM-179 (CA-14): the combined recording of a hybrid meeting (ADR-0062) over the unit of work.</summary>
public sealed class CombinedRecordingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Started = Now.AddHours(-2);

    private readonly Guid _meetingId = Guid.CreateVersion7();
    private readonly FakeRecordingRepository _recordings = new();
    private readonly FakeSpeechClipRepository _clips = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeRecordingStorage _storage = new();
    private readonly FakeSpeechChangePublisher _publisher;
    private readonly Recording _room;

    public CombinedRecordingServiceTests()
    {
        _publisher = new(_unitOfWork);
        _recordings.CombinableMeetings.Add(_meetingId);
        var roomPath = _storage.Create(".wav");
        _storage.Files[roomPath] = [.. CombinedRecordingRules.WavHeader(), .. new byte[RecordingRules.RoomBytesPerSecond]];
        _room = new Recording
        {
            MeetingId = _meetingId, Source = RecordingSource.BrowserCapture, Status = RecordingStatus.Available,
            FileName = "ghi-am-phong.wav", ContentType = "audio/wav", StoragePath = roomPath, Takes = [new(0, 1000, Started)],
            ConsentObtainedAt = Started, ConsentPurpose = "Lập biên bản", CreatedById = Guid.CreateVersion7(),
            CreatedAt = Started, UpdatedAt = Now.AddHours(-1),
        };
        _recordings.Recordings.Add(_room);
        var clipPath = _storage.Create(".wav");
        _storage.Files[clipPath] = CombinedRecordingRules.WavHeader();
        _clips.Mixable.Add(new CombinedClip(Started.AddMilliseconds(500), 500, clipPath));
        _clips.LastChange = Now.AddHours(-1).AddMinutes(-1);
    }

    private CombinedRecordingService Service() => new(_recordings, _clips, _unitOfWork, _storage, _publisher, new FixedClock(Now),
        Options.Create(new RecordingOptions()), NullLogger<CombinedRecordingService>.Instance);

    [Fact]
    public async Task The_room_and_the_online_sentences_are_mixed_into_one_recording_saved_in_one_transaction()
    {
        Assert.True(await Service().BuildNextAsync());

        var combined = Assert.Single(_recordings.Recordings, r => r.Source == RecordingSource.Combined);
        Assert.Equal(RecordingStatus.Available, combined.Status);
        Assert.Equal(_room.ConsentPurpose, combined.ConsentPurpose);
        Assert.Equal(_room.CreatedById, combined.CreatedById);
        Assert.Equal(_room.UpdatedAt, combined.UpdatedAt);
        Assert.True(_storage.Files.ContainsKey(combined.StoragePath));
        Assert.Equal([TransactionMode.None, TransactionMode.None, TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1, 1), (_unitOfWork.Commits, _unitOfWork.Saves, _unitOfWork.Discards));
        Assert.Equal(1, Assert.Single(_publisher.Published).CommitsBefore);
    }

    [Fact]
    public async Task A_combined_recording_the_Secretary_erased_is_not_built_again()
    {
        _recordings.Recordings.Add(new Recording
        {
            MeetingId = _meetingId, Source = RecordingSource.Combined, Status = RecordingStatus.Deleted,
            FileName = "ban-ghi-tong-hop.wav", ContentType = "audio/wav", StoragePath = "erased.wav", CreatedAt = Now.AddMinutes(-30),
        });

        Assert.False(await Service().BuildNextAsync());

        Assert.Equal([TransactionMode.None, TransactionMode.None], _unitOfWork.Modes);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task A_storage_failure_rolls_the_build_back_and_announces_nothing()
    {
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        Assert.True(await Service().BuildNextAsync());

        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Equal(0, _unitOfWork.Discards);
        Assert.Empty(_publisher.Published);
    }
}
