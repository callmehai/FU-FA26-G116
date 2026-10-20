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
