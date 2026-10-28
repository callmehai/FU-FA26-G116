using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Web.Endpoints;

/// <summary>SCRUM-31 — the transcript of a recording: read it, run it again after a failure, correct a segment.</summary>
public static class TranscriptEndpoints
{
    private const string Msg295NotSecretary = "MSG295";
    private const string Msg299ReadFailed = "MSG299";
    private const string Msg300Queued = "MSG300";
    private const string Msg301NotRetryable = "MSG301";
    private const string Msg302SegmentSaved = "MSG302";
    private const string Msg303InvalidText = "MSG303";
    private const string Msg304SaveFailed = "MSG304";
    private const string Msg316SpeakerAssigned = "MSG316";
    private const string Msg317InvalidSpeaker = "MSG317";
    private const string Msg355ExportForbidden = "MSG355";
    private const string Msg356SecretMeeting = "MSG356";
    private const string Msg357TranscriptNotReady = "MSG357";

    private sealed record SegmentBody(string? Text);
    private sealed record SpeakerBody(Guid? UserId);

    public static void MapTranscriptEndpoints(this IEndpointRouteBuilder app)
    {
        var recording = app.MapGroup("/api/recordings/{recordingId:guid}")
            .RequireActiveUser(Error(500, Msg299ReadFailed, "Không tải được bản gỡ băng."));
        recording.MapGet("/transcript", async (Guid recordingId, HttpContext context, ITranscriptionService service, CancellationToken ct)
            => Map(await service.GetAsync(recordingId, CapabilityGate.GatedUserId(context.User), ct)));
        // SCRUM-35 — the secretary names a voice (or clears it with userId: null); the machine only suggested.
        recording.MapPut("/speakers/{label}", async (Guid recordingId, string label, SpeakerBody body, HttpContext context,
            ITranscriptionService service, CancellationToken ct)
            => Map(await service.AssignSpeakerAsync(recordingId, label, body.UserId, CapabilityGate.GatedUserId(context.User), ct),
                Msg316SpeakerAssigned));
        recording.MapGet("/transcript.txt", async (Guid recordingId, HttpContext context, ITranscriptionService service,
            CancellationToken ct) => Map(await service.ExportAsync(recordingId, CapabilityGate.GatedUserId(context.User), ct)));
        recording.MapPost("/transcribe", async (Guid recordingId, HttpContext context, ITranscriptionService service, CancellationToken ct)
            => Map(await service.RetryAsync(recordingId, CapabilityGate.GatedUserId(context.User), ct), Msg300Queued));

        app.MapGroup("/api/transcript-segments/{segmentId:guid}")
            .RequireActiveUser(Error(503, Msg304SaveFailed, "Không lưu được thay đổi bản gỡ băng. Vui lòng thử lại."))
            .MapPut("", async (Guid segmentId, SegmentBody body, HttpContext context, ITranscriptionService service, CancellationToken ct)
                => Map(await service.EditSegmentAsync(segmentId, CapabilityGate.GatedUserId(context.User), body.Text, ct), Msg302SegmentSaved));
    }

    private static IResult Map(TranscriptOutcome result, string? successCode = null)
    {
        if (result.Failure == TranscriptFailure.NotFound) return Results.NotFound();
        if (result.Failure == TranscriptFailure.Inactive) return Results.Forbid();
        if (result.Failure != TranscriptFailure.None)
        {
            var (status, code, message) = result.Failure switch
            {
                TranscriptFailure.Forbidden => (403, Msg295NotSecretary, "Chỉ Thư ký cuộc họp được quản lý bản ghi."),
                TranscriptFailure.NotRetryable => (409, Msg301NotRetryable,
                    "Bản ghi đang chờ hoặc đang được chuyển thành chữ, chưa chạy lại được."),
                TranscriptFailure.ExportForbidden => (403, Msg355ExportForbidden, "Chỉ Chủ tọa hoặc Thư ký được tải bản ghi và bản gỡ băng về."),
                TranscriptFailure.SecretMeeting => (403, Msg356SecretMeeting, "Bản ghi và bản gỡ băng của cuộc họp Mật không được tải về."),
                TranscriptFailure.NotReady => (409, Msg357TranscriptNotReady, "Bản gỡ băng chưa xong nên chưa tải về được."),
                TranscriptFailure.Invalid => (400, Msg303InvalidText, "Nội dung đoạn gỡ băng không hợp lệ."),
                TranscriptFailure.InvalidSpeaker => (400, Msg317InvalidSpeaker, "Người được gán phải có trong danh sách cuộc họp."),
                TranscriptFailure.ReadFailed => (500, Msg299ReadFailed, "Không tải được bản gỡ băng."),
                _ => (503, Msg304SaveFailed, "Không lưu được thay đổi bản gỡ băng. Vui lòng thử lại."),
            };
            return Results.Json(new { code, message, errors = result.Errors }, statusCode: status);
        }
        return successCode switch
        {
            null when result.Data is ExportedText file => TextFile(file),
            null => Results.Ok(result.Data),
            Msg300Queued => Results.Json(new { code = Msg300Queued, message = "Đã đưa bản ghi vào hàng chờ chuyển thành chữ." },
                statusCode: StatusCodes.Status202Accepted),
            Msg316SpeakerAssigned => Results.Ok(new { code = successCode, message = "Đã gán người nói.", speaker = result.Data }),
            _ => Results.Ok(new { code = successCode, message = "Đã lưu đoạn gỡ băng.", segment = result.Data }),
        };
    }

    /// <summary>A UTF-8 text file the browser saves; never cached, as it holds what people said.</summary>
    internal static IResult TextFile(ExportedText file)
        => Results.File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(file.Text)).ToArray(),
            "text/plain; charset=utf-8", file.FileName);

    private static IResult Error(int status, string code, string message) => Results.Json(new { code, message }, statusCode: status);
}
