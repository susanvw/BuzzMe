using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Boards.Events;

/// <summary>APPLICATION_LAYER_SPEC.md §3.4 — RenameBoard's own event, matched exactly by name.</summary>
public sealed record BoardRenamed(Guid EventId, DateTimeOffset OccurredAt, BoardId BoardId, BoardName Name) : IDomainEvent;
