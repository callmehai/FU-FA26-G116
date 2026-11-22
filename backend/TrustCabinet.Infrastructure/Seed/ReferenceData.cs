using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Seed;

/// <summary>
/// Data that is part of the system's definition rather than something a user enters. Applied by
/// <see cref="ReferenceDataSeeder"/> when a database has none, so every database — a teammate's
/// laptop, CI, the demo server — starts with the same answer to "what may a member do?".
/// </summary>
public static class ReferenceData
{
    /// <summary>
    /// The three built-in roles (ADR-0008, ADR-0075), with the fixed ids from <see cref="BuiltInRoles"/>.
    ///
    /// The migration that introduced <c>roles</c> inserted four rows; <c>SystemRolesWithoutChairSecretary</c> turned
    /// them into these three, with the same text as here. This copy is what <see cref="ReferenceDataSeeder"/> inserts
    /// when one is missing. A factory
    /// rather than an array so each caller gets fresh, untracked entities.
    /// </summary>
    public static Role[] Roles() =>
    [
        BuiltIn(BuiltInRoles.Member, MemberName, MemberDescription),
        BuiltIn(BuiltInRoles.Approver, ApproverName, ApproverDescription),
        BuiltIn(BuiltInRoles.Admin, "Quản trị viên", AdminDescription),
    ];

    // Shared with the migration's SQL test, so the seeded and the migrated text cannot drift apart.
    public const string MemberName = "Thành viên";
    public const string MemberDescription = "Tạo cuộc họp, dự họp, đọc tài liệu và biểu quyết.";
    public const string ApproverName = "Phê duyệt viên";
    public const string ApproverDescription = "Tạo cuộc họp và phê duyệt cuộc họp.";
    public const string AdminDescription = "Tạo cuộc họp, thu giọng mẫu hộ, quản lý tài khoản người dùng và phân quyền.";

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
    /// ADR-0075 (Lộc 21/11/2026, agreed by the team): Thành viên creates meetings; Phê duyệt viên creates and approves
    /// them; Quản trị viên creates meetings, collects proxy voice samples and administers users. Attending, reading,
    /// voting, chairing and taking the minutes need no capability: they follow the person's role in that meeting.
    /// </summary>
    public static readonly RoleCapability[] RoleCapabilities =
    [
        new() { RoleId = BuiltInRoles.Member, Capability = Capability.MeetingCreator },
        new() { RoleId = BuiltInRoles.Approver, Capability = Capability.MeetingCreator },
        new() { RoleId = BuiltInRoles.Approver, Capability = Capability.MeetingApprover },
        new() { RoleId = BuiltInRoles.Admin, Capability = Capability.MeetingCreator },
        new() { RoleId = BuiltInRoles.Admin, Capability = Capability.VoiceCollector },
        new() { RoleId = BuiltInRoles.Admin, Capability = Capability.AdminUsers },
    ];
}
