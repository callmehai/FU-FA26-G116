using TrustCabinet.Application.Reports;

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
    private const string Msg484InvalidParameter = "MSG484";

    private const string InvalidPeriodMessage =
        "Khoảng thời gian không hợp lệ. Chọn tối đa 24 tháng, tháng bắt đầu không sau tháng kết thúc.";
    private const string ReadFailedMessage = "Không tải được báo cáo. Vui lòng thử lại.";
    private const string ExportFailedMessage = "Không xuất được báo cáo. Vui lòng thử lại.";
    private const string InvalidParameterMessage = "Tham số báo cáo không hợp lệ. Chọn lại dữ liệu, cách nhóm hoặc bộ lọc.";

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

    /// <summary>
    /// The report query (supervisor review 22/11/2026): one main table grouped, filtered and sorted as the caller picks,
    /// over the same readable meetings, with the same gate. Grouped by meeting it names meetings the caller may open
    /// anyway; it never shows a meeting's content.
    /// </summary>
    public static void MapReportQueryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/reports/query").RequireActiveUser(Error(500, Msg464ReadFailed, ReadFailedMessage))
            .MapGet("", async (HttpContext context, IReportService service, CancellationToken ct) =>
            {
                var result = await service.QueryAsync(CapabilityGate.GatedUserId(context.User), Input(context.Request.Query), ct);
                context.Response.Headers.CacheControl = "private, no-store";
                return result.Failure == ReportFailure.None
                    ? Results.Ok(result.Table)
                    : Refused(result.Failure, Msg464ReadFailed, ReadFailedMessage);
            });

        app.MapGroup("/api/reports/query").RequireActiveUser(Error(500, Msg465ExportFailed, ExportFailedMessage))
            .MapGet("/export", async (HttpContext context, IReportService service, CancellationToken ct) =>
            {
                var result = await service.ExportQueryAsync(CapabilityGate.GatedUserId(context.User), Input(context.Request.Query), ct);
                return result.Failure == ReportFailure.None
                    ? File(context, result.File!)
                    : Refused(result.Failure, Msg465ExportFailed, ExportFailedMessage);
            });
    }

    /// <summary>
    /// The query string as the service reads it. Read by hand: the compile-time endpoint generator cannot bind a record
    /// of nullable strings with [AsParameters]. A parameter given twice counts as invalid, so it is passed on joined.
    /// </summary>
    private static ReportQueryInput Input(IQueryCollection query)
    {
        string? Get(string name) => query.TryGetValue(name, out var value) ? value.ToString() : null;
        return new ReportQueryInput(Get("entity"), Get("from"), Get("to"), Get("groupBy"), Get("sort"), Get("top"),
            Get("status"), Get("mode"), Get("sensitivity"), Get("role"), Get("attendance"), Get("response"),
            Get("conclusion"), Get("voteStatus"), Get("ballot"), Get("fileType"), Get("minutesStatus"), Get("source"),
            Get("transcript"));
    }

    private static IResult Refused(ReportFailure failure, string code, string message) => failure switch
    {
        ReportFailure.InvalidPeriod => Error(400, Msg463InvalidPeriod, InvalidPeriodMessage),
        ReportFailure.InvalidParameter => Error(400, Msg484InvalidParameter, InvalidParameterMessage),
        _ => Error(500, code, message),
    };

    private static IResult File(HttpContext context, ReportFile file)
    {
        // Never cached by the browser or anything in between: a fresh export is computed, and audited, each time.
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.File(file.Bytes, file.ContentType, file.FileName);
    }

    private static IResult Error(int status, string code, string message) => Results.Json(new { code, message }, statusCode: status);
}
