using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Speaking;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// Register to Speak (SCRUM-27, UC-79): raising and lowering a hand in the online room, and the Chair's queue.
/// Contract in docs/03-api/20-dang-ky-phat-bieu.md. No body on any route: every action is named by its path.
/// </summary>
public static class SpeakingEndpoints
{
    private const string Msg117NotInProgress = "MSG117";
    private const string Msg457NotChair = "MSG457";
    private const string Msg458NotInRoom = "MSG458";
    private const string Msg459FloorTaken = "MSG459";
    private const string Msg460NotOpen = "MSG460";
    private const string Msg461ReadFailed = "MSG461";
    private const string Msg462SaveFailed = "MSG462";

    public static void MapSpeakingEndpoints(this IEndpointRouteBuilder app)
    {
        // A queue is live state: never served from a cache.
        var group = app.MapGroup("/api/meetings/{meetingId:guid}/speaking-requests").RequireAuthorization()
            .AddEndpointFilter(async (context, next) =>
            {
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                return await next(context);
            });

        group.MapGet("", async (Guid meetingId, HttpContext context, ISpeakingService service,
            IMeetingGuestIdentityResolver identities, CancellationToken ct)
            => Map(await service.ListAsync(meetingId, Actor(context, identities), ct)));

        Register(SpeakingOperation.Raise, "POST", "");
        Register(SpeakingOperation.Lower, "DELETE", "/{requestId:guid}");
        Register(SpeakingOperation.MoveUp, "POST", "/{requestId:guid}/move-up");
        Register(SpeakingOperation.MoveDown, "POST", "/{requestId:guid}/move-down");
        Register(SpeakingOperation.Decline, "POST", "/{requestId:guid}/decline");
        Register(SpeakingOperation.Invite, "POST", "/{requestId:guid}/invite");
        Register(SpeakingOperation.Start, "POST", "/{requestId:guid}/start");
        Register(SpeakingOperation.Finish, "POST", "/{requestId:guid}/finish");

        void Register(SpeakingOperation operation, string method, string suffix)
            => group.MapMethods(suffix, [method], async (Guid meetingId, HttpContext context, ISpeakingService service,
                IMeetingGuestIdentityResolver identities, CancellationToken ct) =>
            {
                Guid? requestId = context.Request.RouteValues.TryGetValue("requestId", out var value)
                    ? Guid.Parse(value!.ToString()!) : null;
                return Map(await service.MutateAsync(meetingId, requestId, Actor(context, identities), operation, ct));
            });
    }

    private static ParticipantActor Actor(HttpContext context, IMeetingGuestIdentityResolver identities)
        => new(CapabilityGate.CurrentUserId(context.User), identities.Resolve(context.User));

    private static IResult Map(SpeakingOutcome result)
    {
        switch (result.Failure)
        {
            case SpeakingFailure.None:
                return result.Created ? Results.Json(result.Data, statusCode: 201) : Results.Ok(result.Data);
            // Not readable, or not in this meeting: the same answer as a meeting that does not exist.
            case SpeakingFailure.NotFound or SpeakingFailure.NotOwner:
                return Results.NotFound();
            case SpeakingFailure.Inactive:
                return Results.Forbid();
        }
        var (status, code, message) = result.Failure switch
        {
            SpeakingFailure.NotInProgress => (409, Msg117NotInProgress, "Phòng họp chỉ mở khi cuộc họp đang diễn ra."),
            SpeakingFailure.NotChair => (403, Msg457NotChair, "Chỉ Chủ toạ hiện hành được điều hành lượt phát biểu."),
            SpeakingFailure.NotInRoom => (409, Msg458NotInRoom, "Bạn cần đang ở trong phòng họp để đăng ký phát biểu."),
            SpeakingFailure.FloorTaken => (409, Msg459FloorTaken, "Đang có người được mời hoặc đang phát biểu. Hãy kết thúc lượt đó trước."),
            SpeakingFailure.NotOpen => (409, Msg460NotOpen, "Yêu cầu phát biểu này đã thay đổi. Hãy tải lại danh sách."),
            SpeakingFailure.ReadFailed => (500, Msg461ReadFailed, "Không tải được danh sách phát biểu. Vui lòng thử lại."),
            _ => (503, Msg462SaveFailed, "Không lưu được thay đổi lượt phát biểu. Vui lòng thử lại.")
        };
        return Results.Json(new { code, message }, statusCode: status);
    }
}
