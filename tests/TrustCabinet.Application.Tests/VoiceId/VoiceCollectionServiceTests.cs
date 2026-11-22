using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Application.VoiceId;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Application.Tests.VoiceId;

/// <summary>
/// ADR-0071, ADR-0073, ADR-0075: a holder of "Thu giọng mẫu hộ" records a person's Voice ID samples for them, with no
/// meeting, over the unit of work. The meeting Secretary's path of ADR-0060 is gone.
/// </summary>
public sealed class VoiceCollectionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeVoiceIdRepository _repository = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeVoiceEmbedder _embedder = new();
    private readonly FakeAuditService _audit = new();
    private readonly ListCallerAccounts _accounts = new();
    private readonly Guid _person = Guid.CreateVersion7();
    private readonly Guid _collector = Guid.CreateVersion7();

    public VoiceCollectionServiceTests()
    {
        _accounts.Users.AddRange(
            new User { Id = _person, FullName = "Thành viên", Email = "tv@trustcabinet.local", IsActive = true },
            new User { Id = _collector, FullName = "Người thu", Email = "thu@trustcabinet.local", IsActive = true });
    }

    private VoiceCollectionService Service() => new(_repository, _notifications, _unitOfWork, _embedder,
        new FakeVoiceprintProtector(), _audit, new FixedClock(Now), _accounts, NullLogger<VoiceCollectionService>.Instance);

    private Task<VoiceOutcome> Collect(bool personAgreed = true)
        => Service().AddForPersonAsync(_collector, _person, new MemoryStream([1, 2, 3]), "audio/webm", personAgreed);

    private VoiceConsent Consented(string notice = VoiceIdRules.ProxyNoticeVersion)
    {
        var consent = new VoiceConsent
        {
            UserId = _person, GrantedAt = Now.AddDays(-1), NoticeVersion = notice, Purpose = VoiceIdRules.Purpose,
        };
        _repository.Consents.Add(consent);
        return consent;
    }

    private VoiceSample Collected(VoiceConsent consent, Guid? collector = null, Guid? meetingId = null)
    {
        var sample = new VoiceSample
        {
            UserId = _person, ConsentId = consent.Id, EmbeddingModel = "test-model", Ciphertext = [0xE0],
            AudioCiphertext = [7, 0xA0], AudioContentType = "audio/webm", CollectedById = collector ?? _collector,
            MeetingId = meetingId, CreatedAt = Now, UpdatedAt = Now,
        };
        _repository.Samples.Add(sample);
        return sample;
    }

    [Fact]
    public async Task A_sample_taken_with_no_meeting_is_kept_under_the_consent_lock_and_its_owner_is_told()
    {
        Consented();

        var outcome = await Collect();

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        var sample = Assert.Single(_repository.Samples);
        Assert.Equal((_person, (Guid?)_collector, (Guid?)null), (sample.UserId, sample.CollectedById, sample.MeetingId));
        var notice = Assert.Single(_notifications.Added);
        Assert.Equal((_person, NotificationType.VoiceSampleCollected, sample.Id), (notice.UserId, notice.Type, notice.TargetId));
        Assert.Contains("Người thu", notice.Body);
        Assert.Equal([$"lock consent {_person}"], _repository.Steps);
        Assert.Equal(TransactionMode.ReadCommitted, _unitOfWork.Modes[^1]);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
        Assert.Equal([AuditActions.VoiceSampleAdded], _audit.Actions);
    }

    [Theory]
    [InlineData(VoiceIdRules.AutoAssignNoticeVersion)]
    [InlineData(VoiceIdRules.CollectionNoticeVersion)]
    public async Task ADR_0073_a_consent_to_a_notice_before_1_4_lets_nobody_record(string notice)
    {
        Consented(notice);

        Assert.Equal(VoiceFailure.NoConsent, (await Collect()).Failure);
        Assert.Equal(0, _embedder.Calls);
        Assert.Empty(_repository.Samples);
        var view = Assert.IsType<PersonVoiceCollectionView>((await Service().GetPersonAsync(_collector, _person)).Data);
        Assert.False(view.Consented);
        Assert.False(view.CanCollect);
    }

    [Fact]
    public async Task ADR_0075_a_consent_to_1_4_still_allows_a_proxy_sample_under_notice_1_5()
    {
        // 1.5 only takes the meeting Secretary out of 1.4: nothing new is done with the voice of a person on 1.4.
        Assert.Equal("1.5", VoiceIdRules.NoticeVersion);
        Consented("1.4");

        Assert.Equal(VoiceFailure.None, (await Collect()).Failure);
    }

    [Fact]
    public async Task ADR_0073_a_take_the_person_did_not_agree_to_is_refused_before_any_audio_reaches_the_worker()
    {
        Consented();

        Assert.Equal(VoiceFailure.NotConfirmed, (await Collect(personAgreed: false)).Failure);
        Assert.Equal(0, _embedder.Calls);
        Assert.Empty(_repository.Samples);
        Assert.Empty(_notifications.Added);
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task ADR_0073_the_confirmation_is_audited_and_the_person_is_told_how_to_undo_it()
    {
        Consented();

        Assert.Equal(VoiceFailure.None, (await Collect()).Failure);

        Assert.Contains("personAgreed = True", Assert.Single(_audit.Details)?.ToString());
        Assert.Contains("Nếu bạn không đồng ý, hãy xoá mẫu", Assert.Single(_notifications.Added).Body);
    }

    [Fact]
    public async Task ADR_0073_without_a_standing_consent_the_answer_is_no_consent_even_when_the_take_was_confirmed()
    {
        Assert.Equal(VoiceFailure.NoConsent, (await Collect()).Failure);
        Assert.Equal(0, _embedder.Calls);
    }

    [Fact]
    public async Task ADR_0075_one_collector_may_fill_the_persons_whole_limit_of_fifty_and_no_more()
    {
        var consent = Consented();
        for (var i = 0; i < VoiceIdRules.MaxProxySamples - 1; i++) Collected(consent);

        Assert.Equal(VoiceFailure.None, (await Collect()).Failure);
        Assert.Equal(VoiceFailure.LimitReached, (await Collect()).Failure);
        Assert.Equal(VoiceIdRules.MaxProxySamples, _repository.Samples.Count);
    }

    [Fact]
    public async Task A_storage_failure_keeps_nothing_and_is_save_failed()
    {
        Consented();
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        Assert.Equal(VoiceFailure.SaveFailed, (await Collect()).Failure);
        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task A_deactivated_or_unknown_person_is_not_found()
    {
        Consented();
        _accounts.Users.Single(u => u.Id == _person).IsActive = false;

        Assert.Equal(VoiceFailure.NotFound, (await Collect()).Failure);
        Assert.Equal(VoiceFailure.NotFound, (await Service().GetPersonAsync(_collector, Guid.NewGuid())).Failure);
        Assert.Equal(0, _embedder.Calls);
    }

    [Fact]
    public async Task The_person_view_lists_every_proxy_sample_including_one_a_Secretary_took_before_ADR_0075()
    {
        var consent = Consented();
        var legacy = Collected(consent, collector: Guid.CreateVersion7(), meetingId: Guid.CreateVersion7());
        var sample = Collected(consent);

        var view = Assert.IsType<PersonVoiceCollectionView>((await Service().GetPersonAsync(_collector, _person)).Data);
        Assert.True(view.Consented);
        Assert.True(view.CanCollect);
        Assert.Equal([legacy.Id, sample.Id], view.Samples.Select(s => s.Id));

        var audio = await Service().PersonAudioAsync(_collector, _person, legacy.Id);
        Assert.Equal([7], Assert.IsType<VoiceSampleAudio>(audio).Bytes);
        Assert.Equal([AuditActions.VoiceSamplePlayed], _audit.Actions);

        Assert.Equal(VoiceFailure.None, (await Service().DeletePersonSampleAsync(_collector, _person, legacy.Id)).Failure);
        Assert.Equal([sample.Id], _repository.Samples.Select(s => s.Id));
    }
}
