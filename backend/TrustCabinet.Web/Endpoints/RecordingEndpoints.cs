using System.Globalization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Recordings;
using TrustCabinet.Infrastructure.Recordings;

namespace TrustCabinet.Web.Endpoints;

/// <summary>SCRUM-29 — store, list, play and erase the recordings of a sitting.</summary>
public static class RecordingEndpoints
{
    private const string Msg290Uploaded = "MSG290";
    private const string Msg291Invalid = "MSG291";
    private const string Msg292UnsupportedAudio = "MSG292";
    private const string Msg293TooLarge = "MSG293";
    private const string Msg294UploadLocked = "MSG294";
    private const string Msg295NotSecretary = "MSG295";
    private const string Msg296Erased = "MSG296";
    private const string Msg297ReadFailed = "MSG297";
    private const string Msg298SaveFailed = "MSG298";
    private const int MaxFieldBytes = 4 * 1024;

    public static void MapRecordingEndpoints(this IEndpointRouteBuilder app)
    {
        var meeting = app.MapGroup("/api/meetings/{meetingId:guid}/recordings")
            .RequireActiveUser(Error(500, Msg297ReadFailed, "Không tải được danh sách bản ghi."));
        meeting.MapGet("", async (Guid meetingId, HttpContext context, IRecordingService service, CancellationToken ct)
            => Map(await service.ListAsync(meetingId, CapabilityGate.GatedUserId(context.User), ct)));
        meeting.MapPost("", UploadAsync);

        var recording = app.MapGroup("/api/recordings/{recordingId:guid}")
            .RequireActiveUser(Error(500, Msg297ReadFailed, "Không tải được bản ghi."));
        recording.MapGet("/playback", async (Guid recordingId, HttpContext context, IRecordingService service, CancellationToken ct)
            => Map(await service.PlaybackAsync(recordingId, CapabilityGate.GatedUserId(context.User), ct)));
        recording.MapDelete("", async (Guid recordingId, HttpContext context, IRecordingService service, CancellationToken ct)
            => Map(await service.EraseAsync(recordingId, CapabilityGate.GatedUserId(context.User), ct), Msg296Erased));

        // An <audio> element cannot send a bearer token, so this one route is reached through a signed, expiring URL
        // handed out by /playback above — which is where the caller's right to listen was checked.
        app.MapGet("/api/recordings/{recordingId:guid}/audio", async (Guid recordingId, long? expires, string? signature,
            IRecordingService service, IRecordingStorage storage, IRecordingPlaybackSigner signer, TimeProvider clock,
            HttpContext context, CancellationToken ct) =>
        {
            if (expires is not { } until || string.IsNullOrEmpty(signature) || !signer.Verify(recordingId, until, signature, clock.GetUtcNow()))
                return Results.NotFound();
            if (await service.FindPlayableAsync(recordingId, ct) is not { } file) return Results.NotFound();
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.Stream(storage.OpenRead(file.StoragePath), file.ContentType, enableRangeProcessing: true);
        });
    }

    /// <summary>
    /// Streams the file part straight to storage instead of buffering the form: a meeting recording is easily a few
    /// hundred megabytes. Permission is checked before the first byte is written.
    /// </summary>
    private static async Task<IResult> UploadAsync(Guid meetingId, HttpContext context, IRecordingService service,
        IRecordingStorage storage, IOptions<RecordingOptions> options, CancellationToken ct)
    {
        var maxBytes = options.Value.MaxUploadMegabytes * 1024L * 1024;
        var actor = CapabilityGate.GatedUserId(context.User);
        var allowed = await service.CanUploadAsync(meetingId, actor, ct);
        if (allowed.Failure != RecordingFailure.None) return Map(allowed);
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = maxBytes + 1024 * 1024;
        if (!MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var type)
            || !type.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)
            || HeaderUtilities.RemoveQuotes(type.Boundary).Value is not { Length: > 0 } boundary)
            return Error(400, Msg291Invalid, "Yêu cầu phải gửi dạng multipart/form-data có tệp ghi âm.");

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        string? stored = null, fileName = null, contentType = null;
        long size = 0;
        try
        {
            var reader = new MultipartReader(boundary, context.Request.Body);
            while (await reader.ReadNextSectionAsync(ct) is { } section)
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
                    || HeaderUtilities.RemoveQuotes(disposition.Name).Value is not { } name)
                    return Invalid(stored, storage, "Một phần của biểu mẫu không có tên.");
                if (disposition.IsFileDisposition())
                {
                    if (name != "file" || stored is not null) return Invalid(stored, storage, "Chỉ gửi đúng một tệp, trong trường file.");
                    contentType = MediaTypeHeaderValue.TryParse(section.ContentType, out var media) ? media.MediaType.Value : null;
                    if (contentType is null || !RecordingRules.Extensions.TryGetValue(contentType, out var extension))
                        return Error(415, Msg292UnsupportedAudio, "Định dạng âm thanh không được hỗ trợ. Dùng mp3, m4a, wav, webm hoặc ogg.");
                    fileName = Path.GetFileName(HeaderUtilities.RemoveQuotes(disposition.FileNameStar.HasValue
                        ? disposition.FileNameStar : disposition.FileName).Value ?? "").Trim();
                    (stored, size) = await storage.SaveAsync(section.Body, extension, maxBytes, ct);
                }
                else
                {
                    using var text = new StreamReader(section.Body);
                    var buffer = new char[MaxFieldBytes + 1];
                    var read = await text.ReadBlockAsync(buffer, ct);
                    if (read > MaxFieldBytes || !fields.TryAdd(name, new string(buffer, 0, read)))
                        return Invalid(stored, storage, "Trường biểu mẫu quá dài hoặc bị lặp.");
                }
            }
        }
        catch (Exception ex) when (ex is RecordingTooLargeException
            || (ex is BadHttpRequestException bad && bad.StatusCode == StatusCodes.Status413PayloadTooLarge))
        {
            if (stored is not null) storage.Delete(stored);
            return Error(413, Msg293TooLarge, $"Bản ghi vượt quá {options.Value.MaxUploadMegabytes} MB.");
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException && !ct.IsCancellationRequested)
        {
            return Invalid(stored, storage, "Không đọc được biểu mẫu tải lên.");
        }

        if (stored is null) return Error(400, Msg291Invalid, "Chưa chọn tệp ghi âm.");
        var source = fields.GetValueOrDefault("source") switch
        {
            null or "" or "Upload" => RecordingSource.Upload,
            "BrowserCapture" => RecordingSource.BrowserCapture,
            _ => (RecordingSource?)null
        };
        long? duration = long.TryParse(fields.GetValueOrDefault("durationMs"), NumberStyles.None, CultureInfo.InvariantCulture, out var ms)
            ? ms : null;
        if (source is null || (fields.ContainsKey("durationMs") && duration is null))
            return Invalid(stored, storage, "Nguồn bản ghi hoặc thời lượng không hợp lệ.");
        var upload = new RecordingUpload(fileName ?? "", contentType!, stored, size, source.Value,
            fields.GetValueOrDefault("consentConfirmed") == "true", fields.GetValueOrDefault("consentPurpose"), duration);
        // The service deletes the stored file itself whenever it refuses to keep it.
        var result = await service.AddAsync(meetingId, actor, upload, ct);
        return Map(result, Msg290Uploaded, meetingId);
    }

    private static IResult Invalid(string? stored, IRecordingStorage storage, string message)
    {
        if (stored is not null) storage.Delete(stored);
        return Error(400, Msg291Invalid, message);
    }

    private static IResult Map(RecordingOutcome result, string? successCode = null, Guid? meetingId = null)
    {
        if (result.Failure == RecordingFailure.NotFound) return Results.NotFound();
        if (result.Failure == RecordingFailure.Inactive) return Results.Forbid();
        if (result.Failure != RecordingFailure.None)
        {
            var (status, code, message) = result.Failure switch
            {
                RecordingFailure.Forbidden => (403, Msg295NotSecretary, "Chỉ Thư ký cuộc họp được quản lý bản ghi."),
                RecordingFailure.Locked => (409, Msg294UploadLocked, "Chỉ lưu bản ghi khi cuộc họp đang diễn ra hoặc đã kết thúc."),
                RecordingFailure.Invalid => (400, Msg291Invalid, "Thông tin bản ghi không hợp lệ."),
                RecordingFailure.ReadFailed => (500, Msg297ReadFailed, "Không tải được danh sách bản ghi."),
                _ => (503, Msg298SaveFailed, "Không lưu được thay đổi bản ghi. Vui lòng thử lại.")
            };
            return Error(status, code, message, result.Errors);
        }
        if (successCode is null) return Results.Ok(result.Data);
        var body = new Dictionary<string, object?>
        {
            ["code"] = successCode,
            ["message"] = successCode == Msg290Uploaded ? "Đã lưu bản ghi." : "Đã xoá bản ghi.",
            ["recordingId"] = result.RecordingId,
        };
        return meetingId is { } id ? Results.Created($"/api/meetings/{id}/recordings", body) : Results.Ok(body);
    }

    private static IResult Error(int status, string code, string message, IReadOnlyDictionary<string, string[]>? errors = null)
        => Results.Json(new { code, message, errors }, statusCode: status);
}
