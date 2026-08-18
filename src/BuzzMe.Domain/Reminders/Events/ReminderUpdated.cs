using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Reminders.Events;

/// <summary>APPLICATION_LAYER_SPEC.md §3.7 — the Reminder's title and/or start date changed.</summary>
public sealed record ReminderUpdated(Guid EventId, DateTimeOffset OccurredAt, ReminderId ReminderId, ReminderTitle Title, DateTime StartDate) : IDomainEvent;
