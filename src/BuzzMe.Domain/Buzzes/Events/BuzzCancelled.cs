using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Buzzes.Events;

/// <summary>
/// IMPLEMENTATION_SPEC.md §4's "cancel pending Buzzes" policy, at the Buzz's own end —
/// raised when Complete/DismissOccurrence or DeleteReminder's cancellation policy reaches
/// this specific Buzz while it was still pending (Scheduled or Generated).
/// </summary>
public sealed record BuzzCancelled(Guid EventId, DateTimeOffset OccurredAt, BuzzId BuzzId, OccurrenceId OccurrenceId, Guid RecipientUserId) : IDomainEvent;
