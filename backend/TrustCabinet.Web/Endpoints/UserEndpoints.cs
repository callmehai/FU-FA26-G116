using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TrustCabinet.Domain.Users;
using TrustCabinet.Infrastructure.Authentication;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// UC-05 … UC-07's <c>:User Controller</c> — turns HTTP into a call on <see cref="IUserService"/>,
/// and an outcome into a status code and a message code.
///
/// It holds no rules of its own, exactly like <c>AuthEndpoints</c>: which branch applies is the
/// service's decision, and this file only decides how each one looks on the wire.
/// </summary>
public static class UserEndpoints
{
    /// <summary>
    /// Message codes from the UC-06a … UC-07 specifications. The response carries the code and a
    /// Vietnamese message: the code is what the frontend and the tester match on, so rewording the
    /// text never breaks either.
    /// </summary>
    private const string Msg07Created = "MSG07";
    private const string Msg08DuplicateEmail = "MSG08";
    private const string Msg09Invalid = "MSG09";
    private const string Msg10Updated = "MSG10";
    private const string Msg11StatusChanged = "MSG11";
    private const string Msg12StatusChangeNotAllowed = "MSG12";
    private const string Msg13PasswordResetSent = "MSG13";
    private const string Msg14PasswordResetNotAvailable = "MSG14";
    private const string Msg15PasswordResetEmailFailed = "MSG15";

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        // Every endpoint in the group requires the AdminUsers capability — the acceptance criteria
        // of SCRUM-13. Declared on the group rather than on each route so adding one cannot
        // accidentally leave it open: a new MapGet here inherits the policy.
        var users = app.MapGroup("/api/users")
            .WithTags("Users")
            // The policy only reads the token's claims, and a token stays valid until it expires —
            // so deactivating someone or taking away their capability would not stop them until
            // then (up to Authentication:AccessTokenMinutes). These endpoints administer accounts,
            // so that window is exactly where the damage would be done. RequireAdminUsers applies
            // the policy together with a re-read of the caller from the database on every request.
            .RequireAdminUsers();

        users.MapGet("/", ListAsync);
        users.MapGet("/{id:guid}", GetAsync);
        users.MapPost("/", CreateAsync);
        users.MapPut("/{id:guid}", UpdateAsync);
        // Status and the profile are separate resources: UC-06b and UC-06c are separate use cases
        // with different rules, and a single PUT would make it possible to deactivate someone as a
        // side effect of renaming them.
        users.MapPut("/{id:guid}/status", ChangeStatusAsync);
        // UC-07. POST, not PUT: each call issues a new link and sends another email, so it is
        // neither idempotent nor an update of something the caller describes.
        users.MapPost("/{id:guid}/password-reset", RequestPasswordResetAsync);

        return app;
    }

    /// <summary>
    /// The authorization policy name. Registered by <see cref="CapabilityGate"/> against
    /// <see cref="JwtTokenIssuer.CapabilityClaim"/>, so the check is on the capability the token
    /// carries rather than on a role name.
    ///
    /// Kept as an alias of <see cref="AdminCallerRevalidation.AdminUsersPolicy"/>, which is where
    /// it now lives alongside the re-read that backs it up.
    /// </summary>
    public static string AdminUsersPolicy => AdminCallerRevalidation.AdminUsersPolicy;

    /// <summary>UC-05 — the whole list, sorted, as the service returns it.</summary>
    private static async Task<IResult> ListAsync(IUserService users, CancellationToken ct)
        => Results.Ok((await users.ListAsync(ct)).Select(UserResponse.From));

    /// <summary>UC-06b's first half — the values the edit form starts from.</summary>
    private static async Task<IResult> GetAsync(Guid id, IUserService users, CancellationToken ct)
    {
        var result = await users.GetAsync(id, ct);

        return result.Succeeded
            ? Results.Ok(UserResponse.From(result.Account!))
            : Rejected(result);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateUserRequest request,
        ClaimsPrincipal principal,
        IUserService users,
        CancellationToken ct)
    {
        var result = await users.CreateAsync(
            new NewUser(request.FullName, request.Email, request.Password, ParseRoleId(request.RoleId)),
            RevalidatedUserId(principal),
            ct);

        if (!result.Succeeded)
        {
            return Rejected(result);
        }

        // 201 with a Location header: the account is a new resource, and the browser gets told
        // where it now lives.
        return Results.Created(
            $"/api/users/{result.Account!.Id}",
            new UserMutationResponse(
                Msg07Created,
                "Tạo tài khoản thành công.",
                UserResponse.From(result.Account)));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateUserRequest request,
        ClaimsPrincipal principal,
        IUserService users,
        CancellationToken ct)
    {
        var result = await users.UpdateAsync(
            id,
            new UserEdit(request.FullName, request.Email, ParseRoleId(request.RoleId)),
            RevalidatedUserId(principal),
            ct);

        return result.Succeeded
            ? Results.Ok(new UserMutationResponse(
                Msg10Updated,
                "Cập nhật tài khoản thành công.",
                UserResponse.From(result.Account!)))
            : Rejected(result);
    }

    /// <summary>
    /// UC-06c — both directions. One endpoint rather than /deactivate and /reactivate: the two
    /// branches of the diagram differ only in which value is written and whether sessions end,
    /// and both answer MSG11.
    /// </summary>
    private static async Task<IResult> ChangeStatusAsync(
        Guid id,
        [FromBody] ChangeUserStatusRequest request,
        ClaimsPrincipal principal,
        IUserService users,
        CancellationToken ct)
    {
        // An absent or misspelled field is a validation error, never a silent deactivation.
        if (request.IsActive is not { } isActive)
        {
            return Results.Json(
                new UserErrorResponse(
                    Msg09Invalid,
                    "Thông tin tài khoản không hợp lệ.",
                    new Dictionary<string, string>
                    {
                        ["isActive"] = "Thiếu trạng thái cần đặt cho tài khoản.",
                    }),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await users.ChangeStatusAsync(
            id, isActive, RevalidatedUserId(principal), ct);

        return result.Succeeded
            ? Results.Ok(new UserMutationResponse(
                Msg11StatusChanged,
                "Đổi trạng thái tài khoản thành công.",
                UserResponse.From(result.Account!)))
            : Rejected(result);
    }

    /// <summary>
    /// UC-07 — emails the account a link to set a new password. No body: the administrator only
    /// confirms, they never see or choose the password.
    /// </summary>
    private static async Task<IResult> RequestPasswordResetAsync(
        Guid id,
        ClaimsPrincipal principal,
        IUserService users,
        CancellationToken ct)
    {
        var result = await users.RequestPasswordResetAsync(id, RevalidatedUserId(principal), ct);

        return result.Succeeded
            ? Results.Ok(new UserMutationResponse(
                Msg13PasswordResetSent,
                "Đã gửi email đặt lại mật khẩu.",
                UserResponse.From(result.Account!)))
            : Rejected(result);
    }

    /// <summary>
    /// The failing branches. Each maps to the status code that describes it, and validation
    /// errors come back per field so the form can mark them — the diagram's "Display invalid
    /// fields".
    /// </summary>
    private static IResult Rejected(UserResult result) => result.Outcome switch
    {
        UserOutcome.NotFound => Results.NotFound(),

        UserOutcome.DuplicateEmail => Results.Json(
            new UserErrorResponse(Msg08DuplicateEmail, "Email đã được sử dụng.", null),
            statusCode: StatusCodes.Status409Conflict),

        UserOutcome.InvalidInformation => Results.Json(
            new UserErrorResponse(
                Msg09Invalid, "Thông tin tài khoản không hợp lệ.", result.Errors),
            statusCode: StatusCodes.Status400BadRequest),

        // 409 like MSG12: the request is fine, the account's state is what refuses it.
        UserOutcome.PasswordResetNotAvailable => Results.Json(
            new UserErrorResponse(
                Msg14PasswordResetNotAvailable,
                "Tài khoản này không dùng được chức năng đặt lại mật khẩu.",
                null),
            statusCode: StatusCodes.Status409Conflict),

        // 503: nothing is wrong with the request or the account — the mail server is unavailable,
        // and the same request may well succeed later, which is what the message tells the admin.
        UserOutcome.PasswordResetEmailFailed => Results.Json(
            new UserErrorResponse(
                Msg15PasswordResetEmailFailed,
                "Không gửi được email đặt lại mật khẩu. Vui lòng thử lại.",
                null),
            statusCode: StatusCodes.Status503ServiceUnavailable),

        // StatusChangeNotAllowed, and defensively any outcome added later without a case here.
        // 409: the request was understood and well-formed, it conflicts with the account's state.
        _ => Results.Json(
            new UserErrorResponse(
                Msg12StatusChangeNotAllowed,
                "Không thể đổi trạng thái tài khoản này.",
                null),
            statusCode: StatusCodes.Status409Conflict),
    };

    /// <summary>
    /// The caller's id, from <see cref="AdminCallerRevalidation"/> — which has already refused the
    /// request if the token carried no usable "sub". Never from the body: an actor id a caller can
    /// choose is not evidence of anything, and it is what the audit log records.
    /// </summary>
    private static Guid RevalidatedUserId(ClaimsPrincipal principal)
        => AdminCallerRevalidation.RevalidatedUserId(principal);

    /// <summary>
    /// The role id as a string, so an unreadable value is a validation error rather than a 400 from
    /// the model binder — the diagram wants "Display invalid fields", not a failed request body.
    /// Missing or unreadable becomes <see cref="Guid.Empty"/>, which no role has, so it lands on
    /// the service's "Vai trò không tồn tại".
    /// </summary>
    private static Guid ParseRoleId(string? roleId)
        => Guid.TryParse(roleId, out var parsed) ? parsed : Guid.Empty;
}

/// <summary>UC-06a's form. Nullable so a missing field is a field-level message, not a 400.</summary>
public sealed record CreateUserRequest(
    string? FullName, string? Email, string? Password, string? RoleId);

/// <summary>UC-06b's form. No password — changing one is UC-07.</summary>
public sealed record UpdateUserRequest(string? FullName, string? Email, string? RoleId);

/// <summary>
/// UC-06c's action. True reactivates, false deactivates.
///
/// Nullable, like the other two request records: a non-nullable bool binds a missing or
/// misspelled field to <c>false</c>, so <c>{}</c> — or <c>{"is_active": true}</c> from a client
/// using snake_case — would read as "deactivate", revoke the person's sessions, write
/// <c>user.deactivated</c> to the permanent audit log, and answer MSG11 "thành công". A field
/// whose default is destructive has to be stated explicitly.
/// </summary>
public sealed record ChangeUserStatusRequest(bool? IsActive);

/// <summary>
/// One account on the wire. Built from <see cref="UserAccount"/>, never from the entity — the
/// entity carries the password hash and the Google subject, and neither belongs in a response.
/// </summary>
public sealed record UserResponse(
    string Id,
    string FullName,
    string Email,
    string RoleId,
    string RoleName,
    bool IsActive,
    bool HasPassword,
    bool HasGoogleLink,
    DateTimeOffset CreatedAt)
{
    public static UserResponse From(UserAccount account) => new(
        account.Id.ToString(),
        account.FullName,
        account.Email,
        account.RoleId.ToString(),
        account.RoleName,
        account.IsActive,
        account.HasPassword,
        account.HasGoogleLink,
        account.CreatedAt);
}

/// <summary>
/// What a successful create, update or status change returns: the message code the use case
/// specifies, and the account as it now stands, so the list screen does not need a second call.
/// </summary>
public sealed record UserMutationResponse(string Code, string Message, UserResponse User);

/// <summary>
/// A refused operation. <paramref name="Errors"/> is field name → message for a validation
/// failure, and null for everything else.
/// </summary>
public sealed record UserErrorResponse(
    string Code, string Message, IReadOnlyDictionary<string, string>? Errors);
