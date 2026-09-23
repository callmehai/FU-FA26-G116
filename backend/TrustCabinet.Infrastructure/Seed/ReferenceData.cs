using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Seed;

/// <summary>
/// Data that is part of the system's definition rather than something a user enters. Applied by
/// <see cref="ReferenceDataSeeder"/> when a database has none, so every database — a teammate's
/// laptop, CI, the demo server — starts with the same answer to "what may a secretary do?".
/// </summary>
public static class ReferenceData
{
    /// <summary>
    /// The four built-in roles (ADR-0008), with the fixed ids from <see cref="BuiltInRoles"/>.
    ///
    /// The migration that introduced <c>roles</c> inserts the same four rows with the same text;
    /// this copy is what <see cref="ReferenceDataSeeder"/> inserts when one is missing. A factory
    /// rather than an array so each caller gets fresh, untracked entities.
    /// </summary>
    public static Role[] Roles() =>
    [
        BuiltIn(BuiltInRoles.Chair, "Chủ toạ", "Chủ trì và phê duyệt cuộc họp."),
        BuiltIn(BuiltInRoles.Secretary, "Thư ký", "Tạo cuộc họp và soạn biên bản."),
        BuiltIn(BuiltInRoles.Member, "Thành viên", "Dự họp, đọc tài liệu và biểu quyết."),
        BuiltIn(BuiltInRoles.Admin, "Quản trị viên", "Quản lý tài khoản người dùng và phân quyền."),
    ];

    private static Role BuiltIn(Guid id, string name, string description) => new()
    {
        Id = id,
        Name = name,
        Description = description,
        IsBuiltIn = true,
    };

    /// <summary>
    /// The capabilities each role carries **on a fresh database**. Since SCRUM-60 an
    /// administrator can change the matrix at runtime, so this array is the starting point, not
    /// the running system's answer — read <c>role_capabilities</c> for that.
    ///
    /// Mirrors ROLE_CAPABILITIES in frontend/src/types/user.ts, which is temporary scaffolding —
    /// this table is the real one, and the frontend copy goes away with SCRUM-46.
    ///
    /// Member is deliberately absent: a plain member attends, reads and votes, and none of those
    /// need a capability. Do not invent one to fill the gap.
    /// </summary>
    public static readonly RoleCapability[] RoleCapabilities =
    [
        new() { RoleId = BuiltInRoles.Chair, Capability = Capability.MeetingCreator },
        new() { RoleId = BuiltInRoles.Chair, Capability = Capability.MeetingApprover },
        new() { RoleId = BuiltInRoles.Secretary, Capability = Capability.MeetingCreator },
        new() { RoleId = BuiltInRoles.Secretary, Capability = Capability.MinutesEditor },
        new() { RoleId = BuiltInRoles.Admin, Capability = Capability.AdminUsers },
    ];
}
