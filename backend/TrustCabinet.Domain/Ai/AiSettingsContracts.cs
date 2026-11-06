using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Domain.Ai;

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
