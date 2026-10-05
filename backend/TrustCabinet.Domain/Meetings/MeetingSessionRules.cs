using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

/// <summary>Shared validation of the planned session required to publish an Approved meeting.</summary>
public static class MeetingSessionRules
{
    public const int MaximumTitleLength = 300;

    public static bool IsValidPlanned(MeetingSession session, Guid meetingId)
        => meetingId != Guid.Empty
            && session.Id != Guid.Empty
            && session.MeetingId == meetingId
            && session.Status == MeetingSessionStatus.Planned
            && !string.IsNullOrWhiteSpace(session.Title)
            && session.Title.Length <= MaximumTitleLength
            && session.PlannedStartsAt != default
            && session.PlannedEndsAt != default
            && session.PlannedEndsAt > session.PlannedStartsAt;
}
