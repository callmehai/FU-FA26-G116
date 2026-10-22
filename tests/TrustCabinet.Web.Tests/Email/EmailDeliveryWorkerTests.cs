using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Notifications;
using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Web.Tests.Email;

public sealed class EmailDeliveryWorkerTests
{
    [Fact]
    public async Task Processing_uses_fresh_scopes_without_legacy_reconciliation()
    {
        var probe = new WorkerProbe();
        using var provider = Services(probe).BuildServiceProvider();
        var worker = Worker(provider);

        var firstResult = await worker.ProcessOnceAsync(CancellationToken.None);
        var secondResult = await worker.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(EmailProcessingOutcome.NoWork, firstResult.Outcome);
        Assert.Equal(EmailProcessingOutcome.NoWork, secondResult.Outcome);
        Assert.Equal(2, probe.ProcessingScopes.Count);
        Assert.Equal(
            2,
            probe.ProcessingScopes.Distinct().Count());
    }

    [Fact]
    public async Task Host_cancellation_stops_processing()
    {
        var probe = new WorkerProbe();
        using var provider = Services(probe).BuildServiceProvider();
        var worker = Worker(provider);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => worker.ProcessOnceAsync(cancellation.Token));

        Assert.Equal(0, probe.ProcessingCalls);
    }

    [Fact]
    public async Task Dispatch_failure_does_not_block_queued_email_and_uses_fresh_scope()
    {
        var probe = new WorkerProbe();
        var services = Services(probe);
        var dispatchScopes = new List<object>();
        services.AddScoped<MeetingInvitationDispatchProcessor>(provider =>
        {
            dispatchScopes.Add(provider.GetRequiredService<ScopeMarker>());
            throw new InvalidOperationException("provider-secret");
        });
        using var provider = services.BuildServiceProvider();
        var worker = Worker(provider);
        await worker.ProcessOnceAsync(default);
        await worker.ProcessOnceAsync(default);
        Assert.Equal(2, probe.ProcessingCalls);
        Assert.Equal(2, dispatchScopes.Distinct().Count());
        Assert.DoesNotContain(dispatchScopes[0], probe.ProcessingScopes);
    }

    [Fact]
    public async Task Dispatch_host_cancellation_propagates_without_sending_email()
    {
        var probe = new WorkerProbe();
        using var cancellation = new CancellationTokenSource();
        var services = Services(probe);
        services.AddScoped<MeetingInvitationDispatchProcessor>(_ =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });
        using var provider = services.BuildServiceProvider();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Worker(provider).ProcessOnceAsync(cancellation.Token));
        Assert.Equal(0, probe.ProcessingCalls);
    }

    private static ServiceCollection Services(WorkerProbe probe)
    {
        var services = new ServiceCollection();
        services.AddSingleton(probe);
        services.AddScoped<ScopeMarker>();
        services.AddScoped<IEmailQueueProcessor, ProbeEmailQueueProcessor>();
        return services;
    }

    private static EmailDeliveryWorker Worker(ServiceProvider provider)
        => new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new EmailDeliveryOptions()),
            TimeProvider.System,
            NullLogger<EmailDeliveryWorker>.Instance);

    private sealed class WorkerProbe
    {
        public int ProcessingCalls { get; set; }
        public List<ScopeMarker> ProcessingScopes { get; } = [];
    }

    private sealed class ScopeMarker;

    private sealed class ProbeEmailQueueProcessor(WorkerProbe probe, ScopeMarker scope)
        : IEmailQueueProcessor
    {
        public Task<EmailProcessingResult> ProcessNextAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            probe.ProcessingCalls++;
            probe.ProcessingScopes.Add(scope);
            return Task.FromResult(new EmailProcessingResult(EmailProcessingOutcome.NoWork));
        }
    }
}
