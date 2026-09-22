using System.Security.Claims;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Users;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// Re-reads the caller's own account before an administration request runs, and refuses unless
/// they are still active and still hold <see cref="Capability.AdminUsers"/>.
///
/// The bearer token is a snapshot of who the caller was when it was issued. Revoking their
/// refresh tokens does not touch the access token they already hold, so without this an
/// administrator who has just been deactivated — or whose role has just lost the capability —
/// keeps full power until that token expires. Long enough to deactivate whoever deactivated them,
/// or to grant their own role its rights back.
///
/// Shared by <c>UserEndpoints</c> and <c>RolePermissionEndpoints</c>: both administer access
/// itself, and the window this closes is precisely where the damage would be done. SCRUM-60 makes
/// it sharper still — an administrator can now take AdminUsers away from a role, and the people
/// holding that role must stop being administrators at their next request rather than at their
/// token's expiry.
/// </summary>
public static class AdminCallerRevalidation
{
    /// <summary>
    /// The authorization policy name. Defined in Program.cs against
    /// <c>JwtTokenIssuer.CapabilityClaim</c>, so the first check is on the capability the token
    /// carries rather than on a role name. This filter is the second, authoritative one.
    /// </summary>
    public const string AdminUsersPolicy = "AdminUsers";

    /// <summary>
    /// Applies the policy and the re-read together, so no administration endpoint can be added
    /// with only one of the two.
    /// </summary>
    public static RouteGroupBuilder RequireAdminUsers(this RouteGroupBuilder group)
        => group
            .RequireAuthorization(AdminUsersPolicy)
            .AddEndpointFilter(RevalidateAsync);

    /// <summary>
    /// 403 rather than 401: the token is genuine and unexpired, it is the account behind it that
    /// may no longer do this.
    /// </summary>
    public static async ValueTask<object?> RevalidateAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var callerId = CurrentUserId(context.HttpContext.User);

        // No usable "sub" claim. Refused rather than defaulted: an actor id of Guid.Empty would
        // match no account, silently disabling the rules that compare the caller against what
        // they are changing, and would be written into the audit log as the actor.
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
    /// Who is making the request, from the validated token, or null when the token carries no
    /// usable "sub". Never from the body: an actor id a caller can choose is not evidence of
    /// anything, and it is what the audit log records.
    /// </summary>
    public static Guid? CurrentUserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue("sub"), out var id) ? id : null;

    /// <summary>
    /// The caller's id for a handler that only runs behind <see cref="RevalidateAsync"/>, which
    /// has already refused the request if there was no usable one. Throws rather than substituting
    /// a placeholder, so removing that filter fails loudly instead of quietly writing an actor
    /// nobody can trace.
    /// </summary>
    public static Guid RevalidatedUserId(ClaimsPrincipal principal)
        => CurrentUserId(principal)
            ?? throw new InvalidOperationException(
                "No caller id on a revalidated request — RevalidateAsync should have refused it. "
                + "Did the endpoint filter get removed from the group?");
}
