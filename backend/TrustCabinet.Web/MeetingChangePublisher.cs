using Microsoft.AspNetCore.SignalR;
using TrustCabinet.Application.Meetings;

namespace TrustCabinet.Web;

/// <summary>
/// Pushes the "re-read" events of a meeting's header, its minutes and its document comments (see
/// <see cref="IMeetingChangePublisher"/>). Each event holds the meeting id and nothing else; what a screen shows next
/// comes from its own reauthorized read.
/// </summary>
public sealed class MeetingChangePublisher(IHubContext<MeetingHub> hub, MeetingAudience audience, TimeProvider clock,
    ILogger<MeetingChangePublisher> logger) : IMeetingChangePublisher
{
    /// <summary>A hub push after the commit: a stalled one must not hold the caller's response for long.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    public Task MeetingChangedAsync(Guid meetingId, CancellationToken ct = default)
        => SendAsync("meetingChanged", meetingId, includeFormer: true);

    public Task MinutesChangedAsync(Guid meetingId, CancellationToken ct = default)
        => SendAsync("minutesChanged", meetingId, includeFormer: false);

    public Task CommentsChangedAsync(Guid meetingId, CancellationToken ct = default)
        => SendAsync("commentsChanged", meetingId, includeFormer: false);

    // The change has committed by now: the push runs on its own budget, not on a request the caller may have dropped.
    private async Task SendAsync(string name, Guid meetingId, bool includeFormer)
    {
        using var timeout = new CancellationTokenSource(Budget);
        try
        {
            var groups = MeetingHub.GroupsOf(meetingId, await audience.ReadersAsync(meetingId, timeout.Token, includeFormer));
            if (groups.Count == 0) return;
            await hub.Clients.Groups(groups).SendAsync(name,
                new MeetingRereadChange(Guid.CreateVersion7(), meetingId, clock.GetUtcNow()), timeout.Token);
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Could not publish {Event} for meeting {MeetingId}.", name, meetingId);
        }
    }
}
