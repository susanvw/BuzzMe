using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Buzzes.Events;

/// <summary>IMPLEMENTATION_SPEC.md §3 — one delivery, planned for one recipient, about one Occurrence, now exists.</summary>
public sealed record BuzzGenerated(Guid EventId, DateTimeOffset OccurredAt, BuzzId BuzzId, OccurrenceId OccurrenceId, Guid RecipientUserId, DateTimeOffset ScheduledAt) : IDomainEvent;
