using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Web.Endpoints;

/// <summary>SCRUM-32 — the draft minutes (UC-58 to UC-60): read, build from the meeting's records, edit, export to Word or PDF.</summary>
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

    internal sealed record CreateBody(bool Rebuild);
    internal sealed record SaveBody(string? Content, DateTimeOffset? UpdatedAt);

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
                    Msg395Created));

        app.MapGroup("/api/minutes/{minutesId:guid}").RequireActiveUser(save)
            .MapPut("", async (Guid minutesId, SaveBody body, HttpContext context, IMinutesService service, CancellationToken ct)
                => Map(await service.SaveAsync(minutesId, CapabilityGate.GatedUserId(context.User), body.Content, body.UpdatedAt, ct),
                    Msg396Saved));
        app.MapGroup("/api/minutes/{minutesId:guid}").RequireActiveUser(read)
            .MapGet("/export", async (Guid minutesId, string? format, HttpContext context, IMinutesService service,
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

    private static IResult Map(MinutesOutcome result, string? successCode = null)
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
                _ => (503, Msg404SaveFailed, "Không lưu được biên bản. Vui lòng thử lại."),
            };
            return Results.Json(new { code, message, errors = result.Errors }, statusCode: status);
        }
        if (result.Data is MinutesFile file)
        {
            return Results.File(file.Bytes, file.ContentType, file.FileName);
        }
        var state = (MinutesState)result.Data!;
        return successCode switch
        {
            null => Results.Ok(state),
            Msg395Created => Results.Json(new { code = Msg395Created, message = "Đã tạo bản nháp biên bản.", state.Minutes, state.CanManage, state.DraftChangedAt },
                statusCode: StatusCodes.Status201Created),
            _ => Results.Ok(new { code = successCode, message = "Đã lưu biên bản.", state.Minutes, state.CanManage, state.DraftChangedAt }),
        };
    }

    private static IResult Error(int status, string code, string message) => Results.Json(new { code, message }, statusCode: status);
}
