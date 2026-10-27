using TrustCabinet.Domain.Documents;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// The personal library: folders UC-72a–d (SCRUM-110), documents UC-34, UC-36, UC-71a–c (SCRUM-111). Only the
/// owner reaches it; another person's folder or document answers 404/MSG175 as if it did not exist (BR-242).
/// An administrator may read a deactivated person's library, audited (PO 27/10/2026).
/// </summary>
public static class PersonalLibraryEndpoints
{
    private const string Msg158ViewFailed = "MSG158";
    private const string Msg160DownloadFailed = "MSG160";
    private const string Msg164InvalidFiles = "MSG164";
    private const string Msg175ItemUnavailable = "MSG175";
    private const string Msg176LoadFailed = "MSG176";
    private const string Msg177Uploaded = "MSG177";
    private const string Msg178UploadFailed = "MSG178";
    private const string Msg179DestinationUnavailable = "MSG179";
    private const string Msg180Deleted = "MSG180";
    private const string Msg181DeleteFailed = "MSG181";
    private const string Msg183Renamed = "MSG183";
    private const string Msg184RenameFailed = "MSG184";
    private const string Msg185Moved = "MSG185";
    private const string Msg186MoveFailed = "MSG186";
    private const string Msg187FolderCreated = "MSG187";
    private const string Msg188FolderNameRequired = "MSG188";
    private const string Msg189TooDeep = "MSG189";
    private const string Msg190FolderCreateFailed = "MSG190";
    private const string Msg191FolderRenamed = "MSG191";
    private const string Msg192FolderRenameFailed = "MSG192";
    private const string Msg193IntoItself = "MSG193";
    private const string Msg194FolderMoved = "MSG194";
    private const string Msg195FolderMoveFailed = "MSG195";
    private const string Msg196FolderNotEmpty = "MSG196";
    private const string Msg197FolderDeleted = "MSG197";
    private const string Msg198FolderDeleteFailed = "MSG198";
    private const string Msg288SubmissionTooLarge = "MSG288";
    private const string Msg345QuotaExceeded = "MSG345";
    private const string Msg346DocumentNameInvalid = "MSG346";
    private const string Msg347TextTooLong = "MSG347";

    private sealed record CreateFolderBody(string? Name, Guid? ParentId);
    private sealed record NameBody(string? Name);
    private sealed record ParentBody(Guid? ParentId);
    private sealed record FileNameBody(string? FileName);
    private sealed record FolderBody(Guid? FolderId);

    public static void MapPersonalLibraryEndpoints(this IEndpointRouteBuilder app)
    {
        // UC-34: every folder plus the documents at one location; AF-1 searches by name or filters by type.
        app.MapGroup("/api/documents").RequireActiveUser(Failed(Msg176LoadFailed))
            .MapGet("", async (HttpContext context, IPersonalLibraryService service, CancellationToken ct,
                Guid? folderId = null, string? q = null, string? type = null) =>
            {
                var actor = CapabilityGate.GatedUserId(context.User);
                var outcome = await service.ViewAsync(actor, actor, folderId, q, type, ct);
                return outcome.Value is { } view ? Results.Ok(view) : Error(outcome.Failure, Msg176LoadFailed);
            });
        app.MapGroup("/api/documents").RequireActiveUser(Failed(Msg158ViewFailed))
            .MapGet("/{documentId:guid}/content", async (Guid documentId, HttpContext context, IPersonalLibraryService service,
                CancellationToken ct, bool download = false) =>
            {
                var actor = CapabilityGate.GatedUserId(context.User);
                return Content(context, await service.OpenContentAsync(actor, actor, documentId, ct), download);
            });
        app.MapGroup("/api/documents").RequireActiveUser(Failed(Msg178UploadFailed))
            .MapPost("/upload", UploadAsync);
        Change("PUT", "/api/documents/{documentId:guid}/name", Msg184RenameFailed,
            async (Guid documentId, FileNameBody body, HttpContext context, IPersonalLibraryService service, CancellationToken ct) =>
                Saved(await service.RenameDocumentAsync(CapabilityGate.GatedUserId(context.User), documentId, body.FileName, ct),
                    Msg184RenameFailed, document => new { code = Msg183Renamed, message = "Đã đổi tên tài liệu.", document }));
        Change("PUT", "/api/documents/{documentId:guid}/folder", Msg186MoveFailed,
            async (Guid documentId, FolderBody body, HttpContext context, IPersonalLibraryService service, CancellationToken ct) =>
                Saved(await service.MoveDocumentAsync(CapabilityGate.GatedUserId(context.User), documentId, body.FolderId, ct),
                    Msg186MoveFailed, document => new { code = Msg185Moved, message = "Đã chuyển tài liệu.", document }));
        Change("DELETE", "/api/documents/{documentId:guid}", Msg181DeleteFailed,
            async (Guid documentId, HttpContext context, IPersonalLibraryService service, CancellationToken ct) =>
                Done(await service.DeleteDocumentAsync(CapabilityGate.GatedUserId(context.User), documentId, ct),
                    Msg181DeleteFailed, new { code = Msg180Deleted, message = "Đã xoá tài liệu khỏi kho của bạn." }));

        Change("POST", "/api/folders", Msg190FolderCreateFailed,
            async (CreateFolderBody body, HttpContext context, IPersonalLibraryService service, CancellationToken ct) =>
            {
                var outcome = await service.CreateFolderAsync(CapabilityGate.GatedUserId(context.User), body.ParentId, body.Name, ct);
                return outcome.Value is { } folder
                    ? Results.Json(new { code = Msg187FolderCreated, message = "Đã tạo thư mục.", folder }, statusCode: StatusCodes.Status201Created)
                    : Error(outcome.Failure, Msg190FolderCreateFailed);
            });
        Change("PUT", "/api/folders/{folderId:guid}/name", Msg192FolderRenameFailed,
            async (Guid folderId, NameBody body, HttpContext context, IPersonalLibraryService service, CancellationToken ct) =>
                Saved(await service.RenameFolderAsync(CapabilityGate.GatedUserId(context.User), folderId, body.Name, ct),
                    Msg192FolderRenameFailed, folder => new { code = Msg191FolderRenamed, message = "Đã đổi tên thư mục.", folder }));
        Change("PUT", "/api/folders/{folderId:guid}/parent", Msg195FolderMoveFailed,
            async (Guid folderId, ParentBody body, HttpContext context, IPersonalLibraryService service, CancellationToken ct) =>
                Saved(await service.MoveFolderAsync(CapabilityGate.GatedUserId(context.User), folderId, body.ParentId, ct),
                    Msg195FolderMoveFailed, folder => new { code = Msg194FolderMoved, message = "Đã chuyển thư mục.", folder }));
        Change("DELETE", "/api/folders/{folderId:guid}", Msg198FolderDeleteFailed,
            async (Guid folderId, HttpContext context, IPersonalLibraryService service, CancellationToken ct) =>
                Done(await service.DeleteFolderAsync(CapabilityGate.GatedUserId(context.User), folderId, ct),
                    Msg198FolderDeleteFailed, new { code = Msg197FolderDeleted, message = "Đã xoá thư mục." }));

        // PO 27/10/2026: an administrator recovers a deactivated person's documents. Every read is audited; an
        // active person's library answers as missing.
        var admin = app.MapGroup("/api/users/{userId:guid}/library").RequireAdminUsers();
        admin.MapGet("", async (Guid userId, HttpContext context, IPersonalLibraryService service, CancellationToken ct,
            Guid? folderId = null) =>
        {
            var outcome = await service.ViewAsync(CapabilityGate.GatedUserId(context.User), userId, folderId, null, null, ct);
            return outcome.Value is { } view ? Results.Ok(view) : Error(outcome.Failure, Msg176LoadFailed);
        });
        admin.MapGet("/documents/{documentId:guid}/content", async (Guid userId, Guid documentId, HttpContext context,
            IPersonalLibraryService service, CancellationToken ct, bool download = false) =>
            Content(context, await service.OpenContentAsync(CapabilityGate.GatedUserId(context.User), userId, documentId, ct),
                download));

        void Change(string method, string pattern, string failedCode, Delegate handler)
            => app.MapGroup(pattern).RequireActiveUser(Failed(failedCode)).MapMethods("", [method], handler);
    }

    /// <summary>UC-36: the destination is checked before the body is read, then the same file checks and write order as UC-33a AF-1.</summary>
    private static async Task<IResult> UploadAsync(HttpContext context, IPersonalLibraryService service, CancellationToken ct,
        Guid? folderId = null)
    {
        var actor = CapabilityGate.GatedUserId(context.User);
        var access = await service.CheckUploadAsync(actor, folderId, ct);
        if (access != LibraryFailure.None) return Error(access, Msg178UploadFailed);

        var tempFiles = new List<string>();
        try
        {
            var validated = await MeetingDocumentEndpoints.ReadFilesAsync(context, tempFiles, ct);
            if (validated.Failure == DocumentUploadFailure.TooMany)
                return Json(400, Msg288SubmissionTooLarge, "Mỗi lần tải tối đa 10 tệp, tổng dung lượng không quá 200 MB.");
            if (validated.Failure != DocumentUploadFailure.None)
                return Results.Json(new { code = Msg164InvalidFiles, message = "Hãy chọn tệp PDF hoặc DOCX hợp lệ, mỗi tệp không quá 50 MB.",
                    errors = validated.Errors }, statusCode: StatusCodes.Status400BadRequest);

            var outcome = await service.UploadAsync(actor, folderId, validated.Files!, ct);
            return outcome.Value is { } documents
                ? Results.Json(new { code = Msg177Uploaded, message = "Đã tải tài liệu lên kho của bạn.", addedCount = documents.Count,
                    documents }, statusCode: StatusCodes.Status201Created)
                : Error(outcome.Failure, Msg178UploadFailed);
        }
        finally
        {
            MeetingDocumentEndpoints.DeleteTempFiles(tempFiles);
        }
    }

    private static IResult Content(HttpContext context, MeetingDocumentContentOutcome outcome, bool download)
        => outcome.Content is { } file ? MeetingDocumentEndpoints.Stream(context, file)
            : outcome.Failure == DocumentReadFailure.NotFound ? Error(LibraryFailure.NotFound, Msg158ViewFailed)
            : download ? Json(503, Msg160DownloadFailed, "Không tải về được tài liệu. Vui lòng thử lại.")
            : Failed(Msg158ViewFailed);

    private static IResult Saved<T>(LibraryOutcome<T> outcome, string failedCode, Func<T, object> success)
        => outcome.Value is { } value ? Results.Ok(success(value)) : Error(outcome.Failure, failedCode);

    private static IResult Done(LibraryFailure failure, string failedCode, object success)
        => failure == LibraryFailure.None ? Results.Ok(success) : Error(failure, failedCode);

    private static IResult Error(LibraryFailure failure, string failedCode) => failure switch
    {
        // Missing, or someone else's: the same answer (BR-242).
        LibraryFailure.NotFound => Json(404, Msg175ItemUnavailable, "Mục này không còn trong kho tài liệu của bạn."),
        LibraryFailure.DestinationNotFound => Json(404, Msg179DestinationUnavailable,
            "Thư mục đích không còn trong kho tài liệu của bạn."),
        LibraryFailure.NameRequired => Json(400, Msg188FolderNameRequired, "Hãy nhập tên thư mục."),
        LibraryFailure.NameTooLong => Json(400, Msg347TextTooLong, "Nội dung vượt quá độ dài cho phép."),
        LibraryFailure.NameInvalid => Json(400, Msg346DocumentNameInvalid,
            "Tên tài liệu không hợp lệ. Tên không được trống và phải giữ phần đuôi .pdf hoặc .docx của tệp."),
        LibraryFailure.TooDeep => Json(409, Msg189TooDeep, "Thư mục chỉ lồng tối đa 5 cấp."),
        LibraryFailure.IntoItself => Json(409, Msg193IntoItself, "Không thể chuyển thư mục vào chính nó hoặc thư mục con của nó."),
        LibraryFailure.NotEmpty => Json(409, Msg196FolderNotEmpty, "Chỉ xoá được thư mục rỗng."),
        LibraryFailure.QuotaExceeded => Json(409, Msg345QuotaExceeded,
            "Kho tài liệu của bạn chỉ chứa tối đa 1 GB. Chưa có tài liệu nào được tải lên."),
        _ => Failed(failedCode),
    };

    private static IResult Failed(string code) => code switch
    {
        Msg176LoadFailed => Json(500, code, "Không tải được kho tài liệu của bạn. Vui lòng thử lại."),
        Msg158ViewFailed => Json(503, code, "Không hiển thị được tài liệu. Vui lòng thử lại."),
        Msg178UploadFailed => Json(503, code, "Không tải lên được tệp. Chưa có tài liệu nào được thêm. Vui lòng thử lại."),
        Msg181DeleteFailed => Json(503, code, "Không xoá được tài liệu. Kho tài liệu của bạn không thay đổi."),
        Msg184RenameFailed => Json(503, code, "Không đổi được tên tài liệu. Tên vẫn giữ nguyên."),
        Msg186MoveFailed => Json(503, code, "Không chuyển được tài liệu. Vị trí vẫn giữ nguyên."),
        Msg190FolderCreateFailed => Json(503, code, "Không tạo được thư mục. Chưa có thư mục nào được tạo."),
        Msg192FolderRenameFailed => Json(503, code, "Không đổi được tên thư mục. Tên vẫn giữ nguyên."),
        Msg195FolderMoveFailed => Json(503, code, "Không chuyển được thư mục. Vị trí vẫn giữ nguyên."),
        _ => Json(503, Msg198FolderDeleteFailed, "Không xoá được thư mục. Thư mục không thay đổi."),
    };

    private static IResult Json(int status, string code, string message)
        => Results.Json(new { code, message }, statusCode: status);
}
