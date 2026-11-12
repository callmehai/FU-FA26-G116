using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// SCRUM-137 — find meeting time slots (UC-85a) and create a draft meeting from one (UC-85b). Every route needs an active
/// account; the Meeting Organizer right is checked again in the service, so a caller without it gets MSG448 (as the
/// shared week does) before any parameter is looked at.
/// </summary>
public static class MeetingSlotEndpoints
{
    private const string Msg29Created = "MSG29";
    private const string Msg31InvalidInformation = "MSG31";
    private const string Msg35CreateFailed = "MSG35";
    private const string Msg277InvalidParticipants = "MSG277";
    private const string Msg444InvalidSearch = "MSG444";
    private const string Msg445MostFree = "MSG445";
    private const string Msg446SlotTaken = "MSG446";
    private const string Msg448NotOrganizer = "MSG448";
    private const string Msg453ReadFailed = "MSG453";

    internal sealed record SearchBody(
        Guid[]? UserIds, bool? IncludeMe, int? DurationMinutes, DateOnly? FromDate, DateOnly? ToDate, int[]? Weekdays,
        string? WorkStart, string? WorkEnd, bool? LunchBreak, string? LunchStart, string? LunchEnd, int? BufferMinutes,
        int? StepMinutes);

    internal sealed record DraftBody(
        string? Title, string? PlannedStartTime, string? PlannedEndTime, string? Mode, string? Location, string? OnlineLink,
        string? Sensitivity, string? Description, Guid[]? UserIds, bool? IncludeMe, int? BufferMinutes,
        Guid[]? AcceptedBusyUserIds);

    public static void MapMeetingSlotEndpoints(this IEndpointRouteBuilder app)
    {
        var slots = app.MapGroup("/api/calendar/slots").WithTags("MeetingSlots")
            .RequireActiveUser(Error(500, Msg453ReadFailed, "Không tải được dữ liệu chia sẻ lịch. Vui lòng thử lại."));

        slots.MapPost("", async (SearchBody? body, HttpContext context, IMeetingSlotService service, CancellationToken ct) =>
        {
            var input = new SlotSearchInput(body?.UserIds, body?.IncludeMe, body?.DurationMinutes, body?.FromDate,
                body?.ToDate, body?.Weekdays, body?.WorkStart, body?.WorkEnd, body?.LunchBreak, body?.LunchStart,
                body?.LunchEnd, body?.BufferMinutes, body?.StepMinutes);
            var outcome = await service.FindAsync(CapabilityGate.GatedUserId(context.User), input, ct);
            // Who is busy when is personal data: never kept by a browser or proxy cache.
            context.Response.Headers.CacheControl = "no-store";
            if (outcome.Failure != MeetingSlotFailure.None) return Failure(outcome.Failure, outcome.Errors);

            var result = outcome.Result!;
            var mostFree = result.Kind == SlotResultKind.MostFree;
            return Results.Ok(new
            {
                code = mostFree ? Msg445MostFree : null,
                message = mostFree ? "Không có khung giờ nào hợp với tất cả. Đang hiện các khung nhiều người dự được nhất." : null,
                allFree = result.Kind == SlotResultKind.AllFree,
                truncated = result.Truncated,
                people = result.People,
                days = result.Days,
            });
        });

        slots.MapPost("/draft", async (DraftBody? body, HttpContext context, IMeetingSlotService service,
            CancellationToken ct) =>
        {
            var input = new SlotDraftInput(body?.Title, body?.PlannedStartTime, body?.PlannedEndTime, body?.Mode,
                body?.Location, body?.OnlineLink, body?.Sensitivity, body?.Description, body?.UserIds, body?.IncludeMe,
                body?.BufferMinutes, body?.AcceptedBusyUserIds);
            var outcome = await service.CreateDraftAsync(CapabilityGate.GatedUserId(context.User), input, ct);
            if (outcome.Failure != MeetingSlotFailure.None) return Failure(outcome.Failure, outcome.Errors);

            var result = outcome.Meeting!;
            return result.Outcome switch
            {
                MeetingOutcome.Success => Results.Created($"/api/meetings/{result.Meeting!.Id}",
                    new MeetingMutationResponse(Msg29Created, "Tạo cuộc họp thành công.", MeetingResponse.From(result.Meeting))),
                MeetingOutcome.InvalidInformation => Error(400, Msg31InvalidInformation, "Thông tin cuộc họp không hợp lệ.",
                    result.Errors),
                MeetingOutcome.InvalidParticipants => Error(400, Msg277InvalidParticipants,
                    "Danh sách thành phần tham dự không hợp lệ.", result.Errors),
                _ => Error(500, Msg35CreateFailed, "Không thể tạo cuộc họp. Vui lòng thử lại."),
            };
        });
    }

    private static IResult Failure(MeetingSlotFailure failure, IReadOnlyDictionary<string, string>? errors) => failure switch
    {
        MeetingSlotFailure.NotOrganizer => Error(403, Msg448NotOrganizer,
            "Bạn cần quyền tạo cuộc họp để thực hiện thao tác này."),
        MeetingSlotFailure.InvalidSearch => Error(400, Msg444InvalidSearch,
            "Thông tin tìm giờ họp không hợp lệ. Kiểm tra các trường được đánh dấu.", errors),
        MeetingSlotFailure.SlotTaken => Error(409, Msg446SlotTaken, "Khung giờ này không còn trống. Hãy chọn khung khác."),
        MeetingSlotFailure.InvalidSlot => Error(400, Msg31InvalidInformation, "Thông tin cuộc họp không hợp lệ.", errors),
        MeetingSlotFailure.InvalidInvitees => Error(400, Msg277InvalidParticipants,
            "Danh sách thành phần tham dự không hợp lệ.", errors),
        _ => Error(500, Msg453ReadFailed, "Không tải được dữ liệu chia sẻ lịch. Vui lòng thử lại."),
    };

    private static IResult Error(int status, string code, string message, IReadOnlyDictionary<string, string>? errors = null)
        => Results.Json(new MeetingErrorResponse(code, message, errors), statusCode: status);
}
