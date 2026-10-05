using System.Security.Claims;
using System.Text.Json;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// HTTP boundary for UC-53 (view) and UC-26 to UC-28 (create, update, cancel) Meeting Sessions.
/// Access is by relationship to the meeting, so no system capability gates these routes.
/// </summary>
public static class MeetingSessionEndpoints
{
    private const string Msg48Created = "MSG48";
    private const string Msg49Updated = "MSG49";
    private const string Msg50Cancelled = "MSG50";
    private const string Msg53InvalidInformation = "MSG53";
    private const string Msg54CannotAdd = "MSG54";
    private const string Msg55CannotEdit = "MSG55";
    private const string Msg56CannotCancel = "MSG56";
    private const string Msg127LoadFailed = "MSG127";
    private const string Msg131ActiveUserCheckFailed = "MSG131";
    private const string Msg138CreateFailed = "MSG138";
    private const string Msg139UpdateFailed = "MSG139";
    private const string Msg140CancelFailed = "MSG140";

    public static IEndpointRouteBuilder MapMeetingSessionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/meetings/{meetingId:guid}/sessions")
            .WithTags("Meeting Sessions")
            .RequireActiveUser(Error(
                StatusCodes.Status500InternalServerError,
                Msg127LoadFailed,
                "Không thể tải danh sách phiên họp. Vui lòng thử lại."))
            .MapGet("/", ListAsync);

        var planning = app.MapGroup("/api/meetings/{meetingId:guid}/sessions")
            .WithTags("Meeting Sessions")
            .RequireActiveUser(Error(
                StatusCodes.Status503ServiceUnavailable,
                Msg131ActiveUserCheckFailed,
                "Không thể kiểm tra tài khoản để thực hiện thao tác cuộc họp. Vui lòng thử lại."));
        planning.MapPost("/", CreateAsync);
        planning.MapPut("/{sessionId:guid}", UpdateAsync);
        planning.MapPost("/{sessionId:guid}/cancel", CancelAsync);
        return app;
    }

    private static async Task<IResult> ListAsync(
        Guid meetingId, ClaimsPrincipal principal, IMeetingSessionService service, CancellationToken ct)
    {
        if (CapabilityGate.CurrentUserId(principal) is not { } actorId)
        {
            return Results.Unauthorized();
        }

        var result = await service.ListAsync(meetingId, actorId, ct);
        return result.Outcome switch
        {
            MeetingSessionOutcome.Success => Results.Ok(result.Sessions!.Select(MeetingSessionResponse.From).ToArray()),
            MeetingSessionOutcome.NotFound => Results.NotFound(),
            _ => Error(
                StatusCodes.Status500InternalServerError,
                Msg127LoadFailed,
                "Không thể tải danh sách phiên họp. Vui lòng thử lại."),
        };
    }

    private static async Task<IResult> CreateAsync(
        Guid meetingId, HttpRequest request, ClaimsPrincipal principal, IMeetingSessionService service, CancellationToken ct)
    {
        if (CapabilityGate.CurrentUserId(principal) is not { } actorId)
        {
            return Results.Unauthorized();
        }

        if (await ReadInputAsync(request, ct) is not { } input)
        {
            return InvalidBody();
        }

        var result = await service.CreateAsync(meetingId, input, actorId, ct);
        return ToResult(
            result,
            StatusCodes.Status201Created,
            Msg48Created,
            "Tạo phiên họp thành công.",
            Msg54CannotAdd,
            "Không thể thêm phiên họp cho cuộc họp này nữa.",
            Msg138CreateFailed,
            "Không thể tạo phiên họp. Vui lòng thử lại.");
    }

    private static async Task<IResult> UpdateAsync(
        Guid meetingId,
        Guid sessionId,
        HttpRequest request,
        ClaimsPrincipal principal,
        IMeetingSessionService service,
        CancellationToken ct)
    {
        if (CapabilityGate.CurrentUserId(principal) is not { } actorId)
        {
            return Results.Unauthorized();
        }

        if (await ReadInputAsync(request, ct) is not { } input)
        {
            return InvalidBody();
        }

        var result = await service.UpdateAsync(meetingId, sessionId, input, actorId, ct);
        return ToResult(
            result,
            StatusCodes.Status200OK,
            Msg49Updated,
            "Cập nhật phiên họp thành công.",
            Msg55CannotEdit,
            "Phiên họp này không thể chỉnh sửa được nữa.",
            Msg139UpdateFailed,
            "Không thể cập nhật phiên họp. Vui lòng thử lại.");
    }

    private static async Task<IResult> CancelAsync(
        Guid meetingId, Guid sessionId, ClaimsPrincipal principal, IMeetingSessionService service, CancellationToken ct)
    {
        if (CapabilityGate.CurrentUserId(principal) is not { } actorId)
        {
            return Results.Unauthorized();
        }

        var result = await service.CancelAsync(meetingId, sessionId, actorId, ct);
        return ToResult(
            result,
            StatusCodes.Status200OK,
            Msg50Cancelled,
            "Hủy phiên họp thành công.",
            Msg56CannotCancel,
            "Phiên họp này không thể hủy được nữa.",
            Msg140CancelFailed,
            "Không thể hủy phiên họp. Vui lòng thử lại.");
    }

    /// <summary>The same code covers "not the assigned Secretary" (403) and "status no longer allows it" (409).</summary>
    private static IResult ToResult(
        MeetingSessionResult result,
        int successStatus,
        string successCode,
        string successMessage,
        string refusedCode,
        string refusedMessage,
        string failedCode,
        string failedMessage)
        => result.Outcome switch
        {
            MeetingSessionOutcome.Success => Results.Json(
                new MeetingSessionMutationResponse(successCode, successMessage, MeetingSessionResponse.From(result.Session!)),
                statusCode: successStatus),
            MeetingSessionOutcome.InvalidInformation => Error(
                StatusCodes.Status400BadRequest,
                Msg53InvalidInformation,
                "Thông tin phiên họp không hợp lệ.",
                result.Errors),
            MeetingSessionOutcome.NotFound => Results.NotFound(),
            MeetingSessionOutcome.Forbidden => Error(StatusCodes.Status403Forbidden, refusedCode, refusedMessage),
            MeetingSessionOutcome.StatusConflict => Error(StatusCodes.Status409Conflict, refusedCode, refusedMessage),
            _ => Error(StatusCodes.Status500InternalServerError, failedCode, failedMessage),
        };

    /// <summary>Null when the body is not JSON or does not match the request shape.</summary>
    private static async Task<MeetingSessionInput?> ReadInputAsync(HttpRequest request, CancellationToken ct)
    {
        if (!request.HasJsonContentType())
        {
            return null;
        }

        try
        {
            var body = await request.ReadFromJsonAsync<MeetingSessionRequest>(ct);
            return new MeetingSessionInput(body?.Title, body?.PlannedStartsAt, body?.PlannedEndsAt);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IResult InvalidBody()
        => Error(StatusCodes.Status400BadRequest, Msg53InvalidInformation, "Nội dung JSON không hợp lệ.");

    private static IResult Error(
        int statusCode, string code, string message, IReadOnlyDictionary<string, string>? errors = null)
        => Results.Json(new MeetingErrorResponse(code, message, errors), statusCode: statusCode);
}

/// <summary>Timestamps are ISO 8601 strings with <c>Z</c> or an offset, validated by the server.</summary>
public sealed record MeetingSessionRequest(string? Title, string? PlannedStartsAt, string? PlannedEndsAt);

public sealed record MeetingSessionResponse(
    Guid Id,
    Guid MeetingId,
    string Title,
    DateTimeOffset PlannedStartsAt,
    DateTimeOffset PlannedEndsAt,
    string Status)
{
    public static MeetingSessionResponse From(MeetingSessionDetails session) => new(
        session.Id,
        session.MeetingId,
        session.Title,
        session.PlannedStartsAt,
        session.PlannedEndsAt,
        session.Status.ToString());
}

public sealed record MeetingSessionMutationResponse(string Code, string Message, MeetingSessionResponse Session);
