namespace TrustCabinet.Domain.Access;

/// <summary>
/// Why a UC-10 configuration attempt ended the way it did — the alt branches of its sequence
/// diagram and nothing else. The endpoint maps each to a message code and a status code; the
/// service only decides which one happened.
///
/// Mirrors <c>UserOutcome</c>: same shape, same split of responsibility.
/// </summary>
public enum RolePermissionOutcome
{
    /// <summary>
    /// MSG17 — the diagram's "Invalid permission configuration". An unknown role, an unknown
    /// capability, or a configuration that would leave nobody able to administer.
    /// Carries the reasons.
    /// </summary>
    InvalidConfiguration,

    /// <summary>
    /// MSG18 — the diagram's "Permission configuration cannot be saved". The database refused the
    /// write. Distinct from <see cref="InvalidConfiguration"/>: nothing was wrong with what was
    /// asked, it just did not persist, so the administrator should retry rather than change it.
    /// </summary>
    SaveFailed,

    /// <summary>MSG16 — saved.</summary>
    Success,
}
