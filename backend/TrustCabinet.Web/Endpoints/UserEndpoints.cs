using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Users;
using TrustCabinet.Infrastructure.Authentication;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// UC-04 … UC-07's <c>:User Controller</c> — turns HTTP into a call on <see cref="IUserService"/>,
/// and an outcome into a status code and a message code.
///
/// It holds no rules of its own, exactly like <c>AuthEndpoints</c>: which branch applies is the
/// service's decision, and this file only decides how each one looks on the wire.
/// </summary>
public static class UserEndpoints
{
    /// <summary>
    /// Message codes from the UC-05 … UC-07 specifications. The response carries the code and a
    /// Vietnamese message: the code is what the frontend and the tester match on, so rewording the
    /// text never breaks either.
    /// </summary>
    private const string Msg07Created = "MSG07";
    private const string Msg08DuplicateEmail = "MSG08";
    private const string Msg10Updated = "MSG10";
    private const string Msg11StatusChanged = "MSG11";

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        // Every endpoint in the group requires the AdminUsers capability — the acceptance criteria
        // of SCRUM-13. Declared on the group rather than on each route so adding one cannot
        // accidentally leave it open: a new MapGet here inherits the policy.
        var users = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization(AdminUsersPolicy)
            // The policy above only reads the token's claims, and a token stays valid until it
            // expires — so deactivating someone or taking away their capability would not stop
            // them until then (up to Authentication:AccessTokenMinutes). These endpoints
            // administer accounts, so that window is exactly where the damage would be done:
            // re-read the caller from the database on every request instead.
            .AddEndpointFilter(RevalidateCallerAsync);

        users.MapGet("/", ListAsync);
        users.MapGet("/{id:guid}", GetAsync);
        users.MapPost("/", CreateAsync);
        users.MapPut("/{id:guid}", UpdateAsync);
        // Status and the profile are separate resources: UC-06 and UC-07 are separate use cases
        // with different rules, and a single PUT would make it possible to deactivate someone as a
        // side effect of renaming them.
        users.MapPut("/{id:guid}/status", ChangeStatusAsync);

        return app;
    }

    /// <summary>
    /// Re-reads the caller's own account before any user-management request runs, and refuses
    /// unless they are still active and still hold <see cref="Capability.AdminUsers"/>.
    ///
    /// The bearer token is a snapshot of who the caller was when it was issued. Revoking their
    /// refresh tokens does not touch the access token they already hold, so without this an
    /// administrator who has just been deactivated keeps full power over every account until
    /// their token expires — long enough to deactivate whoever deactivated them, or to mint
    /// themselves a fresh administrator account.
    ///
    /// 403 rather than 401: the token is genuine and unexpired, it is the account behind it that
    /// may no longer do this.
    /// </summary>
    private static async ValueTask<object?> RevalidateCallerAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var callerId = CurrentUserId(context.HttpContext.User);

        // No usable "sub" claim. Refused rather than defaulted: an actor id of Guid.Empty would
        // match no account, silently disabling the rules that compare the caller against the
        // account being changed, and would be written into the audit log as the actor.
        if (callerId is null)
        {
            return Results.Forbid();
        }

        var users = context.HttpContext.RequestServices.GetRequiredService<IUserAccountRepository>();
        var caller = await users.FindByIdAsync(callerId.Value, context.HttpContext.RequestAborted);

        if (caller is null || !caller.IsActive)
        {
            return Results.Forbid();
        }

        var capabilities = await users.GetCapabilitiesAsync(
            caller.Role, context.HttpContext.RequestAborted);

        // Checked against the capability, not the role name — the same rule the policy follows,
        // except read from the database rather than from the token.
        return capabilities.Contains(Capability.AdminUsers)
            ? await next(context)
            : Results.Forbid();
    }

    /// <summary>
    /// The authorization policy name. Defined in Program.cs against
    /// <see cref="JwtTokenIssuer.CapabilityClaim"/>, so the check is on the capability the token
    /// carries rather than on a role name.
    /// </summary>
    public const string AdminUsersPolicy = "AdminUsers";

    /// <summary>UC-04 — the whole list, sorted, as the service returns it.</summary>
    private static async Task<IResult> ListAsync(IUserService users, CancellationToken ct)
        => Results.Ok((await users.ListAsync(ct)).Select(UserResponse.From));

    /// <summary>UC-06's first half — the values the edit form starts from.</summary>
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
            new NewUser(request.FullName, request.Email, request.Password, ParseRole(request.Role)),
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
            new UserEdit(request.FullName, request.Email, ParseRole(request.Role)),
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
    /// UC-07 — both directions. One endpoint rather than /deactivate and /reactivate: the two
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
                    "MSG09",
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
                "MSG09", "Thông tin tài khoản không hợp lệ.", result.Errors),
            statusCode: StatusCodes.Status400BadRequest),

        // StatusChangeNotAllowed, and defensively any outcome added later without a case here.
        // 409: the request was understood and well-formed, it conflicts with the account's state.
        _ => Results.Json(
            new UserErrorResponse(
                "MSG12",
                "Không thể đổi trạng thái tài khoản này.",
                null),
            statusCode: StatusCodes.Status409Conflict),
    };

    /// <summary>
    /// Who is making the request, from the validated token, or null when the token carries no
    /// usable "sub". Never from the body: an actor id a caller can choose is not evidence of
    /// anything, and it is what the audit log records.
    ///
    /// Nullable rather than falling back to <see cref="Guid.Empty"/>: an empty actor matches no
    /// account, so it would quietly switch off every rule that compares the caller against the
    /// account being changed. <see cref="RevalidateCallerAsync"/> refuses the request instead,
    /// which is why the handlers below can use the value without re-checking.
    /// </summary>
    private static Guid? CurrentUserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue("sub"), out var id) ? id : null;

    /// <summary>
    /// The caller's id for a handler that only runs behind <see cref="RevalidateCallerAsync"/>,
    /// which has already refused the request if there was no usable one. Throws rather than
    /// substituting a placeholder, so removing that filter fails loudly instead of quietly
    /// writing an actor nobody can trace.
    /// </summary>
    private static Guid RevalidatedUserId(ClaimsPrincipal principal)
        => CurrentUserId(principal)
            ?? throw new InvalidOperationException(
                "No caller id on a revalidated request — RevalidateCallerAsync should have "
                + "refused it. Did the endpoint filter get removed from the group?");

    /// <summary>
    /// The role as a string, so an unknown value is a validation error rather than a 400 from the
    /// model binder — the diagram wants "Display invalid fields", not a failed request body.
    /// Case-insensitive because JSON casing conventions differ between the two ends.
    /// </summary>
    private static SystemRole ParseRole(string? role)
        => Enum.TryParse<SystemRole>(role, ignoreCase: true, out var parsed)
            ? parsed
            // Cast to an undefined value on purpose: UserService.Validate rejects anything
            // Enum.IsDefined does not know, so this lands on "Vai trò không hợp lệ".
            : (SystemRole)(-1);
}

/// <summary>UC-05's form. Nullable so a missing field is a field-level message, not a 400.</summary>
public sealed record CreateUserRequest(
    string? FullName, string? Email, string? Password, string? Role);

/// <summary>UC-06's form. No password — changing one is UC-08.</summary>
public sealed record UpdateUserRequest(string? FullName, string? Email, string? Role);

/// <summary>
/// UC-07's action. True reactivates, false deactivates.
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
    string Role,
    bool IsActive,
    bool HasPassword,
    bool HasGoogleLink,
    DateTimeOffset CreatedAt)
{
    public static UserResponse From(UserAccount account) => new(
        account.Id.ToString(),
        account.FullName,
        account.Email,
        account.Role.ToString(),
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
