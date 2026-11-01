using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// UC-32a, UC-32c — list, view and download meeting documents (SCRUM-21); UC-33a AF-1 — upload
/// files from the computer into a meeting (SCRUM-107, ADR-0024); UC-33a from the library, UC-33b,
/// UC-33c, UC-33d — add, link, remove and unlink (SCRUM-58). A guest with a current UC-04 session reads the
/// list and the content too (SCRUM-123); every change stays internal-only.
/// </summary>
public static class MeetingDocumentEndpoints
{
    private const string Msg156DocumentsUnavailable = "MSG156";
    private const string Msg157ListFailed = "MSG157";
    private const string Msg158ViewFailed = "MSG158";
    private const string Msg159SecretNoDownload = "MSG159";
    private const string Msg160DownloadFailed = "MSG160";
    private const string Msg161Added = "MSG161";
    private const string Msg162AddDenied = "MSG162";
    private const string Msg164InvalidFiles = "MSG164";
    private const string Msg165AlreadyInMeeting = "MSG165";
    private const string Msg166AddFailed = "MSG166";
    private const string Msg167Removed = "MSG167";
    private const string Msg169RemoveDenied = "MSG169";
    private const string Msg170NotInMeeting = "MSG170";
    private const string Msg171RemoveFailed = "MSG171";
    private const string Msg172MeetingEnded = "MSG172";
    private const string Msg174MeetingUnavailable = "MSG174";
    private const string Msg238Attached = "MSG238";
    private const string Msg239Detached = "MSG239";
    private const string Msg240LinkDenied = "MSG240";
    private const string Msg241UnlinkDenied = "MSG241";
    private const string Msg242AgendaItemUnavailable = "MSG242";
    private const string Msg243AlreadyLinked = "MSG243";
    private const string Msg244LinkFailed = "MSG244";
    private const string Msg245UnlinkFailed = "MSG245";
    private const string Msg246NotLinked = "MSG246";
    private const string Msg247LinksLocked = "MSG247";
    private const string Msg248RemoveEnded = "MSG248";
    private const string Msg249LibrarySelectionInvalid = "MSG249";
    private const string Msg288SubmissionTooLarge = "MSG288";
    private const string Msg346StorageFull = "MSG346";

    /// <summary>MSG346, for the library upload as well (ADR-0034).</summary>
    internal const string StorageFullMessage =
        "Bộ nhớ tài liệu của hệ thống đã đầy. Chưa có tài liệu nào được tải lên. Hãy báo quản trị viên.";
    private const string Msg349MeetingCancelled = "MSG349";

    /// <summary>The 200 MB of files plus room for the multipart framing and the page counts.</summary>
    private const long MaxRequestBytes = DocumentFileRules.MaxTotalBytes + 1024 * 1024;

    /// <summary>Room for well over 10 files and their page counts, so a few extra files still get
    /// MSG288; far more is malformed (MSG164).</summary>
    private const int MaxParts = 64;
    private const int MaxPageCountBytes = 16;
    /// <summary>ASP.NET Core's own form reader allows the same.</summary>
    private const int MaxBoundaryLength = 128;

    /// <summary>
    /// The codes one change answers with where they differ: who may not, after the meeting ended, and a failed
    /// save. The other failures mean the same thing on every change.
    /// </summary>
    private sealed record ChangeCodes(string Forbidden, string ForbiddenMessage, string Ended, string EndedMessage,
        string Failed, string FailedMessage);

    private static readonly ChangeCodes AddCodes = new(
        Msg162AddDenied, "Bạn không còn quyền thêm tài liệu vào cuộc họp này.",
        Msg172MeetingEnded, "Không thể thêm tài liệu sau khi cuộc họp đã kết thúc.",
        Msg166AddFailed, "Không thêm được tài liệu. Chưa có tài liệu nào được thêm. Vui lòng thử lại.");

    private static readonly ChangeCodes RemoveCodes = new(
        Msg169RemoveDenied, "Bạn không còn quyền gỡ tài liệu khỏi cuộc họp này.",
        Msg248RemoveEnded, "Không thể gỡ tài liệu sau khi cuộc họp đã kết thúc.",
        Msg171RemoveFailed, "Không gỡ được tài liệu. Tài liệu vẫn còn trong cuộc họp. Vui lòng thử lại.");

    private static readonly ChangeCodes LinkCodes = new(
        Msg240LinkDenied, "Bạn không còn quyền gắn tài liệu trong cuộc họp này.",
        Msg247LinksLocked, "Không thể thay đổi liên kết tài liệu sau khi cuộc họp đã kết thúc.",
        Msg244LinkFailed, "Không gắn được tài liệu. Chưa có liên kết nào thay đổi. Vui lòng thử lại.");

    /// <summary>UC-33b AF-1 and AF-2 add documents to the meeting as well, so a failed save is UC-33a's.</summary>
    private static readonly ChangeCodes LinkNewCodes = LinkCodes with { Failed = AddCodes.Failed, FailedMessage = AddCodes.FailedMessage };

    private static readonly ChangeCodes UnlinkCodes = new(
        Msg241UnlinkDenied, "Bạn không còn quyền gỡ liên kết tài liệu trong cuộc họp này.",
        Msg247LinksLocked, LinkCodes.EndedMessage,
        Msg245UnlinkFailed, "Không gỡ được liên kết tài liệu. Chưa có liên kết nào thay đổi. Vui lòng thử lại.");

    /// <summary>Meeting document IDs for UC-33b, library document IDs for UC-33a and UC-33b AF-2.</summary>
    private sealed record DocumentIdsBody(Guid[]? DocumentIds);

    public static void MapMeetingDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        const string prefix = "/api/meetings/{meetingId:guid}/documents";
        app.MapGroup(prefix).RequireActiveUserOrGuest(ReadError(DocumentReadFailure.ReadFailed, Msg157ListFailed))
            .MapGet("", async (Guid meetingId, HttpContext context, IMeetingDocumentService service,
                IMeetingGuestIdentityResolver identities, CancellationToken ct) =>
            {
                var outcome = await service.ListAsync(meetingId, Reader(context, identities), ct);
                return outcome.List is { } list ? Results.Ok(list) : ReadError(outcome.Failure, Msg157ListFailed);
            });
        // The gate cannot tell a view from a download, so a database failure there reads as a failed view.
        app.MapGroup(prefix).RequireActiveUserOrGuest(ReadError(DocumentReadFailure.ReadFailed, Msg158ViewFailed))
            .MapGet("/{meetingDocumentId:guid}/content", ContentAsync);
        app.MapGroup(prefix)
            .RequireActiveUser(Map(new(DocumentUploadFailure.StoreFailed), Guid.Empty))
            .MapPost("/upload", UploadAsync);

        // UC-33a: from the caller's library.
        app.MapGroup(prefix).RequireActiveUser(Change(new(DocumentUploadFailure.StoreFailed), AddCodes))
            .MapPost("", async (Guid meetingId, DocumentIdsBody body, HttpContext context, IMeetingDocumentService service,
                MeetingDocumentChangePublisher publisher, ILoggerFactory logs, CancellationToken ct) =>
            {
                var outcome = await service.AddFromLibraryAsync(meetingId, CapabilityGate.GatedUserId(context.User),
                    body.DocumentIds ?? [], ct);
                await PublishAsync(outcome, meetingId, publisher, logs);
                return Map(outcome, meetingId);
            });

        // UC-33c.
        app.MapGroup(prefix).RequireActiveUser(Change(new(DocumentUploadFailure.StoreFailed), RemoveCodes))
            .MapDelete("/{meetingDocumentId:guid}", async (Guid meetingId, Guid meetingDocumentId, HttpContext context,
                IMeetingDocumentService service, MeetingDocumentChangePublisher publisher, ILoggerFactory logs,
                CancellationToken ct) =>
            {
                var outcome = await service.RemoveAsync(meetingId, meetingDocumentId, CapabilityGate.GatedUserId(context.User), ct);
                await PublishAsync(outcome, meetingId, publisher, logs);
                return Change(outcome, RemoveCodes, () => Results.Ok(new
                {
                    code = Msg167Removed, message = "Đã gỡ tài liệu khỏi cuộc họp.", meetingId, meetingDocumentId,
                }));
            });

        // UC-33b and UC-33d: a link between a meeting document and an agenda item, for the Chair or the Secretary.
        const string links = "/api/meetings/{meetingId:guid}/agenda/{agendaItemId:guid}/documents";
        app.MapGroup(links).RequireActiveUser(Change(new(DocumentUploadFailure.StoreFailed), LinkCodes))
            .MapPost("", (Guid meetingId, Guid agendaItemId, DocumentIdsBody body, HttpContext context,
                    IMeetingDocumentService service, MeetingDocumentChangePublisher publisher, ILoggerFactory logs,
                    CancellationToken ct)
                => AttachAsync(meetingId, agendaItemId, DocumentAttachSource.Meeting, body, context, service, publisher, logs, ct));
        app.MapGroup(links).RequireActiveUser(Change(new(DocumentUploadFailure.StoreFailed), LinkNewCodes))
            .MapPost("/from-library", (Guid meetingId, Guid agendaItemId, DocumentIdsBody body, HttpContext context,
                    IMeetingDocumentService service, MeetingDocumentChangePublisher publisher, ILoggerFactory logs,
                    CancellationToken ct)
                => AttachAsync(meetingId, agendaItemId, DocumentAttachSource.Library, body, context, service, publisher, logs, ct));
        app.MapGroup(links).RequireActiveUser(Change(new(DocumentUploadFailure.StoreFailed), LinkNewCodes))
            .MapPost("/upload", AttachUploadAsync);
        app.MapGroup(links).RequireActiveUser(Change(new(DocumentUploadFailure.StoreFailed), UnlinkCodes))
            .MapDelete("/{meetingDocumentId:guid}", async (Guid meetingId, Guid agendaItemId, Guid meetingDocumentId,
                HttpContext context, IMeetingDocumentService service, MeetingDocumentChangePublisher publisher,
                ILoggerFactory logs, CancellationToken ct) =>
            {
                var outcome = await service.DetachAsync(meetingId, agendaItemId, meetingDocumentId,
                    CapabilityGate.GatedUserId(context.User), ct);
                await PublishAsync(outcome, meetingId, publisher, logs);
                return Change(outcome, UnlinkCodes, () => Results.Ok(new
                {
                    code = Msg239Detached, message = "Đã gỡ tài liệu khỏi mục nghị sự.", meetingId, agendaItemId,
                    meetingDocumentId,
                }));
            });
    }

    /// <summary>
    /// Streams the stored bytes unchanged (BR-183, BR-216); the browser renders PDF and DOCX itself.
    /// <paramref name="download"/> marks the attachment as a download, refused for a Secret meeting (BR-188).
    /// </summary>
    private static async Task<IResult> ContentAsync(Guid meetingId, Guid meetingDocumentId, HttpContext context,
        IMeetingDocumentService service, IMeetingGuestIdentityResolver identities, CancellationToken ct, bool download = false)
    {
        var outcome = await service.OpenContentAsync(meetingId, meetingDocumentId, Reader(context, identities), download, ct);
        return outcome.Content is { } file
            ? Stream(context, file)
            : ReadError(outcome.Failure, download ? Msg160DownloadFailed : Msg158ViewFailed);
    }

    /// <summary>A guest token reads as the guest, never as a user; otherwise the gated user.</summary>
    private static ParticipantActor Reader(HttpContext context, IMeetingGuestIdentityResolver identities)
        => identities.Resolve(context.User) is { } guest ? new(Guest: guest) : new(CapabilityGate.GatedUserId(context.User));

    /// <summary>Hands over an opened file; the personal library's content endpoint uses it too.</summary>
    internal static IResult Stream(HttpContext context, MeetingDocumentContent file)
    {
        context.Response.ContentLength = file.ByteSize;
        // Confidential content must not linger in a shared browser's or proxy's cache.
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        // Always an attachment, never inline: our origin must not render an uploaded file as a page.
        // fileDownloadName writes filename*=UTF-8''… rather than the raw name.
        return Results.Stream(file.Content, file.ContentType, fileDownloadName: file.FileName);
    }

    private static IResult ReadError(DocumentReadFailure failure, string readFailedCode)
    {
        var (status, code, message) = failure switch
        {
            // Same answer for a meeting that does not exist, one the caller cannot see, and a
            // document removed from it or from another meeting (SCRUM-22, BR-211).
            DocumentReadFailure.NotFound => (404, Msg156DocumentsUnavailable, "Không truy cập được tài liệu của cuộc họp."),
            DocumentReadFailure.DownloadForbidden => (403, Msg159SecretNoDownload,
                "Tài liệu của cuộc họp Mật chỉ được xem trực tuyến, không được tải về."),
            _ when readFailedCode == Msg157ListFailed => (500, Msg157ListFailed, "Không tải được danh sách tài liệu. Vui lòng thử lại."),
            _ when readFailedCode == Msg160DownloadFailed => (503, Msg160DownloadFailed, "Không tải về được tài liệu. Vui lòng thử lại."),
            _ => (503, Msg158ViewFailed, "Không hiển thị được tài liệu. Vui lòng thử lại."),
        };
        return Results.Json(new { code, message }, statusCode: status);
    }

    private static async Task<IResult> UploadAsync(Guid meetingId, HttpContext context, IMeetingDocumentService service,
        MeetingDocumentChangePublisher publisher, ILoggerFactory logs, CancellationToken ct)
    {
        var actor = CapabilityGate.GatedUserId(context.User);

        // Before the body: someone who may not upload should not get to stream 200 MB to disk first.
        var access = await service.CheckUploadAsync(meetingId, actor, ct);
        if (access.Failure != DocumentUploadFailure.None) return Map(access, meetingId);

        var tempFiles = new List<string>();
        try
        {
            var validated = await ReadFilesAsync(context, tempFiles, ct);
            if (validated.Failure != DocumentUploadFailure.None) return Map(validated, meetingId);

            var uploaded = await service.UploadAsync(meetingId, actor, validated.Files!, ct);
            await PublishAsync(uploaded, meetingId, publisher, logs);
            return Map(uploaded, meetingId);
        }
        finally
        {
            DeleteTempFiles(tempFiles);
        }
    }

    /// <summary>UC-33b AF-1: the same checks and write order as UC-33a AF-1, plus the agenda item.</summary>
    private static async Task<IResult> AttachUploadAsync(Guid meetingId, Guid agendaItemId, HttpContext context,
        IMeetingDocumentService service, MeetingDocumentChangePublisher publisher, ILoggerFactory logs, CancellationToken ct)
    {
        var actor = CapabilityGate.GatedUserId(context.User);
        var access = await service.CheckAttachUploadAsync(meetingId, agendaItemId, actor, ct);
        if (access.Failure != DocumentUploadFailure.None) return Attached(access, meetingId, agendaItemId, LinkNewCodes);

        var tempFiles = new List<string>();
        try
        {
            var validated = await ReadFilesAsync(context, tempFiles, ct);
            if (validated.Failure != DocumentUploadFailure.None) return Attached(validated, meetingId, agendaItemId, LinkNewCodes);

            var uploaded = await service.AttachUploadAsync(meetingId, agendaItemId, actor, validated.Files!, ct);
            await PublishAsync(uploaded, meetingId, publisher, logs);
            return Attached(uploaded, meetingId, agendaItemId, LinkNewCodes);
        }
        finally
        {
            DeleteTempFiles(tempFiles);
        }
    }

    private static async Task<IResult> AttachAsync(Guid meetingId, Guid agendaItemId, DocumentAttachSource source,
        DocumentIdsBody body, HttpContext context, IMeetingDocumentService service, MeetingDocumentChangePublisher publisher,
        ILoggerFactory logs, CancellationToken ct)
    {
        var outcome = await service.AttachAsync(meetingId, agendaItemId, CapabilityGate.GatedUserId(context.User), source,
            body.DocumentIds ?? [], ct);
        await PublishAsync(outcome, meetingId, publisher, logs);
        return Attached(outcome, meetingId, agendaItemId, source == DocumentAttachSource.Library ? LinkNewCodes : LinkCodes);
    }

    /// <summary>
    /// Reads and checks the files of a multipart upload: the request's form, its size (BR-319), then each file
    /// by its bytes (BR-187, BR-214, BR-215). The caller deletes <paramref name="tempFiles"/>; the library
    /// upload (UC-36) uses this too.
    /// </summary>
    internal static async Task<DocumentUploadOutcome> ReadFilesAsync(HttpContext context, List<string> tempFiles,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!request.HasFormContentType || !request.ContentType!.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return new(DocumentUploadFailure.Invalid,
                Errors: new Dictionary<string, string[]> { ["files"] = ["Yêu cầu phải gửi tệp dạng multipart/form-data."] });
        if (request.ContentLength > MaxRequestBytes) return new(DocumentUploadFailure.TooMany);

        // Kestrel stops at 30 MB by default; uploads alone need 200 MB.
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size)
            size.MaxRequestBodySize = MaxRequestBytes;

        DocumentUploadCandidate[] candidates;
        try
        {
            candidates = await ReadUploadAsync(request, tempFiles, ct);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return new(DocumentUploadFailure.TooMany);
        }
        catch (InvalidDataException)
        {
            return new(DocumentUploadFailure.Invalid,
                Errors: new Dictionary<string, string[]> { ["files"] = ["Nội dung multipart không hợp lệ."] });
        }
        return await DocumentFileRules.ValidateAsync(candidates, ct);
    }

    internal static void DeleteTempFiles(List<string> tempFiles)
    {
        foreach (var path in tempFiles)
        {
            try { File.Delete(path); }
            catch (IOException) { /* the OS temp cleaner gets it */ }
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

    /// <summary>
    /// The documents are committed; a lost event only means readers refresh later. The same 2 s budget as
    /// agenda events, so a slow hub or database never holds up the response.
    /// </summary>
    private static async Task PublishAsync(DocumentUploadOutcome outcome, Guid meetingId,
        MeetingDocumentChangePublisher publisher, ILoggerFactory logs)
    {
        if (outcome.Failure != DocumentUploadFailure.None) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await publisher.PublishAsync(meetingId, timeout.Token); }
        catch (Exception ex)
        {
            logs.CreateLogger(nameof(MeetingDocumentEndpoints))
                .LogWarning(ex, "Could not publish documentsChanged for meeting {MeetingId}", meetingId);
        }
    }

    /// <summary>UC-33a, from the computer or from the library.</summary>
    private static IResult Map(DocumentUploadOutcome outcome, Guid meetingId)
        => Change(outcome, AddCodes, () => Results.Json(new
        {
            code = Msg161Added,
            message = "Đã thêm tài liệu vào cuộc họp.",
            meetingId,
            addedCount = outcome.Documents!.Count,
            documents = outcome.Documents,
        }, statusCode: StatusCodes.Status201Created));

    /// <summary>UC-33b: <c>documents</c> lists the meeting documents AF-1 and AF-2 added; linking existing ones adds none.</summary>
    private static IResult Attached(DocumentUploadOutcome outcome, Guid meetingId, Guid agendaItemId, ChangeCodes codes)
        => Change(outcome, codes, () => Results.Ok(new
        {
            code = Msg238Attached,
            message = "Đã gắn tài liệu vào mục nghị sự.",
            meetingId,
            agendaItemId,
            documents = outcome.Documents,
        }));

    private static IResult Change(DocumentUploadOutcome outcome, ChangeCodes codes, Func<IResult> success)
        => outcome.Failure == DocumentUploadFailure.None ? success() : Change(outcome, codes);

    private static IResult Change(DocumentUploadOutcome outcome, ChangeCodes codes)
    {
        if (outcome.Failure == DocumentUploadFailure.Inactive) return Results.Forbid();
        var (status, code, message) = outcome.Failure switch
        {
            // Same answer for a meeting that does not exist and one the caller cannot see (SCRUM-22).
            DocumentUploadFailure.NotFound => (404, Msg174MeetingUnavailable, "Không truy cập được cuộc họp này."),
            DocumentUploadFailure.Forbidden => (403, codes.Forbidden, codes.ForbiddenMessage),
            DocumentUploadFailure.Ended => (409, codes.Ended, codes.EndedMessage),
            DocumentUploadFailure.Cancelled => (409, Msg349MeetingCancelled, "Cuộc họp đã huỷ nên không thay đổi được nữa."),
            DocumentUploadFailure.Invalid => (400, Msg164InvalidFiles, "Hãy chọn tệp PDF hoặc DOCX hợp lệ, mỗi tệp không quá 50 MB."),
            DocumentUploadFailure.TooMany => (400, Msg288SubmissionTooLarge, "Mỗi lần tải tối đa 10 tệp, tổng dung lượng không quá 200 MB."),
            // The system's storage limit (ADR-0034).
            DocumentUploadFailure.StorageFull => (409, Msg346StorageFull, StorageFullMessage),
            DocumentUploadFailure.LibraryUnavailable => (400, Msg249LibrarySelectionInvalid,
                "Hãy chọn một hoặc nhiều tài liệu còn trong kho của bạn."),
            DocumentUploadFailure.AlreadyInMeeting => (409, Msg165AlreadyInMeeting,
                "Một hoặc nhiều tài liệu đã chọn đã có trong cuộc họp."),
            // Removed, or of another meeting: the same answer (BR-211).
            DocumentUploadFailure.NotInMeeting => (404, Msg170NotInMeeting,
                "Tài liệu này không còn trong cuộc họp. Hãy tải lại danh sách tài liệu."),
            DocumentUploadFailure.AgendaItemNotFound => (404, Msg242AgendaItemUnavailable,
                "Hãy chọn một mục nghị sự còn trong cuộc họp này."),
            DocumentUploadFailure.AlreadyLinked => (409, Msg243AlreadyLinked,
                "Một hoặc nhiều tài liệu đã chọn đã được gắn vào mục nghị sự này."),
            DocumentUploadFailure.NotLinked => (404, Msg246NotLinked,
                "Tài liệu này không còn gắn với mục nghị sự đã chọn. Hãy tải lại danh sách tài liệu."),
            _ => (503, codes.Failed, codes.FailedMessage),
        };
        return Results.Json(new { code, message, errors = outcome.Errors }, statusCode: status);
    }
}
