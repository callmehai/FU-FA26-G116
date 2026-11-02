using TrustCabinet.Domain.Documents;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// UC-35 — comments on a meeting document (SCRUM-23), for anyone who can view the meeting, and for a verified guest
/// of it on their UC-04 token (BR-181), who writes under their invitation.
/// </summary>
public static class DocumentCommentEndpoints
{
    private const string Msg156DocumentsUnavailable = "MSG156";
    private const string Msg199CommentAdded = "MSG199";
    private const string Msg200CommentBlank = "MSG200";
    private const string Msg202CommentFailed = "MSG202";
    private const string Msg204CommentsLoadFailed = "MSG204";
    private const string Msg251CommentsReadOnly = "MSG251";
    private const string Msg348TextTooLong = "MSG348";
    private const string Msg349MeetingCancelled = "MSG349";

    internal sealed record CommentBody(string? Body, int? PageNumber, Guid? ParentCommentId);

    public static void MapDocumentCommentEndpoints(this IEndpointRouteBuilder app)
    {
        const string pattern = "/api/meetings/{meetingId:guid}/documents/{meetingDocumentId:guid}/comments";
        app.MapGroup(pattern).RequireActiveUserOrGuest(Error(CommentFailure.ReadFailed))
            .MapGet("", async (Guid meetingId, Guid meetingDocumentId, HttpContext context, IDocumentCommentService service,
                IMeetingGuestIdentityResolver identities, CancellationToken ct) =>
            {
                var outcome = await service.ListAsync(meetingId, meetingDocumentId,
                    MeetingDocumentEndpoints.Reader(context, identities), ct);
                return outcome.Value is { } threads ? Results.Ok(threads) : Error(outcome.Failure);
            });
        app.MapGroup(pattern).RequireActiveUserOrGuest(Error(CommentFailure.SaveFailed))
            .MapPost("", async (Guid meetingId, Guid meetingDocumentId, CommentBody body, HttpContext context,
                IDocumentCommentService service, IMeetingGuestIdentityResolver identities, CancellationToken ct) =>
            {
                var outcome = await service.AddAsync(meetingId, meetingDocumentId,
                    MeetingDocumentEndpoints.Reader(context, identities), body.Body, body.PageNumber, body.ParentCommentId, ct);
                return outcome.Value is { } comment
                    ? Results.Json(new { code = Msg199CommentAdded, message = "Đã gửi góp ý.", comment },
                        statusCode: StatusCodes.Status201Created)
                    : Error(outcome.Failure);
            });
    }

    private static IResult Error(CommentFailure failure)
    {
        var (status, code, message) = failure switch
        {
            CommentFailure.Blank => (400, Msg200CommentBlank, "Hãy nhập nội dung góp ý."),
            CommentFailure.TooLong => (400, Msg348TextTooLong, "Nội dung vượt quá độ dài cho phép."),
            // A meeting the caller cannot view, a removed document, a document or a parent comment of another
            // meeting: the same answer (BR-211, BR-284).
            CommentFailure.NotFound => (404, Msg156DocumentsUnavailable, "Không truy cập được tài liệu của cuộc họp."),
            CommentFailure.Ended => (409, Msg251CommentsReadOnly, "Cuộc họp đã kết thúc nên góp ý chỉ còn để đọc."),
            CommentFailure.Cancelled => (409, Msg349MeetingCancelled, "Cuộc họp đã huỷ nên không thay đổi được nữa."),
            CommentFailure.ReadFailed => (500, Msg204CommentsLoadFailed, "Không tải được góp ý của tài liệu. Vui lòng thử lại."),
            _ => (503, Msg202CommentFailed, "Không gửi được góp ý. Chưa có góp ý nào được lưu. Vui lòng thử lại."),
        };
        return Results.Json(new { code, message }, statusCode: status);
    }
}
