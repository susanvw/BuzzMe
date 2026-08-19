using BuzzMe.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuzzMe.Workers.Jobs;

/// <summary>
/// DEVELOPMENT_GUIDE.md §7's category A (event-reactive) work, hosted per that section's
/// own naming — `OutboxDispatcherJob` polls the `outbox` collection and invokes each due
/// row's matching Policy. Same plain `BackgroundService` + `PeriodicTimer` shape as
/// BuzzDeliveryWorker, for the same reason: this codebase's outbox rows carry their own
/// `availableAt` (the retry-backoff mechanism), so polling — not a message broker — is
/// sufficient here too.
/// </summary>
public sealed class OutboxDispatcherJob(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcherJob> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "OutboxDispatcherJob: unhandled error while processing a batch");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Internal, not private, so integration tests can drive exactly this orchestration directly against real MongoDB without waiting on the PeriodicTimer — see BuzzDeliveryWorker's own precedent.</summary>
    internal async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
        await dispatcher.DispatchPendingBatchAsync(BatchSize, cancellationToken);
    }
}
