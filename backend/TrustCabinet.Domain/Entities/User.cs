using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A person who can sign in, by either of the two ways the system allows: email + password, or
/// Google (ADR-0004 — no separate IdP). An account may have one or both, and must have at least
/// one, which is enforced by a check constraint rather than left to the application.
/// </summary>
public class User : Entity
{
    public required string FullName { get; set; }

    /// <summary>Login identifier. Unique, compared case-insensitively.</summary>
    public required string Email { get; set; }

    /// <summary>
    /// Hash of the password, never the password itself, and never a bare digest — the algorithm,
    /// its parameters and a per-user salt all live inside this string, which is why there is no
    /// separate salt column. Produced by ASP.NET Core's PasswordHasher in SCRUM-11.
    ///
    /// Null for an account that only ever signs in with Google: there is no password to hash, and
    /// storing a placeholder would be a credential-shaped value that some later code might try to
    /// verify against. See <see cref="GoogleSubjectId"/>.
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Google's subject identifier (the `sub` claim of a verified ID token) for a linked account,
    /// null when none is linked.
    ///
    /// The `sub` rather than the email: Google guarantees it is stable and unique for the lifetime
    /// of the account, while an email address can be changed or, on a workspace domain, reassigned
    /// to a different person. Matching on email would hand that person the previous owner's
    /// account.
    /// </summary>
    public string? GoogleSubjectId { get; set; }

    public SystemRole Role { get; set; } = SystemRole.Member;

    /// <summary>
    /// Deactivated, never deleted (SCRUM-13). Someone who left the organisation still appears as
    /// the chair of last quarter's meeting, and their approval still has to be attributable.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public ICollection<MeetingParticipant> Participations { get; set; } = [];
}
