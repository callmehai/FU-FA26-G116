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
