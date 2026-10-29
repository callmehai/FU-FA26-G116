using Microsoft.Net.Http.Headers;
using TrustCabinet.Domain.Recordings;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// SCRUM-35 — the caller's own Voice ID: consent, enrolment samples, withdrawal. There is no route that names another
/// person: nobody can enrol, read or keep someone else's voice.
/// </summary>
public static class VoiceIdEndpoints
{
    private const string Msg292UnsupportedAudio = "MSG292";
    private const string Msg305ConsentGranted = "MSG305";
    private const string Msg306ConsentWithdrawn = "MSG306";
    private const string Msg307SampleAdded = "MSG307";
    private const string Msg308NoConsent = "MSG308";
    private const string Msg309SampleRejected = "MSG309";
    private const string Msg310SampleLimit = "MSG310";
    private const string Msg311SampleDeleted = "MSG311";
    private const string Msg312EngineUnavailable = "MSG312";
    private const string Msg313Invalid = "MSG313";
    private const string Msg314ReadFailed = "MSG314";
    private const string Msg315SaveFailed = "MSG315";

    private sealed record ConsentBody(string? NoticeVersion);

    public static void MapVoiceIdEndpoints(this IEndpointRouteBuilder app)
    {
        var voice = app.MapGroup("/api/me/voice").RequireActiveUser(Error(500, Msg314ReadFailed, "Không tải được thông tin giọng nói."));
        voice.MapGet("", async (HttpContext context, IVoiceIdService service, CancellationToken ct)
            => Map(await service.GetAsync(CapabilityGate.GatedUserId(context.User), ct)));
        voice.MapPost("/consent", async (ConsentBody body, HttpContext context, IVoiceIdService service, CancellationToken ct)
            => Map(await service.GrantAsync(CapabilityGate.GatedUserId(context.User), body.NoticeVersion, ct),
                Msg305ConsentGranted, "Đã bật nhận diện giọng nói cho tài khoản của bạn."));
        voice.MapDelete("/consent", async (HttpContext context, IVoiceIdService service, CancellationToken ct)
            => Map(await service.WithdrawAsync(CapabilityGate.GatedUserId(context.User), ct),
                Msg306ConsentWithdrawn, "Đã tắt nhận diện giọng nói và xoá toàn bộ giọng mẫu."));
        voice.MapPost("/samples", AddSampleAsync);
        // The owner hears their own sample back; anyone else's, or one not kept, is a plain 404.
        voice.MapGet("/samples/{sampleId:guid}/audio", async (Guid sampleId, HttpContext context, IVoiceIdService service,
            CancellationToken ct) =>
        {
            var audio = await service.SampleAudioAsync(CapabilityGate.GatedUserId(context.User), sampleId, ct);
            if (audio is null) return Results.NotFound();
            context.Response.Headers.CacheControl = "no-store";
            return Results.File(audio.Bytes, audio.ContentType, enableRangeProcessing: true);
        });
        voice.MapDelete("/samples/{sampleId:guid}", async (Guid sampleId, HttpContext context, IVoiceIdService service, CancellationToken ct)
            => Map(await service.DeleteSampleAsync(CapabilityGate.GatedUserId(context.User), sampleId, ct),
                Msg311SampleDeleted, "Đã xoá giọng mẫu."));
    }

    /// <summary>One short recording in a multipart form, read into memory: a sample is at most a minute of speech.</summary>
    private static async Task<IResult> AddSampleAsync(HttpContext context, IVoiceIdService service, CancellationToken ct)
    {
        if (!context.Request.HasFormContentType) return Error(400, Msg313Invalid, "Yêu cầu phải gửi dạng multipart/form-data có tệp giọng mẫu.");
        if (context.Request.ContentLength > VoiceIdRules.MaxSampleBytes + 64 * 1024)
            return Rejected("too_long");
        IFormFile? file;
        try
        {
            file = (await context.Request.ReadFormAsync(ct)).Files.GetFile("file");
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException && !ct.IsCancellationRequested)
        {
            return Error(400, Msg313Invalid, "Không đọc được biểu mẫu tải lên.");
        }
        if (file is null || file.Length == 0) return Error(400, Msg313Invalid, "Chưa có tệp giọng mẫu.");
        if (file.Length > VoiceIdRules.MaxSampleBytes) return Rejected("too_long");
        var type = MediaTypeHeaderValue.TryParse(file.ContentType, out var media) ? media.MediaType.Value : null;
        if (type is null || !RecordingRules.Extensions.ContainsKey(type))
            return Error(415, Msg292UnsupportedAudio, "Định dạng âm thanh không được hỗ trợ. Dùng mp3, m4a, wav, webm hoặc ogg.");
        await using var audio = file.OpenReadStream();
        return Map(await service.AddSampleAsync(CapabilityGate.GatedUserId(context.User), audio, type, ct),
            Msg307SampleAdded, "Đã lưu giọng mẫu.", created: true);
    }

    private static IResult Rejected(string reason) => Results.Json(new
    {
        code = Msg309SampleRejected,
        message = VoiceIdRules.RejectionMessage(reason),
        reason,
        errors = new Dictionary<string, string[]> { ["file"] = [VoiceIdRules.RejectionMessage(reason)] },
    }, statusCode: StatusCodes.Status422UnprocessableEntity);

    private static IResult Map(VoiceOutcome result, string? successCode = null, string? successMessage = null, bool created = false)
    {
        if (result.Failure == VoiceFailure.NotFound) return Results.NotFound();
        if (result.Failure == VoiceFailure.Inactive) return Results.Forbid();
        if (result.Failure == VoiceFailure.Rejected) return Rejected(result.Reason ?? "rejected");
        if (result.Failure != VoiceFailure.None)
        {
            var (status, code, message) = result.Failure switch
            {
                VoiceFailure.NoConsent => (409, Msg308NoConsent, "Cần đồng ý nhận diện giọng nói trước khi ghi giọng mẫu."),
                VoiceFailure.LimitReached => (409, Msg310SampleLimit, $"Đã đủ {VoiceIdRules.MaxSamples} giọng mẫu. Xoá bớt rồi ghi lại."),
                VoiceFailure.EngineUnavailable => (503, Msg312EngineUnavailable, "Máy chủ nhận dạng giọng nói chưa sẵn sàng. Vui lòng thử lại sau."),
                VoiceFailure.Invalid => (400, Msg313Invalid, "Thông tin gửi lên không hợp lệ."),
                VoiceFailure.ReadFailed => (500, Msg314ReadFailed, "Không tải được thông tin giọng nói."),
                _ => (503, Msg315SaveFailed, "Không lưu được thay đổi. Vui lòng thử lại."),
            };
            return Results.Json(new { code, message, errors = result.Errors }, statusCode: status);
        }
        if (successCode is null) return Results.Ok(result.Data);
        var body = new { code = successCode, message = successMessage, data = result.Data };
        return created ? Results.Json(body, statusCode: StatusCodes.Status201Created) : Results.Ok(body);
    }

    private static IResult Error(int status, string code, string message) => Results.Json(new { code, message }, statusCode: status);
}
