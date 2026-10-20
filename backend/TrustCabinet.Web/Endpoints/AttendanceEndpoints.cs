using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrustCabinet.Domain.Attendance;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Web.Endpoints;

public static class AttendanceEndpoints
{
    private const string Msg147Saved = "MSG147";
    private const string Msg148Invalid = "MSG148";
    private const string Msg149Locked = "MSG149";
    private const string Msg150SaveFailed = "MSG150";
    private const string Msg151Stale = "MSG151";
    private const string Msg277ReadFailed = "MSG277";
    private const string Msg278InvalidQuery = "MSG278";
    private const string Msg34UnsupportedMedia = "MSG34";
    private const string Msg87TooLarge = "MSG87";
    private const int MaxBytes = 64 * 1024;
    private static readonly JsonSerializerOptions InputJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private sealed record Body(AttendanceEntry?[]? Entries);

    public static void MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/meetings/{meetingId:guid}/attendance").RequireAuthorization();
        group.MapGet("", async (Guid meetingId, HttpContext context, IAttendanceService service,
            IParticipantGuestIdentityResolver identities, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var actor = new ParticipantActor(CapabilityGate.CurrentUserId(context.User), identities.Resolve(context.User));
            return Map(await service.ReadAsync(meetingId, actor, Query(context.Request), ct));
        });
        group.MapPost("", async (Guid meetingId, HttpContext context, IAttendanceService service,
            IParticipantGuestIdentityResolver identities, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var actor = new ParticipantActor(CapabilityGate.CurrentUserId(context.User), identities.Resolve(context.User));
            var input = await ParseAsync(context.Request, ct);
            if (context.Request.Query.Count > 0) input = input with { ParseFailure = AttendanceFailure.Invalid };
            return Map(await service.SaveAsync(meetingId, actor, input, ct));
        });
    }

    private static AttendanceQuery Query(HttpRequest request)
    {
        var invalid = request.Query.Any(p => p.Key is not ("page" or "pageSize") || p.Value.Count != 1);
        var page = 1; var size = 20;
        if (request.Query.TryGetValue("page", out var p) && !int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out page)) invalid = true;
        if (request.Query.TryGetValue("pageSize", out var s) && !int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out size)) invalid = true;
        return new(page, size, invalid);
    }
    private static async Task<AttendanceInput> ParseAsync(HttpRequest request, CancellationToken ct)
    {
        if (!request.HasJsonContentType()) return new(ParseFailure: AttendanceFailure.UnsupportedMedia);
        if (request.ContentLength > MaxBytes) return new(ParseFailure: AttendanceFailure.TooLarge);
        var bytes = new byte[MaxBytes + 1];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = await request.Body.ReadAsync(bytes.AsMemory(count), ct);
            if (read == 0) break;
            count += read;
        }
        if (count > MaxBytes) return new(ParseFailure: AttendanceFailure.TooLarge);
        try
        {
            using var document = JsonDocument.Parse(bytes.AsMemory(0, count));
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            ValidateTree(document.RootElement);
            return new(document.RootElement.Deserialize<Body>(InputJson)!.Entries);
        }
        catch (JsonException) { return new(ParseFailure: AttendanceFailure.Invalid); }
    }
    private static void ValidateTree(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) throw new JsonException();
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in value.EnumerateObject())
            {
                if (!keys.Add(p.Name)) throw new JsonException();
                ValidateTree(p.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) ValidateTree(item);
    }
    private static IResult Map(AttendanceResult result)
    {
        if (result.Failure == AttendanceFailure.NotFound) return Results.NotFound();
        if (result.Failure == AttendanceFailure.Forbidden) return Results.StatusCode(403);
        if (result.Failure != AttendanceFailure.None)
        {
            var (status, code, message) = result.Failure switch
            {
                AttendanceFailure.Locked => (409, Msg149Locked, "Chỉ được điểm danh khi cuộc họp đang diễn ra."),
                AttendanceFailure.Stale => (409, Msg151Stale, "Thành phần hoặc dữ liệu điểm danh đã thay đổi. Vui lòng tải lại và kiểm tra trước khi lưu."),
                AttendanceFailure.ReadFailed => (500, Msg277ReadFailed, "Không tải được dữ liệu điểm danh."),
                AttendanceFailure.InvalidQuery => (400, Msg278InvalidQuery, "Tham số phân trang điểm danh không hợp lệ."),
                AttendanceFailure.SaveFailed => (503, Msg150SaveFailed, "Không xác nhận được kết quả lưu điểm danh. Vui lòng tải lại dữ liệu trước khi thử lại."),
                AttendanceFailure.UnsupportedMedia => (415, Msg34UnsupportedMedia, "Yêu cầu phải dùng Content-Type application/json."),
                AttendanceFailure.TooLarge => (413, Msg87TooLarge, "Nội dung yêu cầu vượt quá 64 KiB."),
                _ => (400, Msg148Invalid, "Thông tin điểm danh không hợp lệ. Chọn Có mặt hoặc Vắng cho từng người được gửi.")
            };
            return Results.Json(new { code, message }, statusCode: status);
        }
        if (result.Page is not null) return Results.Ok(result.Page);
        var saved = result.Saved!;
        return Results.Ok(new { code = Msg147Saved, message = "Lưu điểm danh thành công.", saved.MeetingId, saved.ChangedCount, saved.Entries });
    }
}
