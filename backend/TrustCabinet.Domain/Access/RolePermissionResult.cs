namespace TrustCabinet.Domain.Access;

/// <summary>
/// What <see cref="IRolePermissionService"/> hands back to the endpoint: which branch happened,
/// and the matrix as it now stands when the branch produced one.
///
/// Mirrors <c>UserResult</c> — on failure <see cref="Matrix"/> is null, so an endpoint cannot
/// serialise a matrix for an operation that was refused.
/// </summary>
/// <param name="Errors">
/// Field name → why it is invalid, for <see cref="RolePermissionOutcome.InvalidConfiguration"/>.
/// The diagram's "Show validation error" needs to say *what* was wrong, and "the matrix is
/// invalid" is not something an administrator can act on.
/// </param>
public sealed record RolePermissionResult(
    RolePermissionOutcome Outcome,
    RolePermissionMatrix? Matrix = null,
    IReadOnlyDictionary<string, string>? Errors = null)
{
    public bool Succeeded => Outcome == RolePermissionOutcome.Success;

    public static RolePermissionResult Success(RolePermissionMatrix matrix)
        => new(RolePermissionOutcome.Success, matrix);

    public static RolePermissionResult Failure(RolePermissionOutcome outcome) => new(outcome);

    public static RolePermissionResult Invalid(IReadOnlyDictionary<string, string> errors)
        => new(RolePermissionOutcome.InvalidConfiguration, Errors: errors);
}
