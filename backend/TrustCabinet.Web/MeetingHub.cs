using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Infrastructure;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Web.Endpoints;

namespace TrustCabinet.Web;

[Authorize]
public sealed class MeetingHub(IMeetingRepository meetings, IAgendaGuestIdentityResolver identities,
    IAgendaGuestAccess guests) : Hub
{
    internal static string Group(Guid meetingId, Guid userId) => $"meeting:{meetingId:D}:user:{userId:D}";
    internal static string GuestGroup(AgendaGuestSession guest)
        => $"meeting:{guest.MeetingId:D}:guest:{guest.InvitationId:D}:session:{guest.SessionId:D}";

    public override async Task OnConnectedAsync()
    {
        if (Context.User is { } principal && identities.Resolve(principal) is { } guest)
        {
            if (!await CanGuestRead(guest, guest.MeetingId)) { Context.Abort(); return; }
            await base.OnConnectedAsync();
            return;
        }
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
        if (identities.Resolve(Context.User!) is { } guest)
        {
            if (!await CanGuestRead(guest, meetingId)) throw new HubException("Không tìm thấy cuộc họp.");
            await Groups.AddToGroupAsync(Context.ConnectionId, GuestGroup(guest), Context.ConnectionAborted);
            return;
        }
        var actor = CapabilityGate.GatedUserId(Context.User!);
        if (!await meetings.IsUserActiveAsync(actor, Context.ConnectionAborted)
            || await meetings.FindAccessibleAsync(meetingId, actor, Context.ConnectionAborted) is null)
            throw new HubException("Không tìm thấy cuộc họp.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(meetingId, actor), Context.ConnectionAborted);
    }

    public Task LeaveMeeting(Guid meetingId)
    {
        if (identities.Resolve(Context.User!) is { } guest)
            return guest.MeetingId == meetingId
                ? Groups.RemoveFromGroupAsync(Context.ConnectionId, GuestGroup(guest), Context.ConnectionAborted)
                : Task.CompletedTask;
        return Groups.RemoveFromGroupAsync(Context.ConnectionId,
            Group(meetingId, CapabilityGate.GatedUserId(Context.User!)), Context.ConnectionAborted);
    }

    private async Task<bool> CanGuestRead(AgendaGuestSession guest, Guid meetingId)
    {
        if (!guest.IsValid || guest.MeetingId != meetingId || !await guests.CanReadAsync(guest, Context.ConnectionAborted))
            return false;
        var meeting = await meetings.FindWorkflowAsync(meetingId, ct: Context.ConnectionAborted);
        return meeting is not null && AgendaRules.GuestCanViewMeeting(meeting.Status);
    }
}

public sealed class AgendaChangePublisher(IHubContext<MeetingHub> hub, IMeetingRepository meetings,
    TrustCabinetDbContext db, ILogger<AgendaChangePublisher> logger, IAgendaGuestAccess guests) : IAgendaChangePublisher
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
        if (AgendaRules.GuestCanViewMeeting(meeting.Status))
        {
            var readers = await guests.ListReadersAsync(meeting.Id, ct);
            groups.AddRange(readers.Where(g => g.IsValid && g.MeetingId == meeting.Id)
                .Distinct().Select(MeetingHub.GuestGroup));
        }
        if (groups.Count == 0) return;
        await hub.Clients.Groups(groups).SendAsync("agendaChanged", change, ct);
        logger.LogDebug("Published agenda event {EventId} to {Count} authorized user groups", change.EventId, groups.Count);
    }
}
