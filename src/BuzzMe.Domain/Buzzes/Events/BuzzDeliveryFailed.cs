using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Buzzes.Events;

/// <summary>IMPLEMENTATION_SPEC.md §2 — a delivery attempt to one recipient failed. No retry is scheduled from this event this sprint — see SPRINT_6_REPORT.md's specification gap on retry/backoff.</summary>
public sealed record BuzzDeliveryFailed(Guid EventId, DateTimeOffset OccurredAt, BuzzId BuzzId, OccurrenceId OccurrenceId, Guid RecipientUserId) : IDomainEvent;
