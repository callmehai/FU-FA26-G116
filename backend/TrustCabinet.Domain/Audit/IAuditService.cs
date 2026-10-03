namespace TrustCabinet.Domain.Audit;

/// <summary>
/// The <c>:Audit Service</c> participant of UC-05, UC-06 and UC-07 — writes the business evidence
/// that something was done and by whom.
///
/// Not application logging. <c>AuditLogEntry</c> says it: this is append-only evidence, kept
/// because "who deactivated this person, and when?" has to be answerable months later, while
/// technical logs get rotated away and nobody treats them as authoritative.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Records one action. <paramref name="details"/> is free-form JSON-serialisable data kept
    /// alongside the row — what changed, typically.
    ///
    /// Never put a password, a hash or a token in it: the audit log is read by more people than
    /// the users table, and it is deliberately never deleted.
    /// </summary>
    Task RecordAsync(
        string action,
        string objectType,
        Guid? objectId,
        Guid? actorId,
        object? details = null,
        CancellationToken ct = default);
}

/// <summary>
/// The action codes this story writes. Constants rather than literals scattered across the
/// service, because a typo in one of them produces a row that no later query finds.
/// </summary>
public static class AuditActions
{
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserDeactivated = "user.deactivated";
    public const string UserReactivated = "user.reactivated";

    /// <summary>
    /// A Google identity was linked to an account at its first Google sign-in (ADR-0009). The
    /// actor is the account itself: nobody else was involved.
    /// </summary>
    public const string UserGoogleLinked = "user.google_linked";

    /// <summary>UC-08 — an administrator had a reset link emailed to the account.</summary>
    public const string UserPasswordResetRequested = "user.password_reset_requested";

    /// <summary>
    /// UC-08's link was used and the password changed. The actor is the account itself — the
    /// administrator only sent the link, they never learn or choose the password.
    /// </summary>
    public const string UserPasswordReset = "user.password_reset";

    /// <summary>What <c>AuditLogEntry.ObjectType</c> carries for all of the above.</summary>
    public const string UserObjectType = "User";

    /// <summary>
    /// UC-10 — an administrator changed what a role may do (SCRUM-60). <c>AuditLogEntry</c> names
    /// "changing a person's rights" as something that has to be recorded, and this changes it for
    /// everyone holding the role at once.
    /// </summary>
    public const string RolePermissionsUpdated = "role.permissions.updated";

    /// <summary>UC-11 — an administrator created a role (SCRUM-63).</summary>
    public const string RoleCreated = "role.created";

    /// <summary>UC-12 — an administrator renamed a role or changed its description.</summary>
    public const string RoleUpdated = "role.updated";

    /// <summary>
    /// UC-13 — an administrator deleted a role. Its name and capabilities travel in the details,
    /// because the cascade has removed both from the database by the time anyone reads this row.
    /// </summary>
    public const string RoleDeleted = "role.deleted";

    /// <summary>
    /// What <c>AuditLogEntry.ObjectType</c> carries for every role action above. The row's
    /// <c>ObjectId</c> is the role's id — roles are rows since ADR-0008.
    /// </summary>
    public const string RoleObjectType = "Role";
}
