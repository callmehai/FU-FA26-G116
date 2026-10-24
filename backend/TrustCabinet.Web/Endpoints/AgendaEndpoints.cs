using System.Text.Json;
using System.Text.Json.Serialization;
using TrustCabinet.Domain.Agenda;

namespace TrustCabinet.Web.Endpoints;

public static class AgendaEndpoints
{
    private const string Msg60Created = "MSG60";
    private const string Msg61Updated = "MSG61";
    private const string Msg62Deleted = "MSG62";
    private const string Msg63Reordered = "MSG63";
    private const string Msg64Started = "MSG64";
    private const string Msg65Completed = "MSG65";
    private const string Msg66Invalid = "MSG66";
    private const string Msg67PlanningDenied = "MSG67";
    private const string Msg68CreateFailed = "MSG68";
    private const string Msg69UpdateFailed = "MSG69";
    private const string Msg70DeleteFailed = "MSG70";
    private const string Msg71ReorderFailed = "MSG71";
    private const string Msg72AlreadyCurrent = "MSG72";
    private const string Msg73StartDenied = "MSG73";
    private const string Msg74CompleteDenied = "MSG74";
    private const string Msg75StartFailed = "MSG75";
    private const string Msg76CompleteFailed = "MSG76";
    private const string Msg128ReadFailed = "MSG128";
    private const string Msg142DeleteLocked = "MSG142";
    private const string Msg143OrderConflict = "MSG143";
    private const string Msg34UnsupportedMedia = "MSG34";
    private const string Msg87BodyTooLarge = "MSG87";
    private const string Msg174NotFound = "MSG174";
    private const string Msg271ConclusionSaved = "MSG271";
    private const string Msg272ConclusionInvalid = "MSG272";
    private const string Msg273ConclusionDenied = "MSG273";
    private const string Msg274ConclusionFailed = "MSG274";
    private const int MaxBodyBytes = 64 * 1024;
    private static readonly JsonSerializerOptions InputJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static void MapAgendaEndpoints(this IEndpointRouteBuilder app)
    {
        const string prefix = "/api/meetings/{meetingId:guid}/agenda";
        app.MapGet(prefix, async (Guid meetingId, HttpContext context, IAgendaService service,
            IMeetingGuestIdentityResolver identities, CancellationToken ct) =>
        {
            if (identities.Resolve(context.User) is { } guest)
                return Map(await service.GetForGuestAsync(meetingId, guest, ct), meetingId);
            var actor = CapabilityGate.CurrentUserId(context.User);
            return actor is { } userId ? Map(await service.GetAsync(meetingId, userId, ct), meetingId) : Results.Forbid();
        }).RequireAuthorization();
        var read = app.MapGroup(prefix).RequireActiveUser(Error(500, Msg128ReadFailed, "Không tải được chương trình nghị sự."))
            .AddEndpointFilter(RejectGuestWrite);
        read.MapGet("/editor-options", async (Guid meetingId, HttpContext context, IAgendaService service, CancellationToken ct)
            => Map(await service.EditorOptionsAsync(meetingId, CapabilityGate.GatedUserId(context.User), ct), meetingId));

        Register(AgendaOperation.Create, "POST", "");
        Register(AgendaOperation.Update, "PUT", "/{itemId:guid}");
        Register(AgendaOperation.Delete, "DELETE", "/{itemId:guid}");
        Register(AgendaOperation.Reorder, "PUT", "/order");
        Register(AgendaOperation.Start, "POST", "/{itemId:guid}/current");
        Register(AgendaOperation.Complete, "POST", "/{itemId:guid}/complete");

        Register(AgendaOperation.RecordConclusion, "PUT", "/{itemId:guid}/conclusion");

        void Register(AgendaOperation operation, string method, string suffix)
        {
            app.MapGroup(prefix).RequireActiveUser(Map(new(AgendaFailure.SaveFailed), Guid.Empty, operation))
                .AddEndpointFilter(RejectGuestWrite)
                .MapMethods(suffix, [method], async (Guid meetingId, HttpContext context, IAgendaService service, CancellationToken ct) =>
                {
                    var actor = CapabilityGate.GatedUserId(context.User);
                    var item = context.Request.RouteValues.TryGetValue("itemId", out var value) ? Guid.Parse(value!.ToString()!) : Guid.Empty;
                    var input = operation is AgendaOperation.Create or AgendaOperation.Update or AgendaOperation.Reorder or AgendaOperation.RecordConclusion
                        ? await ParseAsync(context.Request, operation, ct) : new AgendaInput();
                    var result = operation switch
                    {
                        AgendaOperation.Create => await service.CreateAsync(meetingId, actor, input, ct),
                        AgendaOperation.Update => await service.UpdateAsync(meetingId, item, actor, input, ct),
                        AgendaOperation.Delete => await service.DeleteAsync(meetingId, item, actor, ct),
                        AgendaOperation.Reorder => await service.ReorderAsync(meetingId, actor, input, ct),
                        AgendaOperation.Start => await service.StartAsync(meetingId, item, actor, ct),
                        AgendaOperation.RecordConclusion => await service.RecordConclusionAsync(meetingId, item, actor, input, ct),
                        _ => await service.CompleteAsync(meetingId, item, actor, ct)
                    };
                    return Map(result, meetingId, operation);
                });
        }
    }

    private static ValueTask<object?> RejectGuestWrite(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        => context.HttpContext.RequestServices.GetRequiredService<IMeetingGuestIdentityResolver>()
            .Resolve(context.HttpContext.User) is not null
            ? ValueTask.FromResult<object?>(Results.Forbid()) : next(context);

    private sealed record PlanningBody(string? Title, string? Description,
        Guid? PresenterId, Guid? PresenterGuestInvitationId, Guid[]? DocumentIds);
    private sealed record OrderBody(string? OrderVersion, Guid[]? OrderedItemIds);

    private sealed record ConclusionBody(string? Conclusion);

    private static async Task<AgendaInput> ParseAsync(HttpRequest request, AgendaOperation operation, CancellationToken ct)
    {
        if (!request.HasJsonContentType()) return new(ParseFailure: AgendaFailure.UnsupportedMedia);
        if (request.ContentLength > MaxBodyBytes) return new(ParseFailure: AgendaFailure.TooLarge);
        var buffer = new byte[MaxBodyBytes + 1];
        var count = 0;
        while (count < buffer.Length)
        {
            var read = await request.Body.ReadAsync(buffer.AsMemory(count), ct);
            if (read == 0) break;
            count += read;
        }
        if (count > MaxBodyBytes) return new(ParseFailure: AgendaFailure.TooLarge);
        try
        {
            using var json = JsonDocument.Parse(buffer.AsMemory(0, count));
            if (json.RootElement.ValueKind != JsonValueKind.Object
                || json.RootElement.EnumerateObject().Select(PropertyName).Distinct().Count() != json.RootElement.EnumerateObject().Count())
                throw new JsonException();
            if (operation == AgendaOperation.Reorder)
            {
                var order = json.RootElement.Deserialize<OrderBody>(InputJson)!;
                return new(OrderVersion: order.OrderVersion, OrderedItemIds: order.OrderedItemIds);
            }
            if (operation == AgendaOperation.RecordConclusion)
                return new(Conclusion: json.RootElement.Deserialize<ConclusionBody>(InputJson)!.Conclusion);
            var body = json.RootElement.Deserialize<PlanningBody>(InputJson)!;
            return new(body.Title, body.Description, body.PresenterId, body.PresenterGuestInvitationId, body.DocumentIds);
        }
        catch (JsonException) { return new(ParseFailure: operation == AgendaOperation.Reorder ? AgendaFailure.InvalidOrder : AgendaFailure.Invalid); }
    }

    private static string PropertyName(JsonProperty property)
    {
        try { return property.Name; }
        catch (InvalidOperationException ex)
        {
            // JsonDocument defers decoding escaped property names until they are accessed.
            throw new JsonException("Invalid Unicode in JSON property name.", ex);
        }
    }

    private static IResult Map(AgendaOutcome result, Guid meetingId, AgendaOperation? operation = null)
    {
        var denied = operation == AgendaOperation.RecordConclusion ? Msg273ConclusionDenied
            : operation == AgendaOperation.Start ? Msg73StartDenied
            : operation == AgendaOperation.Complete ? Msg74CompleteDenied : Msg67PlanningDenied;
        if (result.Failure == AgendaFailure.NotFound) return operation == AgendaOperation.RecordConclusion
            ? Error(404, Msg174NotFound, "Không truy cập được cuộc họp này.") : Results.NotFound();
        if (result.Failure == AgendaFailure.Inactive) return Results.Forbid();
        if (result.Failure != AgendaFailure.None)
        {
            var (status, code, message) = result.Failure switch
            {
                AgendaFailure.Forbidden => (403, denied, "Bạn không có quyền thực hiện thao tác này."),
                AgendaFailure.Locked => (409, denied, "Trạng thái cuộc họp không cho phép thao tác này."),
                AgendaFailure.Invalid => (400, operation == AgendaOperation.RecordConclusion ? Msg272ConclusionInvalid : Msg66Invalid, "Thông tin mục nghị sự không hợp lệ."),
                AgendaFailure.UnsupportedMedia => (415, Msg34UnsupportedMedia, "Yêu cầu phải dùng Content-Type application/json."),
                AgendaFailure.TooLarge => (413, Msg87BodyTooLarge, "Nội dung yêu cầu vượt quá 64 KiB."),
                AgendaFailure.ReadFailed => (500, Msg128ReadFailed, "Không tải được chương trình nghị sự."),
                AgendaFailure.DeleteLocked => (409, Msg142DeleteLocked, "Chỉ được xóa mục nghị sự chưa bắt đầu."),
                AgendaFailure.InvalidOrder => (400, Msg143OrderConflict, "Danh sách sắp xếp phải chứa đúng mỗi mục nghị sự một lần."),
                AgendaFailure.StaleOrder => (409, Msg143OrderConflict, "Chương trình nghị sự đã thay đổi. Vui lòng tải lại."),
                AgendaFailure.AlreadyCurrent => (409, Msg72AlreadyCurrent, "Đã có mục nghị sự đang diễn ra."),
                AgendaFailure.CannotStart => (409, Msg73StartDenied, "Mục nghị sự không còn ở trạng thái chờ."),
                AgendaFailure.CannotComplete => (409, Msg74CompleteDenied, "Chỉ được hoàn thành mục nghị sự đang diễn ra."),
                _ => (503, operation switch { AgendaOperation.Create => Msg68CreateFailed, AgendaOperation.Update => Msg69UpdateFailed,
                    AgendaOperation.Delete => Msg70DeleteFailed, AgendaOperation.Reorder => Msg71ReorderFailed,
                    AgendaOperation.Start => Msg75StartFailed, AgendaOperation.RecordConclusion => Msg274ConclusionFailed, _ => Msg76CompleteFailed }, "Không lưu được thao tác nghị sự. Vui lòng thử lại.")
            };
            return Error(status, code, message, result.Errors);
        }
        if (operation is null) return Results.Ok(result.Data);
        var (successCode, successMessage) = operation switch
        {
            AgendaOperation.Create => (Msg60Created, "Tạo mục nghị sự thành công."),
            AgendaOperation.Update => (Msg61Updated, "Cập nhật mục nghị sự thành công."),
            AgendaOperation.Delete => (Msg62Deleted, "Xóa mục nghị sự thành công."),
            AgendaOperation.Reorder => (Msg63Reordered, "Sắp xếp mục nghị sự thành công."),
            AgendaOperation.Start => (Msg64Started, "Bắt đầu mục nghị sự thành công."),
            AgendaOperation.RecordConclusion => (Msg271ConclusionSaved, "Lưu kết luận mục nghị sự thành công."),
            _ => (Msg65Completed, "Hoàn thành mục nghị sự thành công.")
        };
        var body = new Dictionary<string, object?> { ["code"] = successCode, ["message"] = successMessage, ["meetingId"] = meetingId };
        if (result.ItemId is { } itemId) body["agendaItemId"] = itemId;
        return operation == AgendaOperation.Create ? Results.Created($"/api/meetings/{meetingId}/agenda", body) : Results.Ok(body);
    }

    private static IResult Error(int status, string code, string message, IReadOnlyDictionary<string, string[]>? errors = null)
        => Results.Json(new { code, message, errors }, statusCode: status);
}
