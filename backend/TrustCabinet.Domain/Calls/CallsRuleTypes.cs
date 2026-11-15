namespace TrustCabinet.Domain.Calls;

/// <summary>
/// LiveKit room names for meeting rooms (ADR-0010). The name is derived from the meeting id on the
/// server, so a caller can never ask for a token to a room of their own choosing.
/// </summary>
public static class CallRoomNames
{
    private const string MeetingPrefix = "meeting-";

    public static string ForMeeting(Guid meetingId) => MeetingPrefix + meetingId.ToString("D");

    public static bool TryParseMeeting(string? roomName, out Guid meetingId)
    {
        meetingId = Guid.Empty;
        return roomName is not null
            && roomName.StartsWith(MeetingPrefix, StringComparison.Ordinal)
            && Guid.TryParseExact(roomName[MeetingPrefix.Length..], "D", out meetingId);
    }
}
