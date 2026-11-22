namespace TrustCabinet.Domain.Access;

/// <summary>
/// The fixed ids of the three roles the system ships with (ADR-0008, ADR-0075).
///
/// Generated once and pasted in, never computed: the migration that introduced <c>roles</c>, the
/// reference-data seeder, the tests and every database have to agree on exactly these values.
///
/// ADR-0075: Chair and Secretary are only roles inside a meeting (<c>MeetingRole</c>), never system roles. The system
/// role "Chủ toạ" became "Phê duyệt viên" under the same id (<see cref="Approver"/>), and "Thư ký"
/// (<c>01a0ce78-7203-7c7f-8326-7a0baebf4c22</c>) was removed, its holders moved to Phê duyệt viên.
///
/// This is the only place the built-in role ids are declared. Callers use helpers here for stable
/// ordering and the default user role; authorization rules still use capabilities.
/// </summary>
public static class BuiltInRoles
{
    /// <summary>Phê duyệt viên: creates and approves meetings. The id the old system role "Chủ toạ" had.</summary>
    public static readonly Guid Approver = new("01a0ce78-7202-7945-be13-d2022cf25203");
    public static readonly Guid Member = new("01a0ce78-7204-7558-b181-e908a08e83a3");
    public static readonly Guid Admin = new("01a0ce78-7205-7486-b976-d52d428d5451");

    /// <summary>
    /// In the order every screen lists them — the order the matrix showed while roles were an
    /// enum. Custom roles follow, by creation time; see <see cref="SortKey"/>.
    /// </summary>
    private static readonly Guid[] Ordered = [Member, Approver, Admin];

    public static IReadOnlyList<Guid> All => Ordered;

    /// <summary>The built-in role selected by default when creating a user.</summary>
    public static bool IsDefault(Guid roleId) => roleId == Member;

    /// <summary>
    /// Where a role goes in a list: the built-in ones first in <see cref="All"/>'s order, then every
    /// other role. Used by both <c>GET /api/roles</c> and the matrix so the two screens never order
    /// the same roles differently.
    /// </summary>
    public static int SortKey(Guid roleId)
    {
        var index = Array.IndexOf(Ordered, roleId);
        return index < 0 ? int.MaxValue : index;
    }
}
