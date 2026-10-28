using System.Globalization;
using Microsoft.Net.Http.Headers;
using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// SCRUM-109 — the per-mic transcript: each attendee's browser sends a sentence when they pause, everyone reads the
/// sentences as they arrive, the Secretary corrects them, the Chair or Secretary takes out what one mic overheard from
/// another, and picks the capture mode before the meeting starts.
/// </summary>
public static class SpeechClipEndpoints
{
    private const string Msg295NotSecretary = "MSG295";
    private const string Msg299ReadFailed = "MSG299";
    private const string Msg302Saved = "MSG302";
    private const string Msg303InvalidText = "MSG303";
    private const string Msg304SaveFailed = "MSG304";
    private const string Msg312EngineUnavailable = "MSG312";
    private const string Msg318ClipSaved = "MSG318";
    private const string Msg319InvalidClip = "MSG319";
    private const string Msg320NotCapturing = "MSG320";
    private const string Msg321ClipTooLarge = "MSG321";
    private const string Msg322ClipDeleted = "MSG322";
    private const string Msg323DeleteForbidden = "MSG323";
    private const string Msg324ModeChanged = "MSG324";
    private const string Msg325ModeLocked = "MSG325";
    private const string Msg326InvalidMode = "MSG326";
    private const string Msg327ModeForbidden = "MSG327";
    private const string Msg349MeetingCancelled = "MSG349";
    private const string Msg351ClipExcluded = "MSG351";
    private const string Msg352ClipRestored = "MSG352";
    private const string Msg353ExcludeForbidden = "MSG353";
    private const string Msg354InvalidExclusion = "MSG354";
    private const string Msg355ExportForbidden = "MSG355";
    private const string Msg356SecretMeeting = "MSG356";

    private sealed record TextBody(string? Text);
    private sealed record ModeBody(string? Mode);
    private sealed record ExclusionBody(bool Excluded, string? Reason);

    public static void MapSpeechClipEndpoints(this IEndpointRouteBuilder app)
    {
        var meeting = app.MapGroup("/api/meetings/{meetingId:guid}")
            .RequireActiveUser(Error(500, Msg299ReadFailed, "Không tải được bản gỡ băng."));
        meeting.MapGet("/speech-clips", async (Guid meetingId, DateTimeOffset? since, HttpContext context,
            ISpeechClipService service, CancellationToken ct)
            => Map(await service.ListAsync(meetingId, CapabilityGate.GatedUserId(context.User), since, ct)));
        meeting.MapGet("/speech-clips.txt", async (Guid meetingId, HttpContext context, ISpeechClipService service,
            CancellationToken ct) =>
        {
            var result = await service.ExportAsync(meetingId, CapabilityGate.GatedUserId(context.User), ct);
            return result is { Failure: SpeechClipFailure.None, Data: Domain.Transcription.ExportedText file }
                ? TranscriptEndpoints.TextFile(file) : Map(result);
        });
        // PUT with the browser's own id: a retry after a lost response stores the sentence once.
        meeting.MapPut("/speech-clips/{clientClipId:guid}", SendClipAsync);
        meeting.MapPut("/capture-mode", async (Guid meetingId, ModeBody body, HttpContext context, ISpeechClipService service,
            CancellationToken ct)
            => Map(await service.SetCaptureModeAsync(meetingId, CapabilityGate.GatedUserId(context.User), body.Mode, ct),
                Msg324ModeChanged, mode: true));

        var clip = app.MapGroup("/api/speech-clips/{clipId:guid}")
            .RequireActiveUser(Error(503, Msg304SaveFailed, "Không lưu được thay đổi bản gỡ băng. Vui lòng thử lại."));
        clip.MapPut("/text", async (Guid clipId, TextBody body, HttpContext context, ISpeechClipService service, CancellationToken ct)
            => Map(await service.EditAsync(clipId, CapabilityGate.GatedUserId(context.User), body.Text, ct), Msg302Saved));
        clip.MapDelete("", async (Guid clipId, HttpContext context, ISpeechClipService service, CancellationToken ct)
            => Map(await service.DeleteAsync(clipId, CapabilityGate.GatedUserId(context.User), ct), Msg322ClipDeleted, delete: true));
        clip.MapPut("/exclusion", async (Guid clipId, ExclusionBody body, HttpContext context, ISpeechClipService service,
            CancellationToken ct)
            => MapExclusion(await service.SetExclusionAsync(clipId, CapabilityGate.GatedUserId(context.User), body.Excluded,
                body.Reason, ct)));
    }

    /// <summary>
    /// The body is the sentence's audio, a few hundred kilobytes at most. It is read into memory, handed to the speech
    /// worker and dropped with the request: no sentence's audio is ever written to disk.
    /// </summary>
    private static async Task<IResult> SendClipAsync(Guid meetingId, Guid clientClipId, string? startedAt, string? durationMs,
        HttpContext context, ISpeechClipService service, CancellationToken ct)
    {
        var actor = CapabilityGate.GatedUserId(context.User);
        var allowed = await service.CanCaptureAsync(meetingId, actor, ct);
        if (allowed.Failure != SpeechClipFailure.None) return Map(allowed);

        if (!MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var type)
            || type.MediaType.Value is not { } media || !SpeechClipRules.ContentTypes.Contains(media))
            return Error(400, Msg319InvalidClip, "Câu nói phải gửi dạng âm thanh wav, webm hoặc ogg.");
        if (!DateTimeOffset.TryParse(startedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var started)
            || !long.TryParse(durationMs, NumberStyles.None, CultureInfo.InvariantCulture, out var duration))
            return Error(400, Msg319InvalidClip, "Thiếu thời điểm bắt đầu hoặc thời lượng của câu nói.");
        if (context.Request.ContentLength > SpeechClipRules.MaxClipBytes) return TooLarge();

        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while ((read = await context.Request.Body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > SpeechClipRules.MaxClipBytes) return TooLarge();
            buffer.Write(chunk, 0, read);
        }

        var result = await service.AddAsync(meetingId, actor, new SpeechClipUpload(clientClipId, started, duration, media,
            buffer.ToArray()), ct);
        // Nothing but silence: the browser drops the sentence, there is no line to show.
        if (result is { Failure: SpeechClipFailure.None, Data: null }) return Results.NoContent();
        return Map(result, Msg318ClipSaved);
    }

    private static IResult TooLarge()
        => Error(413, Msg321ClipTooLarge, $"Một câu nói tối đa {SpeechClipRules.MaxDurationMs / 1000} giây.");

    private static IResult Map(SpeechClipOutcome result, string? successCode = null, bool delete = false, bool mode = false)
    {
        if (result.Failure == SpeechClipFailure.NotFound) return Results.NotFound();
        if (result.Failure == SpeechClipFailure.Inactive) return Results.Forbid();
        if (result.Failure != SpeechClipFailure.None)
        {
            var (status, code, message) = result.Failure switch
            {
                SpeechClipFailure.Forbidden when mode => (403, Msg327ModeForbidden, "Chỉ Chủ tọa hoặc Thư ký được đổi cách ghi âm."),
                SpeechClipFailure.Forbidden when delete => (403, Msg323DeleteForbidden, "Chỉ người nói hoặc Thư ký được xoá câu này."),
                SpeechClipFailure.Forbidden => (403, Msg295NotSecretary, "Chỉ Thư ký cuộc họp được quản lý bản ghi."),
                SpeechClipFailure.ExportForbidden => (403, Msg355ExportForbidden, "Chỉ Chủ tọa hoặc Thư ký được tải bản ghi và bản gỡ băng về."),
                SpeechClipFailure.SecretMeeting => (403, Msg356SecretMeeting, "Bản ghi và bản gỡ băng của cuộc họp Mật không được tải về."),
                SpeechClipFailure.Locked => (409, Msg325ModeLocked, "Cuộc họp đã bắt đầu nên không đổi được cách ghi âm."),
                SpeechClipFailure.Cancelled => (409, Msg349MeetingCancelled, "Cuộc họp đã huỷ nên không thay đổi được nữa."),
                SpeechClipFailure.NotCapturing => (409, Msg320NotCapturing,
                    "Cuộc họp không ghi theo micro từng người, hoặc không còn đang diễn ra."),
                SpeechClipFailure.Invalid when mode => (400, Msg326InvalidMode, "Cách ghi âm không hợp lệ."),
                SpeechClipFailure.Invalid when successCode == Msg318ClipSaved => (400, Msg319InvalidClip, "Câu nói gửi lên không hợp lệ."),
                SpeechClipFailure.Invalid => (400, Msg303InvalidText, "Nội dung đoạn gỡ băng không hợp lệ."),
                SpeechClipFailure.EngineUnavailable => (503, Msg312EngineUnavailable,
                    "Máy chủ nhận dạng giọng nói chưa sẵn sàng. Vui lòng thử lại sau."),
                SpeechClipFailure.ReadFailed => (500, Msg299ReadFailed, "Không tải được bản gỡ băng."),
                _ => (503, Msg304SaveFailed, "Không lưu được thay đổi bản gỡ băng. Vui lòng thử lại."),
            };
            return Results.Json(new { code, message, errors = result.Errors }, statusCode: status);
        }
        return successCode switch
        {
            null => Results.Ok(result.Data),
            Msg318ClipSaved => Results.Ok(new { code = successCode, message = "Đã ghi câu nói.", clip = result.Data }),
            Msg322ClipDeleted => Results.Ok(new { code = successCode, message = "Đã xoá câu nói." }),
            Msg324ModeChanged => Results.Ok(new { code = successCode, message = "Đã đổi cách ghi âm.", captureMode = result.Data }),
            _ => Results.Ok(new { code = successCode, message = "Đã lưu đoạn gỡ băng." }),
        };
    }

    private static IResult MapExclusion(SpeechClipOutcome result) => result switch
    {
        { Failure: SpeechClipFailure.None, Data: true } => Results.Ok(new { code = Msg351ClipExcluded, message = "Đã loại câu khỏi bản ghi." }),
        { Failure: SpeechClipFailure.None } => Results.Ok(new { code = Msg352ClipRestored, message = "Đã đưa câu trở lại bản ghi." }),
        { Failure: SpeechClipFailure.Forbidden } => Error(403, Msg353ExcludeForbidden, "Chỉ Chủ tọa hoặc Thư ký được loại câu khỏi bản ghi."),
        { Failure: SpeechClipFailure.Invalid } => Results.Json(new { code = Msg354InvalidExclusion,
            message = "Lý do loại câu không hợp lệ.", errors = result.Errors }, statusCode: 400),
        _ => Map(result),
    };

    private static IResult Error(int status, string code, string message) => Results.Json(new { code, message }, statusCode: status);
}
