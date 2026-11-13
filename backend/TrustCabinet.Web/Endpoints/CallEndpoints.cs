using System.Security.Claims;
using System.Text.Json;
using TrustCabinet.Domain.Calls;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// The meeting call room (SCRUM-39, SCRUM-41, ADR-0010): room state, joining, chair mute, and the
/// LiveKit webhook. Media and signalling go through LiveKit; this only decides who may take part.
/// A verified guest (UC-04 token, SCRUM-59) may read the room and join it; muting stays a chair's.
/// </summary>
public static class CallEndpoints
{
    /// <summary>
    /// Message codes from docs/03-api/00-danh-muc-ma-thong-bao.md: the UC-37/UC-41 codes reserved for
    /// the room where the situation matches, new codes from MSG280 where none was reserved.
    /// </summary>
    private const string Msg117RoomNotOpen = "MSG117";
    private const string Msg280InPersonMeeting = "MSG280";
    private const string Msg281CallUnavailable = "MSG281";
    private const string Msg118JoinFailed = "MSG118";
    private const string Msg123NotChair = "MSG123";
    private const string Msg282TargetNotInRoom = "MSG282";
    private const string Msg124ProviderFailed = "MSG124";
    private const string Msg283RoomLoadFailed = "MSG283";
    private const string Msg284MuteFailed = "MSG284";
    private const string Msg380RecordingNoticeRequired = "MSG380";

    /// <summary>A webhook body is a few KB; anything this large is not LiveKit.</summary>
    private const int MaxWebhookBytes = 1024 * 1024;

    public static IEndpointRouteBuilder MapCallEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/meetings/{id:guid}/call")
            .WithTags("Calls")
            .RequireActiveUserOrGuest(ServerError(Msg283RoomLoadFailed, "Không thể tải trạng thái phòng họp. Vui lòng thử lại."))
            .MapGet("/", GetAsync);

        app.MapGroup("/api/meetings/{id:guid}/call")
            .WithTags("Calls")
            .RequireActiveUserOrGuest(ServerError(Msg118JoinFailed, "Không thể vào phòng họp. Vui lòng thử lại."))
            .MapPost("/join", JoinAsync);

        app.MapGroup("/api/meetings/{id:guid}/call")
            .WithTags("Calls")
            .RequireActiveUser(ServerError(Msg284MuteFailed, "Không thể tắt micro. Vui lòng thử lại."))
            .MapPost("/participants/{identity}/mute", MuteAsync);

        // Anonymous at the HTTP layer: LiveKit proves itself with a signature over the body.
        app.MapPost("/api/livekit/webhook", ReceiveWebhookAsync)
            .WithTags("Calls")
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> GetAsync(
        Guid id, ClaimsPrincipal principal, IMeetingGuestIdentityResolver identities, ICallRoomService service, CancellationToken ct)
    {
        var result = await service.GetRoomAsync(id, Caller(principal, identities), ct);
        return result.Outcome switch
        {
            CallOutcome.Success => Results.Ok(CallRoomResponse.From(result.Value!)),
            CallOutcome.NotFound => Results.NotFound(),
            _ => ServerError(Msg283RoomLoadFailed, "Không thể tải trạng thái phòng họp. Vui lòng thử lại."),
        };
    }

    private static async Task<IResult> JoinAsync(
        Guid id, HttpRequest request, ClaimsPrincipal principal, IMeetingGuestIdentityResolver identities,
        ICallRoomService service, CancellationToken ct)
    {
        var acknowledged = await ReadAcknowledgedAsync(request, ct);
        var result = await service.JoinAsync(id, Caller(principal, identities), acknowledged, ct);
        return result.Outcome switch
        {
            CallOutcome.Success => Results.Ok(CallJoinResponse.From(result.Value!)),
            CallOutcome.NotFound => Results.NotFound(),
            CallOutcome.InPersonMeeting => Conflict(Msg280InPersonMeeting, "Cuộc họp này họp trực tiếp nên không có phòng họp trực tuyến."),
            CallOutcome.RoomNotOpen => Conflict(Msg117RoomNotOpen, "Phòng họp chỉ mở khi cuộc họp đang diễn ra."),
            CallOutcome.NotConfigured => Unavailable(),
            CallOutcome.RecordingNoticeRequired => Results.Json(
                new CallErrorResponse(Msg380RecordingNoticeRequired, "Vui lòng xác nhận đã đọc thông báo ghi âm trước khi vào phòng họp."),
                statusCode: StatusCodes.Status400BadRequest),
            _ => ServerError(Msg118JoinFailed, "Không thể vào phòng họp. Vui lòng thử lại."),
        };
    }

    /// <summary>Only a guest must send it; a member's join takes no body.</summary>
    internal sealed record JoinBody(bool? RecordingNoticeAcknowledged);

    /// <summary>
    /// Read by hand rather than bound: a body the binder refuses (<c>"true"</c> as a string, broken JSON, another content
    /// type) would be a bare 400, where a guest must get MSG380 as for a missing field. Only a JSON <c>true</c> counts.
    /// </summary>
    private static async Task<bool> ReadAcknowledgedAsync(HttpRequest request, CancellationToken ct)
    {
        if (!request.HasJsonContentType()) return false;
        try
        {
            return (await request.ReadFromJsonAsync<JoinBody>(ct))?.RecordingNoticeAcknowledged == true;
        }
        // InvalidOperationException: a charset the reader does not know (application/json; charset=bogus).
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>A guest token for this meeting, or the signed-in user the filter already checked is active.</summary>
    private static CallCaller Caller(ClaimsPrincipal principal, IMeetingGuestIdentityResolver identities)
        => identities.Resolve(principal) is { } guest ? new CallCaller(Guest: guest) : CapabilityGate.GatedUserId(principal);

    /// <param name="identity">The LiveKit identity from the room: a user id, or <c>guest-{guestSessionId}</c>.</param>
    private static async Task<IResult> MuteAsync(
        Guid id, string identity, ClaimsPrincipal principal, ICallRoomService service, CancellationToken ct)
    {
        if (!CallMember.TryParse(identity, out var target))
        {
            return Results.NotFound();
        }

        var outcome = await service.MuteAsync(id, CapabilityGate.GatedUserId(principal), target, ct);
        return outcome switch
        {
            CallOutcome.Success => Results.NoContent(),
            CallOutcome.NotFound => Results.NotFound(),
            CallOutcome.NotChair => Results.Json(
                new CallErrorResponse(Msg123NotChair, "Chỉ chủ toạ mới tắt được micro của người khác."),
                statusCode: StatusCodes.Status403Forbidden),
            CallOutcome.RoomNotOpen => Conflict(Msg117RoomNotOpen, "Phòng họp chỉ mở khi cuộc họp đang diễn ra."),
            CallOutcome.TargetNotInRoom => Conflict(Msg282TargetNotInRoom, "Người này không còn trong phòng hoặc chưa bật micro."),
            CallOutcome.NotConfigured => Unavailable(),
            CallOutcome.ProviderFailed => Results.Json(
                new CallErrorResponse(Msg124ProviderFailed, "Máy chủ phòng họp không phản hồi. Vui lòng thử lại."),
                statusCode: StatusCodes.Status502BadGateway),
            _ => ServerError(Msg284MuteFailed, "Không thể tắt micro. Vui lòng thử lại."),
        };
    }

    private static async Task<IResult> ReceiveWebhookAsync(
        HttpRequest request,
        ILiveKitGateway liveKit,
        ICallRoomService service,
        ILoggerFactory loggers,
        CancellationToken ct)
    {
        if (!liveKit.IsConfigured)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (request.ContentLength > MaxWebhookBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, ct);
        if (buffer.Length > MaxWebhookBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        // The JWT comes bare, without a "Bearer " prefix.
        var webhook = liveKit.ReadWebhook(buffer.ToArray(), request.Headers.Authorization.ToString());
        if (webhook is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            await service.HandleWebhookAsync(webhook, ct);
            return Results.Ok();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // 500 makes LiveKit retry; the reconciliation job catches whatever still slips through.
            loggers.CreateLogger(nameof(CallEndpoints))
                .LogError(error, "LiveKit webhook {Event} ({Id}) failed", webhook.Event, webhook.Id);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static IResult Conflict(string code, string message)
        => Results.Json(new CallErrorResponse(code, message), statusCode: StatusCodes.Status409Conflict);

    private static IResult Unavailable()
        => Results.Json(
            new CallErrorResponse(Msg281CallUnavailable, "Phòng họp trực tuyến chưa sẵn sàng. Vui lòng liên hệ quản trị viên."),
            statusCode: StatusCodes.Status503ServiceUnavailable);

    private static IResult ServerError(string code, string message)
        => Results.Json(new CallErrorResponse(code, message), statusCode: StatusCodes.Status500InternalServerError);
}

public sealed record CallRoomResponse(
    Guid MeetingId, string Status, string MyRole, bool Available, int? ParticipantCount, int RetentionDays)
{
    public static CallRoomResponse From(CallRoomView view) => new(
        view.MeetingId, view.Status.ToString(), view.MyRole.ToString(), view.Available, view.ParticipantCount,
        view.RetentionDays);
}

public sealed record CallJoinResponse(
    string ServerUrl, string Token, string RoomName, string Identity, DateTimeOffset ExpiresAt)
{
    public static CallJoinResponse From(CallJoinTicket ticket) => new(
        ticket.ServerUrl, ticket.Token, ticket.RoomName, ticket.Identity, ticket.ExpiresAt);
}

public sealed record CallErrorResponse(string Code, string Message);
