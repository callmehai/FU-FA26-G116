using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web.Endpoints;

/// <summary>HTTP boundary for UC-11 Create Meeting and UC-12 Update Meeting.</summary>
public static class MeetingEndpoints
{
    public static IEndpointRouteBuilder MapMeetingEndpoints(this IEndpointRouteBuilder app)
    {
        var meetings = app.MapGroup("/api/meetings")
            .WithTags("Meetings")
            .RequireCapability(Capability.MeetingCreator);

        meetings.MapPost("/", CreateAsync);
        meetings.MapGet("/{id:guid}", GetAsync);
        meetings.MapPut("/{id:guid}", UpdateAsync);
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
            _ => SaveFailed("MSG01", "Không thể tạo cuộc họp. Vui lòng thử lại."),
        };
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ClaimsPrincipal principal,
        IMeetingService service,
        CancellationToken ct)
    {
        var result = await service.GetAsync(id, CapabilityGate.GatedUserId(principal), ct);
        return result.Outcome switch
        {
            MeetingOutcome.Success => Results.Ok(MeetingResponse.From(result.Meeting!)),
            MeetingOutcome.NotFound => Results.NotFound(),
            _ => SaveFailed("MEETING_LOAD_FAILED", "Không thể tải thông tin cuộc họp. Vui lòng thử lại."),
        };
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] MeetingRequest? request,
        ClaimsPrincipal principal,
        IMeetingService service,
        CancellationToken ct)
    {
        var result = await service.UpdateAsync(
            id,
            request?.ToInput() ?? EmptyInput,
            CapabilityGate.GatedUserId(principal),
            ct);

        return result.Outcome switch
        {
            MeetingOutcome.Success => Results.Ok(new MeetingMutationResponse(
                "MSG02", "Cập nhật cuộc họp thành công.", MeetingResponse.From(result.Meeting!))),
            MeetingOutcome.InvalidInformation => Invalid(result),
            MeetingOutcome.NotFound => Results.NotFound(),
            MeetingOutcome.NotEditable => Results.Json(
                new MeetingErrorResponse("MSG03", "Cuộc họp này không thể chỉnh sửa được nữa."),
                statusCode: StatusCodes.Status409Conflict),
            _ => SaveFailed("MSG04", "Không thể cập nhật cuộc họp. Vui lòng thử lại."),
        };
    }

    private static IResult Invalid(MeetingResult result)
        => Results.Json(
            new MeetingErrorResponse(
                "MEETING_INVALID_INFORMATION",
                "Thông tin cuộc họp không hợp lệ.",
                result.Errors),
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult SaveFailed(string code, string message)
        => Results.Json(
            new MeetingErrorResponse(code, message),
            statusCode: StatusCodes.Status503ServiceUnavailable);

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
    string? Description)
{
    public MeetingInput ToInput() => new(
        Title, StartsAt, EndsAt, Mode, Location, Sensitivity, Description);
}

public sealed record MeetingResponse(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Mode,
    string Location,
    string Sensitivity,
    string Status,
    Guid CreatedById,
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
        meeting.CreatedAt,
        meeting.UpdatedAt);
}

public sealed record MeetingMutationResponse(string Code, string Message, MeetingResponse Meeting);

public sealed record MeetingErrorResponse(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string>? Errors = null);
