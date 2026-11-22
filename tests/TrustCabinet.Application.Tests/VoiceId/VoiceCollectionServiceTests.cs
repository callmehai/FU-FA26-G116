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
    private readonly ListCallerAccounts _accounts = new();
    private readonly Guid _collector = Guid.CreateVersion7();

    public VoiceCollectionServiceTests()
    {
        _meeting = new(new FakeMeetingRepository(), MeetingMode.InPerson, Now);
        _repository.Meetings.Add(_meeting.Meeting);
        _accounts.Users.AddRange(
            new User { Id = _meeting.Member, FullName = "Thành viên", Email = "tv@trustcabinet.local", IsActive = true },
            new User { Id = _collector, FullName = "Người thu", Email = "thu@trustcabinet.local", IsActive = true });
    }

    private VoiceCollectionService PersonService() => new(_repository, _notifications, _unitOfWork, _embedder,
        new FakeVoiceprintProtector(), _audit, new FixedClock(Now), _accounts, NullLogger<VoiceCollectionService>.Instance);

    private Task<VoiceOutcome> CollectForPerson(bool personAgreed = true)
        => PersonService().AddForPersonAsync(_collector, _meeting.Member, new MemoryStream([1, 2, 3]), "audio/webm", personAgreed);

    private VoiceCollectionService Service() => new(_repository, _notifications, _unitOfWork, _embedder,
        new FakeVoiceprintProtector(), _audit, new FixedClock(Now), new NoCallerAccounts(), NullLogger<VoiceCollectionService>.Instance);

    private Task<VoiceOutcome> Collect(Guid? actor = null, bool personAgreed = true)
        => Service().AddAsync(_meeting.Meeting.Id, actor ?? _meeting.Secretary, _meeting.Member, new MemoryStream([1, 2, 3]),
            "audio/webm", personAgreed);

    private VoiceConsent Consented(string notice = VoiceIdRules.ProxyNoticeVersion)
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

    [Fact]
    public async Task ADR_0071_a_sample_taken_with_no_meeting_is_kept_under_the_consent_lock_and_its_owner_is_told()
    {
        Consented(VoiceIdRules.ProxyNoticeVersion);

        var outcome = await CollectForPerson();

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        var sample = Assert.Single(_repository.Samples);
        Assert.Equal((_meeting.Member, (Guid?)_collector, (Guid?)null), (sample.UserId, sample.CollectedById, sample.MeetingId));
        var notice = Assert.Single(_notifications.Added);
        Assert.Contains("Người thu", notice.Body);
        Assert.Equal([$"lock consent {_meeting.Member}"], _repository.Steps);
        Assert.Equal(TransactionMode.ReadCommitted, _unitOfWork.Modes[^1]);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
        Assert.Equal([AuditActions.VoiceSampleAdded], _audit.Actions);
    }

    [Fact]
    public async Task ADR_0073_a_consent_to_notice_1_3_lets_nobody_record_in_a_meeting_or_outside_one()
    {
        // Lộc 21/11: the users screen said "has not agreed to the new terms" while the meeting screen recorded the same
        // person. One rule now: no proxy sample under a notice that does not name recording outside a meeting.
        Consented(VoiceIdRules.CollectionNoticeVersion);

        Assert.Equal(VoiceFailure.NoConsent, (await Collect()).Failure);
        Assert.Equal(VoiceFailure.NoConsent, (await CollectForPerson()).Failure);
        Assert.Equal(0, _embedder.Calls);
        Assert.Empty(_repository.Samples);
        var view = Assert.IsType<VoiceCollectionView>((await Service().ListAsync(_meeting.Meeting.Id, _meeting.Secretary)).Data);
        Assert.False(Assert.Single(view.People, p => p.UserId == _meeting.Member).Consented);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ADR_0073_a_take_the_person_did_not_agree_to_is_refused_before_any_audio_reaches_the_worker(bool inMeeting)
    {
        Consented();

        var outcome = inMeeting ? await Collect(personAgreed: false) : await CollectForPerson(personAgreed: false);

        Assert.Equal(VoiceFailure.NotConfirmed, outcome.Failure);
        Assert.Equal(0, _embedder.Calls);
        Assert.Empty(_repository.Samples);
        Assert.Empty(_notifications.Added);
        Assert.Empty(_audit.Actions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ADR_0073_the_confirmation_is_audited_and_the_person_is_told_how_to_undo_it(bool inMeeting)
    {
        Consented();

        Assert.Equal(VoiceFailure.None, (inMeeting ? await Collect() : await CollectForPerson()).Failure);

        Assert.Contains("personAgreed = True", Assert.Single(_audit.Details)?.ToString());
        Assert.Contains("Nếu bạn không đồng ý, hãy xoá mẫu", Assert.Single(_notifications.Added).Body);
    }

    [Fact]
    public async Task ADR_0073_without_a_standing_consent_the_answer_is_no_consent_even_when_the_take_was_confirmed()
    {
        Assert.Equal(VoiceFailure.NoConsent, (await Collect()).Failure);
        Assert.Equal(VoiceFailure.NoConsent, (await CollectForPerson()).Failure);
        Assert.Equal(0, _embedder.Calls);
    }

    [Fact]
    public async Task ADR_0071_a_deactivated_or_unknown_person_is_not_found()
    {
        Consented(VoiceIdRules.ProxyNoticeVersion);
        _accounts.Users.Single(u => u.Id == _meeting.Member).IsActive = false;

        Assert.Equal(VoiceFailure.NotFound, (await CollectForPerson()).Failure);
        Assert.Equal(VoiceFailure.NotFound, (await PersonService().GetPersonAsync(_collector, Guid.NewGuid())).Failure);
        Assert.Equal(0, _embedder.Calls);
    }

    [Fact]
    public async Task ADR_0071_the_person_view_lists_every_proxy_sample_and_hearing_one_is_audited()
    {
        var sample = Collected(Consented(VoiceIdRules.ProxyNoticeVersion));

        var view = Assert.IsType<PersonVoiceCollectionView>((await PersonService().GetPersonAsync(_collector, _meeting.Member)).Data);
        Assert.True(view.Consented);
        Assert.True(view.CanCollect);
        Assert.Equal(sample.Id, Assert.Single(view.Samples).Id);

        var audio = await PersonService().PersonAudioAsync(_collector, _meeting.Member, sample.Id);
        Assert.Equal([7], Assert.IsType<VoiceSampleAudio>(audio).Bytes);
        Assert.Equal([AuditActions.VoiceSamplePlayed], _audit.Actions);

        Assert.Equal(VoiceFailure.None, (await PersonService().DeletePersonSampleAsync(_collector, _meeting.Member, sample.Id)).Failure);
        Assert.Empty(_repository.Samples);
    }
}
