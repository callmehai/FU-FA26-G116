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

    private sealed record LinkBody(string? Token);
    private sealed record VerifyBody(string? Token, string? Code);

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
            default:
                return Error(503, Msg374Unavailable, "Không thể xác minh lúc này. Vui lòng thử lại sau.");
        }
    }

    private static IResult Error(int status, string code, string message)
        => Results.Json(new { code, message }, statusCode: status);
}
