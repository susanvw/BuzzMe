namespace BuzzMe.Domain.SeedWork;

/// <summary>
/// Marker for a domain event — a fact that already happened, named in the past tense.
/// Raised by an AggregateRoot and written to the outbox in the same transaction as the
/// aggregate's own persistence (see DEVELOPMENT_GUIDE.md §7).
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}
