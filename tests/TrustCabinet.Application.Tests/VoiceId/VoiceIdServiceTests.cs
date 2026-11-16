using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Application.VoiceId;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Application.Tests.VoiceId;

/// <summary>SCRUM-180 (CA-15): a person's own Voice ID (SCRUM-35) — consent, samples, withdrawal — over the unit of work.</summary>
public sealed class VoiceIdServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly Guid _user = Guid.CreateVersion7();
    private readonly FakeVoiceIdRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeVoiceEmbedder _embedder = new();
    private readonly FakeVoiceprintProtector _protector = new();
    private readonly FakeAuditService _audit = new();

    private VoiceIdService Service()
    {
        var clock = new FixedClock(Now);
        return new(_repository, _unitOfWork, new VoiceIdErasure(_repository, _unitOfWork, _audit, clock), _embedder, _protector,
            _audit, clock, Options.Create(new VoiceIdOptions { Controller = "Văn phòng" }), NullLogger<VoiceIdService>.Instance);
    }

    private VoiceConsent Consented(string notice = VoiceIdRules.NoticeVersion)
    {
        var consent = new VoiceConsent { UserId = _user, GrantedAt = Now.AddDays(-1), NoticeVersion = notice, Purpose = VoiceIdRules.Purpose };
        _repository.Consents.Add(consent);
        return consent;
    }

    private static MemoryStream Audio() => new([1, 2, 3]);

    [Fact]
    public async Task Agreeing_records_the_consent_and_its_audit_in_one_transaction()
    {
        var outcome = await Service().GrantAsync(_user, VoiceIdRules.NoticeVersion);

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        Assert.Equal(VoiceIdRules.NoticeVersion, Assert.Single(_repository.Consents).NoticeVersion);
        Assert.Equal([AuditActions.VoiceConsentGranted], _audit.Actions);
        Assert.Equal(TransactionMode.ReadCommitted, _unitOfWork.Modes[0]);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
        Assert.NotNull(Assert.IsType<VoiceProfile>(outcome.Data).Consent);
    }

    [Fact]
    public async Task Two_agreements_at_once_answer_the_loser_as_agreed()
    {
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.Unknown);

        var outcome = await Service().GrantAsync(_user, VoiceIdRules.NoticeVersion);

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        Assert.IsType<VoiceProfile>(outcome.Data);
        Assert.Equal(1, _unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task A_storage_failure_while_agreeing_is_save_failed()
    {
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().GrantAsync(_user, VoiceIdRules.NoticeVersion);

        Assert.Equal(VoiceFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task Withdrawing_locks_the_consent_first_then_erases_every_sample_and_suggestion_in_one_transaction()
    {
        var consent = Consented();
        _repository.Samples.Add(new VoiceSample { UserId = _user, ConsentId = consent.Id, EmbeddingModel = "test-model", Ciphertext = [1] });

        var outcome = await Service().WithdrawAsync(_user);

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        Assert.Equal([$"lock consent {_user}", "delete samples", "clear suggestions"], _repository.Steps);
        Assert.Empty(_repository.Samples);
        Assert.Equal(Now, consent.WithdrawnAt);
        Assert.Equal([AuditActions.VoiceConsentWithdrawn], _audit.Actions);
        Assert.Equal(TransactionMode.ReadCommitted, _unitOfWork.Modes[0]);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
    }

    [Fact]
    public async Task A_sample_is_refused_without_consent_before_the_audio_reaches_the_worker()
    {
        var outcome = await Service().AddSampleAsync(_user, Audio(), "audio/webm");

        Assert.Equal(VoiceFailure.NoConsent, outcome.Failure);
        Assert.Equal(0, _embedder.Calls);
        Assert.Empty(_repository.Samples);
    }

    [Fact]
    public async Task A_sample_is_kept_encrypted_under_the_consent_lock_in_one_transaction()
    {
        Consented();

        var outcome = await Service().AddSampleAsync(_user, Audio(), "audio/webm");

        Assert.Equal(VoiceFailure.None, outcome.Failure);
        var sample = Assert.Single(_repository.Samples);
        Assert.Equal([0xE0], sample.Ciphertext);
        Assert.Equal([1, 2, 3, 0xA0], sample.AudioCiphertext);
        Assert.Equal([$"lock consent {_user}"], _repository.Steps);
        Assert.Equal([TransactionMode.None, TransactionMode.None, TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
        Assert.Equal([AuditActions.VoiceSampleAdded], _audit.Actions);
    }

    [Fact]
    public async Task A_storage_failure_while_keeping_a_sample_is_save_failed_and_rolled_back()
    {
        Consented();
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().AddSampleAsync(_user, Audio(), "audio/webm");

        Assert.Equal(VoiceFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }
}
