namespace TrustCabinet.Domain.Access;

/// <summary>
/// The <c>:Role Service</c> participant of UC-11, UC-12 and UC-13 (SCRUM-63) — create, rename and
/// delete roles. Owns every rule about them: what a valid name is, that it is unique, that the
/// built-in roles stay as they are, that a role somebody holds is not deleted, and what the audit
/// log records.
///
/// Capabilities are not edited here, except the initial set on create: <c>IRolePermissionService</c>
/// stays the one path that changes an existing role's capabilities, so the lock-out rule is
/// guarded in one place.
/// </summary>
public interface IRoleService
{
    /// <summary>Every role in display order, with how many accounts hold each.</summary>
    Task<IReadOnlyCollection<RoleSummary>> ListAsync(CancellationToken ct = default);

    /// <summary>UC-11 — MSG19 on success; MSG20 invalid; MSG21 name taken.</summary>
    Task<RoleResult> CreateAsync(NewRole request, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// UC-12 — name and description only. MSG22 on success, including when nothing changed (then
    /// nothing is written or audited); MSG24 for a built-in role; MSG20 invalid; MSG21 name taken.
    /// </summary>
    Task<RoleResult> UpdateAsync(
        Guid id, RoleEdit request, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// UC-13 — MSG23 on success; MSG24 for a built-in role; MSG25 while any account holds it,
    /// deactivated ones included.
    /// </summary>
    Task<RoleResult> DeleteAsync(Guid id, Guid actorId, CancellationToken ct = default);
}

/// <summary>
/// What UC-11 collects. Capabilities arrive as the names the client sent, so the service can say
/// which one it does not know rather than "something was wrong". Null means none.
/// </summary>
public sealed record NewRole(string? Name, string? Description, IReadOnlyCollection<string>? Capabilities);

/// <summary>What UC-12 collects. No capabilities: those change through the matrix (UC-10).</summary>
public sealed record RoleEdit(string? Name, string? Description);

/// <summary>One role as the role list shows it.</summary>
/// <param name="UserCount">
/// Every account holding the role, deactivated ones included — exactly the number that blocks
/// deleting it.
/// </param>
/// <param name="IsDefault">Whether this role is selected by default when creating a user.</param>
public sealed record RoleSummary(
    Guid Id, string Name, string? Description, bool IsBuiltIn, bool IsDefault, int UserCount);

/// <summary>
/// Which alt branch of UC-11 … UC-13 happened. The endpoint maps each to a message code and a
/// status code; the service only decides which one.
/// </summary>
public enum RoleOutcome
{
    /// <summary>MSG20 — name missing or too long, description too long, unknown capability.</summary>
    InvalidInformation,

    /// <summary>MSG21 — another role already uses the name, compared without regard to case.</summary>
    DuplicateName,

    /// <summary>No role with that id — a stale list, or another administrator deleted it.</summary>
    NotFound,

    /// <summary>MSG24 — one of the four built-in roles, which are never renamed or deleted.</summary>
    BuiltInRole,

    /// <summary>MSG25 — accounts still hold the role. Carries how many.</summary>
    InUse,

    /// <summary>
    /// The diagrams' "Role cannot be saved / deleted": the database refused for a reason that is
    /// not the request's fault, so retrying is the right advice.
    /// </summary>
    SaveFailed,

    Success,
}

/// <summary>
/// What <see cref="IRoleService"/> hands back. Mirrors <c>UserResult</c>: on failure
/// <see cref="Role"/> is null, so an endpoint cannot serialise a role for a refused operation.
/// </summary>
/// <param name="Errors">Field name → message, for <see cref="RoleOutcome.InvalidInformation"/>.</param>
/// <param name="UserCount">How many accounts hold the role, for <see cref="RoleOutcome.InUse"/>.</param>
public sealed record RoleResult(
    RoleOutcome Outcome,
    RoleSummary? Role = null,
    IReadOnlyDictionary<string, string>? Errors = null,
    int UserCount = 0)
{
    public bool Succeeded => Outcome == RoleOutcome.Success;

    public static RoleResult Success(RoleSummary? role = null) => new(RoleOutcome.Success, role);

    public static RoleResult Failure(RoleOutcome outcome) => new(outcome);

    public static RoleResult Invalid(IReadOnlyDictionary<string, string> errors)
        => new(RoleOutcome.InvalidInformation, Errors: errors);

    public static RoleResult InUse(int userCount) => new(RoleOutcome.InUse, UserCount: userCount);
}
