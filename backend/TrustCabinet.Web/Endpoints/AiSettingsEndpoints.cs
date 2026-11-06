using TrustCabinet.Domain.Ai;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// ADR-0056 — the administrator's "Cấu hình AI" screen: which model writes the AI drafts of ordinary meetings, the in-house
/// one (ADR-0054) or an OpenAI-compatible API, and that API's address, model and key. Behind <c>AdminUsers</c>, like the
/// other screens that administer the system. The key is accepted, never returned.
/// </summary>
public static class AiSettingsEndpoints
{
    private const string Msg408Saved = "MSG408";
    private const string Msg409Invalid = "MSG409";
    private const string Msg410ReadFailed = "MSG410";
    private const string Msg411SaveFailed = "MSG411";
    private const string Msg412TestPassed = "MSG412";
    private const string Msg413TestFailed = "MSG413";

    internal sealed record SettingsBody(string? Mode, string? ApiBaseUrl, string? ApiModel, string? ApiKey);

    public static void MapAiSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/ai").WithTags("AI").RequireAdminUsers();

        group.MapGet("", async (IAiSettingsService service, CancellationToken ct) => Map(await service.GetAsync(ct)));
        group.MapPut("", async (SettingsBody body, HttpContext context, IAiSettingsService service, CancellationToken ct)
            => Map(await service.UpdateAsync(Input(body), CapabilityGate.GatedUserId(context.User), ct), Msg408Saved));
        group.MapPost("/test", async (SettingsBody body, IAiSettingsService service, CancellationToken ct)
            => Map(await service.TestAsync(Input(body), ct)));
    }

    private static AiSettingsInput Input(SettingsBody body) => new(body.Mode, body.ApiBaseUrl, body.ApiModel, body.ApiKey);

    private static IResult Map(AiSettingsOutcome result, string? successCode = null)
    {
        if (result.Failure != AiSettingsFailure.None)
        {
            var (status, code, message) = result.Failure switch
            {
                AiSettingsFailure.Invalid => (400, Msg409Invalid, "Cấu hình AI không hợp lệ."),
                AiSettingsFailure.ReadFailed => (500, Msg410ReadFailed, "Không tải được cấu hình AI."),
                // The API's own words: refused key, unknown model, unreachable address.
                AiSettingsFailure.TestFailed => (502, Msg413TestFailed, result.Message ?? "Không gọi được API AI."),
                _ => (503, Msg411SaveFailed, "Không lưu được cấu hình AI. Vui lòng thử lại."),
            };
            return Results.Json(new { code, message, errors = result.Errors }, statusCode: status);
        }
        if (result.Test is { } test)
            return Results.Ok(new { code = Msg412TestPassed, message = $"Kết nối được {test.Model}.", test.Model, test.ElapsedMs });
        return successCode is null
            ? Results.Ok(result.Data)
            : Results.Ok(new { code = successCode, message = "Đã lưu cấu hình AI.", settings = result.Data });
    }
}
