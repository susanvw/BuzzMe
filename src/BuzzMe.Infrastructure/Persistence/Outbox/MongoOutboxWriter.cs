using System.Text.Json;
using BuzzMe.Application.Abstractions;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Infrastructure.Persistence.Mongo;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Outbox;

public sealed class MongoOutboxWriter(MongoContext context, IClock clock) : IOutboxWriter
{
    public async Task WriteAsync(
        IClientSessionHandle session, IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        if (domainEvents.Count == 0) return;

        var now = clock.UtcNow;
        var rows = domainEvents.Select(domainEvent => new OutboxMessage
        {
            Id = domainEvent.EventId,
            EventType = domainEvent.GetType().Name,
            PayloadJson = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            OccurredAt = domainEvent.OccurredAt,
            AvailableAt = now,
        });

        await context.Outbox.InsertManyAsync(session, rows, cancellationToken: cancellationToken);
    }
}
