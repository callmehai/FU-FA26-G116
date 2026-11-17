using Microsoft.AspNetCore.SignalR;
using TrustCabinet.Application.Attendance;
using TrustCabinet.Application.Meetings;

namespace TrustCabinet.Web;

public sealed class AttendanceChangePublisher(IHubContext<MeetingHub> hub, MeetingAudience audience) : IAttendanceChangePublisher
{
    public async Task PublishAsync(AttendanceChange change, CancellationToken ct)
    {
        var groups = MeetingHub.GroupsOf(change.MeetingId, await audience.AttendanceReadersAsync(change.MeetingId, ct));
        if (groups.Count > 0) await hub.Clients.Groups(groups).SendAsync("attendanceChanged", change, ct);
    }
}
