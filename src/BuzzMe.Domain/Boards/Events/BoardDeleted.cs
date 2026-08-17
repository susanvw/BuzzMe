using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Boards.Events;

/// <summary>IMPLEMENTATION_SPEC.md §2 — DeleteBoard's own event, matched exactly by name.</summary>
public sealed record BoardDeleted(Guid EventId, DateTimeOffset OccurredAt, BoardId BoardId) : IDomainEvent;
