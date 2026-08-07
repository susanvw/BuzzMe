using BuzzMe.Domain.Reminders;

namespace BuzzMe.Application.Reminders.Models;

/// <summary>
/// The Application-layer shape of a Reminder — DEVELOPMENT_GUIDE.md §3/§4. `NextOccurrence`
/// is always null this sprint: Occurrence is explicitly out of scope, but API_CONTRACT.md's
/// Reminder resource already reserves the field, so it's kept present-but-empty rather than
/// silently dropped (see SPRINT_2_REPORT.md §4).
/// </summary>
public sealed record ReminderResult(
    Guid Id,
    Guid BoardId,
    string Title,
    string Recurrence,
    DateTime StartDate,
    string ReferenceTimezone,
    string NotifyPreset,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ReminderResult FromDomain(Reminder reminder) => new(
        reminder.Id.Value,
        reminder.BoardId.Value,
        reminder.Title.Value,
        reminder.Schedule.Recurrence.ToCode(),
        reminder.Schedule.StartDate,
        reminder.Schedule.ReferenceTimezone,
        reminder.NotifyPreset.ToCode(),
        reminder.CreatedAt,
        reminder.UpdatedAt);
}
