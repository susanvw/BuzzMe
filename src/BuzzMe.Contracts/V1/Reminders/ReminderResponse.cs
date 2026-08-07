namespace BuzzMe.Contracts.V1.Reminders;

/// <summary>
/// API_CONTRACT.md §3 — the Reminder resource field list. `NextOccurrence` is always null
/// this sprint (Occurrence is out of scope) but kept present so this shape doesn't need a
/// breaking change once Occurrence ships — see SPRINT_2_REPORT.md §4.
/// </summary>
public sealed record ReminderResponse(
    Guid Id,
    Guid BoardId,
    string Title,
    string Recurrence,
    DateTime StartDate,
    string ReferenceTimezone,
    string NotifyPreset,
    object? NextOccurrence,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
