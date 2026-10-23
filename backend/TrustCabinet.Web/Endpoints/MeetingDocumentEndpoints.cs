using Microsoft.AspNetCore.Http.Features;
using TrustCabinet.Domain.Documents;

namespace TrustCabinet.Web.Endpoints;

/// <summary>UC-33a AF-1 — upload files from the computer into a meeting (SCRUM-107, ADR-0024).</summary>
public static class MeetingDocumentEndpoints
{
    private const string Msg161Added = "MSG161";
    private const string Msg162AddDenied = "MSG162";
    private const string Msg164InvalidFiles = "MSG164";
    private const string Msg166AddFailed = "MSG166";
    private const string Msg172MeetingEnded = "MSG172";
    private const string Msg174MeetingUnavailable = "MSG174";
    private const string Msg288SubmissionTooLarge = "MSG288";

    /// <summary>The 200 MB of files plus room for the multipart framing and the page counts.</summary>
    private const long MaxRequestBytes = DocumentFileRules.MaxTotalBytes + 1024 * 1024;

    public static void MapMeetingDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/meetings/{meetingId:guid}/documents")
            .RequireActiveUser(Map(new(DocumentUploadFailure.StoreFailed), Guid.Empty))
            .MapPost("/upload", UploadAsync);
    }

    private static async Task<IResult> UploadAsync(Guid meetingId, HttpContext context, IMeetingDocumentService service,
        CancellationToken ct)
    {
        var actor = CapabilityGate.GatedUserId(context.User);

        // Before the body: someone who may not upload should not get to stream 200 MB to disk first.
        var access = await service.CheckUploadAsync(meetingId, actor, ct);
        if (access.Failure != DocumentUploadFailure.None) return Map(access, meetingId);

        var request = context.Request;
        if (!request.HasFormContentType || !request.ContentType!.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return Map(new(DocumentUploadFailure.Invalid,
                Errors: new Dictionary<string, string[]> { ["files"] = ["Yêu cầu phải gửi tệp dạng multipart/form-data."] }), meetingId);
        if (request.ContentLength > MaxRequestBytes) return Map(new(DocumentUploadFailure.TooMany), meetingId);

        // Kestrel stops at 30 MB and the form reader at 128 MB by default; this endpoint alone needs 200 MB.
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size)
            size.MaxRequestBodySize = MaxRequestBytes;
        // Parts over 64 KB are buffered to a temp file, never held in memory (1 GB server, ADR-0024).
        // Smaller ones stay in memory, so the part count is capped too: room for well over 10 files
        // and their page counts, so a few extra files still get MSG288; far more is malformed (MSG164).
        context.Features.Set<IFormFeature>(new FormFeature(request, new FormOptions
        {
            MultipartBodyLengthLimit = MaxRequestBytes,
            ValueCountLimit = 64,
        }));

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(ct);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Map(new(DocumentUploadFailure.TooMany), meetingId);
        }
        catch (InvalidDataException)
        {
            return Map(new(DocumentUploadFailure.Invalid,
                Errors: new Dictionary<string, string[]> { ["files"] = ["Nội dung multipart không hợp lệ."] }), meetingId);
        }

        // pageCounts[i] belongs to the i-th file part; the browser counts PDF pages with pdf.js.
        var pageCounts = form["pageCounts"].Concat(form["pageCounts[]"]).ToArray();
        var files = form.Files.GetFiles("files").Concat(form.Files.GetFiles("files[]"));
        var candidates = files.Select((file, index) => new DocumentUploadCandidate(file.FileName, file.Length,
            file.OpenReadStream, index < pageCounts.Length ? pageCounts[index] : null)).ToArray();
        var validated = await DocumentFileRules.ValidateAsync(candidates, ct);
        if (validated.Failure != DocumentUploadFailure.None) return Map(validated, meetingId);

        return Map(await service.UploadAsync(meetingId, actor, validated.Files!, ct), meetingId);
    }

    private static IResult Map(DocumentUploadOutcome outcome, Guid meetingId)
    {
        if (outcome.Failure == DocumentUploadFailure.Inactive) return Results.Forbid();
        if (outcome.Failure != DocumentUploadFailure.None)
        {
            var (status, code, message) = outcome.Failure switch
            {
                // Same answer for a meeting that does not exist and one the caller cannot see (SCRUM-22).
                DocumentUploadFailure.NotFound => (404, Msg174MeetingUnavailable, "Không truy cập được cuộc họp này."),
                DocumentUploadFailure.Forbidden => (403, Msg162AddDenied, "Bạn không còn quyền thêm tài liệu vào cuộc họp này."),
                DocumentUploadFailure.Ended => (409, Msg172MeetingEnded, "Không thể thêm tài liệu sau khi cuộc họp đã kết thúc."),
                DocumentUploadFailure.Invalid => (400, Msg164InvalidFiles, "Hãy chọn tệp PDF hoặc DOCX hợp lệ, mỗi tệp không quá 50 MB."),
                DocumentUploadFailure.TooMany => (400, Msg288SubmissionTooLarge, "Mỗi lần tải tối đa 10 tệp, tổng dung lượng không quá 200 MB."),
                _ => (503, Msg166AddFailed, "Không thêm được tài liệu. Chưa có tài liệu nào được thêm. Vui lòng thử lại."),
            };
            return Results.Json(new { code, message, errors = outcome.Errors }, statusCode: status);
        }

        var documents = outcome.Documents!;
        return Results.Json(new
        {
            code = Msg161Added,
            message = "Đã thêm tài liệu vào cuộc họp.",
            meetingId,
            addedCount = documents.Count,
            documents,
        }, statusCode: StatusCodes.Status201Created);
    }
}
