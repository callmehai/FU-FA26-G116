using Microsoft.AspNetCore.Mvc;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web.Endpoints;

/// <summary>HTTP boundary for meeting creation, updates, detail and authorized listing.</summary>
public static class MeetingEndpoints
{
    private const string Msg110Started = "MSG110";
    private const string Msg111Ended = "MSG111";
    private const string Msg112StartConflict = "MSG112";
    private const string Msg114EndConflict = "MSG114";
    private const string Msg115StartFailed = "MSG115";
    private const string Msg116EndFailed = "MSG116";
    private const string Msg277InvalidParticipants = "MSG277";
    private const int MaxMeetingRequestBodyBytes = 64 * 1024;
    private const string Msg29Created = "MSG29";
    private const string Msg30Updated = "MSG30";
    private const string Msg31InvalidInformation = "MSG31";
    private const string Msg32InvalidQuery = "MSG32";
    private const string Msg33NotEditable = "MSG33";
    private const string Msg34UnsupportedContentType = "MSG34";
    private const string Msg35CreateFailed = "MSG35";
    private const string Msg36UpdateFailed = "MSG36";
    private const string Msg37LoadFailed = "MSG37";
    private const string Msg38ListFailed = "MSG38";
    private const string Msg41SubmitConflict = "MSG41";
    private const string Msg42SubmitFailed = "MSG42";
    private const string Msg43Approved = "MSG43";
    private const string Msg44Rejected = "MSG44";
    private const string Msg45Published = "MSG45";
    private const string Msg46PublishConflict = "MSG46";
    private const string Msg47PublishFailed = "MSG47";
    private const string Msg78Submitted = "MSG78";
    private const string Msg79ReturnedToDraft = "MSG79";
    private const string Msg80InvalidTransition = "MSG80";
    private const string Msg81SubmissionNotReady = "MSG81";
    private const string Msg82ApproverInvalid = "MSG82";
    private const string Msg83RejectionReasonRequired = "MSG83";
    private const string Msg84RejectionReasonTooLong = "MSG84";
    private const string Msg85ActionForbidden = "MSG85";
    private const string Msg86HistoryInvalidQuery = "MSG86";
    private const string Msg87BodyTooLarge = "MSG87";
    private const string Msg88TransitionFailed = "MSG88";
    private const string Msg136WorkflowReadFailed = "MSG136";
    private const string Msg130ApproversLoadFailed = "MSG130";
    private const string Msg131WorkflowActiveUserCheckFailed = "MSG131";
    private const string Msg132ApprovalConflict = "MSG132";
    private const string Msg133ReturnToDraftConflict = "MSG133";
    private const string Msg89OrganizerTransferred = "MSG89";
    private const string Msg134OrganizerInvalid = "MSG134";
    private const string Msg135OrganizerTransferConflict = "MSG135";
    private const string Msg137PublishRosterNotReady = "MSG137";
    private const string Msg138Rescheduled = "MSG138";

    public static IEndpointRouteBuilder MapMeetingEndpoints(this IEndpointRouteBuilder app)
    {
        var creatorMeetings = app.MapGroup("/api/meetings")
            .WithTags("Meetings")
            .RequireCapability(Capability.MeetingCreator);

        var approverList = app.MapGroup("/api/meetings")
            .WithTags("Meetings")
            .RequireCapability(
                Capability.MeetingCreator,
                SaveFailed(
                    Msg130ApproversLoadFailed,
                    "Không thể tải danh sách người duyệt. Vui lòng thử lại."));

        creatorMeetings.MapPost("/", CreateAsync);
        creatorMeetings.MapPut("/{id:guid}", UpdateAsync);
        approverList.MapGet("/approvers", ListApproversAsync);

        var listMeetings = app.MapGroup("/api/meetings")
            .WithTags("Meetings")
            .RequireActiveUser(ServerError(Msg38ListFailed, "Không thể tải danh sách cuộc họp. Vui lòng thử lại."));
        listMeetings.MapGet("/", ListAsync);

        var detailMeetings = app.MapGroup("/api/meetings")
            .WithTags("Meetings")
            .RequireActiveUser(ServerError(Msg37LoadFailed, "Không thể tải thông tin cuộc họp. Vui lòng thử lại."));
        detailMeetings.MapGet("/{id:guid}", GetAsync);

        var workflowMeetings = app.MapGroup("/api/meetings")
            .WithTags("Meetings")
            .RequireActiveUser(SaveFailed(
                Msg131WorkflowActiveUserCheckFailed,
                "Không thể kiểm tra tài khoản để thực hiện thao tác cuộc họp. Vui lòng thử lại."));
        workflowMeetings.MapGet("/{id:guid}/submission-readiness", GetSubmissionReadinessAsync);
        workflowMeetings.MapPost("/{id:guid}/transitions", TransitionAsync);
        workflowMeetings.MapGet("/{id:guid}/history", GetHistoryAsync);
        return app;
    }

    private static async Task<IResult> GetSubmissionReadinessAsync(
        Guid id,
        ClaimsPrincipal principal,
        IMeetingService service,
        CancellationToken ct)
    {
        var actorId = CapabilityGate.CurrentUserId(principal);
        if (actorId is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.GetSubmissionReadinessAsync(id, actorId.Value, ct);
        return result.Outcome switch
        {
            MeetingWorkflowOutcome.Success => Results.Ok(new MeetingSubmissionReadinessResponse(
                result.MeetingId, result.Status.ToString(), result.Ready, result.Errors)),
            MeetingWorkflowOutcome.NotFound => Results.NotFound(),
            MeetingWorkflowOutcome.Forbidden => WorkflowError(
                StatusCodes.Status403Forbidden, Msg85ActionForbidden, "Bạn không có quyền thực hiện thao tác này."),
            MeetingWorkflowOutcome.StatusConflict => WorkflowError(
                StatusCodes.Status409Conflict, Msg41SubmitConflict, "Cuộc họp không còn ở trạng thái Draft hoặc Rejected."),
            _ => SaveFailed(Msg136WorkflowReadFailed, "Không thể tải dữ liệu quy trình cuộc họp. Vui lòng thử lại."),
        };
    }

    private static async Task<IResult> TransitionAsync(
        Guid id,
        HttpRequest request,
        ClaimsPrincipal principal,
        IMeetingService service,
        IMeetingLifecycleService lifecycle,
        CancellationToken ct)
    {
        var actorId = CapabilityGate.CurrentUserId(principal);
        if (actorId is null)
        {
            return Results.Unauthorized();
        }

        if (!request.HasJsonContentType())
        {
            return WorkflowError(
                StatusCodes.Status415UnsupportedMediaType,
                Msg34UnsupportedContentType,
                "Content-Type phải là application/json.");
        }

        using var bodyBuffer = await ReadBoundedBodyAsync(request, ct);
        if (bodyBuffer is null)
        {
            return BodyTooLarge();
        }

        MeetingTransitionCommand? command;
        try
        {
            bodyBuffer.Position = 0;
            using var document = await JsonDocument.ParseAsync(bodyBuffer, cancellationToken: ct);
            command = ParseTransitionCommand(document.RootElement);
        }
        catch (JsonException)
        {
            command = null;
        }
        catch (FormatException)
        {
            command = null;
        }

        if (command is null)
        {
            return WorkflowError(
                StatusCodes.Status400BadRequest,
                Msg80InvalidTransition,
                "Yêu cầu chuyển trạng thái không hợp lệ.");
        }

        var result = command.Action is MeetingTransitionAction.Start or MeetingTransitionAction.End
            ? await lifecycle.TransitionAsync(id, command, actorId.Value, ct)
            : await service.TransitionAsync(id, command, actorId.Value, ct);
        return result.Outcome switch
        {
            MeetingWorkflowOutcome.Success when command.Action == MeetingTransitionAction.TransferOrganizer
                => Results.Ok(new MeetingOrganizerTransferResponse(
                    TransitionSuccessCode(command.Action),
                    TransitionMessage(command.Action),
                    result.History!.MeetingId,
                    result.History.ResultingStatus.ToString(),
                    result.History.NewOrganizerId!.Value,
                    MeetingHistoryItemResponse.From(result.History))),
            MeetingWorkflowOutcome.Success => Results.Ok(new MeetingTransitionResponse(
                TransitionSuccessCode(command.Action),
                TransitionMessage(command.Action),
                MeetingResponse.From(result.Meeting!),
                MeetingHistoryItemResponse.From(result.History!))),
            MeetingWorkflowOutcome.NotFound => Results.NotFound(),
            MeetingWorkflowOutcome.Forbidden when command.Action == MeetingTransitionAction.Publish => WorkflowError(
                StatusCodes.Status403Forbidden, Msg46PublishConflict, "Cuộc họp này không còn có thể công bố."),
            MeetingWorkflowOutcome.Forbidden when command.Action is MeetingTransitionAction.Start or MeetingTransitionAction.End
                => WorkflowError(StatusCodes.Status403Forbidden, TransitionConflictCode(command.Action), LifecycleConflictMessage(command.Action)),
            MeetingWorkflowOutcome.Forbidden => WorkflowError(
                StatusCodes.Status403Forbidden, Msg85ActionForbidden, "Bạn không có quyền thực hiện thao tác này."),
            MeetingWorkflowOutcome.InvalidRequest => WorkflowError(
                StatusCodes.Status400BadRequest, Msg80InvalidTransition, "Yêu cầu chuyển trạng thái không hợp lệ."),
            MeetingWorkflowOutcome.SubmissionNotReady => WorkflowError(
                StatusCodes.Status400BadRequest, Msg81SubmissionNotReady, "Cuộc họp chưa đủ thông tin để gửi duyệt.", result.Errors),
            MeetingWorkflowOutcome.InvalidApprover => WorkflowError(
                StatusCodes.Status400BadRequest, Msg82ApproverInvalid, "Người duyệt không hợp lệ hoặc không còn quyền duyệt cuộc họp."),
            MeetingWorkflowOutcome.InvalidOrganizer => WorkflowError(
                StatusCodes.Status400BadRequest, Msg134OrganizerInvalid, "Người tổ chức mới không hợp lệ hoặc không còn quyền tạo cuộc họp."),
            MeetingWorkflowOutcome.RejectionReasonRequired => WorkflowError(
                StatusCodes.Status400BadRequest, Msg83RejectionReasonRequired, "Cần nhập lý do từ chối."),
            MeetingWorkflowOutcome.RejectionReasonTooLong => WorkflowError(
                StatusCodes.Status400BadRequest, Msg84RejectionReasonTooLong, "Lý do từ chối không được vượt quá 1.000 ký tự."),
            MeetingWorkflowOutcome.PublicationRosterNotReady => WorkflowError(
                StatusCodes.Status409Conflict,
                Msg137PublishRosterNotReady,
                "Cuộc họp cần có đúng một Chủ tọa đang hoạt động và ít nhất một Thư ký đang hoạt động trước khi công bố."),
            MeetingWorkflowOutcome.StatusConflict => WorkflowError(
                StatusCodes.Status409Conflict,
                TransitionConflictCode(command.Action),
                command.Action switch
                {
                    MeetingTransitionAction.SubmitForApproval => "Cuộc họp không còn ở trạng thái Draft hoặc Rejected.",
                    MeetingTransitionAction.Publish => "Cuộc họp này không còn có thể công bố.",
                    MeetingTransitionAction.Start or MeetingTransitionAction.End => LifecycleConflictMessage(command.Action),
                    _ => "Trạng thái cuộc họp đã thay đổi hoặc không cho phép thao tác này.",
                }),
            _ => SaveFailed(
                TransitionFailureCode(command.Action),
                command.Action switch
                {
                    MeetingTransitionAction.Start => "Không thể bắt đầu cuộc họp. Vui lòng thử lại.",
                    MeetingTransitionAction.End => "Không thể kết thúc cuộc họp. Vui lòng thử lại.",
                    _ => "Không thể chuyển trạng thái cuộc họp. Vui lòng thử lại.",
                }),
        };
    }

    private static async Task<IResult> GetHistoryAsync(
        Guid id,
        HttpRequest request,
        ClaimsPrincipal principal,
        IMeetingService service,
        CancellationToken ct)
    {
        var actorId = CapabilityGate.CurrentUserId(principal);
        if (actorId is null)
        {
            return Results.Unauthorized();
        }

        var page = 1;
        var pageSize = 20;
        if (request.Query.TryGetValue("page", out var rawPage)
            && (!int.TryParse(rawPage, out page) || page < 1))
        {
            return InvalidHistoryQuery();
        }

        if (request.Query.TryGetValue("pageSize", out var rawPageSize)
            && (!int.TryParse(rawPageSize, out pageSize) || pageSize is < 1 or > 100))
        {
            return InvalidHistoryQuery();
        }

        if (request.Query.Keys.Any(key => key is not ("page" or "pageSize")))
        {
            return InvalidHistoryQuery();
        }

        var result = await service.GetHistoryAsync(id, actorId.Value, page, pageSize, ct);
        return result.Outcome switch
        {
            MeetingWorkflowOutcome.Success => Results.Ok(MeetingHistoryPageResponse.From(result.Page!)),
            MeetingWorkflowOutcome.NotFound => Results.NotFound(),
            MeetingWorkflowOutcome.InvalidRequest => InvalidHistoryQuery(),
            _ => SaveFailed(Msg136WorkflowReadFailed, "Không thể tải dữ liệu quy trình cuộc họp. Vui lòng thử lại."),
        };
    }

    private static MeetingTransitionCommand? ParseTransitionCommand(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? actionText = null;
        string? reason = null;
        Guid? assignedApproverId = null;
        Guid? organizerId = null;
        var hasReason = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                return null;
            }

            switch (property.Name)
            {
                case "action" when property.Value.ValueKind == JsonValueKind.String:
                    actionText = property.Value.GetString();
                    break;
                case "assignedApproverId" when property.Value.ValueKind == JsonValueKind.String:
                    if (!Guid.TryParse(property.Value.GetString(), out var parsedId) || parsedId == Guid.Empty)
                    {
                        return null;
                    }
                    assignedApproverId = parsedId;
                    break;
                case "organizerId" when property.Value.ValueKind == JsonValueKind.String:
                    if (!Guid.TryParse(property.Value.GetString(), out var parsedOrganizerId)
                        || parsedOrganizerId == Guid.Empty)
                    {
                        return null;
                    }
                    organizerId = parsedOrganizerId;
                    break;
                case "reason" when property.Value.ValueKind == JsonValueKind.String:
                    hasReason = true;
                    reason = property.Value.GetString();
                    break;
                case "reason" when property.Value.ValueKind == JsonValueKind.Null:
                    hasReason = true;
                    reason = null;
                    break;
                default:
                    return null;
            }
        }

        if (!Enum.TryParse<MeetingTransitionAction>(actionText, ignoreCase: false, out var action)
            || !Enum.IsDefined(action)
            || !string.Equals(actionText, action.ToString(), StringComparison.Ordinal))
        {
            return null;
        }

        var command = new MeetingTransitionCommand(action, assignedApproverId, reason, organizerId);
        return action switch
        {
            MeetingTransitionAction.SubmitForApproval when assignedApproverId.HasValue && !hasReason
                && !organizerId.HasValue => command,
            MeetingTransitionAction.Reject when !assignedApproverId.HasValue && !organizerId.HasValue => command,
            MeetingTransitionAction.TransferOrganizer when organizerId.HasValue
                && !assignedApproverId.HasValue && !hasReason => command,
            _ when action != MeetingTransitionAction.TransferOrganizer
                && !assignedApproverId.HasValue && !hasReason && !organizerId.HasValue => command,
            _ => null,
        };
    }

    private static string TransitionMessage(MeetingTransitionAction action) => action switch
    {
        MeetingTransitionAction.Start => "Bắt đầu cuộc họp thành công.",
        MeetingTransitionAction.End => "Kết thúc cuộc họp thành công.",
        MeetingTransitionAction.SubmitForApproval => "Đã gửi cuộc họp để duyệt.",
        MeetingTransitionAction.Approve => "Đã duyệt cuộc họp.",
        MeetingTransitionAction.Reject => "Đã từ chối cuộc họp.",
        MeetingTransitionAction.ReturnToDraft => "Đã đưa cuộc họp về bản nháp.",
        MeetingTransitionAction.Publish => "Đã công bố cuộc họp.",
        MeetingTransitionAction.TransferOrganizer => "Đã chuyển cuộc họp cho người tổ chức mới.",
        _ => "Đã chuyển trạng thái cuộc họp.",
    };

    private static string TransitionSuccessCode(MeetingTransitionAction action) => action switch
    {
        MeetingTransitionAction.Start => Msg110Started,
        MeetingTransitionAction.End => Msg111Ended,
        MeetingTransitionAction.SubmitForApproval => Msg78Submitted,
        MeetingTransitionAction.Approve => Msg43Approved,
        MeetingTransitionAction.Reject => Msg44Rejected,
        MeetingTransitionAction.ReturnToDraft => Msg79ReturnedToDraft,
        MeetingTransitionAction.Publish => Msg45Published,
        MeetingTransitionAction.TransferOrganizer => Msg89OrganizerTransferred,
        _ => Msg80InvalidTransition,
    };

    private static string TransitionConflictCode(MeetingTransitionAction action) => action switch
    {
        MeetingTransitionAction.Start => Msg112StartConflict,
        MeetingTransitionAction.End => Msg114EndConflict,
        MeetingTransitionAction.SubmitForApproval => Msg41SubmitConflict,
        MeetingTransitionAction.Approve or MeetingTransitionAction.Reject => Msg132ApprovalConflict,
        MeetingTransitionAction.ReturnToDraft => Msg133ReturnToDraftConflict,
        MeetingTransitionAction.Publish => Msg46PublishConflict,
        MeetingTransitionAction.TransferOrganizer => Msg135OrganizerTransferConflict,
        _ => Msg80InvalidTransition,
    };

    private static string TransitionFailureCode(MeetingTransitionAction action) => action switch
    {
        MeetingTransitionAction.Start => Msg115StartFailed,
        MeetingTransitionAction.End => Msg116EndFailed,
        MeetingTransitionAction.SubmitForApproval => Msg42SubmitFailed,
        MeetingTransitionAction.Approve or MeetingTransitionAction.Reject or MeetingTransitionAction.ReturnToDraft
            => Msg88TransitionFailed,
        MeetingTransitionAction.Publish => Msg47PublishFailed,
        _ => Msg88TransitionFailed,
    };

    private static string LifecycleConflictMessage(MeetingTransitionAction action)
        => action == MeetingTransitionAction.Start ? "Không thể bắt đầu cuộc họp này." : "Không thể kết thúc cuộc họp này.";

    private static IResult InvalidHistoryQuery()
        => WorkflowError(StatusCodes.Status400BadRequest, Msg86HistoryInvalidQuery, "Tham số truy vấn không hợp lệ.");

    private static IResult WorkflowError(
        int statusCode,
        string code,
        string message,
        IReadOnlyDictionary<string, string>? errors = null)
        => Results.Json(new MeetingErrorResponse(code, message, errors), statusCode: statusCode);

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateMeetingRequest? request,
        ClaimsPrincipal principal,
        IMeetingService service,
        CancellationToken ct)
    {
        var result = await service.CreateAsync(
            request?.ToInput() ?? EmptyInput,
            CapabilityGate.GatedUserId(principal),
            request?.Participants, ct);

        return result.Outcome switch
        {
            MeetingOutcome.Success => Results.Created(
                $"/api/meetings/{result.Meeting!.Id}",
                new MeetingMutationResponse(Msg29Created, "Tạo cuộc họp thành công.", MeetingResponse.From(result.Meeting))),
            MeetingOutcome.InvalidParticipants => WorkflowError(400, Msg277InvalidParticipants, "Danh sách thành phần tham dự không hợp lệ.", result.Errors),
            MeetingOutcome.InvalidInformation => Invalid(result),
            _ => ServerError(Msg35CreateFailed, "Không thể tạo cuộc họp. Vui lòng thử lại."),
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
            _ => ServerError(Msg37LoadFailed, "Không thể tải thông tin cuộc họp. Vui lòng thử lại."),
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
                new MeetingErrorResponse(Msg32InvalidQuery, "Tiêu chí danh sách cuộc họp không hợp lệ.", errors),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await service.ListAsync(query!, actorId.Value, ct);
        return result.Outcome == MeetingOutcome.Success
            ? Results.Ok(MeetingListResponse.From(result.Page!))
            : ServerError(Msg38ListFailed, "Không thể tải danh sách cuộc họp. Vui lòng thử lại.");
    }

    private static async Task<IResult> ListApproversAsync(
        ClaimsPrincipal principal,
        IMeetingService service,
        CancellationToken ct)
    {
        var actorId = CapabilityGate.GatedUserId(principal);
        var result = await service.ListEligibleApproversAsync(ct);
        return result.Outcome switch
        {
            MeetingOutcome.Success => Results.Ok(result.Approvers!.Where(approver => approver.Id != actorId).ToArray()),
            _ => SaveFailed(
                Msg130ApproversLoadFailed,
                "Không thể tải danh sách người duyệt. Vui lòng thử lại."),
        };
    }

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
                new MeetingErrorResponse(Msg34UnsupportedContentType, "Content-Type phải là application/json."),
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        using var bodyBuffer = await ReadBoundedBodyAsync(request, ct);
        if (bodyBuffer is null)
        {
            return BodyTooLarge();
        }

        MeetingResult result;
        try
        {
            result = await service.UpdateAsync(
                id,
                async token =>
                {
                    bodyBuffer.Position = 0;
                    using var document = await JsonDocument.ParseAsync(bodyBuffer, cancellationToken: token);
                    if (document.RootElement.ValueKind == JsonValueKind.Object
                        && document.RootElement.EnumerateObject().Any(p => p.Name.Equals("participants", StringComparison.OrdinalIgnoreCase)))
                        throw new JsonException("Participants can only be supplied when creating a meeting.");
                    var parsed = document.RootElement.Deserialize<MeetingRequest>(jsonOptions.Value.SerializerOptions);
                    return parsed?.ToInput() ?? EmptyInput;
                },
                actorId,
                ct);
        }
        catch (JsonException)
        {
            return Results.Json(
                new MeetingErrorResponse(Msg31InvalidInformation, "Nội dung JSON không hợp lệ."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        return result.Outcome switch
        {
            MeetingOutcome.Success when result.Rescheduled => Results.Ok(new MeetingMutationResponse(
                Msg138Rescheduled,
                "Đã dời lịch cuộc họp. Người được mời sẽ nhận thông báo.",
                MeetingResponse.From(result.Meeting!))),
            MeetingOutcome.Success => Results.Ok(new MeetingMutationResponse(
                Msg30Updated, "Cập nhật cuộc họp thành công.", MeetingResponse.From(result.Meeting!))),
            MeetingOutcome.InvalidInformation => Invalid(result),
            MeetingOutcome.NotFound => Results.NotFound(),
            MeetingOutcome.NotEditable => Results.Json(
                new MeetingErrorResponse(Msg33NotEditable, "Cuộc họp này không thể chỉnh sửa được nữa."),
                statusCode: StatusCodes.Status409Conflict),
            _ => ServerError(Msg36UpdateFailed, "Không thể cập nhật cuộc họp. Vui lòng thử lại."),
        };
    }

    private static IResult UpdateStateError(MeetingOutcome outcome)
        => outcome switch
        {
            MeetingOutcome.NotFound => Results.NotFound(),
            MeetingOutcome.NotEditable => Results.Json(
                new MeetingErrorResponse(Msg33NotEditable, "Cuộc họp này không thể chỉnh sửa được nữa."),
                statusCode: StatusCodes.Status409Conflict),
            _ => ServerError(Msg37LoadFailed, "Không thể tải thông tin cuộc họp. Vui lòng thử lại."),
        };

    private static async Task<MemoryStream?> ReadBoundedBodyAsync(HttpRequest request, CancellationToken ct)
    {
        if (request.ContentLength > MaxMeetingRequestBodyBytes)
        {
            return null;
        }

        var body = new MemoryStream();
        try
        {
            var buffer = new byte[8192];
            while (true)
            {
                var bytesToRead = Math.Min(buffer.Length, MaxMeetingRequestBodyBytes + 1 - (int)body.Length);
                var bytesRead = await request.Body.ReadAsync(buffer.AsMemory(0, bytesToRead), ct);
                if (bytesRead == 0)
                {
                    body.Position = 0;
                    return body;
                }

                if (body.Length + bytesRead > MaxMeetingRequestBodyBytes)
                {
                    body.Dispose();
                    return null;
                }

                await body.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            }
        }
        catch
        {
            body.Dispose();
            throw;
        }
    }

    private static IResult BodyTooLarge()
        => Results.Json(
            new MeetingErrorResponse(Msg87BodyTooLarge, "Nội dung JSON vượt quá 64 KiB."),
            statusCode: StatusCodes.Status413PayloadTooLarge);

    private static IResult Invalid(MeetingResult result)
        => Results.Json(
            new MeetingErrorResponse(
                Msg31InvalidInformation,
                "Thông tin cuộc họp không hợp lệ.",
                result.Errors),
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult ServerError(string code, string message)
        => Results.Json(
            new MeetingErrorResponse(code, message),
            statusCode: StatusCodes.Status500InternalServerError);

    private static IResult SaveFailed(string code, string message)
        => Results.Json(
            new MeetingErrorResponse(code, message),
            statusCode: StatusCodes.Status503ServiceUnavailable);

    private static readonly MeetingInput EmptyInput = new(null, null, null, null, null, null, null, null);
}

/// <summary>
/// The only caller-editable meeting fields. State, identity, actual times and audit timestamps are
/// server-owned. Times are ISO 8601 with an offset.
/// </summary>
public sealed record CreateMeetingRequest(
    string? Title, string? PlannedStartTime, string? PlannedEndTime, string? Mode,
    string? Location, string? OnlineLink, string? Sensitivity, string? Description,
    ParticipantSeed[]? Participants, string? CaptureMode = null)
{
    public MeetingInput ToInput() => new(Title, PlannedStartTime, PlannedEndTime, Mode, Location, OnlineLink, Sensitivity, Description,
        CaptureMode);
}

public sealed record MeetingRequest(
    string? Title,
    string? PlannedStartTime,
    string? PlannedEndTime,
    string? Mode,
    string? Location,
    string? OnlineLink,
    string? Sensitivity,
    string? Description,
    string? CaptureMode = null)
{
    public MeetingInput ToInput() => new(
        Title, PlannedStartTime, PlannedEndTime, Mode, Location, OnlineLink, Sensitivity, Description, CaptureMode);
}

public sealed record MeetingResponse(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset PlannedStartTime,
    DateTimeOffset PlannedEndTime,
    DateTimeOffset? ActualStartedAt,
    DateTimeOffset? ActualEndedAt,
    string Mode,
    string Location,
    string? OnlineLink,
    string Sensitivity,
    string Status,
    Guid CreatedById,
    Guid? AssignedApproverId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    MeetingPerson? Chair, IReadOnlyList<MeetingPerson> Secretaries, string? MyMeetingRole,
    string? MyResponse, bool CanRespond, string CaptureMode)
{
    public static MeetingResponse From(MeetingDetails meeting) => new(
        meeting.Id,
        meeting.Title,
        meeting.Description,
        meeting.PlannedStartTime,
        meeting.PlannedEndTime,
        meeting.ActualStartedAt,
        meeting.ActualEndedAt,
        meeting.Mode.ToString(),
        meeting.Location,
        meeting.OnlineLink,
        meeting.Sensitivity.ToString(),
        meeting.Status.ToString(),
        meeting.CreatedById,
        meeting.AssignedApproverId,
        meeting.CreatedAt,
        meeting.UpdatedAt, meeting.Chair, meeting.Secretaries ?? [], meeting.MyMeetingRole,
        meeting.MyResponse, meeting.CanRespond, meeting.CaptureMode.ToString());
}

public sealed record MeetingMutationResponse(string Code, string Message, MeetingResponse Meeting);

public sealed record MeetingListItemResponse(
    Guid Id,
    string Title,
    DateTimeOffset PlannedStartTime,
    DateTimeOffset PlannedEndTime,
    string Mode,
    string Location,
    DateTimeOffset CreatedAt,
    string Status,
    string Sensitivity,
    string? MyResponse,
    bool CanRespond)
{
    public static MeetingListItemResponse From(MeetingListItem item) => new(
        item.Id, item.Title, item.PlannedStartTime, item.PlannedEndTime, item.Mode.ToString(), item.Location,
        item.CreatedAt, item.Status.ToString(), item.Sensitivity.ToString(), item.MyResponse, item.CanRespond);
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

public sealed record MeetingSubmissionReadinessResponse(
    Guid MeetingId,
    string Status,
    bool Ready,
    IReadOnlyDictionary<string, string> Errors);

public sealed record MeetingHistoryItemResponse(
    Guid Id,
    Guid MeetingId,
    string Action,
    string? FromStatus,
    string ResultingStatus,
    Guid ActorId,
    string ActorName,
    DateTimeOffset OccurredAt,
    string? RejectionReason,
    Guid? AssignedApproverId,
    Guid? PreviousOrganizerId = null,
    Guid? NewOrganizerId = null)
{
    public static MeetingHistoryItemResponse From(MeetingHistoryRecord history) => new(
        history.Id,
        history.MeetingId,
        history.Action.ToString(),
        history.FromStatus?.ToString(),
        history.ResultingStatus.ToString(),
        history.ActorId,
        history.ActorName,
        history.OccurredAt,
        history.RejectionReason,
        history.AssignedApproverId,
        history.PreviousOrganizerId,
        history.NewOrganizerId);
}

public sealed record MeetingHistoryPageResponse(
    IReadOnlyList<MeetingHistoryItemResponse> Items,
    int Page,
    int PageSize,
    long TotalCount,
    long TotalPages)
{
    public static MeetingHistoryPageResponse From(MeetingHistoryPage page) => new(
        page.Items.Select(MeetingHistoryItemResponse.From).ToArray(),
        page.Page,
        page.PageSize,
        page.TotalCount,
        page.TotalCount / page.PageSize + (page.TotalCount % page.PageSize == 0 ? 0 : 1));
}

public sealed record MeetingTransitionResponse(
    string Code,
    string Message,
    MeetingResponse Meeting,
    MeetingHistoryItemResponse History);

public sealed record MeetingOrganizerTransferResponse(
    string Code,
    string Message,
    Guid MeetingId,
    string Status,
    Guid OrganizerId,
    MeetingHistoryItemResponse History);
