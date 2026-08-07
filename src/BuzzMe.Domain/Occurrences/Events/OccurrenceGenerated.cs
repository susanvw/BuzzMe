using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Occurrences.Events;

/// <summary>EVENT_STORMING.md §D1/§N — a concrete due instance now exists.</summary>
public sealed record OccurrenceGenerated(Guid EventId, DateTimeOffset OccurredAt, OccurrenceId OccurrenceId, ReminderId ReminderId, DateTimeOffset DueAt) : IDomainEvent;
