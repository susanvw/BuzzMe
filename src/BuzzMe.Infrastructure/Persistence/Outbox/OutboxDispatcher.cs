using System.Text.Json;
using BuzzMe.Application.Abstractions;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Infrastructure.Persistence.Mongo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Outbox;

/// <summary>
/// DEVELOPMENT_GUIDE.md §7's outbox dispatcher — claims a batch of due rows (an atomic
/// find-and-modify per row, same "safe under concurrent instances" shape as
/// IBuzzRepository.ClaimPendingAsync), resolves each row's matching
/// <see cref="IPolicy{TEvent}"/> implementations from the current DI scope by the event's
/// own runtime type (reflection — there is no other way to route a heterogeneous
/// IDomainEvent payload to a closed generic interface at compile time), and invokes them.
/// A row whose EventType has no registered Policy (<see cref="OutboxEventTypeRegistry"/>)
/// is marked processed immediately — "nothing consumes this yet" is a permanent, expected
/// state, not a transient failure worth retrying forever. A Policy that throws leaves the
/// row for the next poll, already pushed <see cref="RetryBackoff"/> into the future by the
/// claim step itself — this is the actual "retried until success" guarantee
/// DEVELOPMENT_GUIDE.md §7 names; no dead-lettering or max-attempt cutoff exists yet (see
/// SPRINT_17_REPORT.md's specification gap, same category as Sprint 6's own unbuilt Buzz
/// retry/backoff).
/// </summary>
public sealed class OutboxDispatcher(MongoContext context, IServiceProvider serviceProvider, IClock clock, ILogger<OutboxDispatcher> logger)
    : IOutboxDispatcher
{
    private static readonly TimeSpan RetryBackoff = TimeSpan.FromSeconds(30);

    private IMongoCollection<OutboxMessage> Collection => context.Outbox;

    public async Task<int> DispatchPendingBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        var claimed = await ClaimPendingAsync(batchSize, cancellationToken);

        foreach (var row in claimed)
            await ProcessRowAsync(row, cancellationToken);

        return claimed.Count;
    }

    /// <summary>
    /// Each claim both selects the row and pushes AvailableAt forward — a crash between
    /// claiming and marking processed self-heals after <see cref="RetryBackoff"/> without
    /// needing a separate heartbeat/lease mechanism, same reasoning as
    /// IBuzzRepository.ClaimPendingAsync's own claim-by-transition approach.
    /// </summary>
    private async Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var filter = Builders<OutboxMessage>.Filter.Eq(d => d.ProcessedAt, null) & Builders<OutboxMessage>.Filter.Lte(d => d.AvailableAt, now);
        var update = Builders<OutboxMessage>.Update.Inc(d => d.Attempts, 1).Set(d => d.AvailableAt, now.Add(RetryBackoff));
        var options = new FindOneAndUpdateOptions<OutboxMessage>
        {
            Sort = Builders<OutboxMessage>.Sort.Ascending(d => d.AvailableAt),
            ReturnDocument = ReturnDocument.After,
        };

        var claimed = new List<OutboxMessage>();
        for (var i = 0; i < batchSize; i++)
        {
            var row = await Collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
            if (row is null)
                break;

            claimed.Add(row);
        }

        return claimed;
    }

    private async Task ProcessRowAsync(OutboxMessage row, CancellationToken cancellationToken)
    {
        if (!OutboxEventTypeRegistry.TryResolve(row.EventType, out var eventType))
        {
            await MarkProcessedAsync(row.Id, cancellationToken);
            return;
        }

        var domainEvent = (IDomainEvent)JsonSerializer.Deserialize(row.PayloadJson, eventType)!;
        var policyType = typeof(IPolicy<>).MakeGenericType(eventType);
        var policies = serviceProvider.GetServices(policyType).ToList();
        var handleMethod = policyType.GetMethod(nameof(IPolicy<IDomainEvent>.HandleAsync))!;

        try
        {
            foreach (var policy in policies)
                await (Task)handleMethod.Invoke(policy, [domainEvent, cancellationToken])!;

            await MarkProcessedAsync(row.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "Outbox row {OutboxId} ({EventType}) failed on attempt {Attempts} — will retry after {AvailableAt}",
                row.Id, row.EventType, row.Attempts, row.AvailableAt);
        }
    }

    private async Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken)
    {
        var filter = Builders<OutboxMessage>.Filter.Eq(d => d.Id, id);
        var update = Builders<OutboxMessage>.Update.Set(d => d.ProcessedAt, clock.UtcNow);
        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}
