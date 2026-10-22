using System.Net;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Participants;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Participants;

public sealed class ParticipantHubTests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    private HubConnection Hub(HttpClient client) => new HubConnectionBuilder()
        .WithUrl("http://localhost/hubs/meetings", options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.HttpMessageHandlerFactory = _ => f.Host.Server.CreateHandler();
            options.AccessTokenProvider = () => Task.FromResult(client.DefaultRequestHeaders.Authorization?.Parameter);
        }).Build();

    [Fact]
    public async Task Dedicated_handover_notifies_both_readers_after_commit_and_never_on_rollback()
    {
        var s = await f.Seed(); using var chair = f.Client(s.Chair); using var member = f.Client(s.Member);
        f.Changes.Transport = new ParticipantsChangePublisher(f.Host.Services.GetRequiredService<IHubContext<MeetingHub>>());
        await using var oldHub = Hub(chair); await using var newHub = Hub(member);
        var oldEvents = Channel.CreateUnbounded<ParticipantsChange>();
        var newEvents = Channel.CreateUnbounded<ParticipantsChange>();
        oldHub.On<ParticipantsChange>("participantsChanged", e => oldEvents.Writer.TryWrite(e));
        newHub.On<ParticipantsChange>("participantsChanged", e => newEvents.Writer.TryWrite(e));
        await oldHub.StartAsync(); await newHub.StartAsync();
        await oldHub.InvokeAsync("JoinMeeting", s.MeetingId); await newHub.InvokeAsync("JoinMeeting", s.MeetingId);
        Task<HttpResponseMessage> Post() => Write(chair, s.Url + "/participants/chair-handover", HttpMethod.Post,
            new { newChairParticipantId = s.MemberAssignment, confirmed = true });
        f.Failure.MeetingId = s.MeetingId; f.Failure.FailOnSave = 2;
        try { await Code(await Post(), 503, "MSG102"); }
        finally { f.Failure.Reset(); }
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await oldEvents.Reader.ReadAsync(timeout.Token));
        Assert.False(newEvents.Reader.TryRead(out _));
        await Code(await Post(), 200, "MSG257");
        var previous = await oldEvents.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var next = await newEvents.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(previous, next); Assert.Equal("1", previous.Revision);
        Assert.NotEqual(Guid.Empty, previous.EventId);
        await oldHub.InvokeAsync("JoinMeeting", s.MeetingId);
        await newHub.InvokeAsync("JoinMeeting", s.MeetingId);
        Assert.False(oldEvents.Reader.TryRead(out _)); Assert.False(newEvents.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Committed_response_and_removal_reach_joined_reader_but_rollback_does_not()
    {
        var s = await f.Seed();
        using var member = f.Client(s.Member);
        using var secretary = f.Client(s.Secretary);
        using var outsider = f.Client(s.Outsider);
        f.Changes.Transport = new ParticipantsChangePublisher(f.Host.Services.GetRequiredService<IHubContext<MeetingHub>>());
        await using var hub = Hub(member);
        await using var excluded = Hub(outsider);
        var events = Channel.CreateUnbounded<ParticipantsChange>();
        hub.On<ParticipantsChange>("participantsChanged", e => events.Writer.TryWrite(e));
        await hub.StartAsync();
        await excluded.StartAsync();
        await hub.InvokeAsync("JoinMeeting", s.MeetingId);
        await Assert.ThrowsAsync<HubException>(() => excluded.InvokeAsync("JoinMeeting", s.MeetingId));
        await Code(await Write(member, s.Url + "/invitation-response", HttpMethod.Put, new { response = "Accepted" }), 200, "MSG105");
        var response = await events.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(s.MeetingId, response.MeetingId);
        Assert.NotEqual(Guid.Empty, response.EventId);
        Assert.Equal("0", response.Revision);

        f.Failure.MeetingId = s.MeetingId;
        try { await Code(await Write(secretary, s.Url + $"/participants/{s.MemberAssignment}", HttpMethod.Delete), 503, "MSG104"); }
        finally { f.Failure.Reset(); }
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await events.Reader.ReadAsync(timeout.Token));

        await Code(await Write(secretary, s.Url + $"/participants/{s.MemberAssignment}", HttpMethod.Delete), 200, "MSG103");
        var removed = await events.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("1", removed.Revision);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync(s.Url + "/participants")).StatusCode);
        await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync("JoinMeeting", s.MeetingId));
    }
}
