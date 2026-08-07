using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Boards.Events;

/// <summary>APPLICATION_LAYER_SPEC.md §3.4 — reverses BoardMuted for this one person; delivery for this Board resumes on their next generated Buzz.</summary>
public sealed record BoardUnmuted(Guid EventId, DateTimeOffset OccurredAt, BoardId BoardId, Guid UserId) : IDomainEvent;
