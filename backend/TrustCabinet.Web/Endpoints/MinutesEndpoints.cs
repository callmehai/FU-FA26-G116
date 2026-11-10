using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// SCRUM-32 — the draft minutes (UC-58 to UC-60): read, build from the meeting's records, edit, export to Word or PDF.
/// SCRUM-33 — submit (UC-61), approve and issue (UC-64), return (UC-65), correct (UC-83), and read one revision.
/// </summary>
public static class MinutesEndpoints
{
    private const string Msg395Created = "MSG395";
    private const string Msg396Saved = "MSG396";
    private const string Msg397NotOfficer = "MSG397";
    private const string Msg398WrongMeetingStatus = "MSG398";
    private const string Msg399Exists = "MSG399";
    private const string Msg400Locked = "MSG400";
    private const string Msg401Stale = "MSG401";
    private const string Msg402Invalid = "MSG402";
    private const string Msg403ReadFailed = "MSG403";
    private const string Msg404SaveFailed = "MSG404";
    private const string Msg405SecretMeeting = "MSG405";
    private const string Msg406UnknownFormat = "MSG406";
    private const string Msg424Submitted = "MSG424";
    private const string Msg425NotSecretary = "MSG425";
    private const string Msg426MeetingNotEnded = "MSG426";
    private const string Msg427Issued = "MSG427";
    private const string Msg428NotChair = "MSG428";
    private const string Msg429NotWaiting = "MSG429";
    private const string Msg430Returned = "MSG430";
    private const string Msg431ReasonInvalid = "MSG431";
    private const string Msg432CorrectionCreated = "MSG432";
    private const string Msg433NotIssued = "MSG433";
    private const string Msg434CorrectionInProgress = "MSG434";
    private const string Msg456CorrectionRebuild = "MSG456";

    internal sealed record CreateBody(bool Rebuild);
    internal sealed record SaveBody(string? Content, DateTimeOffset? UpdatedAt);
    internal sealed record ActionBody(DateTimeOffset? UpdatedAt);
    internal sealed record ReturnBody(string? Reason, DateTimeOffset? UpdatedAt);

    /// <summary>The success answer of each write: its code, its message and its HTTP status.</summary>
    private sealed record Success(string Code, string Message, int Status = StatusCodes.Status200OK);

    private static readonly Success Created = new(Msg395Created, "Đã tạo bản nháp biên bản.", StatusCodes.Status201Created);
    private static readonly Success Saved = new(Msg396Saved, "Đã lưu biên bản.");
    private static readonly Success Submitted = new(Msg424Submitted, "Đã gửi biên bản cho Chủ toạ duyệt.");
    private static readonly Success Issued = new(Msg427Issued, "Đã duyệt và ban hành biên bản.");
    private static readonly Success Returned = new(Msg430Returned, "Đã trả lại biên bản cho Thư ký.");
    private static readonly Success CorrectionCreated = new(Msg432CorrectionCreated, "Đã tạo bản đính chính.",
        StatusCodes.Status201Created);

    public static void MapMinutesEndpoints(this IEndpointRouteBuilder app)
    {
        var read = Error(500, Msg403ReadFailed, "Không tải được biên bản.");
        var save = Error(503, Msg404SaveFailed, "Không lưu được biên bản. Vui lòng thử lại.");

        app.MapGroup("/api/meetings/{meetingId:guid}/minutes").RequireActiveUser(read)
            .MapGet("", async (Guid meetingId, HttpContext context, IMinutesService service, CancellationToken ct)
                => Map(await service.GetAsync(meetingId, CapabilityGate.GatedUserId(context.User), ct)));
        app.MapGroup("/api/meetings/{meetingId:guid}/minutes").RequireActiveUser(save)
            .MapPost("", async (Guid meetingId, CreateBody? body, HttpContext context, IMinutesService service, CancellationToken ct)
                => Map(await service.CreateAsync(meetingId, CapabilityGate.GatedUserId(context.User), body?.Rebuild == true, ct),
                    Created));

        app.MapGroup("/api/minutes/{minutesId:guid}").RequireActiveUser(save)
            .MapPut("", async (Guid minutesId, SaveBody body, HttpContext context, IMinutesService service, CancellationToken ct)
                => Map(await service.SaveAsync(minutesId, CapabilityGate.GatedUserId(context.User), body.Content, body.UpdatedAt, ct),
                    Saved));
        var writes = app.MapGroup("/api/minutes/{minutesId:guid}").RequireActiveUser(save);
        writes.MapPost("/submit", async (Guid minutesId, ActionBody? body, HttpContext context, IMinutesService service,
                CancellationToken ct)
            => Map(await service.SubmitAsync(minutesId, CapabilityGate.GatedUserId(context.User), body?.UpdatedAt, ct), Submitted));
        writes.MapPost("/approve", async (Guid minutesId, ActionBody? body, HttpContext context, IMinutesService service,
                CancellationToken ct)
            => Map(await service.ApproveAsync(minutesId, CapabilityGate.GatedUserId(context.User), body?.UpdatedAt, ct), Issued));
        writes.MapPost("/reject", async (Guid minutesId, ReturnBody? body, HttpContext context, IMinutesService service,
                CancellationToken ct)
            => Map(await service.ReturnAsync(minutesId, CapabilityGate.GatedUserId(context.User), body?.Reason, body?.UpdatedAt, ct),
                Returned));
        writes.MapPost("/corrections", async (Guid minutesId, HttpContext context, IMinutesService service, CancellationToken ct)
            => Map(await service.CorrectAsync(minutesId, CapabilityGate.GatedUserId(context.User), ct), CorrectionCreated));
        var reads = app.MapGroup("/api/minutes/{minutesId:guid}").RequireActiveUser(read);
        reads.MapGet("", async (Guid minutesId, HttpContext context, IMinutesService service, CancellationToken ct)
            => Map(await service.GetRevisionAsync(minutesId, CapabilityGate.GatedUserId(context.User), ct)));
        reads.MapGet("/export", async (Guid minutesId, string? format, HttpContext context, IMinutesService service,
                CancellationToken ct) =>
            {
                // Word unless the caller asks for PDF; anything else is a wrong link, refused before the minutes are read.
                MinutesExportFormat? kind = string.IsNullOrWhiteSpace(format) || format.Equals("docx", StringComparison.OrdinalIgnoreCase)
                    ? MinutesExportFormat.Docx
                    : format.Equals("pdf", StringComparison.OrdinalIgnoreCase) ? MinutesExportFormat.Pdf : null;
                if (kind is null) return Error(400, Msg406UnknownFormat, "Chỉ tải biên bản dạng Word (docx) hoặc PDF.");
                // Never cached, by the browser or anything between: the minutes hold what was said and decided.
                context.Response.Headers.CacheControl = "private, no-store";
                return Map(await service.ExportAsync(minutesId, CapabilityGate.GatedUserId(context.User), kind.Value, ct));
            });
    }

    private static IResult Map(MinutesOutcome result, Success? success = null)
    {
        if (result.Failure == MinutesFailure.NotFound) return Results.NotFound();
        if (result.Failure == MinutesFailure.Inactive) return Results.Forbid();
        if (result.Failure != MinutesFailure.None)
        {
            var (status, code, message) = result.Failure switch
            {
                MinutesFailure.Forbidden => (403, Msg397NotOfficer, "Chỉ Chủ toạ hoặc Thư ký được soạn biên bản."),
                MinutesFailure.WrongMeetingStatus => (409, Msg398WrongMeetingStatus,
                    "Chỉ soạn biên bản khi cuộc họp đang diễn ra hoặc đã kết thúc."),
                MinutesFailure.Exists => (409, Msg399Exists, "Cuộc họp đã có bản nháp biên bản."),
                MinutesFailure.Locked => (409, Msg400Locked, "Biên bản đã gửi duyệt hoặc đã ban hành, không sửa được nữa."),
                MinutesFailure.Stale => (409, Msg401Stale, "Biên bản vừa được người khác sửa. Tải lại để xem bản mới nhất."),
                MinutesFailure.Invalid => (400, Msg402Invalid, "Nội dung biên bản không hợp lệ."),
                MinutesFailure.SecretMeeting => (403, Msg405SecretMeeting, "Biên bản của cuộc họp Mật không được tải về."),
                MinutesFailure.ReadFailed => (500, Msg403ReadFailed, "Không tải được biên bản."),
                MinutesFailure.NotSecretary => (403, Msg425NotSecretary, "Chỉ Thư ký được gửi duyệt biên bản."),
                MinutesFailure.MeetingNotEnded => (409, Msg426MeetingNotEnded, "Chỉ gửi duyệt biên bản sau khi cuộc họp kết thúc."),
                MinutesFailure.NotChair => (403, Msg428NotChair, "Chỉ Chủ toạ được duyệt hoặc trả lại biên bản."),
                MinutesFailure.NotWaiting => (409, Msg429NotWaiting, "Biên bản không ở trạng thái chờ duyệt."),
                MinutesFailure.ReasonInvalid => (400, Msg431ReasonInvalid, "Nhập lý do trả lại, tối đa 2.000 ký tự."),
                MinutesFailure.NotIssued => (409, Msg433NotIssued, "Chỉ đính chính được biên bản đã ban hành."),
                MinutesFailure.CorrectionInProgress => (409, Msg434CorrectionInProgress,
                    "Biên bản đang có một bản đính chính chưa ban hành."),
                MinutesFailure.CorrectionRebuild => (409, Msg456CorrectionRebuild,
                    "Bản đính chính không tạo lại từ mẫu được, hãy sửa trực tiếp."),
                _ => (503, Msg404SaveFailed, "Không lưu được biên bản. Vui lòng thử lại."),
            };
            return Results.Json(new { code, message, errors = result.Errors }, statusCode: status);
        }
        if (result.Data is MinutesFile file)
        {
            return Results.File(file.Bytes, file.ContentType, file.FileName);
        }
        var state = (MinutesState)result.Data!;
        return success is null
            ? Results.Ok(state)
            : Results.Json(new { code = success.Code, message = success.Message, state.Minutes, state.CanManage,
                state.DraftChangedAt, state.History, state.CanSubmit, state.CanApprove }, statusCode: success.Status);
    }

    private static IResult Error(int status, string code, string message) => Results.Json(new { code, message }, statusCode: status);
}
