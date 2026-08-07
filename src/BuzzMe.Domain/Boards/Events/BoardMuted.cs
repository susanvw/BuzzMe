using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Boards.Events;

/// <summary>APPLICATION_LAYER_SPEC.md §3.4 — one person's own future Buzz delivery for this Board is suppressed; never affects anyone else's delivery or the Board's own content.</summary>
public sealed record BoardMuted(Guid EventId, DateTimeOffset OccurredAt, BoardId BoardId, Guid UserId) : IDomainEvent;
