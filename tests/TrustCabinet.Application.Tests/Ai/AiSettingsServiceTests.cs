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

    private AiSettingsService Service(ILanguageModel? local = null) => new(_settings, _unitOfWork, _protector, _apis,
        Options.Create(new LanguageModelOptions { BaseUrl = "http://ai.local:11434/v1", Model = "qwen-local" }), local ?? _local,
        _audit, new FixedClock(Now), NullLogger<AiSettingsService>.Instance);

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

    [Fact]
    public async Task Testing_the_local_model_with_a_backup_asks_both_machines_and_says_which_answered()
    {
        var machines = new TwoMachines(new LocalMachineCheck(false, 0, "Không kết nối được."), new LocalMachineCheck(true, 420, null));

        var outcome = await Service(machines).TestAsync(new AiSettingsInput("Local", null, null, null));

        Assert.Equal(AiSettingsFailure.None, outcome.Failure);
        Assert.Equal(420, outcome.Test!.ElapsedMs);
        Assert.Equal("Máy chính: Không kết nối được. Máy dự phòng sẽ viết thay, kết nối được qwen-local.", outcome.Test.Message);
        Assert.Equal(1, machines.Checks);
    }

    [Fact]
    public async Task Testing_the_local_model_fails_only_when_neither_machine_answers()
    {
        var machines = new TwoMachines(new LocalMachineCheck(false, 0, "Hết thời gian."), new LocalMachineCheck(true, 0, "Tắt máy."));

        var outcome = await Service(machines).TestAsync(new AiSettingsInput("Local", null, null, null));

        Assert.Equal(AiSettingsFailure.TestFailed, outcome.Failure);
        Assert.Equal("Máy chính: Hết thời gian. Máy dự phòng: Tắt máy.", outcome.Message);
    }

    /// <summary>The in-house model with a backup machine, each answering "Thử kết nối" as given.</summary>
    private sealed class TwoMachines(LocalMachineCheck main, LocalMachineCheck backup) : ILanguageModel, ILocalModelMachines
    {
        public int Checks { get; private set; }
        public bool IsConfigured => true;
        public string Model => "qwen-local";
        public bool HasBackup => true;

        public Task<string> CompleteAsync(string system, string user, CancellationToken ct) => Task.FromResult("ok");

        public Task<IReadOnlyList<LocalMachineCheck>> CheckAsync(TimeSpan timeout, CancellationToken ct)
        {
            Checks++;
            return Task.FromResult<IReadOnlyList<LocalMachineCheck>>([main, backup]);
        }
    }
}
