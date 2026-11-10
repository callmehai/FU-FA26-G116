using TrustCabinet.Domain.CalendarSharing;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// SCRUM-142 — calendar sharing (UC-84a → UC-84f) and the shared week (UC-85c). Every route needs an active account;
/// the Meeting Organizer right is checked in the service, so a caller without it gets MSG448 rather than a bare 403.
/// A share the caller is not one of the two people of answers 404 without a body (POST-3: nothing disclosed).
/// </summary>
public static class CalendarSharingEndpoints
{
    private const string Msg435Requested = "MSG435";
    private const string Msg436Accepted = "MSG436";
    private const string Msg437Declined = "MSG437";
    private const string Msg438Withdrawn = "MSG438";
    private const string Msg441Shared = "MSG441";
    private const string Msg442Updated = "MSG442";
    private const string Msg443InvalidValues = "MSG443";
    private const string Msg447SaveFailed = "MSG447";
    private const string Msg448NotOrganizer = "MSG448";
    private const string Msg449AlreadyShared = "MSG449";
    private const string Msg450NotAllowed = "MSG450";
    private const string Msg451Ended = "MSG451";
    private const string Msg452ChangeDeclined = "MSG452";
    private const string Msg453ReadFailed = "MSG453";
    private const string Msg454ChangeRequested = "MSG454";
    private const string Msg455CannotReceive = "MSG455";
    private const string Msg467CandidatesReadFailed = "MSG467";
    private const string Msg468SenderDeactivated = "MSG468";

    internal sealed record RequestBody(Guid? OwnerId, string? Level, DateOnly? StartDate, DateOnly? EndDate);

    internal sealed record DirectBody(Guid? ViewerId, DateOnly? StartDate, DateOnly? EndDate);

    internal sealed record ValuesBody(string? Level, DateOnly? StartDate, DateOnly? EndDate);

    public static void MapCalendarSharingEndpoints(this IEndpointRouteBuilder app)
    {
        var readFailed = Error(500, Msg453ReadFailed, "Không tải được dữ liệu chia sẻ lịch. Vui lòng thử lại.");
        var saveFailed = Error(503, Msg447SaveFailed, "Không lưu được dữ liệu chia sẻ lịch. Vui lòng thử lại.");

        var read = app.MapGroup("/api/calendar").WithTags("CalendarSharing").RequireActiveUser(readFailed);
        read.MapGet("/shares", async (HttpContext context, ICalendarSharingService service, CancellationToken ct)
            => Read(await service.ListAsync(Caller(context), ct), context));

        read.MapGet("/shared-week", async (DateOnly? week, HttpContext context, ISharedCalendarService service,
                CancellationToken ct)
            => Read(await service.ReadWeekAsync(Caller(context), week, ct), context));

        // UC-84a/UC-84b step 1. Its own group: a failure re-reading the caller's account is the list's failure (MSG467).
        app.MapGroup("/api/calendar").WithTags("CalendarSharing")
            .RequireActiveUser(Error(500, Msg467CandidatesReadFailed, "Không tải được danh sách người."))
            .MapGet("/share-candidates", async (HttpContext context, ICalendarSharingService service, CancellationToken ct) =>
            {
                if (!TryCandidateQuery(context.Request.Query, out var query)) return Failure(CalendarShareFailure.BadQuery);
                return Read(await service.CandidatesAsync(Caller(context), query, ct), context);
            });

        var write = app.MapGroup("/api/calendar/shares").WithTags("CalendarSharing").RequireActiveUser(saveFailed);
        write.MapPost("", async (RequestBody? body, HttpContext context, ICalendarSharingService service,
                CancellationToken ct)
            => Write(await service.RequestAsync(Caller(context), body?.OwnerId,
                new ShareValues(body?.Level, body?.StartDate, body?.EndDate), ct)));
        write.MapPost("/direct", async (DirectBody? body, HttpContext context, ICalendarSharingService service,
                CancellationToken ct)
            => Write(await service.ShareDirectlyAsync(Caller(context), body?.ViewerId, body?.StartDate, body?.EndDate, ct)));
        write.MapPost("/{id:guid}/accept", async (Guid id, ValuesBody? body, HttpContext context,
                ICalendarSharingService service, CancellationToken ct)
            => Write(await service.AcceptAsync(Caller(context), id, Values(body), ct)));
        write.MapPost("/{id:guid}/decline", async (Guid id, HttpContext context, ICalendarSharingService service,
                CancellationToken ct)
            => Write(await service.DeclineAsync(Caller(context), id, ct)));
        write.MapPatch("/{id:guid}", async (Guid id, ValuesBody? body, HttpContext context,
                ICalendarSharingService service, CancellationToken ct)
            => Write(await service.ChangeAsync(Caller(context), id, Values(body), ct)));
        write.MapPost("/{id:guid}/withdraw", async (Guid id, HttpContext context, ICalendarSharingService service,
                CancellationToken ct)
            => Write(await service.WithdrawAsync(Caller(context), id, ct)));
        write.MapPost("/{id:guid}/end", async (Guid id, HttpContext context, ICalendarSharingService service,
                CancellationToken ct)
            => Write(await service.EndAsync(Caller(context), id, ct)));
    }

    /// <summary>
    /// <c>purpose=request|direct</c> (required), <c>q</c> up to 200 characters, <c>page</c> from 1, <c>pageSize</c> 1–100
    /// (default 20). Anything else, or a parameter given twice, is a malformed query: 400 without a code.
    /// </summary>
    private static bool TryCandidateQuery(IQueryCollection values, out CandidateQuery query)
    {
        query = null!;
        if (values.Keys.Any(key => key is not ("purpose" or "q" or "page" or "pageSize")) || values.Any(v => v.Value.Count > 1))
            return false;
        CandidatePurpose purpose;
        switch (values["purpose"].ToString())
        {
            case "request": purpose = CandidatePurpose.Request; break;
            case "direct": purpose = CandidatePurpose.Direct; break;
            default: return false;
        }

        var q = values["q"].ToString();
        if (q.Length > CandidateQuery.MaxQueryLength) return false;
        var page = 1;
        var pageSize = CandidateQuery.DefaultPageSize;
        if (values.ContainsKey("page") && (!int.TryParse(values["page"], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out page) || page < 1 || page > 100_000))
            return false;
        if (values.ContainsKey("pageSize") && (!int.TryParse(values["pageSize"], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out pageSize) || pageSize < 1
                || pageSize > CandidateQuery.MaxPageSize))
            return false;
        query = new CandidateQuery(purpose, q, page, pageSize);
        return true;
    }

    private static Guid Caller(HttpContext context) => CapabilityGate.GatedUserId(context.User);

    private static ShareValues Values(ValuesBody? body) => new(body?.Level, body?.StartDate, body?.EndDate);

    private static IResult Read<T>(CalendarShareRead<T> result, HttpContext context) where T : class
    {
        // Who shares with whom is personal data: never kept by a browser or proxy cache.
        context.Response.Headers.CacheControl = "no-store";
        return result.Failure == CalendarShareFailure.None ? Results.Ok(result.Value) : Failure(result.Failure);
    }

    private static IResult Write(CalendarShareOutcome outcome)
    {
        if (outcome.Failure != CalendarShareFailure.None) return Failure(outcome.Failure);
        var (status, code, message) = outcome.Success switch
        {
            CalendarShareSuccess.Requested => (201, Msg435Requested, "Đã gửi yêu cầu xem lịch."),
            CalendarShareSuccess.Shared => (201, Msg441Shared, "Đã chia sẻ lịch. Đang chờ bên nhận đồng ý."),
            CalendarShareSuccess.Accepted => (200, Msg436Accepted, "Đã chấp nhận chia sẻ lịch."),
            CalendarShareSuccess.Declined => (200, Msg437Declined, "Đã từ chối yêu cầu chia sẻ."),
            CalendarShareSuccess.ChangeDeclined => (200, Msg452ChangeDeclined, "Đã từ chối thay đổi chia sẻ."),
            CalendarShareSuccess.ChangeRequested => (201, Msg454ChangeRequested, "Đã gửi yêu cầu thay đổi chia sẻ."),
            CalendarShareSuccess.Updated => (200, Msg442Updated, "Đã cập nhật chia sẻ lịch."),
            CalendarShareSuccess.Withdrawn => (200, Msg438Withdrawn, "Đã rút yêu cầu chia sẻ lịch."),
            CalendarShareSuccess.Ended => (200, Msg451Ended, "Đã ngừng chia sẻ lịch."),
            _ => throw new InvalidOperationException($"No message for calendar share success {outcome.Success}."),
        };
        return Results.Json(new { code, message, share = outcome.Share }, statusCode: status);
    }

    private static IResult Failure(CalendarShareFailure failure) => failure switch
    {
        CalendarShareFailure.NotFound => Results.NotFound(),
        CalendarShareFailure.NotOrganizer => Error(403, Msg448NotOrganizer,
            "Bạn cần quyền tạo cuộc họp để thực hiện thao tác này."),
        CalendarShareFailure.InvalidValues => Error(400, Msg443InvalidValues, "Khoảng ngày không đúng."),
        CalendarShareFailure.AlreadyShared => Error(409, Msg449AlreadyShared,
            "Đã có chia sẻ chờ hoặc hiệu lực với người này."),
        CalendarShareFailure.CannotReceive => Error(400, Msg455CannotReceive, "Người này không nhận được chia sẻ lịch."),
        CalendarShareFailure.NotAllowed => Error(409, Msg450NotAllowed,
            "Không thể thực hiện ở trạng thái hiện tại của chia sẻ."),
        CalendarShareFailure.SenderDeactivated => Error(409, Msg468SenderDeactivated,
            "Tài khoản người gửi đã bị vô hiệu hoá nên không chấp nhận được."),
        CalendarShareFailure.CandidatesReadFailed => Error(500, Msg467CandidatesReadFailed, "Không tải được danh sách người."),
        CalendarShareFailure.BadQuery => Results.BadRequest(),
        CalendarShareFailure.ReadFailed => Error(500, Msg453ReadFailed,
            "Không tải được dữ liệu chia sẻ lịch. Vui lòng thử lại."),
        _ => Error(503, Msg447SaveFailed, "Không lưu được dữ liệu chia sẻ lịch. Vui lòng thử lại."),
    };

    private static IResult Error(int status, string code, string message)
        => Results.Json(new { code, message }, statusCode: status);
}
