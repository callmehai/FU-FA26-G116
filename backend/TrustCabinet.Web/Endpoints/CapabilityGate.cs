using System.Security.Claims;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Users;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// The server-side capability gate — SCRUM-12. Every business endpoint goes through here, whatever
/// the interface chose to render.
///
/// Two checks, deliberately, because one is not enough:
///
/// <list type="number">
/// <item>the authorization policy, on the capability claim the bearer token carries;</item>
/// <item>a re-read of the caller's own account, which is the authoritative one.</item>
/// </list>
///
/// The token is a snapshot of who the caller was when it was issued, and revoking their refresh
/// tokens does not touch an access token already in their hands. Without the second check, someone
/// deactivated — or whose role has just lost the capability, which SCRUM-60 makes an everyday
/// administrative action — keeps their rights until the token expires. On the access-management
/// endpoints that window is exactly where the damage would be done: long enough to deactivate
/// whoever deactivated them, or to grant their own role its rights back.
///
/// Registration and enforcement are the same call: <see cref="AddCapabilityPolicies"/> declares one
/// policy per <see cref="Capability"/>, so <see cref="RequireCapability"/> can never name a policy
/// that was not registered.
/// </summary>
public static class CapabilityGate
{
    /// <summary>
    /// The policy name for a capability. One per enum member, so a capability added to
    /// <see cref="Capability"/> is gateable without touching this file.
    /// </summary>
    public static string PolicyFor(Capability capability) => $"cap:{capability}";

    /// <summary>
    /// Declares a policy for every capability, each requiring that capability's claim.
    ///
    /// Generated from the enum rather than written out one by one: a capability added to
    /// <see cref="Capability"/> but forgotten here would make
    /// <see cref="RequireCapability"/> throw at startup for a policy that does not exist, and an
    /// endpoint that cannot be gated is an endpoint that ships ungated.
    ///
    /// The check is on the capability claim, never on the role — a capability moved to another
    /// role in <c>role_capabilities</c> then takes effect without a code change, and a role an
    /// administrator creates needs no code at all (ADR-0008).
    /// </summary>
    public static IServiceCollection AddCapabilityPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            foreach (var capability in Enum.GetValues<Capability>())
            {
                options.AddPolicy(
                    PolicyFor(capability),
                    policy => policy.RequireClaim(
                        Infrastructure.Authentication.JwtTokenIssuer.CapabilityClaim,
                        capability.ToString()));
            }
        });

        return services;
    }

    /// <summary>
    /// Requires <paramref name="capability"/> for every endpoint in the group: the token policy and
    /// the database re-read together, so no endpoint can be added with only one of the two.
    ///
    /// Applied to the group rather than to each route on purpose — a new <c>MapGet</c> inside a
    /// gated group inherits the gate, where a per-route list would silently leave the one somebody
    /// forgot wide open.
    /// </summary>
    public static RouteGroupBuilder RequireCapability(
        this RouteGroupBuilder group, Capability capability)
        => group
            .RequireAuthorization(PolicyFor(capability))
            .AddEndpointFilter(async (context, next) =>
                await StillHeldAsync(context, capability)
                    ? await next(context)
                    : Results.Forbid());

    /// <summary>
    /// Requires an authenticated caller whose account is still active according to the database.
    /// Use for resource endpoints whose access is authorized by a relationship rather than one
    /// system capability.
    /// </summary>
    public static RouteGroupBuilder RequireActiveUser(this RouteGroupBuilder group)
        => group
            .RequireAuthorization()
            .AddEndpointFilter(async (context, next) =>
                await IsActiveUserAsync(context)
                    ? await next(context)
                    : Results.Forbid());

    /// <summary>
    /// Whether the caller still holds <paramref name="capability"/> according to the database, as
    /// opposed to according to the token they presented.
    /// </summary>
    private static async ValueTask<bool> StillHeldAsync(
        EndpointFilterInvocationContext context, Capability capability)
    {
        var callerId = CurrentUserId(context.HttpContext.User);

        // No usable "sub" claim. Refused rather than defaulted: an actor id of Guid.Empty would
        // match no account, silently disabling the rules that compare the caller against what they
        // are changing, and would be written into the audit log as the actor.
        if (callerId is null)
        {
            return false;
        }

        var ct = context.HttpContext.RequestAborted;
        var users = context.HttpContext.RequestServices.GetRequiredService<IUserAccountRepository>();
        var caller = await users.FindByIdAsync(callerId.Value, ct);

        if (caller is null || !caller.IsActive)
        {
            return false;
        }

        return (await users.GetCapabilitiesAsync(caller.RoleId, ct)).Contains(capability);
    }

    private static async ValueTask<bool> IsActiveUserAsync(EndpointFilterInvocationContext context)
    {
        var callerId = CurrentUserId(context.HttpContext.User);
        if (callerId is null)
        {
            return false;
        }

        var users = context.HttpContext.RequestServices.GetRequiredService<IUserAccountRepository>();
        var caller = await users.FindByIdAsync(callerId.Value, context.HttpContext.RequestAborted);
        return caller is not null && caller.IsActive;
    }

    /// <summary>
    /// Who is making the request, from the validated token, or null when the token carries no
    /// usable "sub". Never from the body: an actor id a caller can choose is not evidence of
    /// anything, and it is what the audit log records.
    /// </summary>
    public static Guid? CurrentUserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue("sub"), out var id) ? id : null;

    /// <summary>
    /// The caller's id for a handler that only runs behind the gate, which has already refused the
    /// request if there was no usable one. Throws rather than substituting a placeholder, so
    /// removing the gate fails loudly instead of quietly writing an actor nobody can trace.
    /// </summary>
    public static Guid GatedUserId(ClaimsPrincipal principal)
        => CurrentUserId(principal)
            ?? throw new InvalidOperationException(
                "No caller id on a gated request — RequireCapability should have refused it. "
                + "Did the endpoint filter get removed from the group?");
}
