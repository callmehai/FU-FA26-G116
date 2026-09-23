using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TrustCabinet.Domain.Access;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// UC-11 … UC-13's <c>:Role Controller</c> (SCRUM-63) — turns HTTP into a call on
/// <see cref="IRoleService"/>, and an outcome into a status code and a message code.
///
/// It holds no rules of its own, exactly like <c>UserEndpoints</c>: which branch applies is the
/// service's decision, and this file only decides how each one looks on the wire.
/// </summary>
public static class RoleEndpoints
{
    /// <summary>
    /// Message codes for UC-11 … UC-13. The code is what the frontend and the tester match on, so
    /// rewording the text never breaks either.
    /// </summary>
    private const string Msg19Created = "MSG19";
    private const string Msg20Invalid = "MSG20";
    private const string Msg21DuplicateName = "MSG21";
    private const string Msg22Updated = "MSG22";
    private const string Msg23Deleted = "MSG23";
    private const string Msg24BuiltIn = "MSG24";
    private const string Msg25InUse = "MSG25";

    /// <summary>
    /// The diagrams' "Unable to create / update / delete role" has no code of its own. It is the
    /// same situation as UC-10's MSG18 — nothing was wrong with the request, retry — so it shares
    /// that code until the BA assigns one.
    /// </summary>
    private const string Msg18SaveFailed = "MSG18";

    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        // Same gate as /api/users and /api/roles/capabilities: the token policy plus a re-read of
        // the caller from the database on every request.
        var roles = app.MapGroup("/api/roles")
            .WithTags("Roles")
            .RequireAdminUsers();

        roles.MapGet("/", ListAsync);
        roles.MapPost("/", CreateAsync);
        roles.MapPut("/{id:guid}", UpdateAsync);
        roles.MapDelete("/{id:guid}", DeleteAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(IRoleService roles, CancellationToken ct)
        => Results.Ok((await roles.ListAsync(ct)).Select(RoleResponse.From));

    /// <summary>UC-11.</summary>
    private static async Task<IResult> CreateAsync(
        [FromBody] CreateRoleRequest? request,
        ClaimsPrincipal principal,
        IRoleService roles,
        CancellationToken ct)
    {
        var result = await roles.CreateAsync(
            // An empty body is a form with nothing filled in: MSG20 for the name, not the
            // framework's own 400 the frontend has no code for.
            new NewRole(request?.Name, request?.Description, request?.Capabilities),
            AdminCallerRevalidation.RevalidatedUserId(principal),
            ct);

        if (!result.Succeeded)
        {
            return Rejected(result, "Không thể tạo vai trò. Vui lòng thử lại.");
        }

        return Results.Created(
            $"/api/roles/{result.Role!.Id}",
            new RoleMutationResponse(
                Msg19Created, "Tạo vai trò thành công.", RoleResponse.From(result.Role)));
    }

    /// <summary>UC-12 — name and description only; capabilities change through UC-10.</summary>
    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateRoleRequest? request,
        ClaimsPrincipal principal,
        IRoleService roles,
        CancellationToken ct)
    {
        var result = await roles.UpdateAsync(
            id,
            new RoleEdit(request?.Name, request?.Description),
            AdminCallerRevalidation.RevalidatedUserId(principal),
            ct);

        return result.Succeeded
            ? Results.Ok(new RoleMutationResponse(
                Msg22Updated, "Cập nhật vai trò thành công.", RoleResponse.From(result.Role!)))
            : Rejected(result, "Không thể cập nhật vai trò. Vui lòng thử lại.");
    }

    /// <summary>UC-13.</summary>
    private static async Task<IResult> DeleteAsync(
        Guid id,
        ClaimsPrincipal principal,
        IRoleService roles,
        CancellationToken ct)
    {
        var result = await roles.DeleteAsync(id, AdminCallerRevalidation.RevalidatedUserId(principal), ct);

        return result.Succeeded
            ? Results.Ok(new RoleMessageResponse(Msg23Deleted, "Xoá vai trò thành công."))
            : Rejected(result, "Không thể xoá vai trò. Vui lòng thử lại.");
    }

    /// <summary>The failing branches, each with the status code that describes it.</summary>
    private static IResult Rejected(RoleResult result, string saveFailedMessage) => result.Outcome switch
    {
        // No body: there is nothing to say about an id that matches no role. The client refreshes
        // its list, as the diagrams' "Role not found" branch has it.
        RoleOutcome.NotFound => Results.NotFound(),

        RoleOutcome.InvalidInformation => Results.Json(
            new RoleErrorResponse(Msg20Invalid, "Thông tin vai trò không hợp lệ.", result.Errors),
            statusCode: StatusCodes.Status400BadRequest),

        RoleOutcome.DuplicateName => Results.Json(
            new RoleErrorResponse(
                Msg21DuplicateName, "Đã có vai trò dùng tên này. Hãy đặt một tên khác.", null),
            statusCode: StatusCodes.Status409Conflict),

        RoleOutcome.BuiltInRole => Results.Json(
            new RoleErrorResponse(
                Msg24BuiltIn,
                "Đây là vai trò mặc định của hệ thống, không đổi tên hoặc xoá được. "
                + "Bạn vẫn sửa được quyền của nó.",
                null),
            statusCode: StatusCodes.Status409Conflict),

        RoleOutcome.InUse => Results.Json(
            new RoleInUseResponse(
                Msg25InUse,
                $"Vai trò này vẫn còn {result.UserCount} tài khoản mang. "
                + "Hãy chuyển họ sang vai trò khác trước khi xoá.",
                result.UserCount),
            statusCode: StatusCodes.Status409Conflict),

        // SaveFailed, and defensively any outcome added later without a case here. 500: nothing
        // the administrator did was wrong, so retrying is the right advice.
        _ => Results.Json(
            new RoleErrorResponse(Msg18SaveFailed, saveFailedMessage, null),
            statusCode: StatusCodes.Status500InternalServerError),
    };
}

/// <summary>
/// UC-11's form. <paramref name="Capabilities"/> is optional — a role with none works like Member —
/// and names the capabilities by code, as the matrix does.
/// </summary>
public sealed record CreateRoleRequest(
    string? Name, string? Description, IReadOnlyCollection<string>? Capabilities);

/// <summary>UC-12's form. No capabilities: those change through <c>PUT /api/roles/capabilities</c>.</summary>
public sealed record UpdateRoleRequest(string? Name, string? Description);

/// <summary>One role as the role list shows it.</summary>
/// <param name="UserCount">Every account holding it, deactivated ones included.</param>
public sealed record RoleResponse(
    string Id, string Name, string? Description, bool IsBuiltIn, int UserCount)
{
    public static RoleResponse From(RoleSummary role) => new(
        role.Id.ToString(), role.Name, role.Description, role.IsBuiltIn, role.UserCount);
}

/// <summary>What a successful create or update returns: the code, and the role as it now stands.</summary>
public sealed record RoleMutationResponse(string Code, string Message, RoleResponse Role);

/// <summary>What a successful delete returns — there is no role left to describe.</summary>
public sealed record RoleMessageResponse(string Code, string Message);

/// <summary>A refused operation. <paramref name="Errors"/> is field name → message for MSG20.</summary>
public sealed record RoleErrorResponse(
    string Code, string Message, IReadOnlyDictionary<string, string>? Errors);

/// <summary>MSG25 — carries the count, so the dialog can say how many people to move first.</summary>
public sealed record RoleInUseResponse(string Code, string Message, int UserCount);
