namespace BuzzMe.Infrastructure.Persistence.Outbox;

/// <summary>
/// The transactional outbox row — a purely technical mechanism, not a Domain concept
/// (DEVELOPMENT_GUIDE.md §7). Written in the same MongoDB transaction as the aggregate
/// change that raised the event; dispatched afterward, at-least-once, retried until
/// <see cref="ProcessedAt"/> is set.
/// </summary>
public sealed class OutboxMessage
{
    public required Guid Id { get; init; }

    /// <summary>The IDomainEvent's own type name — used to route to the matching Policy at dispatch time.</summary>
    public required string EventType { get; init; }

    /// <summary>The event, serialized. Kept as an opaque payload here — Infrastructure never needs to understand it, only carry it.</summary>
    public required string PayloadJson { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Not visible to the dispatcher until this time — the mechanism behind delayed notification retries (Implementation Spec §4).</summary>
    public required DateTimeOffset AvailableAt { get; init; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int Attempts { get; set; }
}
