using Microsoft.AspNetCore.Mvc;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web.Endpoints;

/// <summary>HTTP boundary for meeting creation, updates, detail and authorized listing.</summary>
public static class MeetingEndpoints
{
    public static IEndpointRouteBuilder MapMeetingEndpoints(this IEndpointRouteBuilder app)
    {
        var creatorMeetings = app.MapGroup("/api/meetings")
            .WithTags("Meetings")
            .RequireCapability(Capability.MeetingCreator);

        creatorMeetings.MapPost("/", CreateAsync);
        creatorMeetings.MapGet("/approvers", ListApproversAsync);
        creatorMeetings.MapPut("/{id:guid}", UpdateAsync);

        var listMeetings = app.MapGroup("/api/meetings")
            .WithTags("Meetings")
            .RequireActiveUser(ServerError("MEETING_LIST_FAILED", "Không thể tải danh sách cuộc họp. Vui lòng thử lại."));
        listMeetings.MapGet("/", ListAsync);

        var detailMeetings = app.MapGroup("/api/meetings")
            .WithTags("Meetings")
            .RequireActiveUser(ServerError("MEETING_LOAD_FAILED", "Không thể tải thông tin cuộc họp. Vui lòng thử lại."));
        detailMeetings.MapGet("/{id:guid}", GetAsync);
        return app;
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] MeetingRequest? request,
        ClaimsPrincipal principal,
        IMeetingService service,
        CancellationToken ct)
    {
        var result = await service.CreateAsync(
            request?.ToInput() ?? EmptyInput,
            CapabilityGate.GatedUserId(principal),
            ct);

        return result.Outcome switch
        {
            MeetingOutcome.Success => Results.Created(
                $"/api/meetings/{result.Meeting!.Id}",
                new MeetingMutationResponse("MEETING_CREATED", "Tạo cuộc họp thành công.", MeetingResponse.From(result.Meeting))),
            MeetingOutcome.InvalidInformation => Invalid(result),
            _ => ServerError("MEETING_CREATE_FAILED", "Không thể tạo cuộc họp. Vui lòng thử lại."),
        };
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ClaimsPrincipal principal,
        IMeetingService service,
        CancellationToken ct)
    {
        var actorId = CapabilityGate.CurrentUserId(principal);
        if (actorId is null)
        {
            return Results.Forbid();
        }

        var result = await service.GetAsync(id, actorId.Value, ct);
        return result.Outcome switch
        {
            MeetingOutcome.Success => Results.Ok(MeetingResponse.From(result.Meeting!)),
            MeetingOutcome.NotFound => Results.NotFound(),
            _ => ServerError("MEETING_LOAD_FAILED", "Không thể tải thông tin cuộc họp. Vui lòng thử lại."),
        };
    }

    private static async Task<IResult> ListAsync(
        HttpRequest request,
        ClaimsPrincipal principal,
        IMeetingService service,
        CancellationToken ct)
    {
        var actorId = CapabilityGate.CurrentUserId(principal);
        if (actorId is null)
        {
            return Results.Forbid();
        }

        if (!MeetingListQueryParser.TryParse(request.Query, out var query, out var errors))
        {
            return Results.Json(
                new MeetingErrorResponse("MEETING_INVALID_QUERY", "Tham số truy vấn không hợp lệ.", errors),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await service.ListAsync(query!, actorId.Value, ct);
        return result.Outcome == MeetingOutcome.Success
            ? Results.Ok(MeetingListResponse.From(result.Page!))
            : ServerError("MEETING_LIST_FAILED", "Không thể tải danh sách cuộc họp. Vui lòng thử lại.");
    }

    private static async Task<IResult> ListApproversAsync(
        IMeetingService service,
        CancellationToken ct)
        => Results.Ok(await service.ListEligibleApproversAsync(ct));

    private static async Task<IResult> UpdateAsync(
        Guid id,
        HttpRequest request,
        ClaimsPrincipal principal,
        IMeetingService service,
        IOptions<HttpJsonOptions> jsonOptions,
        CancellationToken ct)
    {
        var actorId = CapabilityGate.GatedUserId(principal);
        var state = await service.CheckUpdateAsync(id, actorId, ct);
        if (state.Outcome != MeetingOutcome.Success)
        {
            return UpdateStateError(state.Outcome);
        }

        if (!request.HasJsonContentType())
        {
            return Results.Json(
                new MeetingErrorResponse("MEETING_UNSUPPORTED_CONTENT_TYPE", "Content-Type phải là application/json."),
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        MeetingInput input;
        try
        {
            var parsed = await request.ReadFromJsonAsync<MeetingRequest>(jsonOptions.Value.SerializerOptions, ct);
            input = parsed?.ToInput() ?? EmptyInput;
        }
        catch (JsonException)
        {
            return Results.Json(
                new MeetingErrorResponse("MEETING_INVALID_INFORMATION", "Nội dung JSON không hợp lệ."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await service.UpdateAsync(id, input, actorId, ct);
        return result.Outcome switch
        {
            MeetingOutcome.Success => Results.Ok(new MeetingMutationResponse(
                "MEETING_UPDATED", "Cập nhật cuộc họp thành công.", MeetingResponse.From(result.Meeting!))),
            MeetingOutcome.InvalidInformation => Invalid(result),
            MeetingOutcome.NotFound => Results.NotFound(),
            MeetingOutcome.NotEditable => Results.Json(
                new MeetingErrorResponse("MEETING_NOT_EDITABLE", "Cuộc họp này không thể chỉnh sửa được nữa."),
                statusCode: StatusCodes.Status409Conflict),
            _ => ServerError("MEETING_UPDATE_FAILED", "Không thể cập nhật cuộc họp. Vui lòng thử lại."),
        };
    }

    private static IResult UpdateStateError(MeetingOutcome outcome)
        => outcome switch
        {
            MeetingOutcome.NotFound => Results.NotFound(),
            MeetingOutcome.NotEditable => Results.Json(
                new MeetingErrorResponse("MEETING_NOT_EDITABLE", "Cuộc họp này không thể chỉnh sửa được nữa."),
                statusCode: StatusCodes.Status409Conflict),
            _ => ServerError("MEETING_LOAD_FAILED", "Không thể tải thông tin cuộc họp. Vui lòng thử lại."),
        };

    private static IResult Invalid(MeetingResult result)
        => Results.Json(
            new MeetingErrorResponse(
                "MEETING_INVALID_INFORMATION",
                "Thông tin cuộc họp không hợp lệ.",
                result.Errors),
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult ServerError(string code, string message)
        => Results.Json(
            new MeetingErrorResponse(code, message),
            statusCode: StatusCodes.Status500InternalServerError);

    private static readonly MeetingInput EmptyInput = new(null, null, null, null, null, null, null);
}

/// <summary>The only caller-editable meeting fields. State, identity and timestamps are server-owned.</summary>
public sealed record MeetingRequest(
    string? Title,
    string? StartsAt,
    string? EndsAt,
    string? Mode,
    string? Location,
    string? Sensitivity,
    string? Description,
    Guid? AssignedApproverId)
{
    public MeetingInput ToInput() => new(
        Title, StartsAt, EndsAt, Mode, Location, Sensitivity, Description, AssignedApproverId);
}

public sealed record MeetingResponse(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string Mode,
    string Location,
    string Sensitivity,
    string Status,
    Guid CreatedById,
    Guid? AssignedApproverId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static MeetingResponse From(MeetingDetails meeting) => new(
        meeting.Id,
        meeting.Title,
        meeting.Description,
        meeting.StartsAt,
        meeting.EndsAt,
        meeting.Mode.ToString(),
        meeting.Location,
        meeting.Sensitivity.ToString(),
        meeting.Status.ToString(),
        meeting.CreatedById,
        meeting.AssignedApproverId,
        meeting.CreatedAt,
        meeting.UpdatedAt);
}

public sealed record MeetingMutationResponse(string Code, string Message, MeetingResponse Meeting);

public sealed record MeetingListItemResponse(
    Guid Id,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset CreatedAt,
    string Status,
    string Sensitivity)
{
    public static MeetingListItemResponse From(MeetingListItem item) => new(
        item.Id, item.Title, item.StartsAt, item.CreatedAt,
        item.Status.ToString(), item.Sensitivity.ToString());
}

public sealed record MeetingListResponse(
    IReadOnlyList<MeetingListItemResponse> Items,
    int Page,
    int PageSize,
    long TotalCount,
    long TotalPages)
{
    public static MeetingListResponse From(MeetingListPage page) => new(
        page.Items.Select(MeetingListItemResponse.From).ToArray(),
        page.Page,
        page.PageSize,
        page.TotalCount,
        page.TotalCount / page.PageSize + (page.TotalCount % page.PageSize == 0 ? 0 : 1));
}

public sealed record MeetingErrorResponse(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string>? Errors = null);
