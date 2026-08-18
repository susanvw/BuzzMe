using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Reminders.Events;

/// <summary>
/// IMPLEMENTATION_SPEC.md §1 — the Reminder's notify preset changed. Its own event, distinct
/// from ReminderUpdated, since it drives a different downstream policy (§5: reschedule every
/// not-yet-delivered Buzz for every not-yet-resolved Occurrence, immediately — not the
/// "only future Occurrences" rule recurrence changes get).
/// </summary>
public sealed record NotifyPresetUpdated(Guid EventId, DateTimeOffset OccurredAt, ReminderId ReminderId, NotifyPreset NotifyPreset) : IDomainEvent;
