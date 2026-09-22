using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Authentication;

/// <summary>
/// The <c>:User Repository</c> participant of UC-01 — the only way the authentication service
/// reaches the database.
///
/// It exists because the service's four branches are the part worth unit-testing, and they should
/// be testable without a PostgreSQL instance. Keep it this narrow: it is not a general-purpose
/// user repository, and a screen that needs to list users gets its own query.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// The account with this email, or null. The email column is <c>citext</c>, so matching is
    /// case-insensitive in the database and the caller does not normalise case.
    /// </summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// The account with this id, or null. Used when refreshing, where the caller has a user id
    /// from the token store rather than an email.
    /// </summary>
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The account linked to this Google subject, or null when none is. UC-02's
    /// "Find internal account by Google account".
    ///
    /// Matched on the subject, never on the email the token also carries — see
    /// <see cref="User.GoogleSubjectId"/> for why that distinction is a security boundary.
    ///
    /// <para>
    /// Takes a nullable string because a null is exactly what this has to survive. It reads the
    /// <c>sub</c> claim of a token that some caller may not have validated, and a missing claim
    /// arrives here as null — which EF would translate into <c>IS NULL</c>, matching every
    /// password-only account rather than none. Declaring the parameter non-nullable would not stop
    /// that value arriving; it would only stop the compiler discussing it. Implementations answer
    /// null for anything blank.
    /// </para>
    /// </summary>
    Task<User?> FindByGoogleSubjectIdAsync(string? googleSubjectId, CancellationToken ct = default);

    /// <summary>
    /// What this role may do, read from <c>role_capabilities</c> rather than hard-coded, so the
    /// administrator screen in SCRUM-60 can change it without a deployment.
    /// Empty is a legitimate answer — a plain Member has no capability by design.
    /// </summary>
    Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(SystemRole role, CancellationToken ct = default);
}
