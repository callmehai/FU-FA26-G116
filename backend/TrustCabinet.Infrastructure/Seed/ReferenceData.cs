using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Seed;

/// <summary>
/// Data that is part of the system's definition rather than something a user enters. It ships
/// inside the migration, so every database — a teammate's laptop, CI, the demo server — starts
/// with the same answer to "what may a secretary do?".
/// </summary>
public static class ReferenceData
{
    /// <summary>
    /// Which capabilities each role carries. Mirrors ROLE_CAPABILITIES in
    /// frontend/src/types/user.ts, which is temporary scaffolding — this table is the real one,
    /// and the frontend copy goes away with SCRUM-46.
    ///
    /// Member is deliberately absent: a plain member attends, reads and votes, and none of those
    /// need a capability. Do not invent one to fill the gap.
    /// </summary>
    public static readonly RoleCapability[] RoleCapabilities =
    [
        new() { Role = SystemRole.Chair, Capability = Capability.MeetingCreator },
        new() { Role = SystemRole.Chair, Capability = Capability.MeetingApprover },
        new() { Role = SystemRole.Secretary, Capability = Capability.MeetingCreator },
        new() { Role = SystemRole.Secretary, Capability = Capability.MinutesEditor },
        new() { Role = SystemRole.Admin, Capability = Capability.AdminUsers },
    ];
}
