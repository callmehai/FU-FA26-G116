using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// UC-10 and UC-11's <c>:Role Permission Controller</c> (SCRUM-60) — turns HTTP into a call on
/// <see cref="IRolePermissionService"/>, and an outcome into a status code and a message code.
///
/// It holds no rules of its own, exactly like <c>UserEndpoints</c>: which branch applies is the
/// service's decision, and this file only decides how each one looks on the wire.
/// </summary>
public static class RolePermissionEndpoints
{
    /// <summary>
    /// Message codes from the UC-11 specification. The response carries the code and a Vietnamese
    /// message: the code is what the frontend and the tester match on, so rewording the text never
    /// breaks either.
    /// </summary>
    private const string Msg16Updated = "MSG16";
    private const string Msg17Invalid = "MSG17";
    private const string Msg18SaveFailed = "MSG18";

    public static IEndpointRouteBuilder MapRolePermissionEndpoints(this IEndpointRouteBuilder app)
    {
        // Same gate as /api/users, for a stronger reason: this screen decides who is an
        // administrator at all. RequireAdminUsers applies the token policy *and* re-reads the
        // caller from the database, so someone whose role lost AdminUsers a moment ago is refused
        // on their next request rather than when their token expires.
        var roles = app.MapGroup("/api/roles")
            .WithTags("Roles")
            .RequireAdminUsers();

        roles.MapGet("/capabilities", GetMatrixAsync);
        roles.MapPut("/capabilities", UpdateAsync);

        return app;
    }

    /// <summary>UC-10 — the matrix and the capability catalogue, as the service returns them.</summary>
    private static async Task<IResult> GetMatrixAsync(
        IRolePermissionService roles, CancellationToken ct)
        => Results.Ok(MatrixResponse.From(await roles.GetMatrixAsync(ct)));

    /// <summary>
    /// UC-11 — sets exactly which capabilities one role carries.
    ///
    /// PUT of one role's row rather than of the whole matrix: the screen toggles a cell, and a
    /// whole-matrix PUT would make two administrators saving different rows overwrite each other.
    /// </summary>
    private static async Task<IResult> UpdateAsync(
        [FromBody] UpdateRolePermissionsRequest request,
        ClaimsPrincipal principal,
        IRolePermissionService roles,
        CancellationToken ct)
    {
        // An absent list is not an empty one. Read as empty, "{}" would strip the role of every
        // capability and answer MSG16 "thành công" — the destructive reading of a missing field,
        // the same trap ChangeUserStatusRequest.IsActive documents.
        if (request.Capabilities is null)
        {
            return Invalid(new Dictionary<string, string>
            {
                ["capabilities"] = "Thiếu danh sách quyền cần đặt cho vai trò.",
            });
        }

        // Unknown names are reported here rather than inside the service: once parsed they are
        // all the same out-of-range value, so the service could only say "something was wrong"
        // while the administrator needs to know which code to correct.
        var unknown = request.Capabilities
            .Where(c => !Enum.TryParse<Capability>(c, ignoreCase: true, out _))
            .ToArray();

        if (unknown.Length > 0)
        {
            return Invalid(new Dictionary<string, string>
            {
                ["capabilities"] = $"Quyền không hợp lệ: {string.Join(", ", unknown)}.",
            });
        }

        var result = await roles.UpdateAsync(
            // Unreadable or missing → Guid.Empty, which no role has, so it lands on the service's
            // "Vai trò không hợp lệ" rather than a 400 from the model binder.
            Guid.TryParse(request.RoleId, out var roleId) ? roleId : Guid.Empty,
            [.. request.Capabilities.Select(ParseCapability)],
            AdminCallerRevalidation.RevalidatedUserId(principal),
            ct);

        return result.Outcome switch
        {
            RolePermissionOutcome.Success => Results.Ok(new MatrixMutationResponse(
                Msg16Updated,
                "Cập nhật phân quyền thành công.",
                MatrixResponse.From(result.Matrix!))),

            RolePermissionOutcome.InvalidConfiguration => Invalid(result.Errors),

            // MSG18 — understood and valid, but it did not persist. 500 rather than a 4xx: nothing
            // the administrator did was wrong, so retrying the same request is the right advice.
            _ => Results.Json(
                new MatrixErrorResponse(
                    Msg18SaveFailed, "Không thể cập nhật phân quyền. Vui lòng thử lại.", null),
                statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>
    /// MSG17 — the diagram's "Show validation error". 400 with the reasons per field, so the
    /// screen can mark what was wrong instead of showing one opaque message.
    /// </summary>
    private static IResult Invalid(IReadOnlyDictionary<string, string>? errors)
        => Results.Json(
            new MatrixErrorResponse(Msg17Invalid, "Cấu hình phân quyền không hợp lệ.", errors),
            statusCode: StatusCodes.Status400BadRequest);

    /// <summary>
    /// An unknown name becomes a value no capability has, which the service rejects — the
    /// endpoint has already reported unknown names above, so this is the second lock.
    /// </summary>
    private static Capability ParseCapability(string? capability)
        => Enum.TryParse<Capability>(capability, ignoreCase: true, out var parsed)
            ? parsed
            : (Capability)(-1);
}

/// <summary>
/// UC-11's submission: one role, by id, and exactly the capabilities it should end up with.
///
/// <paramref name="RoleId"/> is a string so an unreadable id is a validation message rather than
/// a 400 from the model binder. <paramref name="Capabilities"/> is nullable so a missing list is a
/// validation message rather than a silent revocation of everything the role had.
/// </summary>
public sealed record UpdateRolePermissionsRequest(string? RoleId, IReadOnlyCollection<string>? Capabilities);

/// <summary>
/// The matrix on the wire — UC-10's answer, and what a successful UC-11 returns so the screen does
/// not need a second call.
/// </summary>
public sealed record MatrixResponse(
    IReadOnlyCollection<PermissionResponse> Capabilities,
    IReadOnlyCollection<RolePermissionsResponse> Roles)
{
    public static MatrixResponse From(RolePermissionMatrix matrix) => new(
        [.. matrix.Capabilities.Select(PermissionResponse.From)],
        [.. matrix.Roles.Select(RolePermissionsResponse.From)]);
}

/// <summary>
/// One capability, described. <paramref name="Code"/> is the enum name — the value the PUT speaks
/// and the table stores; the rest is for the administrator to read.
/// </summary>
public sealed record PermissionResponse(
    string Code, string Name, string Description, string FunctionalGroup)
{
    public static PermissionResponse From(PermissionDescriptor permission) => new(
        permission.Code, permission.Name, permission.Description, permission.FunctionalGroup);
}

/// <summary>One row of the grid.</summary>
public sealed record RolePermissionsResponse(
    string Id,
    string Name,
    string? Description,
    bool IsBuiltIn,
    IReadOnlyCollection<string> Capabilities)
{
    public static RolePermissionsResponse From(RolePermissions role) => new(
        role.Id.ToString(),
        role.Name,
        role.Description,
        role.IsBuiltIn,
        [.. role.Capabilities.Select(c => c.ToString())]);
}

/// <summary>What a successful UC-11 returns: the message code, and the matrix as it now stands.</summary>
public sealed record MatrixMutationResponse(string Code, string Message, MatrixResponse Matrix);

/// <summary>
/// A refused configuration. <paramref name="Errors"/> is field name → message for MSG17, and null
/// for MSG18, where nothing about the request was wrong.
/// </summary>
public sealed record MatrixErrorResponse(
    string Code, string Message, IReadOnlyDictionary<string, string>? Errors);
