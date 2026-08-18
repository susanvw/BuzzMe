using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Occurrences.Events;

/// <summary>APPLICATION_LAYER_SPEC.md §3.8 — a Member marked this Occurrence resolved as done.</summary>
public sealed record OccurrenceCompleted(
    Guid EventId, DateTimeOffset OccurredAt, OccurrenceId OccurrenceId, ReminderId ReminderId, Guid ResolvedByUserId) : IDomainEvent;
