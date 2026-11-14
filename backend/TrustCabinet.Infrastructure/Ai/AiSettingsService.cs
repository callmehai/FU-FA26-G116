using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TrustCabinet.Domain.Ai;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Summaries;
using TrustCabinet.Infrastructure.Summaries;

namespace TrustCabinet.Infrastructure.Ai;

/// <summary>
/// ADR-0056 — the administrator's "Máy AI" screen: the in-house model or an API for ordinary meetings, the API's address,
/// model and key. The key goes in and is never read back out: the screen only learns whether one is stored and its last
/// four characters.
/// </summary>
public sealed class AiSettingsService(TrustCabinetDbContext db, AiKeyProtector protector, IHttpClientFactory clients,
    IOptions<LanguageModelOptions> options, ILanguageModel local, IAuditService audit, TimeProvider clock,
    ILogger<AiSettingsService> logger)
    : IAiSettingsService
{
    /// <summary>
    /// How long "Thử kết nối" waits for the in-house model: loading a 7B model on a laptop takes a few seconds, while a
    /// machine that is off or off the tailnet would otherwise hold the button for the draft's whole 15-minute timeout.
    /// </summary>
    internal static readonly TimeSpan LocalTestTimeout = TimeSpan.FromSeconds(90);

    /// <summary>The one row: a fixed id, so two first saves at once cannot leave two settings behind.</summary>
    internal static readonly Guid SettingId = Guid.Parse("01a1b200-0000-7000-8000-000000000001");

    public async Task<AiSettingsOutcome> GetAsync(CancellationToken ct = default)
    {
        try
        {
            return new(Data: await ViewAsync(ct));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not read the AI settings");
            return new(AiSettingsFailure.ReadFailed);
        }
    }

    public async Task<AiSettingsOutcome> UpdateAsync(AiSettingsInput input, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var row = await db.AiProviderSettings.FirstOrDefaultAsync(s => s.Id == SettingId, ct);
            var errors = Validate(input, out var mode, out var baseUrl, out var model, out var key);
            if (mode == AiProviderMode.Api && key is null && row?.ApiKeyCiphertext is null)
                errors.TryAdd("apiKey", ["Nhập key của API."]);
            if (mode == AiProviderMode.Api && key == "")
                errors.TryAdd("apiKey", ["Đang dùng API thì không xoá key được."]);
            if (errors.Count > 0) return new(AiSettingsFailure.Invalid, Errors: errors.ToDictionary(e => e.Key, e => e.Value));

            var now = clock.GetUtcNow();
            if (row is null)
            {
                row = new AiProviderSetting { Id = SettingId, CreatedAt = now };
                db.AiProviderSettings.Add(row);
            }
            row.Mode = mode;
            row.ApiBaseUrl = baseUrl;
            row.ApiModel = model;
            var keyChange = key switch
            {
                null => "kept",
                "" => "removed",
                _ => "replaced",
            };
            if (key == "")
            {
                row.ApiKeyCiphertext = null;
                row.ApiKeyHint = "";
            }
            else if (key is not null)
            {
                row.ApiKeyCiphertext = protector.Protect(key, SettingId);
                row.ApiKeyHint = key[^4..];
            }
            row.UpdatedById = actorId;
            row.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            // Where drafts go, and whether the key changed; never the key itself.
            await audit.RecordAsync(AuditActions.AiSettingsUpdated, nameof(AiProviderSetting), SettingId, actorId,
                new { mode = mode.ToString(), apiBaseUrl = baseUrl, apiModel = model, apiKey = keyChange }, ct);
            return new(Data: await ViewAsync(ct));
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            logger.LogWarning(error, "Two first saves of the AI settings at once");
            return new(AiSettingsFailure.SaveFailed);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not save the AI settings");
            return new(AiSettingsFailure.SaveFailed);
        }
    }

    public async Task<AiSettingsOutcome> TestAsync(AiSettingsInput input, CancellationToken ct = default)
    {
        if (input.Mode is nameof(AiProviderMode.Local)) return await TestLocalAsync(ct);
        var errors = Validate(input with { Mode = nameof(AiProviderMode.Api) }, out _, out var baseUrl, out var model, out var key);
        string? stored = null;
        if (key is null or "")
        {
            var row = await db.AiProviderSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == SettingId, ct);
            if (row?.ApiKeyCiphertext is { } ciphertext)
            {
                try
                {
                    stored = protector.Unprotect(ciphertext, SettingId);
                }
                catch (CryptographicException)
                {
                    errors.TryAdd("apiKey", ["Key đã lưu không đọc được nữa. Nhập lại key."]);
                }
            }
            else errors.TryAdd("apiKey", ["Nhập key của API."]);
        }
        if (errors.Count > 0) return new(AiSettingsFailure.Invalid, Errors: errors.ToDictionary(e => e.Key, e => e.Value));

        var api = new OpenAiCompatibleModel(clients, LanguageModelProvider.ApiHttpClientName, baseUrl, model,
            string.IsNullOrEmpty(key) ? stored! : key, timeoutMinutes: 1, maxTokens: 1024, remote: true,
            options.Value.ApiReasoningEffort);
        var watch = Stopwatch.StartNew();
        try
        {
            await api.CompleteAsync("Bạn là trợ lý kiểm tra kết nối.", "Trả lời đúng một từ: OK", ct);
            return new(Test: new AiTestResult(model, watch.ElapsedMilliseconds));
        }
        catch (LanguageModelException failure)
        {
            return new(AiSettingsFailure.TestFailed, Message: failure.Message);
        }
    }

    /// <summary>
    /// The in-house model as the server reaches it for real (its <c>Ai:BaseUrl</c>, over the tailnet), so an
    /// administrator can tell before a meeting whether an Internal, Secret or Top secret draft will be written: those
    /// never go to the API (ADR-0056). Nothing is saved; the form's API fields are ignored.
    /// </summary>
    private async Task<AiSettingsOutcome> TestLocalAsync(CancellationToken ct)
    {
        if (!local.IsConfigured)
            return new(AiSettingsFailure.TestFailed, Message: "Server chưa cấu hình máy local (Ai:BaseUrl, Ai:Model).");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(LocalTestTimeout);
        var watch = Stopwatch.StartNew();
        try
        {
            await local.CompleteAsync("Bạn là trợ lý kiểm tra kết nối.", "Trả lời đúng một từ: OK", limit.Token);
            return new(Test: new AiTestResult(local.Model, watch.ElapsedMilliseconds));
        }
        catch (LanguageModelException failure)
        {
            return new(AiSettingsFailure.TestFailed, Message: failure.Message);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(AiSettingsFailure.TestFailed,
                Message: $"Máy local không trả lời sau {(int)LocalTestTimeout.TotalSeconds} giây. Kiểm tra máy còn bật và Tailscale đang kết nối.");
        }
    }

    private async Task<AiSettingsView> ViewAsync(CancellationToken ct)
    {
        var local = options.Value;
        var row = await db.AiProviderSettings.AsNoTracking().Where(s => s.Id == SettingId)
            .Select(s => new { s.Mode, s.ApiBaseUrl, s.ApiModel, HasKey = s.ApiKeyCiphertext != null, s.ApiKeyHint, s.UpdatedAt,
                UpdatedBy = s.UpdatedBy != null ? s.UpdatedBy.FullName : null })
            .FirstOrDefaultAsync(ct);
        return row is null
            ? new(nameof(AiProviderMode.Local), "", "", false, "", local.Model, local.IsConfigured, null, null)
            : new(row.Mode.ToString(), row.ApiBaseUrl, row.ApiModel, row.HasKey, row.ApiKeyHint, local.Model, local.IsConfigured,
                row.UpdatedAt, row.UpdatedBy);
    }

    /// <summary>
    /// The API's address must be https (the key travels with every call) and at most 500 characters, the model a single
    /// word of at most 100; both are required when the API is chosen and kept as typed otherwise, so switching back to
    /// the in-house model does not lose them. A new key is 8 to 500 characters without spaces.
    /// </summary>
    private static Dictionary<string, string[]> Validate(AiSettingsInput input, out AiProviderMode mode, out string baseUrl,
        out string model, out string? key)
    {
        var errors = new Dictionary<string, string[]>();
        mode = AiProviderMode.Local;
        if (input.Mode is nameof(AiProviderMode.Local)) mode = AiProviderMode.Local;
        else if (input.Mode is nameof(AiProviderMode.Api)) mode = AiProviderMode.Api;
        else errors["mode"] = ["Chọn máy local hoặc API."];

        baseUrl = (input.ApiBaseUrl ?? "").Trim();
        model = (input.ApiModel ?? "").Trim();
        key = input.ApiKey?.Trim();

        if (baseUrl.Length > 0 && (baseUrl.Length > 500 || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)))
            errors["apiBaseUrl"] = ["Địa chỉ API phải là https://… và không quá 500 ký tự."];
        else if (baseUrl.Length == 0 && mode == AiProviderMode.Api)
            errors["apiBaseUrl"] = ["Nhập địa chỉ API."];

        if (model.Length > 100 || model.Any(char.IsWhiteSpace))
            errors["apiModel"] = ["Tên model không có khoảng trắng và không quá 100 ký tự."];
        else if (model.Length == 0 && mode == AiProviderMode.Api)
            errors["apiModel"] = ["Nhập tên model."];

        if (key is { Length: > 0 } && (key.Length is < 8 or > 500 || key.Any(char.IsWhiteSpace)))
            errors["apiKey"] = ["Key dài 8–500 ký tự, không có khoảng trắng."];
        return errors;
    }
}
