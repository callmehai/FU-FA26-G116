using System.Globalization;
using TrustCabinet.Domain.GuestAccess;

namespace TrustCabinet.Web.Endpoints;

/// <summary>UC-04 — docs/03-api/16-truy-cap-khach-uc04.md.</summary>
public static class GuestAccessEndpoints
{
    private const string Msg126InvalidCode = "MSG126";
    private const string Msg371InvitationInvalid = "MSG371";
    private const string Msg372CodeLocked = "MSG372";
    private const string Msg373TooSoon = "MSG373";
    private const string Msg374Unavailable = "MSG374";
    private const string Msg375CodeSent = "MSG375";
    private const string Msg376ProfileSaved = "MSG376";
    private const string Msg377ProfileInvalid = "MSG377";
    private const string Msg378SessionEnded = "MSG378";

    private sealed record LinkBody(string? Token);
    private sealed record VerifyBody(string? Token, string? Code);
    private sealed record ProfileBody(string? DisplayName, string? JobTitle);

    public static void MapGuestAccessEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous: the link token in the body is the only credential before verification.
        var invitation = app.MapGroup("/api/guest-access/invitations/{invitationId:guid}").AllowAnonymous();
        invitation.MapPost("", async (Guid invitationId, LinkBody? body, IGuestAccessService service, HttpContext context,
            CancellationToken ct) => Map(await service.OpenAsync(invitationId, body?.Token, ct), context));
        invitation.MapPost("/code", async (Guid invitationId, LinkBody? body, IGuestAccessService service, HttpContext context,
            CancellationToken ct) =>
        {
            var result = await service.SendCodeAsync(invitationId, body?.Token, ct);
            return result is { Failure: GuestAccessFailure.None, Data: GuestCodeSent sent }
                ? Map(result with { Data = new { code = Msg375CodeSent, message = "Đã gửi mã xác minh tới email được mời.",
                    sent.ExpiresInSeconds, sent.ResendAfterSeconds } }, context)
                : Map(result, context);
        });
        invitation.MapPost("/verify", async (Guid invitationId, VerifyBody? body, IGuestAccessService service,
            HttpContext context, CancellationToken ct)
            => Map(await service.VerifyAsync(invitationId, body?.Token, body?.Code, ct), context));

        // The guest's own session. A fixed endpoint, so an internal token gets 403 rather than 404.
        var session = app.MapGroup("/api/guest-access/session").RequireAuthorization();
        session.MapGet("", async (HttpContext context, IMeetingGuestIdentityResolver identities, IGuestAccessService service,
            CancellationToken ct) => identities.Resolve(context.User) is { } guest
                ? Map(await service.GetSessionAsync(guest, ct), context)
                : Results.Forbid());
        session.MapPut("/profile", async (ProfileBody? body, HttpContext context, IMeetingGuestIdentityResolver identities,
            IGuestAccessService service, CancellationToken ct) =>
        {
            if (identities.Resolve(context.User) is not { } guest) return Results.Forbid();
            var result = await service.SaveProfileAsync(guest, body?.DisplayName, body?.JobTitle, ct);
            return result is { Failure: GuestAccessFailure.None }
                ? Map(result with { Data = new { code = Msg376ProfileSaved, message = "Đã lưu thông tin hiển thị.", session = result.Data } }, context)
                : Map(result, context);
        });
        session.MapDelete("", async (HttpContext context, IMeetingGuestIdentityResolver identities, IGuestAccessService service,
            CancellationToken ct) =>
        {
            if (identities.Resolve(context.User) is not { } guest) return Results.Forbid();
            var result = await service.SignOutAsync(guest, ct);
            return result.Failure == GuestAccessFailure.None ? Results.NoContent() : Map(result, context);
        });
    }

    private static IResult Map(GuestAccessResult result, HttpContext context)
    {
        // Responses carry tokens and personal data; no cache may keep them.
        context.Response.Headers.CacheControl = "no-store";
        switch (result.Failure)
        {
            case GuestAccessFailure.None:
                return Results.Ok(result.Data);
            case GuestAccessFailure.InvalidInvitation:
                return Error(404, Msg371InvitationInvalid, "Lời mời không còn hiệu lực. Vui lòng liên hệ Thư ký cuộc họp.");
            case GuestAccessFailure.InvalidCode:
                return Error(400, Msg126InvalidCode, "Không xác minh được lời mời. Vui lòng kiểm tra mã và thử lại.");
            case GuestAccessFailure.CodeLocked:
                return Error(400, Msg372CodeLocked, "Bạn đã nhập sai quá số lần cho phép. Vui lòng gửi lại mã mới.");
            case GuestAccessFailure.TooSoon:
                context.Response.Headers.RetryAfter = result.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
                return Results.Json(new { code = Msg373TooSoon, message = "Vui lòng chờ trước khi gửi lại mã.",
                    retryAfterSeconds = result.RetryAfterSeconds }, statusCode: 429);
            case GuestAccessFailure.InvalidProfile:
                return Results.Json(new { code = Msg377ProfileInvalid, message = "Thông tin hiển thị không hợp lệ.",
                    errors = result.Errors }, statusCode: 400);
            case GuestAccessFailure.SessionEnded:
                return Error(404, Msg378SessionEnded, "Phiên truy cập đã kết thúc. Vui lòng mở lại liên kết trong email.");
            default:
                return Error(503, Msg374Unavailable, "Không thể xác minh lúc này. Vui lòng thử lại sau.");
        }
    }

    private static IResult Error(int status, string code, string message)
        => Results.Json(new { code, message }, statusCode: status);
}
