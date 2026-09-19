namespace TrustCabinet.Domain.Enums;

/// <summary>
/// A user's standing role in the organisation. One per user; what it allows is expressed as
/// capabilities, never checked by comparing against the role name in business code.
/// </summary>
public enum SystemRole
{
    Chair,
    Secretary,
    Member,
    Admin,
}

/// <summary>
/// What a user may do. Capabilities are deliberately coarse and named after actions rather than
/// job titles, so a role change never means editing every endpoint.
///
/// This enum is the single source of truth. The browser copy in frontend/src/types/user.ts is
/// scaffolding that disappears with SCRUM-46 — and hiding a button was never access control
/// anyway: every endpoint re-checks the capability server-side.
/// </summary>
public enum Capability
{
    MeetingCreator,
    MeetingApprover,
    MinutesEditor,
    AdminUsers,
}
