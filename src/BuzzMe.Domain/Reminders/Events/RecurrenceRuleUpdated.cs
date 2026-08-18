using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Reminders.Events;

/// <summary>
/// IMPLEMENTATION_SPEC.md §1 — the Reminder's recurrence changed. Raised as its own event,
/// distinct from ReminderUpdated, because it drives its own downstream policy
/// (APPLICATION_LAYER_SPEC.md §3.7/§7: regenerate only not-yet-generated future
/// Occurrences) rather than sharing ReminderUpdated's.
/// </summary>
public sealed record RecurrenceRuleUpdated(Guid EventId, DateTimeOffset OccurredAt, ReminderId ReminderId, Recurrence Recurrence) : IDomainEvent;
