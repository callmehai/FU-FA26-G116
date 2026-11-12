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
    MinutesEditor,
    AdminUsers,

    /// <summary>
    /// ADR-0060: may record Voice ID samples for the people of a meeting it is the Secretary of, on the room's mic, and
    /// hear back and delete the ones taken there. Held apart from the Secretary's meeting role because it reaches
    /// other people's biometric data: an administrator grants it only to Secretaries the organisation trusts with that.
    /// </summary>
    VoiceCollector,
}
