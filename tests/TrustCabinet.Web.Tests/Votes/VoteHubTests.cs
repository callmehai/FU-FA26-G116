using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Votes;
using TrustCabinet.Web.Tests.Participants;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;
using static TrustCabinet.Web.Tests.Votes.VoteEndpointTests;

namespace TrustCabinet.Web.Tests.Votes;

public sealed class VoteHubTests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    private static async Task<VoteChange> Next(ChannelReader<VoteChange> events)
        => await events.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

    private static async Task None(ChannelReader<VoteChange> events)
    {
        using var timeout = new CancellationTokenSource(250);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await events.ReadAsync(timeout.Token));
    }

    [Fact]
    public async Task Readers_hear_votes_open_and_fill_while_drafts_stay_with_the_chair_and_secretary()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair); using var secretary = f.Client(s.Secretary); using var member = f.Client(s.Member);
        await using var managerHub = f.Hub(secretary); await using var memberHub = f.Hub(member);
        var managerEvents = Channel.CreateUnbounded<VoteChange>(); var memberEvents = Channel.CreateUnbounded<VoteChange>();
        managerHub.On<VoteChange>("voteChanged", e => managerEvents.Writer.TryWrite(e));
        memberHub.On<VoteChange>("voteChanged", e => memberEvents.Writer.TryWrite(e));
        await Task.WhenAll(managerHub.StartAsync(), memberHub.StartAsync());
        await managerHub.InvokeAsync("JoinMeeting", s.MeetingId);
        await memberHub.InvokeAsync("JoinMeeting", s.MeetingId);

        // A draft is news only to the Chair and Secretary (BR-253), and carries no content.
        var id = await Draft(chair, s, Body("Thông qua dự toán"));
        var created = await Next(managerEvents.Reader);
        Assert.Equal(("DraftCreated", id, s.MeetingId), (created.ChangeType, created.VoteId, created.MeetingId));
        Assert.Null(created.Title);
        await None(memberEvents.Reader);

        // Opening names the vote, so the room can say "Chủ toạ mở biểu quyết: {nội dung}".
        await Code(await chair.PostAsync($"{s.Url}/votes/{id}/open", null), 200, "MSG217");
        var opened = await Next(memberEvents.Reader);
        Assert.Equal(("Opened", "Thông qua dự toán"), (opened.ChangeType, opened.Title));
        Assert.Equal("Opened", (await Next(managerEvents.Reader)).ChangeType);

        // A ballot moves the count for everyone and says nothing about who cast it.
        await Code(await Ballot(member, s, id, (await Options(chair, s, id))[0]), 200, "MSG227");
        var cast = await Next(managerEvents.Reader);
        Assert.Equal("BallotCast", cast.ChangeType);
        Assert.Null(cast.Title);
        Assert.Equal("BallotCast", (await Next(memberEvents.Reader)).ChangeType);

        await Code(await chair.PostAsync($"{s.Url}/votes/{id}/close", null), 200, "MSG229");
        Assert.Equal("Closed", (await Next(memberEvents.Reader)).ChangeType);
        // A refused write publishes nothing.
        await Code(await chair.PostAsync($"{s.Url}/votes/{id}/close", null), 409, "MSG230");
        await None(memberEvents.Reader);
    }
}
