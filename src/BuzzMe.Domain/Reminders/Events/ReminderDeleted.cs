using BuzzMe.Domain.Boards;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Reminders.Events;

/// <summary>
/// EVENT_STORMING.md §C3/§N — Reminder retired; History survives (out of scope this
/// sprint). Occurrence-cancellation side effects (Implementation Spec §4) are explicitly
/// not wired this sprint — no Occurrence exists to cancel.
/// </summary>
public sealed record ReminderDeleted(Guid EventId, DateTimeOffset OccurredAt, ReminderId ReminderId, BoardId BoardId) : IDomainEvent;
