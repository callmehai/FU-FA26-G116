using TrustCabinet.Domain.Reports;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// SCRUM-38 — the meeting report (UC-70a) and its Excel export (UC-70b). Open to every signed-in, active internal user
/// with no capability (BR-432): the figures count only the meetings the caller may read, so the report shows nobody more
/// than the meeting list already does.
/// </summary>
public static class ReportEndpoints
{
    private const string Msg463InvalidPeriod = "MSG463";
    private const string Msg464ReadFailed = "MSG464";
    private const string Msg465ExportFailed = "MSG465";

    private const string InvalidPeriodMessage =
        "Khoảng thời gian không hợp lệ. Chọn tối đa 24 tháng, tháng bắt đầu không sau tháng kết thúc.";
    private const string ReadFailedMessage = "Không tải được báo cáo. Vui lòng thử lại.";
    private const string ExportFailedMessage = "Không xuất được báo cáo. Vui lòng thử lại.";

    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/reports/meetings").RequireActiveUser(Error(500, Msg464ReadFailed, ReadFailedMessage))
            .MapGet("", async (string? from, string? to, HttpContext context, IReportService service, CancellationToken ct) =>
            {
                var result = await service.GetMeetingReportAsync(CapabilityGate.GatedUserId(context.User), from, to, ct);
                context.Response.Headers.CacheControl = "private, no-store";
                return result.Failure switch
                {
                    ReportFailure.None => Results.Ok(result.Report),
                    ReportFailure.InvalidPeriod => Error(400, Msg463InvalidPeriod, InvalidPeriodMessage),
                    _ => Error(500, Msg464ReadFailed, ReadFailedMessage),
                };
            });

        app.MapGroup("/api/reports/meetings").RequireActiveUser(Error(500, Msg465ExportFailed, ExportFailedMessage))
            .MapGet("/export", async (string? from, string? to, HttpContext context, IReportService service, CancellationToken ct) =>
            {
                var result = await service.ExportMeetingReportAsync(CapabilityGate.GatedUserId(context.User), from, to, ct);
                return result.Failure switch
                {
                    ReportFailure.None => File(context, result.File!),
                    ReportFailure.InvalidPeriod => Error(400, Msg463InvalidPeriod, InvalidPeriodMessage),
                    _ => Error(500, Msg465ExportFailed, ExportFailedMessage),
                };
            });
    }

    private static IResult File(HttpContext context, ReportFile file)
    {
        // Never cached by the browser or anything in between: a fresh export is computed, and audited, each time.
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.File(file.Bytes, file.ContentType, file.FileName);
    }

    private static IResult Error(int status, string code, string message) => Results.Json(new { code, message }, statusCode: status);
}
