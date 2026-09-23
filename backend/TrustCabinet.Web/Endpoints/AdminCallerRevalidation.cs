using System.Security.Claims;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// The <see cref="Capability.AdminUsers"/> gate, for the two groups that administer access itself.
///
/// Everything it used to do now lives in <see cref="CapabilityGate"/>, which does the same for any
/// capability — SCRUM-12 asks for the check on *every* business endpoint, and the meeting and
/// minutes endpoints that follow need the same two-step gate rather than a copy of it. This type
/// stays as the name the access-management endpoints read well with.
/// </summary>
public static class AdminCallerRevalidation
{
    /// <summary>
    /// The authorization policy name, registered by
    /// <see cref="CapabilityGate.AddCapabilityPolicies"/> along with every other capability's.
    /// </summary>
    public static string AdminUsersPolicy => CapabilityGate.PolicyFor(Capability.AdminUsers);

    /// <summary>
    /// Requires <see cref="Capability.AdminUsers"/>: the token policy and the database re-read
    /// together. See <see cref="CapabilityGate"/> for why one check is not enough.
    /// </summary>
    public static RouteGroupBuilder RequireAdminUsers(this RouteGroupBuilder group)
        => group.RequireCapability(Capability.AdminUsers);

    /// <inheritdoc cref="CapabilityGate.CurrentUserId"/>
    public static Guid? CurrentUserId(ClaimsPrincipal principal)
        => CapabilityGate.CurrentUserId(principal);

    /// <inheritdoc cref="CapabilityGate.GatedUserId"/>
    public static Guid RevalidatedUserId(ClaimsPrincipal principal)
        => CapabilityGate.GatedUserId(principal);
}
