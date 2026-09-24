using Microsoft.AspNetCore.Mvc;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web.Endpoints;

/// <summary>HTTP boundary for UC-11 Create Meeting and UC-12 Update Meeting.</summary>
public static class MeetingEndpoints
{
    private const int MaxUpdateBodyBytes = 64 * 1024;

    public static IEndpointRouteBuilder MapMeetingEndpoints(this IEndpointRouteBuilder app)
    {
        var creatorMeetings = app.MapGroup("/api/meetings")
            .WithTags("Meetings")
            .RequireCapability(Capability.MeetingCreator);

        creatorMeetings.MapPost("/", CreateAsync);
        creatorMeetings.MapGet("/approvers", ListApproversAsync);
        creatorMeetings.MapPut("/{id:guid}", UpdateAsync);

        app.MapGroup("/api/meetings")
            .WithTags("Meetings")
            .RequireActiveUser()
            .MapGet("/{id:guid}", GetAsync);
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
            _ => SaveFailed("MEETING_LOAD_FAILED", "Không thể tải thông tin cuộc họp. Vui lòng thử lại."),
        };
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

        if (request.ContentLength > MaxUpdateBodyBytes)
        {
            return BodyTooLarge();
        }

        await using var bodyBuffer = new MemoryStream();
        var readBuffer = new byte[8192];
        while (true)
        {
            var bytesToRead = Math.Min(readBuffer.Length, MaxUpdateBodyBytes + 1 - (int)bodyBuffer.Length);
            var bytesRead = await request.Body.ReadAsync(readBuffer.AsMemory(0, bytesToRead), ct);
            if (bytesRead == 0)
            {
                break;
            }

            if (bodyBuffer.Length + bytesRead > MaxUpdateBodyBytes)
            {
                return BodyTooLarge();
            }

            await bodyBuffer.WriteAsync(readBuffer.AsMemory(0, bytesRead), ct);
        }

        MeetingResult result;
        try
        {
            result = await service.UpdateAsync(
                id,
                async token =>
                {
                    bodyBuffer.Position = 0;
                    var parsed = await JsonSerializer.DeserializeAsync<MeetingRequest>(
                        bodyBuffer,
                        jsonOptions.Value.SerializerOptions,
                        token);
                    return parsed?.ToInput() ?? EmptyInput;
                },
                actorId,
                ct);
        }
        catch (JsonException)
        {
            return Results.Json(
                new MeetingErrorResponse("MEETING_INVALID_INFORMATION", "Nội dung JSON không hợp lệ."),
                statusCode: StatusCodes.Status400BadRequest);
        }

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

    private static IResult UpdateStateError(MeetingOutcome outcome)
        => outcome switch
        {
            MeetingOutcome.NotFound => Results.NotFound(),
            MeetingOutcome.NotEditable => Results.Json(
                new MeetingErrorResponse("MSG03", "Cuộc họp này không thể chỉnh sửa được nữa."),
                statusCode: StatusCodes.Status409Conflict),
            _ => SaveFailed("MEETING_LOAD_FAILED", "Không thể tải thông tin cuộc họp. Vui lòng thử lại."),
        };

    private static IResult BodyTooLarge()
        => Results.Json(
            new MeetingErrorResponse("MEETING_BODY_TOO_LARGE", "Nội dung JSON vượt quá 64 KiB."),
            statusCode: StatusCodes.Status413PayloadTooLarge);

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

public sealed record MeetingErrorResponse(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string>? Errors = null);
