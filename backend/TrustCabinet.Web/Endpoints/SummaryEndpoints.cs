using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// SCRUM-36 — the AI draft of a meeting's content (UC-47 to UC-50): read it, ask the AI for it (again), correct it, review
/// it. Writing it takes minutes, so asking only queues it; the page polls GET until it is ready.
/// </summary>
public static class SummaryEndpoints
{
    private const string Msg384Queued = "MSG384";
    private const string Msg385NotOfficer = "MSG385";
    private const string Msg386WrongMeetingStatus = "MSG386";
    private const string Msg387Busy = "MSG387";
    private const string Msg388Saved = "MSG388";
    private const string Msg389Invalid = "MSG389";
    private const string Msg390NotDrafted = "MSG390";
    private const string Msg391Reviewed = "MSG391";
    private const string Msg392ReadFailed = "MSG392";
    private const string Msg393SaveFailed = "MSG393";
    private const string Msg394AiUnavailable = "MSG394";

    internal sealed record EditBody(string? Objectives, string? Discussion, string? Conclusions);

    public static void MapSummaryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/meetings/{meetingId:guid}/summary")
            .RequireActiveUser(Error(500, Msg392ReadFailed, "Không tải được dự thảo."))
            .MapGet("", async (Guid meetingId, HttpContext context, ISummaryService service, CancellationToken ct)
                => Map(await service.GetAsync(meetingId, CapabilityGate.GatedUserId(context.User), ct)));

        var write = app.MapGroup("/api/meetings/{meetingId:guid}/summary")
            .RequireActiveUser(Error(503, Msg393SaveFailed, "Không lưu được dự thảo. Vui lòng thử lại."));
        write.MapPost("", async (Guid meetingId, HttpContext context, ISummaryService service, CancellationToken ct)
            => Map(await service.RequestAsync(meetingId, CapabilityGate.GatedUserId(context.User), ct), Msg384Queued));
        write.MapPut("", async (Guid meetingId, EditBody body, HttpContext context, ISummaryService service, CancellationToken ct)
            => Map(await service.EditAsync(meetingId, CapabilityGate.GatedUserId(context.User),
                new SummaryEdit(body.Objectives, body.Discussion, body.Conclusions), ct), Msg388Saved));
        write.MapPost("/review", async (Guid meetingId, HttpContext context, ISummaryService service, CancellationToken ct)
            => Map(await service.ReviewAsync(meetingId, CapabilityGate.GatedUserId(context.User), ct), Msg391Reviewed));
    }

    private static IResult Map(SummaryOutcome result, string? successCode = null)
    {
        if (result.Failure == SummaryFailure.NotFound) return Results.NotFound();
        if (result.Failure == SummaryFailure.Inactive) return Results.Forbid();
        if (result.Failure != SummaryFailure.None)
        {
            var (status, code, message) = result.Failure switch
            {
                SummaryFailure.Forbidden => (403, Msg385NotOfficer, "Chỉ Chủ tọa hoặc Thư ký được tạo, sửa và duyệt dự thảo."),
                SummaryFailure.WrongMeetingStatus => (409, Msg386WrongMeetingStatus,
                    "Chỉ tạo dự thảo khi cuộc họp đang diễn ra hoặc đã kết thúc."),
                SummaryFailure.Busy => (409, Msg387Busy, "AI đang viết dự thảo, vui lòng chờ."),
                SummaryFailure.NotDrafted => (409, Msg390NotDrafted, "Chưa có dự thảo để sửa hoặc duyệt."),
                SummaryFailure.Invalid => (400, Msg389Invalid, "Nội dung dự thảo không hợp lệ."),
                SummaryFailure.AiUnavailable => (503, Msg394AiUnavailable, "Máy AI chưa sẵn sàng. Liên hệ quản trị hệ thống."),
                SummaryFailure.ReadFailed => (500, Msg392ReadFailed, "Không tải được dự thảo."),
                _ => (503, Msg393SaveFailed, "Không lưu được dự thảo. Vui lòng thử lại."),
            };
            return Results.Json(new { code, message, errors = result.Errors }, statusCode: status);
        }
        var state = (SummaryState)result.Data!;
        return successCode switch
        {
            null => Results.Ok(state),
            Msg384Queued => Results.Json(new
            {
                code = Msg384Queued, message = "Đã gửi yêu cầu. Dự thảo sẽ tự hiện khi AI viết xong.", state.Summary, state.CanManage, state.AiReady, state.Readiness,
            }, statusCode: StatusCodes.Status202Accepted),
            Msg391Reviewed => Results.Ok(new { code = successCode, message = "Đã duyệt dự thảo.", state.Summary, state.CanManage, state.AiReady, state.Readiness }),
            _ => Results.Ok(new { code = successCode, message = "Đã lưu dự thảo.", state.Summary, state.CanManage, state.AiReady, state.Readiness }),
        };
    }

    private static IResult Error(int status, string code, string message) => Results.Json(new { code, message }, statusCode: status);
}
