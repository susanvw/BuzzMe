using BuzzMe.Domain.Boards;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Reminders.Events;

/// <summary>EVENT_STORMING.md §C1/§N — a new reminder definition now exists.</summary>
public sealed record ReminderCreated(Guid EventId, DateTimeOffset OccurredAt, ReminderId ReminderId, BoardId BoardId, ReminderTitle Title) : IDomainEvent;
