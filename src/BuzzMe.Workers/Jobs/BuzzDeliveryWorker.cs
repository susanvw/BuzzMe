using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Buzzes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuzzMe.Workers.Jobs;

/// <summary>
/// Time-scheduled work (DEVELOPMENT_GUIDE.md §7's category B — plain `BackgroundService` +
/// `PeriodicTimer`, no external scheduler), not the outbox-reactive pattern DEVELOPMENT_GUIDE.md's
/// own process table lists for "Dispatch Push Notifications" — see SPRINT_6_REPORT.md's
/// architecture observation for why polling is the correct mechanism here regardless: a
/// Buzz must not be picked up before its own ScheduledAt (the NotifyPreset lead time),
/// which pure reactivity to `BuzzGenerated` (raised at Buzz *creation*, per Sprint 4)
/// cannot express.
///
/// Claim → dispatch (temporary <see cref="INotificationDispatcher"/>, Sprint 6's own
/// stand-in for a real provider) → mark outcome → continue. One DI scope per batch, since
/// `BuzzApplicationService`/`IBuzzRepository` are scoped but this hosted service is a
/// singleton (DEVELOPMENT_GUIDE.md §9).
/// </summary>
public sealed class BuzzDeliveryWorker(
    IServiceScopeFactory scopeFactory, INotificationDispatcher dispatcher, ILogger<BuzzDeliveryWorker> logger) : BackgroundService
{
    /// <summary>Operational parameters, not fixed by any specification — same open-parameter category as IMPLEMENTATION_SPEC.md §6's retry cadence.</summary>
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
                // Never let one bad batch stop future polling — the next tick tries again.
                logger.LogError(ex, "BuzzDeliveryWorker: unhandled error while processing a batch");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Internal, not private, so integration tests can drive exactly this orchestration directly against real MongoDB without waiting on the PeriodicTimer — see BuzzMe.Workers.csproj's InternalsVisibleTo.</summary>
    internal async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var buzzService = scope.ServiceProvider.GetRequiredService<BuzzApplicationService>();

        var claimed = await buzzService.ClaimPendingBuzzesAsync(BatchSize, cancellationToken);
        foreach (var buzz in claimed)
        {
            try
            {
                var delivered = await dispatcher.DispatchAsync(buzz, cancellationToken);
                if (delivered)
                    await buzzService.MarkDeliveredAsync(buzz, cancellationToken);
                else
                    await buzzService.MarkFailedAsync(buzz, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // No dead-letter/recovery sweep this sprint (orchestration only) — a Buzz
                // that throws here stays claimed (Generated) rather than being marked
                // Failed, since the dispatcher's own outcome is exactly what's unknown.
                // Documented, not silently papered over — see SPRINT_6_REPORT.md.
                logger.LogError(ex, "BuzzDeliveryWorker: failed to process Buzz {BuzzId}", buzz.Id);
            }
        }
    }
}
