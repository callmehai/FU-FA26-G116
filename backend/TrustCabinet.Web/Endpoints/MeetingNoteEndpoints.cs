using TrustCabinet.Domain.Notes;

namespace TrustCabinet.Web.Endpoints;

/// <summary>SCRUM-30 — the Secretary's running notes (UC-82): read them, save one.</summary>
public static class MeetingNoteEndpoints
{
    private const string Msg415Saved = "MSG415";
    private const string Msg416NotSecretary = "MSG416";
    private const string Msg417Closed = "MSG417";
    private const string Msg418Stale = "MSG418";
    private const string Msg419TooLong = "MSG419";
    private const string Msg420ItemGone = "MSG420";
    private const string Msg421ReadFailed = "MSG421";
    private const string Msg422SaveFailed = "MSG422";

    internal sealed record SaveBody(Guid? AgendaItemId, string? Body, DateTimeOffset? UpdatedAt);

    public static void MapMeetingNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var read = Error(500, Msg421ReadFailed, "Không tải được ghi chú.");
        var save = Error(503, Msg422SaveFailed, "Không lưu được ghi chú. Nội dung vẫn giữ trên máy này và sẽ gửi lại.");

        app.MapGroup("/api/meetings/{meetingId:guid}/notes").RequireActiveUser(read)
            .MapGet("", async (Guid meetingId, HttpContext context, IMeetingNoteService service, CancellationToken ct)
                => Map(await service.GetAsync(meetingId, CapabilityGate.GatedUserId(context.User), ct)));
        app.MapGroup("/api/meetings/{meetingId:guid}/notes").RequireActiveUser(save)
            .MapPut("", async (Guid meetingId, SaveBody body, HttpContext context, IMeetingNoteService service, CancellationToken ct)
                => Map(await service.SaveAsync(meetingId, CapabilityGate.GatedUserId(context.User), body.AgendaItemId, body.Body,
                    body.UpdatedAt, ct)));
    }

    private static IResult Map(NoteOutcome result)
    {
        if (result.Failure == NoteFailure.NotFound) return Results.NotFound();
        if (result.Failure == NoteFailure.Inactive) return Results.Forbid();
        if (result.Failure != NoteFailure.None)
        {
            var (status, code, message) = result.Failure switch
            {
                NoteFailure.Forbidden => (403, Msg416NotSecretary, "Chỉ Thư ký được ghi chú cuộc họp."),
                NoteFailure.Closed => (409, Msg417Closed,
                    "Chỉ ghi chú khi cuộc họp đang diễn ra, hoặc sau khi kết thúc mà chưa tạo bản nháp biên bản."),
                NoteFailure.Stale => (409, Msg418Stale, "Ghi chú vừa được sửa ở cửa sổ khác. Tải lại để xem bản mới nhất."),
                NoteFailure.TooLong => (400, Msg419TooLong, "Ghi chú quá dài, tối đa 20.000 ký tự."),
                NoteFailure.ItemGone => (409, Msg420ItemGone, "Mục nghị sự này không còn trong cuộc họp."),
                NoteFailure.ReadFailed => (500, Msg421ReadFailed, "Không tải được ghi chú."),
                _ => (503, Msg422SaveFailed, "Không lưu được ghi chú. Nội dung vẫn giữ trên máy này và sẽ gửi lại."),
            };
            return Results.Json(new { code, message }, statusCode: status);
        }
        if (result.Note is { } note) return Results.Ok(new { code = Msg415Saved, message = "Đã lưu ghi chú.", note });
        return Results.Ok(result.State);
    }

    private static IResult Error(int status, string code, string message) => Results.Json(new { code, message }, statusCode: status);
}
