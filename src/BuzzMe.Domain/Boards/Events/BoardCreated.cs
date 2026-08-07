using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Boards.Events;

/// <summary>EVENT_STORMING.md §B1 / §N — a new shared space now exists.</summary>
public sealed record BoardCreated(Guid EventId, DateTimeOffset OccurredAt, BoardId BoardId, BoardName Name) : IDomainEvent;
