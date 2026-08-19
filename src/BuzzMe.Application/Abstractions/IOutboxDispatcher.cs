namespace BuzzMe.Application.Abstractions;

/// <summary>
/// DEVELOPMENT_GUIDE.md §7's outbox dispatcher, as an Application-visible capability — the
/// Workers host's OutboxDispatcherJob depends on this abstraction, not on Infrastructure's
/// MongoDB-specific outbox machinery directly (same "Workers is thin, delegates to
/// Application/an abstraction" posture as BuzzDeliveryWorker's own IServiceScopeFactory +
/// INotificationDispatcher shape).
/// </summary>
public interface IOutboxDispatcher
{
    /// <summary>Claims and processes up to <paramref name="batchSize"/> unprocessed, available outbox rows, invoking each row's matching Policies. Returns the number of rows claimed (processed or left for retry).</summary>
    Task<int> DispatchPendingBatchAsync(int batchSize, CancellationToken cancellationToken);
}
