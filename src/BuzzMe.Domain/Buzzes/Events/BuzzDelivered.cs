using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Buzzes.Events;

/// <summary>IMPLEMENTATION_SPEC.md §2 — a delivery attempt to one recipient succeeded.</summary>
public sealed record BuzzDelivered(Guid EventId, DateTimeOffset OccurredAt, BuzzId BuzzId, OccurrenceId OccurrenceId, Guid RecipientUserId) : IDomainEvent;
