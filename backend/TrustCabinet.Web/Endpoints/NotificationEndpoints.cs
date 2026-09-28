using System.Security.Claims;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Notifications;

namespace TrustCabinet.Web.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var notifications = app.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireActiveUser(ServiceUnavailable());

        notifications.MapGet("/", ListAsync);
        notifications.MapGet("/unread-count", CountAsync);
        notifications.MapPost("/{id:guid}/read", MarkReadAsync);
        notifications.MapPost("/read-all", MarkAllReadAsync);
        notifications.MapGet("/{id:guid}/target", TargetAsync);
        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpRequest request,
        ClaimsPrincipal principal,
        INotificationService service,
        CancellationToken ct)
    {
        if (!NotificationListQueryParser.TryParse(
            request.Query, out var page, out var pageSize, out var errors))
        {
            return Results.Json(
                new NotificationErrorResponse(
                    "NOTIFICATION_INVALID_QUERY",
                    "Tham số truy vấn không hợp lệ.",
                    errors),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await service.ListAsync(
            CapabilityGate.GatedUserId(principal), page, pageSize, ct);
        return Results.Ok(new
        {
            items = result.Items.Select(NotificationResponse.From),
            result.UnreadCount,
            result.TotalCount,
            result.Page,
            result.PageSize,
            totalPages = result.TotalCount / result.PageSize
                + (result.TotalCount % result.PageSize == 0 ? 0 : 1),
        });
    }

    private static async Task<IResult> CountAsync(
        ClaimsPrincipal principal, INotificationService service, CancellationToken ct)
        => Results.Ok(new
        {
            unreadCount = await service.GetUnreadCountAsync(CapabilityGate.GatedUserId(principal), ct),
        });

    private static async Task<IResult> MarkReadAsync(
        Guid id, ClaimsPrincipal principal, INotificationService service, CancellationToken ct)
    {
        var item = await service.MarkReadAsync(CapabilityGate.GatedUserId(principal), id, ct);
        return item is null ? Results.NotFound() : Results.Ok(NotificationResponse.From(item));
    }

    private static async Task<IResult> MarkAllReadAsync(
        ClaimsPrincipal principal, INotificationService service, CancellationToken ct)
        => Results.Ok(new
        {
            markedCount = await service.MarkAllReadAsync(CapabilityGate.GatedUserId(principal), ct),
            unreadCount = 0,
        });

    private static async Task<IResult> TargetAsync(
        Guid id, ClaimsPrincipal principal, INotificationService service, CancellationToken ct)
    {
        var target = await service.GetTargetAsync(CapabilityGate.GatedUserId(principal), id, ct);
        return target is null
            ? Results.NotFound()
            : Results.Ok(NotificationTargetResponse.From(target));
    }

    private static IResult ServiceUnavailable()
        => Results.Json(
            new NotificationErrorResponse(
                "NOTIFICATION_LOAD_FAILED",
                "Không thể tải thông báo. Vui lòng thử lại."),
            statusCode: StatusCodes.Status503ServiceUnavailable);
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
            item.MeetingId, item.TargetType, item.TargetId,
            NotificationTargetResponse.BuildUrl(item.TargetType, item.TargetId),
            item.IsRead, item.ReadAt, item.CreatedAt);
}

public sealed record NotificationTargetResponse(string TargetType, Guid TargetId, string Url)
{
    public static NotificationTargetResponse From(NotificationTarget target)
        => new(target.TargetType, target.TargetId,
            BuildUrl(target.TargetType, target.TargetId)
                ?? throw new InvalidOperationException("Unsupported notification target."));

    public static string? BuildUrl(string? targetType, Guid? targetId)
        => targetType == "Meeting" && targetId is { } id
            ? $"/mgmt/meetings/{id:D}"
            : null;
}

public sealed record NotificationErrorResponse(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string>? Errors = null);
