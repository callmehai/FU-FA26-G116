using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Infrastructure;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Web.Endpoints;

namespace TrustCabinet.Web;

[Authorize]
public sealed class MeetingHub(IMeetingRepository meetings) : Hub
{
    internal static string Group(Guid meetingId, Guid userId) => $"meeting:{meetingId:D}:user:{userId:D}";

    public override async Task OnConnectedAsync()
    {
        var actor = Context.User is null ? null : CapabilityGate.CurrentUserId(Context.User);
        if (actor is null || !await meetings.IsUserActiveAsync(actor.Value, Context.ConnectionAborted))
        {
            Context.Abort();
            return;
        }
        await base.OnConnectedAsync();
    }

    public async Task JoinMeeting(Guid meetingId)
    {
        var actor = CapabilityGate.GatedUserId(Context.User!);
        if (!await meetings.IsUserActiveAsync(actor, Context.ConnectionAborted)
            || await meetings.FindAccessibleAsync(meetingId, actor, Context.ConnectionAborted) is null)
            throw new HubException("Không tìm thấy cuộc họp.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(meetingId, actor), Context.ConnectionAborted);
    }

    public Task LeaveMeeting(Guid meetingId) => Groups.RemoveFromGroupAsync(Context.ConnectionId,
        Group(meetingId, CapabilityGate.GatedUserId(Context.User!)), Context.ConnectionAborted);
}

public sealed class AgendaChangePublisher(IHubContext<MeetingHub> hub, IMeetingRepository meetings,
    TrustCabinetDbContext db, ILogger<AgendaChangePublisher> logger) : IAgendaChangePublisher
{
    public async Task PublishAsync(AgendaChange change, CancellationToken ct)
    {
        // Fail closed. The service logs failed publication without undoing the committed write.
        var meeting = await meetings.FindWorkflowAsync(change.MeetingId, ct: ct);
        if (meeting is null) return;
        var candidates = meeting.Participants.Select(p => p.UserId).Append(meeting.CreatedById)
            .Concat(meeting.AssignedApproverId is { } approver ? [approver] : [])
            .Distinct().Where(actor => MeetingReadPolicy.CanRead(meeting, actor)).ToArray();
        var active = await db.Users.AsNoTracking().Where(u => candidates.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id).ToListAsync(ct);
        var groups = active.Select(actor => MeetingHub.Group(meeting.Id, actor)).ToList();
        if (groups.Count == 0) return;
        await hub.Clients.Groups(groups).SendAsync("agendaChanged", change, ct);
        logger.LogDebug("Published agenda event {EventId} to {Count} authorized user groups", change.EventId, groups.Count);
    }
}
