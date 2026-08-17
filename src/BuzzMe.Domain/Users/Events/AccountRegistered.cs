using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Users.Events;

/// <summary>IMPLEMENTATION_SPEC.md §2 — RegisterAccount's own event, matched exactly by name.</summary>
public sealed record AccountRegistered(Guid EventId, DateTimeOffset OccurredAt, UserId UserId) : IDomainEvent;
