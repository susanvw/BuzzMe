using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Occurrences.Events;

/// <summary>APPLICATION_LAYER_SPEC.md §3.8 — a prior Complete/Dismiss resolution was reversed within the grace window (IMPLEMENTATION_SPEC.md §1's `UndoOccurrenceResolution`).</summary>
public sealed record OccurrenceUndone(Guid EventId, DateTimeOffset OccurredAt, OccurrenceId OccurrenceId, ReminderId ReminderId) : IDomainEvent;
