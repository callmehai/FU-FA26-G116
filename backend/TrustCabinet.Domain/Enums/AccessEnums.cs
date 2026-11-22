namespace TrustCabinet.Domain.Enums;

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
    // MinutesEditor was removed by ADR-0075: drafting and editing the minutes belong to the meeting's Secretary (and
    // approving them to its Chair), decided by meeting role, never by a system capability. The name is not reused.
    AdminUsers,

    /// <summary>
    /// ADR-0071, ADR-0075: may record any consenting person's Voice ID samples for them, hear back and delete every proxy
    /// sample. It reaches other people's biometric data, so an administrator grants it sparingly; by default only
    /// Quản trị viên holds it. A meeting's Secretary has no part in it.
    /// </summary>
    VoiceCollector,
}
