using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Web.Endpoints;

public static class ParticipantEndpoints
{
    private const string Msg95ReadFailed = "MSG95";
    private const string Msg96Added = "MSG96";
    private const string Msg97Invalid = "MSG97";
    private const string Msg98Locked = "MSG98";
    private const string Msg99AddFailed = "MSG99";
    private const string Msg100RoleChanged = "MSG100";
    private const string Msg101InvalidRole = "MSG101";
    private const string Msg102ChangeFailed = "MSG102";
    private const string Msg103Removed = "MSG103";
    private const string Msg104RemoveFailed = "MSG104";
    private const string Msg105Responded = "MSG105";
    private const string Msg106InvalidResponse = "MSG106";
    private const string Msg107CannotRespond = "MSG107";
    private const string Msg108ResponseFailed = "MSG108";
    private const string Msg109RequiredRoles = "MSG109";
    private const string Msg152InvalidPermission = "MSG152";
    private const string Msg153PermissionsChanged = "MSG153";
    private const string Msg154NoChanges = "MSG154";
    private const string Msg238Stale = "MSG238";
    private const string Msg239InvalidRequest = "MSG239";
    private const string Msg34UnsupportedMedia = "MSG34";
    private const string Msg87TooLarge = "MSG87";
    private const int MaxBytes = 64 * 1024;
    private static readonly JsonSerializerOptions InputJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static void MapParticipantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/meetings/{meetingId:guid}").RequireAuthorization();
        Read("/participant-context", ParticipantOperation.Context);
        Read("/participants", ParticipantOperation.Roster);
        Read("/participants/editor-options", ParticipantOperation.Options);
        Read("/invitation-response", ParticipantOperation.OwnResponse);
        Read("/invitation-responses", ParticipantOperation.Responses);
        Write("POST", "/participants/secretary-recovery", ParticipantOperation.RecoverSecretary);
        Write("POST", "/participants", ParticipantOperation.AddInternal);
        Write("POST", "/guest-invitations", ParticipantOperation.AddGuest);
        Write("PUT", "/participants/assignments", ParticipantOperation.ChangeAssignments);
        Write("PUT", "/guest-invitations/{targetId:guid}/permissions", ParticipantOperation.ChangeGuestPermissions);
        Write("DELETE", "/participants/{targetId:guid}", ParticipantOperation.RemoveInternal);
        Write("DELETE", "/guest-invitations/{targetId:guid}", ParticipantOperation.RevokeGuest);
        Write("PUT", "/invitation-response", ParticipantOperation.Respond);

        void Read(string path, ParticipantOperation operation) => group.MapGet(path,
            async (Guid meetingId, HttpContext context, IParticipantService service, IParticipantGuestIdentityResolver identities, CancellationToken ct) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                var actor = new ParticipantActor(CapabilityGate.CurrentUserId(context.User), identities.Resolve(context.User));
                var query = Query(context.Request, operation);
                return Map(await service.ReadAsync(meetingId, actor, operation, query, ct), operation, context);
            });
        void Write(string method, string path, ParticipantOperation operation) => group.MapMethods(path, [method],
            async (Guid meetingId, HttpContext context, IParticipantService service, IParticipantGuestIdentityResolver identities, CancellationToken ct) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                var actor = new ParticipantActor(CapabilityGate.CurrentUserId(context.User), identities.Resolve(context.User));
                Guid? target = context.Request.RouteValues.TryGetValue("targetId", out var value) ? Guid.Parse(value!.ToString()!) : null;
                var input = method == "DELETE" ? new ParticipantInput() : await ParseAsync(context.Request, operation, ct);
                if (method == "DELETE" && await context.Request.Body.ReadAsync(new byte[1], ct) != 0)
                    input = input with { ParseFailure = ParticipantFailure.Invalid };
                if (context.Request.Query.Count > 0) input = input with { ParseFailure = ParticipantFailure.InvalidRequest };
                if (operation != ParticipantOperation.Respond) input = input with { Revision = ParseRevision(context.Request.Headers.IfMatch.ToString()) };
                return Map(await service.MutateAsync(meetingId, target, actor, operation, input, ct), operation, context);
            });
    }

    private static ParticipantQuery Query(HttpRequest request, ParticipantOperation operation)
    {
        var allowed = operation is ParticipantOperation.Roster or ParticipantOperation.Options ? new[] { "q", "page", "pageSize" }
            : operation == ParticipantOperation.Responses ? ["page", "pageSize"] : [];
        var invalid = request.Query.Any(p => !allowed.Contains(p.Key, StringComparer.Ordinal) || p.Value.Count != 1);
        var page = 1; var size = 20;
        if (request.Query.TryGetValue("page", out var p) && !int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out page)) invalid = true;
        if (request.Query.TryGetValue("pageSize", out var s) && !int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out size)) invalid = true;
        return new(page, size, request.Query.TryGetValue("q", out var q) ? q.ToString().Trim() : null, invalid);
    }
    private static long? ParseRevision(string value)
    {
        const string prefix = "\"participants-";
        return value.StartsWith(prefix, StringComparison.Ordinal) && value.EndsWith('"')
            && long.TryParse(value.AsSpan(prefix.Length, value.Length - prefix.Length - 1), NumberStyles.None,
                CultureInfo.InvariantCulture, out var revision) && revision >= 0 ? revision : null;
    }
    private sealed record RecoveryBody(Guid? UserId);
    private sealed record InternalBody(Guid[]? UserIds, string? Role, string[]? AdditionalPermissions);
    private sealed record GuestBody(string? InvitedName, string? Email, string[]? AdditionalPermissions);
    private sealed record AssignmentsBody(ParticipantChange[]? Changes, ChairReplacement? ChairReplacement);
    private sealed record PermissionsBody(string[]? AdditionalPermissions);
    private sealed record ResponseBody(string? Response);

    private static async Task<ParticipantInput> ParseAsync(HttpRequest request, ParticipantOperation operation, CancellationToken ct)
    {
        if (!request.HasJsonContentType()) return new(ParseFailure: ParticipantFailure.UnsupportedMedia);
        if (request.ContentLength > MaxBytes) return new(ParseFailure: ParticipantFailure.TooLarge);
        var bytes = new byte[MaxBytes + 1];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = await request.Body.ReadAsync(bytes.AsMemory(count), ct);
            if (read == 0) break;
            count += read;
        }
        if (count > MaxBytes) return new(ParseFailure: ParticipantFailure.TooLarge);
        try
        {
            using var document = JsonDocument.Parse(bytes.AsMemory(0, count));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new JsonException();
            ValidateTree(root);
            return operation switch
            {
                ParticipantOperation.RecoverSecretary => new(UserId: root.Deserialize<RecoveryBody>(InputJson)!.UserId),
                ParticipantOperation.AddInternal => Internal(root.Deserialize<InternalBody>(InputJson)!),
                ParticipantOperation.AddGuest => Guest(root.Deserialize<GuestBody>(InputJson)!),
                ParticipantOperation.ChangeAssignments => Assign(root.Deserialize<AssignmentsBody>(InputJson)!),
                ParticipantOperation.ChangeGuestPermissions => new(AdditionalPermissions: root.Deserialize<PermissionsBody>(InputJson)!.AdditionalPermissions),
                ParticipantOperation.Respond => new(Response: root.Deserialize<ResponseBody>(InputJson)!.Response),
                _ => throw new JsonException()
            };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return new(ParseFailure: operation == ParticipantOperation.ChangeAssignments ? ParticipantFailure.InvalidRole : ParticipantFailure.Invalid);
        }
    }
    private static ParticipantInput Internal(InternalBody b) => new(UserIds: b.UserIds, Role: b.Role, AdditionalPermissions: b.AdditionalPermissions);
    private static ParticipantInput Guest(GuestBody b) => new(InvitedName: b.InvitedName, Email: b.Email, AdditionalPermissions: b.AdditionalPermissions);
    private static ParticipantInput Assign(AssignmentsBody b) => new(Changes: b.Changes, ChairReplacement: b.ChairReplacement);
    private static void ValidateTree(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in value.EnumerateObject())
            {
                if (!names.Add(p.Name) || p.Value.ValueKind == JsonValueKind.Null && p.Name != "chairReplacement") throw new JsonException();
                ValidateTree(p.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) { if (item.ValueKind == JsonValueKind.Null) throw new JsonException(); ValidateTree(item); }
    }

    private static IResult Map(ParticipantResult result, ParticipantOperation operation, HttpContext context)
    {
        if (result.Failure == ParticipantFailure.NotFound) return Results.NotFound();
        if (result.Failure is ParticipantFailure.Forbidden or ParticipantFailure.Inactive) return Results.StatusCode(403);
        if (result.Failure != ParticipantFailure.None)
        {
            var (status, code, message) = result.Failure switch
            {
                ParticipantFailure.ReadFailed => (500, Msg95ReadFailed, "Không tải được danh sách thành phần hoặc phản hồi lời mời."),
                ParticipantFailure.Locked => (409, Msg98Locked, "Không thể sửa thành phần ở trạng thái hiện tại của cuộc họp."),
                ParticipantFailure.InvalidRole => (400, Msg101InvalidRole, "Vai trò cuộc họp hoặc yêu cầu thay vai trò không hợp lệ."),
                ParticipantFailure.InvalidPermission => (400, Msg152InvalidPermission, "Quyền bổ sung đã chọn không được phép."),
                ParticipantFailure.RequiredRoles => (409, Msg109RequiredRoles, "Cuộc họp phải giữ Chủ tọa và Thư ký theo quy định. Hãy phân công người thay thế."),
                ParticipantFailure.Stale => (409, Msg238Stale, "Danh sách thành phần đã thay đổi. Vui lòng tải lại và thử lại."),
                ParticipantFailure.InvalidRequest => (400, Msg239InvalidRequest, "Query hoặc If-Match không hợp lệ. Vui lòng tải lại dữ liệu."),
                ParticipantFailure.CannotRespond => (409, Msg107CannotRespond, "Lời mời này không còn cho phép phản hồi."),
                ParticipantFailure.UnsupportedMedia => (415, Msg34UnsupportedMedia, "Yêu cầu phải dùng Content-Type application/json."),
                ParticipantFailure.TooLarge => (413, Msg87TooLarge, "Nội dung yêu cầu vượt quá 64 KiB."),
                ParticipantFailure.Invalid => (400, operation == ParticipantOperation.Respond ? Msg106InvalidResponse : Msg97Invalid, "Thông tin gửi lên không hợp lệ."),
                _ => (503, operation switch
                {
                    ParticipantOperation.AddInternal or ParticipantOperation.AddGuest or ParticipantOperation.RecoverSecretary => Msg99AddFailed,
                    ParticipantOperation.RemoveInternal or ParticipantOperation.RevokeGuest => Msg104RemoveFailed,
                    ParticipantOperation.Respond => Msg108ResponseFailed,
                    _ => Msg102ChangeFailed
                }, "Không lưu được thay đổi. Vui lòng thử lại.")
            };
            return Results.Json(new { code, message, errors = result.Errors }, statusCode: status);
        }
        if (result.Revision is { } revision) context.Response.Headers.ETag = $"\"participants-{revision.ToString(CultureInfo.InvariantCulture)}\"";
        if (operation is ParticipantOperation.Context or ParticipantOperation.Roster or ParticipantOperation.Options or ParticipantOperation.OwnResponse or ParticipantOperation.Responses)
            return Results.Ok(result.Data);
        var (successCode, successMessage) = result.Success switch
        {
            ParticipantSuccess.Created => (Msg96Added, "Thêm thành phần tham dự thành công."),
            ParticipantSuccess.RoleChanged => (Msg100RoleChanged, "Cập nhật vai trò cuộc họp thành công."),
            ParticipantSuccess.PermissionsChanged => (Msg153PermissionsChanged, "Cập nhật quyền cuộc họp thành công."),
            ParticipantSuccess.Removed => (Msg103Removed, "Gỡ thành phần tham dự thành công."),
            ParticipantSuccess.Responded => (Msg105Responded, "Đã ghi nhận phản hồi lời mời."),
            _ => (Msg154NoChanges, "Không có thay đổi cần lưu.")
        };
        var body = new Dictionary<string, object?> { ["code"] = successCode, ["message"] = successMessage };
        if (result.Revision is { } v) body["revision"] = v.ToString(CultureInfo.InvariantCulture);
        if (result.Data is InvitationResponseView view) body["response"] = view;
        else if (result.Data is not null)
            foreach (var prop in JsonSerializer.SerializeToElement(result.Data, InputJson).EnumerateObject()) body[prop.Name] = prop.Value.Clone();
        return Results.Json(body, statusCode: result.Success == ParticipantSuccess.Created ? 201 : 200);
    }
}
