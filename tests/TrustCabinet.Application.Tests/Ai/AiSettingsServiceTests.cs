using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Ai;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Ai;

public sealed class AiSettingsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);
    private const string Key = "sk-test-1234567890";

    private readonly FakeAiProviderSettingRepository _settings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAiKeyProtector _protector = new();
    private readonly FakeApiLanguageModelFactory _apis = new();
    private readonly FakeLanguageModel _local = new("qwen-local");
    private readonly FakeAuditService _audit = new();
    private readonly Guid _admin = Guid.CreateVersion7();

    private AiSettingsService Service() => new(_settings, _unitOfWork, _protector, _apis,
        Options.Create(new LanguageModelOptions { BaseUrl = "http://ai.local:11434/v1", Model = "qwen-local" }), _local, _audit,
        new FixedClock(Now), NullLogger<AiSettingsService>.Instance);

    private static AiSettingsInput Api(string? key = Key) => new("Api", "https://api.example.vn/v1", "deepseek-v4", key);

    [Fact]
    public async Task Choosing_the_api_stores_the_key_encrypted_and_shows_only_its_last_four_characters()
    {
        var outcome = await Service().UpdateAsync(Api(), _admin);

        Assert.Equal(AiSettingsFailure.None, outcome.Failure);
        var row = Assert.IsType<AiProviderSetting>(_settings.Row);
        Assert.Equal(AiProviderMode.Api, row.Mode);
        Assert.NotEqual(System.Text.Encoding.UTF8.GetBytes(Key), row.ApiKeyCiphertext);
        Assert.Equal(Key, _protector.Unprotect(row.ApiKeyCiphertext!, AiSettingsService.SettingId));
        Assert.Equal("7890", outcome.Data!.ApiKeyHint);
        Assert.True(outcome.Data.HasApiKey);
        Assert.Equal([AuditActions.AiSettingsUpdated], _audit.Actions);
        Assert.Equal(1, _unitOfWork.Saves);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task Choosing_the_api_without_a_key_is_refused_and_nothing_is_saved()
    {
        var outcome = await Service().UpdateAsync(Api(key: null), _admin);

        Assert.Equal(AiSettingsFailure.Invalid, outcome.Failure);
        Assert.Contains("apiKey", outcome.Errors!.Keys);
        Assert.Null(_settings.Row);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task A_storage_failure_while_saving_is_a_failed_save()
    {
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().UpdateAsync(Api(), _admin);

        Assert.Equal(AiSettingsFailure.SaveFailed, outcome.Failure);
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task Two_first_saves_at_once_answer_the_loser_with_a_failed_save()
    {
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.Unknown);

        var outcome = await Service().UpdateAsync(Api(), _admin);

        Assert.Equal(AiSettingsFailure.SaveFailed, outcome.Failure);
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task A_storage_failure_while_reading_is_a_failed_read()
    {
        _settings.Fail = new PersistenceUnavailableException();

        var outcome = await Service().GetAsync();

        Assert.Equal(AiSettingsFailure.ReadFailed, outcome.Failure);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task Testing_without_a_new_key_calls_the_api_with_the_stored_one_and_saves_nothing()
    {
        await Service().UpdateAsync(Api(), _admin);

        var outcome = await Service().TestAsync(Api(key: null));

        Assert.Equal(AiSettingsFailure.None, outcome.Failure);
        Assert.Equal("deepseek-v4", outcome.Test!.Model);
        var call = Assert.Single(_apis.Created);
        Assert.Equal(("https://api.example.vn/v1", "deepseek-v4", Key, 1, 1024), (call.BaseUrl, call.Model, call.ApiKey,
            call.TimeoutMinutes, call.MaxTokens));
        Assert.Single(_apis.Last!.Systems);
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Testing_with_a_stored_key_that_no_longer_decrypts_asks_for_it_again()
    {
        await Service().UpdateAsync(Api(), _admin);
        _protector.Broken = true;

        var outcome = await Service().TestAsync(Api(key: null));

        Assert.Equal(AiSettingsFailure.Invalid, outcome.Failure);
        Assert.Contains("apiKey", outcome.Errors!.Keys);
        Assert.Empty(_apis.Created);
    }

    [Fact]
    public async Task A_meeting_that_is_not_ordinary_never_reaches_the_api()
    {
        await Service().UpdateAsync(Api(), _admin);
        var provider = new LanguageModelProvider(_settings, _unitOfWork, _local, _protector, _apis,
            Options.Create(new LanguageModelOptions()), NullLogger<LanguageModelProvider>.Instance);

        Assert.Same(_local, await provider.ForAsync(Sensitivity.Internal, default));
        Assert.Empty(_apis.Created);
        var api = await provider.ForAsync(Sensitivity.Normal, default);
        Assert.True(api.Remote);
        Assert.Equal(Key, Assert.Single(_apis.Created).ApiKey);
    }

    [Fact]
    public async Task A_stored_key_that_no_longer_decrypts_keeps_ordinary_meetings_on_the_in_house_model()
    {
        await Service().UpdateAsync(Api(), _admin);
        _protector.Broken = true;
        var provider = new LanguageModelProvider(_settings, _unitOfWork, _local, _protector, _apis,
            Options.Create(new LanguageModelOptions()), NullLogger<LanguageModelProvider>.Instance);

        Assert.Same(_local, await provider.ForAsync(Sensitivity.Normal, default));
        Assert.Empty(_apis.Created);
    }
}
