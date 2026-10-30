using System.Text.Json;
using System.Text.Json.Serialization;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Votes;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// Votes during a meeting (SCRUM-28, UC-52a–UC-57), their documents and the read confirmations ballots wait for
/// (SCRUM-96, UC-74, UC-76a, UC-76b). Contract in docs/03-api/15-bieu-quyet.md.
/// </summary>
public static class VoteEndpoints
{
    private const string Msg156DocumentsUnavailable = "MSG156";
    private const string Msg164InvalidFiles = "MSG164";
    private const string Msg165AlreadyInMeeting = "MSG165";
    private const string Msg166AddFailed = "MSG166";
    private const string Msg170NotInMeeting = "MSG170";
    private const string Msg249LibrarySelectionInvalid = "MSG249";
    private const string Msg252DocumentsUnread = "MSG252";
    private const string Msg253ReadConfirmed = "MSG253";
    private const string Msg254LastPageNotReached = "MSG254";
    private const string Msg255ReadConfirmFailed = "MSG255";
    private const string Msg265DocumentsAttached = "MSG265";
    private const string Msg266AlreadyAttached = "MSG266";
    private const string Msg267AttachFailed = "MSG267";
    private const string Msg268DocumentDetached = "MSG268";
    private const string Msg269NotAttached = "MSG269";
    private const string Msg270DetachFailed = "MSG270";
    private const string Msg288SubmissionTooLarge = "MSG288";
    private const string Msg346StorageFull = "MSG346";
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
        // A ballot, an open vote's turnout or a read confirmation must never be served from a cache.
        var group = app.MapGroup("/api/meetings/{meetingId:guid}/votes").RequireAuthorization().AddEndpointFilter(NoStore);
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

        // UC-76a: from the meeting's documents, from the caller's library (AF-2), or uploaded (AF-1); UC-76b.
        Register(VoteOperation.AttachDocuments, "POST", "/{voteId:guid}/documents");
        Register(VoteOperation.AttachFromLibrary, "POST", "/{voteId:guid}/documents/from-library");
        group.MapPost("/{voteId:guid}/documents/upload", AttachUploadAsync);
        group.MapDelete("/{voteId:guid}/documents/{meetingDocumentId:guid}", async (Guid meetingId, Guid voteId,
            Guid meetingDocumentId, HttpContext context, IVoteService service, IMeetingGuestIdentityResolver identities,
            CancellationToken ct) =>
        {
            var outcome = await service.MutateAsync(meetingId, voteId, Actor(context, identities), VoteOperation.DetachDocument,
                new VoteInput(MeetingDocumentId: meetingDocumentId), ct);
            return Map(outcome, meetingId, VoteOperation.DetachDocument, meetingDocumentId: meetingDocumentId);
        });

        // UC-74: on the document, because one confirmation counts for every vote that links it (BR-291).
        app.MapGroup("/api/meetings/{meetingId:guid}/documents/{meetingDocumentId:guid}/read-confirmation")
            .RequireAuthorization().AddEndpointFilter(NoStore)
            .MapPost("", async (Guid meetingId, Guid meetingDocumentId, HttpContext context, IVoteService service,
                IMeetingGuestIdentityResolver identities, CancellationToken ct) =>
            {
                var input = await ParseAsync(context.Request, null, ct);
                return MapRead(await service.ConfirmReadAsync(meetingId, meetingDocumentId, Actor(context, identities), input, ct),
                    meetingId);
            });

        void Register(VoteOperation operation, string method, string suffix)
            => group.MapMethods(suffix, [method], async (Guid meetingId, HttpContext context, IVoteService service,
                IMeetingGuestIdentityResolver identities, MeetingDocumentChangePublisher documents, ILoggerFactory logs,
                CancellationToken ct) =>
            {
                Guid? voteId = context.Request.RouteValues.TryGetValue("voteId", out var value)
                    ? Guid.Parse(value!.ToString()!) : null;
                var input = operation is VoteOperation.Open or VoteOperation.Close or VoteOperation.Delete
                    ? new VoteInput() : await ParseAsync(context.Request, operation, ct);
                var outcome = await service.MutateAsync(meetingId, voteId, Actor(context, identities), operation, input, ct);
                if (operation == VoteOperation.AttachFromLibrary) await PublishDocumentsAsync(outcome, meetingId, documents, logs);
                return Map(outcome, meetingId, operation, input.OpenImmediately);
            });
    }

    private static async ValueTask<object?> NoStore(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        return await next(context);
    }

    /// <summary>UC-76a AF-1: the same checks and write order as uploading to the meeting (UC-33a AF-1).</summary>
    private static async Task<IResult> AttachUploadAsync(Guid meetingId, Guid voteId, HttpContext context, IVoteService service,
        IMeetingGuestIdentityResolver identities, MeetingDocumentChangePublisher documents, ILoggerFactory logs,
        CancellationToken ct)
    {
        var actor = Actor(context, identities);
        // Before the body: someone who may not attach should not get to stream 200 MB to disk first.
        var access = await service.CheckAttachUploadAsync(meetingId, voteId, actor, ct);
        if (access.Failure != VoteFailure.None) return Map(access, meetingId, VoteOperation.AttachUpload);

        var tempFiles = new List<string>();
        try
        {
            var validated = await MeetingDocumentEndpoints.ReadFilesAsync(context, tempFiles, ct);
            if (validated.Failure != DocumentUploadFailure.None)
                return Map(new VoteOutcome(validated.Failure == DocumentUploadFailure.TooMany ? VoteFailure.FilesTooMany
                    : VoteFailure.FilesInvalid, Errors: validated.Errors), meetingId, VoteOperation.AttachUpload);
            var outcome = await service.AttachUploadAsync(meetingId, voteId, actor, validated.Files!, ct);
            await PublishDocumentsAsync(outcome, meetingId, documents, logs);
            return Map(outcome, meetingId, VoteOperation.AttachUpload);
        }
        finally
        {
            MeetingDocumentEndpoints.DeleteTempFiles(tempFiles);
        }
    }

    /// <summary>Documents added to the meeting on the way show on its Documents tab too; a lost event only delays that.</summary>
    private static async Task PublishDocumentsAsync(VoteOutcome outcome, Guid meetingId, MeetingDocumentChangePublisher documents,
        ILoggerFactory logs)
    {
        if (outcome.Failure != VoteFailure.None) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await documents.PublishAsync(meetingId, timeout.Token); }
        catch (Exception ex)
        {
            logs.CreateLogger(nameof(VoteEndpoints)).LogWarning(ex, "Could not publish documentsChanged for meeting {MeetingId}", meetingId);
        }
    }

    private static ParticipantActor Actor(HttpContext context, IMeetingGuestIdentityResolver identities)
        => new(CapabilityGate.CurrentUserId(context.User), identities.Resolve(context.User));

    /// <summary>Create and update alike; <c>openImmediately</c> opens from the form (UC-53 AF-3).</summary>
    private sealed record DraftBody(string? Title, string? Description, string?[]? Options, bool? IsAnonymous,
        string?[]? EligibleRoles, bool? OpenImmediately);
    private sealed record BallotBody(Guid? OptionId);
    private sealed record ConclusionBody(string? Conclusion, string? Note);
    private sealed record AttachBody(Guid[]? MeetingDocumentIds);
    private sealed record LibraryBody(Guid[]? DocumentIds);
    private sealed record ReadBody(bool? LastPageReached);

    /// <summary><paramref name="operation"/> null is a read confirmation (UC-74).</summary>
    private static async Task<VoteInput> ParseAsync(HttpRequest request, VoteOperation? operation, CancellationToken ct)
    {
        // A body that cannot be read is the operation's own "nothing valid was sent".
        var invalid = operation switch
        {
            null => VoteFailure.LastPageNotReached,
            VoteOperation.Cast => VoteFailure.OptionMissing,
            VoteOperation.RecordConclusion => VoteFailure.ConclusionInvalid,
            VoteOperation.AttachDocuments => VoteFailure.DocumentNotInMeeting,
            VoteOperation.AttachFromLibrary => VoteFailure.LibraryUnavailable,
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
                case null:
                    return new(LastPageReached: json.RootElement.Deserialize<ReadBody>(InputJson)!.LastPageReached);
                case VoteOperation.AttachDocuments:
                    return new(DocumentIds: json.RootElement.Deserialize<AttachBody>(InputJson)!.MeetingDocumentIds);
                case VoteOperation.AttachFromLibrary:
                    return new(DocumentIds: json.RootElement.Deserialize<LibraryBody>(InputJson)!.DocumentIds);
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

    private static IResult Map(VoteOutcome result, Guid meetingId, VoteOperation? operation = null, bool openImmediately = false,
        Guid? meetingDocumentId = null)
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
                VoteFailure.DocumentsUnread => (409, Msg252DocumentsUnread, "Hãy xác nhận đã đọc mọi tài liệu của biểu quyết trước khi bỏ phiếu."),
                // Removed, of another meeting, or none selected: the same answer (BR-211).
                VoteFailure.DocumentNotInMeeting => (404, Msg170NotInMeeting, "Tài liệu này không còn trong cuộc họp. Hãy tải lại danh sách tài liệu."),
                VoteFailure.DocumentAlreadyAttached => (409, Msg266AlreadyAttached, "Một hoặc nhiều tài liệu đã chọn đã được gắn vào biểu quyết."),
                VoteFailure.DocumentNotAttached => (404, Msg269NotAttached, "Tài liệu này không còn gắn với biểu quyết. Hãy tải lại danh sách."),
                VoteFailure.FilesInvalid => (400, Msg164InvalidFiles, "Hãy chọn tệp PDF hoặc DOCX hợp lệ, mỗi tệp không quá 50 MB."),
                VoteFailure.FilesTooMany => (400, Msg288SubmissionTooLarge, "Mỗi lần tải tối đa 10 tệp, tổng dung lượng không quá 200 MB."),
                VoteFailure.StorageFull => (409, Msg346StorageFull, MeetingDocumentEndpoints.StorageFullMessage),
                VoteFailure.LibraryUnavailable => (400, Msg249LibrarySelectionInvalid, "Hãy chọn một hoặc nhiều tài liệu còn trong kho của bạn."),
                VoteFailure.AlreadyInMeeting => (409, Msg165AlreadyInMeeting, "Một hoặc nhiều tài liệu đã chọn đã có trong cuộc họp."),
                _ => opening ? (503, Msg221OpenFailed, "Không mở được biểu quyết. Biểu quyết chưa được mở. Vui lòng thử lại.") : operation switch
                {
                    VoteOperation.AttachDocuments => (503, Msg267AttachFailed, "Không gắn được tài liệu. Chưa có tài liệu nào được gắn. Vui lòng thử lại."),
                    // AF-1 and AF-2 add documents to the meeting as well, so a failed save is UC-33a's.
                    VoteOperation.AttachFromLibrary or VoteOperation.AttachUpload => (503, Msg166AddFailed, "Không thêm được tài liệu. Chưa có tài liệu nào được thêm. Vui lòng thử lại."),
                    VoteOperation.DetachDocument => (503, Msg270DetachFailed, "Không gỡ được tài liệu. Chưa có thay đổi nào. Vui lòng thử lại."),
                    VoteOperation.Create => (503, Msg214CreateFailed, "Không tạo được biểu quyết nháp. Chưa có nháp nào được tạo. Vui lòng thử lại."),
                    VoteOperation.Update => (503, Msg215UpdateFailed, "Không cập nhật được biểu quyết nháp. Nháp vẫn giữ nguyên. Vui lòng thử lại."),
                    VoteOperation.Delete => (503, Msg216DeleteFailed, "Không xoá được biểu quyết nháp. Nháp vẫn giữ nguyên. Vui lòng thử lại."),
                    VoteOperation.Cast => (503, Msg228CastFailed, "Không ghi được phiếu. Chưa có phiếu nào được ghi. Vui lòng thử lại."),
                    VoteOperation.Close => (503, Msg231CloseFailed, "Không đóng được biểu quyết. Biểu quyết và các phiếu đã ghi vẫn giữ nguyên. Vui lòng thử lại."),
                    _ => (503, Msg236ConclusionFailed, "Không ghi được kết luận biểu quyết. Chưa có kết luận nào được ghi. Vui lòng thử lại.")
                }
            };
            // MSG252 lists what is still to read, so the voter can open each one (UC-55 EX-8).
            return result.Failure == VoteFailure.DocumentsUnread
                ? Results.Json(new { code, message, documents = result.Data }, statusCode: status)
                : Results.Json(new { code, message, errors = result.Errors }, statusCode: status);
        }
        if (operation is null) return Results.Ok(result.Data);
        var (successCode, successMessage) = opening ? (Msg217Opened, "Mở biểu quyết thành công.") : operation switch
        {
            VoteOperation.Create => (Msg210Created, "Tạo biểu quyết nháp thành công."),
            VoteOperation.Update => (Msg211Updated, "Cập nhật biểu quyết nháp thành công."),
            VoteOperation.Delete => (Msg212Deleted, "Xoá biểu quyết nháp thành công."),
            VoteOperation.Cast => (Msg227Cast, "Đã ghi nhận phiếu của bạn."),
            VoteOperation.Close => (Msg229Closed, "Đã đóng biểu quyết. Kết quả đã sẵn sàng."),
            VoteOperation.AttachDocuments or VoteOperation.AttachFromLibrary or VoteOperation.AttachUpload
                => (Msg265DocumentsAttached, "Gắn tài liệu vào biểu quyết thành công."),
            VoteOperation.DetachDocument => (Msg268DocumentDetached, "Gỡ tài liệu khỏi biểu quyết thành công."),
            _ => (Msg235Concluded, "Ghi kết luận biểu quyết thành công.")
        };
        var body = new Dictionary<string, object?>
        {
            ["code"] = successCode, ["message"] = successMessage, ["meetingId"] = meetingId, ["voteId"] = result.VoteId
        };
        if (operation == VoteOperation.Close) body["result"] = result.Data;
        // The meeting documents AF-1 and AF-2 added; attaching existing ones adds none.
        if (operation is VoteOperation.AttachDocuments or VoteOperation.AttachFromLibrary or VoteOperation.AttachUpload)
            body["documents"] = result.Data;
        if (operation == VoteOperation.DetachDocument) body["meetingDocumentId"] = meetingDocumentId;
        return operation == VoteOperation.Create && !opening
            ? Results.Created($"/api/meetings/{meetingId}/votes/{result.VoteId}", body) : Results.Ok(body);
    }

    /// <summary>UC-74. Already confirmed is a success too, with the original time (AF-1).</summary>
    private static IResult MapRead(VoteOutcome result, Guid meetingId)
    {
        if (result.Failure == VoteFailure.Inactive) return Results.Forbid();
        if (result.Data is DocumentReadView read)
            return Results.Ok(new
            {
                code = Msg253ReadConfirmed, message = "Đã xác nhận đọc tài liệu.", meetingId, read.MeetingDocumentId,
                read.ConfirmedAt, read.AlreadyConfirmed
            });
        var (status, code, message) = result.Failure switch
        {
            // A meeting the caller cannot see, a removed document and one no visible vote links: the same answer.
            VoteFailure.DocumentUnavailable => (404, Msg156DocumentsUnavailable, "Không truy cập được tài liệu của cuộc họp."),
            // Only a voter of an Open vote that links the document confirms it; UC-55's codes for the same situations.
            VoteFailure.NotEligible => (409, Msg224NotEligible, "Bạn không có trong danh sách được bỏ phiếu của biểu quyết này."),
            VoteFailure.NotAccepting => (409, Msg225NotAccepting, "Biểu quyết này không còn nhận phiếu."),
            VoteFailure.LastPageNotReached => (400, Msg254LastPageNotReached, "Hãy cuộn tới trang cuối của tài liệu trước khi xác nhận đã đọc."),
            VoteFailure.UnsupportedMedia => (415, Msg34UnsupportedMedia, "Yêu cầu phải dùng Content-Type application/json."),
            VoteFailure.TooLarge => (413, Msg87BodyTooLarge, "Nội dung yêu cầu vượt quá 64 KiB."),
            _ => (503, Msg255ReadConfirmFailed, "Không xác nhận được việc đọc tài liệu. Vui lòng thử lại.")
        };
        return Results.Json(new { code, message }, statusCode: status);
    }
}
