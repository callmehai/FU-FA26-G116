using TrustCabinet.Application.Summaries;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Ai;

/// <summary>Picks the model that may write a meeting's draft (ADR-0056).</summary>
public interface ILanguageModelProvider
{
    /// <summary>
    /// The API when an administrator chose it and the meeting is <see cref="Sensitivity.Normal"/>; the in-house model
    /// otherwise. A meeting of any other sensitivity never leaves the in-house model.
    /// </summary>
    Task<ILanguageModel> ForAsync(Sensitivity sensitivity, CancellationToken ct);
}

/// <summary>What the administrator sees: never the key, only whether one is stored and its last four characters.</summary>
public sealed record AiSettingsView(
    /// <summary>"Local" or "Api", as the form sends it back.</summary>
    string Mode,
    string ApiBaseUrl,
    string ApiModel,
    bool HasApiKey,
    string ApiKeyHint,
    string LocalModel,
    bool LocalConfigured,
    DateTimeOffset? UpdatedAt,
    string? UpdatedByName);

/// <summary>
/// The form. <see cref="ApiKey"/> null keeps the stored key, empty removes it, anything else replaces it.
/// </summary>
public sealed record AiSettingsInput(string? Mode, string? ApiBaseUrl, string? ApiModel, string? ApiKey);

public sealed record AiTestResult(string Model, long ElapsedMs);

public enum AiSettingsFailure
{
    None,
    Invalid,
    ReadFailed,
    SaveFailed,
    TestFailed,
}

public sealed record AiSettingsOutcome(
    AiSettingsFailure Failure = AiSettingsFailure.None,
    AiSettingsView? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    AiTestResult? Test = null,
    string? Message = null);

/// <summary>The administrator's "Máy AI" screen (ADR-0056).</summary>
public interface IAiSettingsService
{
    Task<AiSettingsOutcome> GetAsync(CancellationToken ct = default);

    Task<AiSettingsOutcome> UpdateAsync(AiSettingsInput input, Guid actorId, CancellationToken ct = default);

    /// <summary>One short call to the API as the form stands (the stored key when the form gives none), saving nothing.</summary>
    Task<AiSettingsOutcome> TestAsync(AiSettingsInput input, CancellationToken ct = default);
}

/// <summary>Encrypts the AI API key at rest, bound to the settings row it belongs to (ADR-0056).</summary>
public interface IAiKeyProtector
{
    byte[] Protect(string apiKey, Guid settingId);

    /// <summary>Throws <see cref="System.Security.Cryptography.CryptographicException"/> when the key cannot be read back.</summary>
    string Unprotect(byte[] ciphertext, Guid settingId);
}

/// <summary>Builds the client of an OpenAI-compatible API an administrator chose (ADR-0056); nothing is sent until it is called.</summary>
public interface IApiLanguageModelFactory
{
    /// <summary>A model whose <see cref="ILanguageModel.Remote"/> is true.</summary>
    ILanguageModel Create(string baseUrl, string model, string apiKey, int timeoutMinutes, int maxTokens,
        string? reasoningEffort);
}

/// <summary>The administrator's choice of model: at most one row, <c>ai_provider_settings</c>.</summary>
public interface IAiProviderSettingRepository
{
    /// <summary>The row with this id, tracked when <paramref name="tracked"/>; null when there is none.</summary>
    Task<AiProviderSetting?> FindAsync(Guid id, bool tracked, CancellationToken ct);

    /// <summary>The one row whatever its id, untracked; null when there is none.</summary>
    Task<AiProviderSetting?> FindCurrentAsync(CancellationToken ct);

    /// <summary>What the screen shows of the row with this id, never the key; null when there is none.</summary>
    Task<AiSettingsRow?> FindViewAsync(Guid id, CancellationToken ct);

    void Add(AiProviderSetting setting);
}

/// <summary>The stored settings as <see cref="IAiProviderSettingRepository.FindViewAsync"/> reads them.</summary>
public sealed record AiSettingsRow(AiProviderMode Mode, string ApiBaseUrl, string ApiModel, bool HasKey, string ApiKeyHint,
    DateTimeOffset UpdatedAt, string? UpdatedBy);
