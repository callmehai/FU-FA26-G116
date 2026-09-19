using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A person who can sign in. Authentication is local (email + password), so this table holds the
/// password hash — see <see cref="PasswordHash"/> for what may and may not go in it.
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
    /// </summary>
    public required string PasswordHash { get; set; }

    public SystemRole Role { get; set; } = SystemRole.Member;

    public Guid? OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }

    /// <summary>
    /// Deactivated, never deleted (SCRUM-13). Someone who left the organisation still appears as
    /// the chair of last quarter's meeting, and their approval still has to be attributable.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public ICollection<MeetingParticipant> Participations { get; set; } = [];
}
