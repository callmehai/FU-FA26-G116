using TrustCabinet.Domain.Audit;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// UC-13 — the read-only audit log (SCRUM-16). Behind <c>AdminUsers</c> like the other administration screens (BR-440):
/// <c>403</c> without it, since the path is a fixed, documented endpoint. Nothing here writes the log, and viewing it
/// is not itself recorded.
/// </summary>
public static class AuditLogEndpoints
{
    private const string Msg469InvalidFilter = "MSG469";
    private const string Msg470ReadFailed = "MSG470";

    public static void MapAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit-log").WithTags("Audit").RequireAdminUsers();

        group.MapGet("", async (HttpRequest request, IAuditLogQueryService service, CancellationToken ct) =>
        {
            if (!AuditLogQueryParser.TryParse(request.Query, out var query, out var errors))
            {
                return Results.Json(new
                {
                    code = Msg469InvalidFilter,
                    message = "Bộ lọc nhật ký không hợp lệ. Kiểm tra khoảng thời gian và các giá trị đã chọn.",
                    errors,
                }, statusCode: StatusCodes.Status400BadRequest);
            }

            var page = await service.GetPageAsync(query!, ct);
            if (page is null)
            {
                return Results.Json(new { code = Msg470ReadFailed, message = "Không tải được nhật ký hệ thống. Vui lòng thử lại." },
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return Results.Ok(new
            {
                items = page.Items.Select(i => new
                {
                    i.Id,
                    i.OccurredAt,
                    actor = new { kind = i.Actor.Kind.ToString().ToLowerInvariant(), i.Actor.Id, i.Actor.Name, i.Actor.Email },
                    i.Action,
                    i.ActionLabel,
                    i.ObjectType,
                    i.ObjectId,
                    i.ObjectName,
                    i.IpAddress,
                    i.Details,
                }),
                page.Page,
                page.PageSize,
                page.TotalCount,
                page.TotalPages,
            });
        });

        group.MapGet("/event-types", () => Results.Ok(AuditEventCatalog.All));
    }
}

/// <summary>Turns the audit log's query string into validated criteria (BR-442, UC-13 EX-1).</summary>
internal static class AuditLogQueryParser
{
    private const int SearchMaxLength = 100;
    // Case-insensitive like IQueryCollection itself, so "PageSize" is read as pageSize rather than refused.
    private static readonly HashSet<string> Known = new(["from", "to", "actor", "action", "q", "page", "pageSize"],
        StringComparer.OrdinalIgnoreCase);

    public static bool TryParse(IQueryCollection values, out AuditLogQuery? query, out IReadOnlyDictionary<string, string> errors)
    {
        var fieldErrors = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in values.Keys.Where(k => !Known.Contains(k)))
        {
            fieldErrors[key] = "Tham số này không được hỗ trợ.";
        }

        var from = MeetingListQueryParser.ParseTimestamp(MeetingListQueryParser.Read(values, "from", fieldErrors), "from", fieldErrors);
        var to = MeetingListQueryParser.ParseTimestamp(MeetingListQueryParser.Read(values, "to", fieldErrors), "to", fieldErrors);
        if (from is not null && to is not null && from > to)
        {
            fieldErrors["to"] = "Thời điểm kết thúc phải từ thời điểm bắt đầu trở đi.";
        }

        Guid? actorUserId = null;
        AuditActorKind? actorKind = null;
        switch (MeetingListQueryParser.Read(values, "actor", fieldErrors))
        {
            case null:
                break;
            case "guest":
                actorKind = AuditActorKind.Guest;
                break;
            case "system":
                actorKind = AuditActorKind.System;
                break;
            case var raw when Guid.TryParseExact(raw, "D", out var id):
                actorUserId = id;
                break;
            default:
                fieldErrors["actor"] = "Người thực hiện phải là mã tài khoản, guest hoặc system.";
                break;
        }

        var action = MeetingListQueryParser.Read(values, "action", fieldErrors);
        if (action is not null && !AuditEventCatalog.Contains(action))
        {
            fieldErrors["action"] = "Loại sự kiện không có trong danh mục.";
        }

        var search = MeetingListQueryParser.Read(values, "q", fieldErrors)?.Trim();
        if (search?.Length > SearchMaxLength)
        {
            fieldErrors["q"] = $"Từ khoá tối đa {SearchMaxLength} ký tự.";
        }

        var page = MeetingListQueryParser.ParsePositiveInt(MeetingListQueryParser.Read(values, "page", fieldErrors), "page", 1,
            int.MaxValue, fieldErrors);
        var pageSize = MeetingListQueryParser.ParsePositiveInt(MeetingListQueryParser.Read(values, "pageSize", fieldErrors),
            "pageSize", 20, 100, fieldErrors);

        errors = fieldErrors;
        query = fieldErrors.Count > 0 ? null : new AuditLogQuery(from, to, actorUserId, actorKind, action, search, page, pageSize);
        return query is not null;
    }
}
