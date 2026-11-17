using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Web.Endpoints;

namespace TrustCabinet.Web;

[Authorize]
public sealed class MeetingHub(MeetingAudience audience, IMeetingGuestIdentityResolver identities) : Hub
{
    internal static string Group(Guid meetingId, Guid userId) => $"meeting:{meetingId:D}:user:{userId:D}";
    internal static string GuestGroup(MeetingGuestSession guest)
        => $"meeting:{guest.MeetingId:D}:guest:{guest.InvitationId:D}:session:{guest.SessionId:D}";

    /// <summary>The joined groups of these users of the meeting.</summary>
    internal static List<string> GroupsOf(Guid meetingId, IEnumerable<Guid> userIds)
        => userIds.Select(id => Group(meetingId, id)).ToList();

    /// <summary>The joined groups of these users and guest sessions of the meeting.</summary>
    internal static List<string> GroupsOf(Guid meetingId, MeetingAudienceMembers members)
        => members.UserIds.Select(id => Group(meetingId, id)).Concat(members.Guests.Select(GuestGroup)).ToList();

    public override async Task OnConnectedAsync()
    {
        if (Context.User is { } principal && identities.Resolve(principal) is { } guest)
        {
            if (!await audience.CanGuestJoinAsync(guest, guest.MeetingId, Context.ConnectionAborted)) { Context.Abort(); return; }
            await base.OnConnectedAsync();
            return;
        }
        var actor = Context.User is null ? null : CapabilityGate.CurrentUserId(Context.User);
        if (actor is null || !await audience.IsUserActiveAsync(actor.Value, Context.ConnectionAborted))
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
            if (!await audience.CanGuestJoinAsync(guest, meetingId, Context.ConnectionAborted))
                throw new HubException("Không tìm thấy cuộc họp.");
            await Groups.AddToGroupAsync(Context.ConnectionId, GuestGroup(guest), Context.ConnectionAborted);
            return;
        }
        var actor = CapabilityGate.GatedUserId(Context.User!);
        if (!await audience.CanUserJoinAsync(meetingId, actor, Context.ConnectionAborted))
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
}

public sealed class AgendaChangePublisher(IHubContext<MeetingHub> hub, MeetingAudience audience,
    ILogger<AgendaChangePublisher> logger) : IAgendaChangePublisher
{
    public async Task PublishAsync(AgendaChange change, CancellationToken ct)
    {
        // Fail closed. The service logs failed publication without undoing the committed write.
        var groups = MeetingHub.GroupsOf(change.MeetingId, await audience.ReadersAsync(change.MeetingId, ct));
        if (groups.Count == 0) return;
        await hub.Clients.Groups(groups).SendAsync("agendaChanged", change, ct);
        logger.LogDebug("Published agenda event {EventId} to {Count} authorized user groups", change.EventId, groups.Count);
    }
}

/// <summary>Only says which meeting: a reader refetches the list, which is reauthorized. EventId lets a
/// client drop a redelivered event, like the other meeting events.</summary>
public sealed record MeetingDocumentsChange(Guid EventId, Guid MeetingId, DateTimeOffset OccurredAt);

/// <summary>Tells the Documents tab and the room panel that documents were added (SCRUM-21).</summary>
public sealed class MeetingDocumentChangePublisher(IHubContext<MeetingHub> hub, MeetingAudience audience, TimeProvider clock)
{
    public async Task PublishAsync(Guid meetingId, CancellationToken ct)
    {
        var groups = MeetingHub.GroupsOf(meetingId, await audience.ReadersAsync(meetingId, ct));
        if (groups.Count > 0)
            await hub.Clients.Groups(groups).SendAsync("documentsChanged",
                new MeetingDocumentsChange(Guid.CreateVersion7(), meetingId, clock.GetUtcNow()), ct);
    }
}
