using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.GuestAccess;

/// <summary>
/// The one meeting-status rule for guests, used by every guest path (agenda, roster, attendance, votes, documents,
/// realtime) and by the UC-04 session queries. A guest sees the meeting only while it is Published or InProgress and
/// loses everything once it has Ended (PO 30/10/2026), unlike an internal participant.
/// </summary>
public static class GuestAccessRules
{
    /// <summary>An array so EF Core can translate <c>OpenStatuses.Contains(m.Status)</c> in the session queries.</summary>
    public static readonly MeetingStatus[] OpenStatuses = [MeetingStatus.Published, MeetingStatus.InProgress];

    public static bool MeetingOpen(MeetingStatus status) => OpenStatuses.Contains(status);
}
