using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Occurrences.Events;

/// <summary>APPLICATION_LAYER_SPEC.md §3.8 — a Member marked this Occurrence resolved as dismissed (deliberately not done).</summary>
public sealed record OccurrenceDismissed(
    Guid EventId, DateTimeOffset OccurredAt, OccurrenceId OccurrenceId, ReminderId ReminderId, Guid ResolvedByUserId) : IDomainEvent;
