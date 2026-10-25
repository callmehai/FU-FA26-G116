using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
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

    /// <summary>Room for well over 10 files and their page counts, so a few extra files still get
    /// MSG288; far more is malformed (MSG164).</summary>
    private const int MaxParts = 64;
    private const int MaxPageCountBytes = 16;
    /// <summary>ASP.NET Core's own form reader allows the same.</summary>
    private const int MaxBoundaryLength = 128;

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

        // Kestrel stops at 30 MB by default; this endpoint alone needs 200 MB.
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size)
            size.MaxRequestBodySize = MaxRequestBytes;

        var tempFiles = new List<string>();
        try
        {
            DocumentUploadCandidate[] candidates;
            try
            {
                candidates = await ReadUploadAsync(request, tempFiles, ct);
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

            var validated = await DocumentFileRules.ValidateAsync(candidates, ct);
            if (validated.Failure != DocumentUploadFailure.None) return Map(validated, meetingId);

            return Map(await service.UploadAsync(meetingId, actor, validated.Files!, ct), meetingId);
        }
        finally
        {
            foreach (var path in tempFiles)
            {
                try { File.Delete(path); }
                catch (IOException) { /* the OS temp cleaner gets it */ }
            }
        }
    }

    /// <summary>
    /// Reads the multipart body by hand rather than with ReadFormAsync, which reads every
    /// non-file part whole into a string however large it is (its value limit only covers
    /// urlencoded forms). File parts go to temp files; a page count longer than a few digits,
    /// or more parts than any real upload has, is malformed. Other parts are skipped unread.
    /// </summary>
    private static async Task<DocumentUploadCandidate[]> ReadUploadAsync(HttpRequest request, List<string> tempFiles,
        CancellationToken ct)
    {
        var boundary = HeaderUtilities.RemoveQuotes(MediaTypeHeaderValue.Parse(request.ContentType).Boundary).Value;
        if (string.IsNullOrEmpty(boundary) || boundary.Length > MaxBoundaryLength)
            throw new InvalidDataException("Missing or overlong multipart boundary.");
        var reader = new MultipartReader(boundary, request.Body) { BodyLengthLimit = MaxRequestBytes };

        var files = new List<(string? Name, string Path, long Length)>();
        var pageCounts = new List<string>();
        var parts = 0;
        // ReadNextSectionAsync drains whatever of the previous part was not read.
        while (await reader.ReadNextSectionAsync(ct) is { } section)
        {
            if (++parts > MaxParts) throw new InvalidDataException("Too many multipart sections.");
            if (section.GetContentDispositionHeader() is not { } disposition) continue;
            var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;
            if (disposition.IsFileDisposition() && name is "files" or "files[]")
            {
                var path = Path.GetTempFileName();
                tempFiles.Add(path);
                await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920,
                    FileOptions.Asynchronous);
                await section.Body.CopyToAsync(file, ct);
                files.Add((section.AsFileSection()!.FileName, path, file.Length));
            }
            else if (disposition.IsFormDisposition() && name is "pageCounts" or "pageCounts[]")
            {
                var buffer = new byte[MaxPageCountBytes + 1];
                var length = await section.Body.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, ct);
                if (length > MaxPageCountBytes) throw new InvalidDataException("Page count too long.");
                pageCounts.Add(Encoding.UTF8.GetString(buffer, 0, length));
            }
        }

        // pageCounts[i] belongs to the i-th file part; the browser counts PDF pages with pdf.js.
        return files.Select((file, index) => new DocumentUploadCandidate(file.Name, file.Length,
            () => new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan),
            index < pageCounts.Count ? pageCounts[index] : null)).ToArray();
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
