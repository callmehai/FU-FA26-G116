using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Application.Tests.Recordings;
using TrustCabinet.Application.VoiceId;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Application.Tests.VoiceId;

/// <summary>SCRUM-180 (CA-15): the Secretary records Voice ID samples for the meeting's people (ADR-0060) over the unit of work.</summary>
public sealed class VoiceCollectionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeVoiceIdRepository _repository = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeVoiceEmbedder _embedder = new();
    private readonly FakeAuditService _audit = new();
    private readonly RecordingTestMeeting _meeting;

    public VoiceCollectionServiceTests()
    {
        _meeting = new(new FakeMeetingRepository(), MeetingMode.InPerson, Now);
        _repository.Meetings.Add(_meeting.Meeting);
    }

    private VoiceCollectionService Service() => new(_repository, _notifications, _unitOfWork, _embedder,
        new FakeVoiceprintProtector(), _audit, new FixedClock(Now), NullLogger<VoiceCollectionService>.Instance);

    private Task<VoiceOutcome> Collect(Guid? actor = null)
        => Service().AddAsync(_meeting.Meeting.Id, actor ?? _meeting.Secretary, _meeting.Member, new MemoryStream([1, 2, 3]), "audio/webm");

    private VoiceConsent Consented(string notice = VoiceIdRules.CollectionNoticeVersion)
    {
        var consent = new VoiceConsent
        {
            UserId = _meeting.Member, GrantedAt = Now.AddDays(-1), NoticeVersion = notice, Purpose = VoiceIdRules.Purpose,
        };
        _repository.Consents.Add(consent);
        return consent;
    }

    private VoiceSample Collected(VoiceConsent consent)
    {
        var sample = new VoiceSample
        {
            UserId = _meeting.Member, ConsentId = consent.Id, EmbeddingModel = "test-model", Ciphertext = [0xE0],
            AudioCiphertext = [7, 0xA0], AudioContentType = "audio/webm", CollectedById = _meeting.Secretary,
            MeetingId = _meeting.Meeting.Id, CreatedAt = Now, UpdatedAt = Now,
        };
        _repository.Samples.Add(sample);
        return sample;
    }

    [Fact]
    public async Task A_sample_and_the_notice_to_its_owner_are_kept_together_under_the_consent_lock()
    {
        Consented();

        var outcome = await Collect();

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        var sample = Assert.Single(_repository.Samples);
        Assert.Equal((_meeting.Member, _meeting.Secretary), (sample.UserId, sample.CollectedById));
        var notice = Assert.Single(_notifications.Added);
        Assert.Equal((_meeting.Member, NotificationType.VoiceSampleCollected, sample.Id), (notice.UserId, notice.Type, notice.TargetId));
        Assert.Equal([$"lock consent {_meeting.Member}"], _repository.Steps);
        Assert.Equal(TransactionMode.ReadCommitted, _unitOfWork.Modes[^1]);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
        Assert.Equal([AuditActions.VoiceSampleAdded], _audit.Actions);
    }

    [Fact]
    public async Task A_consent_to_an_older_notice_lets_no_Secretary_record_and_no_audio_reaches_the_worker()
    {
        Consented(VoiceIdRules.AutoAssignNoticeVersion);

        var outcome = await Collect();

        Assert.Equal(VoiceFailure.NoConsent, outcome.Failure);
        Assert.Equal(0, _embedder.Calls);
        Assert.Empty(_notifications.Added);
    }

    [Fact]
    public async Task Someone_who_is_not_the_meetings_Secretary_is_answered_as_if_it_did_not_exist()
    {
        Consented();

        var outcome = await Collect(_meeting.Member);

        Assert.Equal(VoiceFailure.NotFound, outcome.Failure);
        Assert.Equal(0, _embedder.Calls);
    }

    [Fact]
    public async Task A_storage_failure_keeps_nothing_and_is_save_failed()
    {
        Consented();
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Collect();

        Assert.Equal(VoiceFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task Hearing_a_sample_is_audited_before_its_bytes_are_handed_out()
    {
        var sample = Collected(Consented());

        var audio = await Service().AudioAsync(_meeting.Meeting.Id, _meeting.Secretary, sample.Id);

        Assert.Equal([7], Assert.IsType<VoiceSampleAudio>(audio).Bytes);
        Assert.Equal([AuditActions.VoiceSamplePlayed], _audit.Actions);
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact]
    public async Task The_list_shows_who_consented_and_the_samples_taken_in_this_meeting()
    {
        Collected(Consented());

        var view = Assert.IsType<VoiceCollectionView>((await Service().ListAsync(_meeting.Meeting.Id, _meeting.Secretary)).Data);

        var member = Assert.Single(view.People, p => p.UserId == _meeting.Member);
        Assert.True(member.Consented);
        Assert.Single(member.Samples);
        Assert.False(Assert.Single(view.People, p => p.UserId == _meeting.Secretary).Consented);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task Deleting_a_sample_taken_here_removes_it_and_audits_it()
    {
        var sample = Collected(Consented());

        var outcome = await Service().DeleteAsync(_meeting.Meeting.Id, _meeting.Secretary, sample.Id);

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        Assert.Empty(_repository.Samples);
        Assert.Equal([AuditActions.VoiceSampleDeleted], _audit.Actions);
        Assert.Equal(1, _unitOfWork.Saves);
    }
}
