using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Notifications;
using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Web.Tests.Email;

public sealed class EmailDeliveryWorkerTests
{
    [Fact]
    public async Task Reconciliation_failures_are_retried_without_blocking_processing_in_fresh_scopes()
    {
        var probe = new WorkerProbe();
        using var provider = Services(probe).BuildServiceProvider();
        var worker = Worker(provider);

        var firstResult = await worker.ProcessOnceAsync(CancellationToken.None);
        var secondResult = await worker.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(EmailProcessingOutcome.NoWork, firstResult.Outcome);
        Assert.Equal(EmailProcessingOutcome.NoWork, secondResult.Outcome);
        Assert.Equal(2, probe.ReconciliationScopes.Count);
        Assert.Equal(2, probe.ProcessingScopes.Count);
        Assert.Equal(
            4,
            probe.ReconciliationScopes.Concat(probe.ProcessingScopes).Distinct().Count());
    }

    [Fact]
    public async Task Host_cancellation_during_reconciliation_stops_before_processing()
    {
        var probe = new WorkerProbe { CancelReconciliation = true };
        using var provider = Services(probe).BuildServiceProvider();
        var worker = Worker(provider);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => worker.ProcessOnceAsync(cancellation.Token));

        Assert.Equal(1, probe.ReconciliationCalls);
        Assert.Equal(0, probe.ProcessingCalls);
    }

    private static ServiceCollection Services(WorkerProbe probe)
    {
        var services = new ServiceCollection();
        services.AddSingleton(probe);
        services.AddScoped<ScopeMarker>();
        services.AddScoped<IMeetingInvitationQueue, FailingReconciliationQueue>();
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
        public int ReconciliationCalls { get; set; }
        public int ProcessingCalls { get; set; }
        public bool CancelReconciliation { get; init; }
        public List<ScopeMarker> ReconciliationScopes { get; } = [];
        public List<ScopeMarker> ProcessingScopes { get; } = [];
    }

    private sealed class ScopeMarker;

    private sealed class FailingReconciliationQueue(WorkerProbe probe, ScopeMarker scope)
        : IMeetingInvitationQueue
    {
        public Task<int> QueueForPublishedMeetingAsync(
            Guid meetingId, CancellationToken ct = default)
            => Task.FromResult(0);

        public Task<Guid?> QueueNextUninitializedPublishedMeetingAsync(
            CancellationToken ct = default)
        {
            probe.ReconciliationCalls++;
            probe.ReconciliationScopes.Add(scope);
            return probe.CancelReconciliation
                ? Task.FromCanceled<Guid?>(ct)
                : Task.FromException<Guid?>(new InvalidOperationException("Reconciliation unavailable."));
        }
    }

    private sealed class ProbeEmailQueueProcessor(WorkerProbe probe, ScopeMarker scope)
        : IEmailQueueProcessor
    {
        public Task<EmailProcessingResult> ProcessNextAsync(CancellationToken ct = default)
        {
            probe.ProcessingCalls++;
            probe.ProcessingScopes.Add(scope);
            return Task.FromResult(new EmailProcessingResult(EmailProcessingOutcome.NoWork));
        }
    }
}
