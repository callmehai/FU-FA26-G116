using System.Text.Json;
using System.Text.Json.Serialization;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Votes;

namespace TrustCabinet.Web.Endpoints;

/// <summary>Votes during a meeting (SCRUM-28, UC-52a–UC-57). Contract in docs/03-api/15-bieu-quyet.md.</summary>
public static class VoteEndpoints
{
    private const string Msg205NotFound = "MSG205";
    private const string Msg206ListFailed = "MSG206";
    private const string Msg208DetailFailed = "MSG208";
    private const string Msg209Invalid = "MSG209";
    private const string Msg210Created = "MSG210";
    private const string Msg211Updated = "MSG211";
    private const string Msg212Deleted = "MSG212";
    private const string Msg213DraftDenied = "MSG213";
    private const string Msg214CreateFailed = "MSG214";
    private const string Msg215UpdateFailed = "MSG215";
    private const string Msg216DeleteFailed = "MSG216";
    private const string Msg217Opened = "MSG217";
    private const string Msg218OpenDenied = "MSG218";
    private const string Msg219AnotherOpen = "MSG219";
    private const string Msg220NoEligibleVoters = "MSG220";
    private const string Msg221OpenFailed = "MSG221";
    private const string Msg222OptionMissing = "MSG222";
    private const string Msg223OptionUnavailable = "MSG223";
    private const string Msg224NotEligible = "MSG224";
    private const string Msg225NotAccepting = "MSG225";
    private const string Msg226AlreadyCast = "MSG226";
    private const string Msg227Cast = "MSG227";
    private const string Msg228CastFailed = "MSG228";
    private const string Msg229Closed = "MSG229";
    private const string Msg230CloseDenied = "MSG230";
    private const string Msg231CloseFailed = "MSG231";
    private const string Msg232ConclusionInvalid = "MSG232";
    private const string Msg233ConclusionRecorded = "MSG233";
    private const string Msg234ConclusionDenied = "MSG234";
    private const string Msg235Concluded = "MSG235";
    private const string Msg236ConclusionFailed = "MSG236";
    private const string Msg34UnsupportedMedia = "MSG34";
    private const string Msg87BodyTooLarge = "MSG87";
    private const int MaxBodyBytes = 64 * 1024;
    private static readonly JsonSerializerOptions InputJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static void MapVoteEndpoints(this IEndpointRouteBuilder app)
    {
        // A ballot or an open vote's turnout must never be served from a cache.
        var group = app.MapGroup("/api/meetings/{meetingId:guid}/votes").RequireAuthorization()
            .AddEndpointFilter(async (context, next) =>
            {
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                return await next(context);
            });
        group.MapGet("", async (Guid meetingId, HttpContext context, IVoteService service,
            IMeetingGuestIdentityResolver identities, CancellationToken ct)
            => Map(await service.ListAsync(meetingId, Actor(context, identities), ct), meetingId));
        group.MapGet("/{voteId:guid}", async (Guid meetingId, Guid voteId, HttpContext context, IVoteService service,
            IMeetingGuestIdentityResolver identities, CancellationToken ct)
            => Map(await service.GetAsync(meetingId, voteId, Actor(context, identities), ct), meetingId));

        Register(VoteOperation.Create, "POST", "");
        Register(VoteOperation.Update, "PUT", "/{voteId:guid}");
        Register(VoteOperation.Delete, "DELETE", "/{voteId:guid}");
        Register(VoteOperation.Open, "POST", "/{voteId:guid}/open");
        Register(VoteOperation.Cast, "POST", "/{voteId:guid}/ballots");
        Register(VoteOperation.Close, "POST", "/{voteId:guid}/close");
        Register(VoteOperation.RecordConclusion, "POST", "/{voteId:guid}/conclusion");

        void Register(VoteOperation operation, string method, string suffix)
            => group.MapMethods(suffix, [method], async (Guid meetingId, HttpContext context, IVoteService service,
                IMeetingGuestIdentityResolver identities, CancellationToken ct) =>
            {
                Guid? voteId = context.Request.RouteValues.TryGetValue("voteId", out var value)
                    ? Guid.Parse(value!.ToString()!) : null;
                var input = operation is VoteOperation.Open or VoteOperation.Close or VoteOperation.Delete
                    ? new VoteInput() : await ParseAsync(context.Request, operation, ct);
                return Map(await service.MutateAsync(meetingId, voteId, Actor(context, identities), operation, input, ct),
                    meetingId, operation, input.OpenImmediately);
            });
    }

    private static ParticipantActor Actor(HttpContext context, IMeetingGuestIdentityResolver identities)
        => new(CapabilityGate.CurrentUserId(context.User), identities.Resolve(context.User));

    /// <summary>Create and update alike; <c>openImmediately</c> opens from the form (UC-53 AF-3).</summary>
    private sealed record DraftBody(string? Title, string? Description, string?[]? Options, bool? IsAnonymous,
        string?[]? EligibleRoles, bool? OpenImmediately);
    private sealed record BallotBody(Guid? OptionId);
    private sealed record ConclusionBody(string? Conclusion, string? Note);

    private static async Task<VoteInput> ParseAsync(HttpRequest request, VoteOperation operation, CancellationToken ct)
    {
        var invalid = operation switch
        {
            VoteOperation.Cast => VoteFailure.OptionMissing,
            VoteOperation.RecordConclusion => VoteFailure.ConclusionInvalid,
            _ => VoteFailure.Invalid
        };
        if (!request.HasJsonContentType()) return new(ParseFailure: VoteFailure.UnsupportedMedia);
        if (request.ContentLength > MaxBodyBytes) return new(ParseFailure: VoteFailure.TooLarge);
        var buffer = new byte[MaxBodyBytes + 1];
        var count = 0;
        while (count < buffer.Length)
        {
            var read = await request.Body.ReadAsync(buffer.AsMemory(count), ct);
            if (read == 0) break;
            count += read;
        }
        if (count > MaxBodyBytes) return new(ParseFailure: VoteFailure.TooLarge);
        try
        {
            using var json = JsonDocument.Parse(buffer.AsMemory(0, count));
            if (json.RootElement.ValueKind != JsonValueKind.Object
                || json.RootElement.EnumerateObject().Select(PropertyName).Distinct().Count() != json.RootElement.EnumerateObject().Count())
                throw new JsonException();
            switch (operation)
            {
                case VoteOperation.Cast:
                    return new(OptionId: json.RootElement.Deserialize<BallotBody>(InputJson)!.OptionId);
                case VoteOperation.RecordConclusion:
                    var conclusion = json.RootElement.Deserialize<ConclusionBody>(InputJson)!;
                    return new(Conclusion: conclusion.Conclusion, Note: conclusion.Note);
                default:
                    var draft = json.RootElement.Deserialize<DraftBody>(InputJson)!;
                    return new(draft.Title, draft.Description, draft.Options, draft.IsAnonymous, draft.EligibleRoles,
                        draft.OpenImmediately ?? false);
            }
        }
        catch (JsonException) { return new(ParseFailure: invalid); }
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

    private static IResult Map(VoteOutcome result, Guid meetingId, VoteOperation? operation = null, bool openImmediately = false)
    {
        var opening = operation == VoteOperation.Open
            || (operation is VoteOperation.Create or VoteOperation.Update && openImmediately);
        if (result.Failure == VoteFailure.Inactive) return Results.Forbid();
        if (result.Failure != VoteFailure.None)
        {
            var (status, code, message) = result.Failure switch
            {
                VoteFailure.NotFound => (404, Msg205NotFound, "Không truy cập được biểu quyết của cuộc họp này."),
                VoteFailure.ListReadFailed => (500, Msg206ListFailed, "Không tải được danh sách biểu quyết. Vui lòng thử lại."),
                VoteFailure.DetailReadFailed => (500, Msg208DetailFailed, "Không tải được trạng thái hoặc kết quả biểu quyết. Vui lòng thử lại."),
                VoteFailure.Invalid => (400, Msg209Invalid, "Thông tin biểu quyết không hợp lệ. Kiểm tra các trường được đánh dấu."),
                VoteFailure.UnsupportedMedia => (415, Msg34UnsupportedMedia, "Yêu cầu phải dùng Content-Type application/json."),
                VoteFailure.TooLarge => (413, Msg87BodyTooLarge, "Nội dung yêu cầu vượt quá 64 KiB."),
                VoteFailure.DraftDenied => (409, Msg213DraftDenied, "Chỉ Chủ toạ hoặc Thư ký hiện hành được quản lý biểu quyết nháp, khi cuộc họp đã duyệt, đã công bố hoặc đang diễn ra."),
                VoteFailure.OpenDenied => (409, Msg218OpenDenied, "Chỉ Chủ toạ hiện hành được mở biểu quyết nháp, khi cuộc họp đang diễn ra."),
                VoteFailure.AnotherOpen => (409, Msg219AnotherOpen, "Cuộc họp đang có biểu quyết khác đang mở. Hãy đóng biểu quyết đó trước."),
                VoteFailure.NoEligibleVoters => (409, Msg220NoEligibleVoters, "Không có ai đủ điều kiện bỏ phiếu theo các vai trò đã chọn. Biểu quyết chưa được mở."),
                VoteFailure.OptionMissing => (400, Msg222OptionMissing, "Chọn một phương án trước khi gửi phiếu."),
                VoteFailure.OptionUnavailable => (400, Msg223OptionUnavailable, "Phương án đã chọn không thuộc biểu quyết này."),
                VoteFailure.NotEligible => (409, Msg224NotEligible, "Bạn không có trong danh sách được bỏ phiếu của biểu quyết này."),
                VoteFailure.NotAccepting => (409, Msg225NotAccepting, "Biểu quyết này không còn nhận phiếu."),
                VoteFailure.AlreadyCast => (409, Msg226AlreadyCast, "Phiếu của bạn đã được ghi nhận, không thể đổi hoặc rút."),
                VoteFailure.CloseDenied => (409, Msg230CloseDenied, "Chỉ Chủ toạ hiện hành được đóng biểu quyết đang mở, khi cuộc họp đang diễn ra hoặc đã kết thúc."),
                VoteFailure.ConclusionInvalid => (400, Msg232ConclusionInvalid, "Chọn Thông qua, Không thông qua hoặc Hoãn."),
                VoteFailure.ConclusionRecorded => (409, Msg233ConclusionRecorded, "Kết luận biểu quyết đã được ghi và không thể sửa."),
                VoteFailure.ConclusionDenied => (409, Msg234ConclusionDenied, "Chỉ Chủ toạ hiện hành được ghi kết luận cho biểu quyết đã đóng, khi cuộc họp đang diễn ra hoặc đã kết thúc."),
                _ => opening ? (503, Msg221OpenFailed, "Không mở được biểu quyết. Biểu quyết chưa được mở. Vui lòng thử lại.") : operation switch
                {
                    VoteOperation.Create => (503, Msg214CreateFailed, "Không tạo được biểu quyết nháp. Chưa có nháp nào được tạo. Vui lòng thử lại."),
                    VoteOperation.Update => (503, Msg215UpdateFailed, "Không cập nhật được biểu quyết nháp. Nháp vẫn giữ nguyên. Vui lòng thử lại."),
                    VoteOperation.Delete => (503, Msg216DeleteFailed, "Không xoá được biểu quyết nháp. Nháp vẫn giữ nguyên. Vui lòng thử lại."),
                    VoteOperation.Cast => (503, Msg228CastFailed, "Không ghi được phiếu. Chưa có phiếu nào được ghi. Vui lòng thử lại."),
                    VoteOperation.Close => (503, Msg231CloseFailed, "Không đóng được biểu quyết. Biểu quyết và các phiếu đã ghi vẫn giữ nguyên. Vui lòng thử lại."),
                    _ => (503, Msg236ConclusionFailed, "Không ghi được kết luận biểu quyết. Chưa có kết luận nào được ghi. Vui lòng thử lại.")
                }
            };
            return Results.Json(new { code, message, errors = result.Errors }, statusCode: status);
        }
        if (operation is null) return Results.Ok(result.Data);
        var (successCode, successMessage) = opening ? (Msg217Opened, "Mở biểu quyết thành công.") : operation switch
        {
            VoteOperation.Create => (Msg210Created, "Tạo biểu quyết nháp thành công."),
            VoteOperation.Update => (Msg211Updated, "Cập nhật biểu quyết nháp thành công."),
            VoteOperation.Delete => (Msg212Deleted, "Xoá biểu quyết nháp thành công."),
            VoteOperation.Cast => (Msg227Cast, "Đã ghi nhận phiếu của bạn."),
            VoteOperation.Close => (Msg229Closed, "Đã đóng biểu quyết. Kết quả đã sẵn sàng."),
            _ => (Msg235Concluded, "Ghi kết luận biểu quyết thành công.")
        };
        var body = new Dictionary<string, object?>
        {
            ["code"] = successCode, ["message"] = successMessage, ["meetingId"] = meetingId, ["voteId"] = result.VoteId
        };
        if (operation == VoteOperation.Close) body["result"] = result.Data;
        return operation == VoteOperation.Create && !opening
            ? Results.Created($"/api/meetings/{meetingId}/votes/{result.VoteId}", body) : Results.Ok(body);
    }
}
