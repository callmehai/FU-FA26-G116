using System.Security.Claims;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Users;
using TrustCabinet.Infrastructure.Notifications;

namespace TrustCabinet.Web.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var notifications = app.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireAuthorization()
            .AddEndpointFilter(async (context, next) =>
            {
                var userId = CapabilityGate.CurrentUserId(context.HttpContext.User);
                if (userId is null)
                {
                    return Results.Forbid();
                }

                var users = context.HttpContext.RequestServices
                    .GetRequiredService<IUserAccountRepository>();
                var user = await users.FindByIdAsync(userId.Value,
                    context.HttpContext.RequestAborted);
                return user is { IsActive: true }
                    ? await next(context)
                    : Results.Forbid();
            });

        notifications.MapGet("/", ListAsync);
        notifications.MapGet("/unread-count", CountAsync);
        notifications.MapPost("/{id:guid}/read", MarkReadAsync);
        notifications.MapPost("/read-all", MarkAllReadAsync);
        notifications.MapGet("/{id:guid}/target", TargetAsync);
        return app;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal, NotificationService service,
        int limit = 50, int offset = 0, CancellationToken ct = default)
    {
        if (limit is < 1 or > 100 || offset < 0)
        {
            return Results.BadRequest(new { message = "Giới hạn hoặc vị trí danh sách không hợp lệ." });
        }

        var page = await service.ListAsync(CapabilityGate.GatedUserId(principal), limit, offset, ct);
        return Results.Ok(new
        {
            items = page.Items.Select(NotificationResponse.From),
            page.UnreadCount,
            page.TotalCount,
            limit,
            offset,
        });
    }

    private static async Task<IResult> CountAsync(
        ClaimsPrincipal principal, NotificationService service, CancellationToken ct)
        => Results.Ok(new
        {
            unreadCount = await service.GetUnreadCountAsync(CapabilityGate.GatedUserId(principal), ct),
        });

    private static async Task<IResult> MarkReadAsync(
        Guid id, ClaimsPrincipal principal, NotificationService service, CancellationToken ct)
    {
        var item = await service.MarkReadAsync(CapabilityGate.GatedUserId(principal), id, ct);
        return item is null ? Results.NotFound() : Results.Ok(NotificationResponse.From(item));
    }

    private static async Task<IResult> MarkAllReadAsync(
        ClaimsPrincipal principal, NotificationService service, CancellationToken ct)
        => Results.Ok(new
        {
            markedCount = await service.MarkAllReadAsync(CapabilityGate.GatedUserId(principal), ct),
            unreadCount = 0,
        });

    private static async Task<IResult> TargetAsync(
        Guid id, ClaimsPrincipal principal, NotificationService service, CancellationToken ct)
    {
        var target = await service.GetTargetAsync(CapabilityGate.GatedUserId(principal), id, ct);
        return target is null ? Results.NotFound() : Results.Ok(target);
    }
}

public sealed record NotificationResponse(
    Guid Id,
    string Type,
    string Title,
    string Body,
    Guid? MeetingId,
    string? TargetType,
    Guid? TargetId,
    string? ActionUrl,
    bool IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt)
{
    public static NotificationResponse From(Notification item)
        => new(item.Id, item.Type.ToString(), item.Title, item.Body,
            item.MeetingId, item.TargetType, item.TargetId, item.ActionUrl,
            item.IsRead, item.ReadAt, item.CreatedAt);
}
