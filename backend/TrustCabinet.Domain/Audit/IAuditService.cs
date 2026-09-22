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

    /// <summary>What <c>AuditLogEntry.ObjectType</c> carries for all of the above.</summary>
    public const string UserObjectType = "User";
}
