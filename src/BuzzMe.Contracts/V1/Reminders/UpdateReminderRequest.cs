namespace BuzzMe.Contracts.V1.Reminders;

/// <summary>
/// API_CONTRACT.md §5 — PATCH /v1/reminders/{reminderId} request body: `{ title?,
/// recurrence?, startDate?, notifyPreset? }`. No `BoardId` field — deliberately absent, not
/// merely unused: API_CONTRACT.md's own words are "`boardId` is not an accepted field;
/// present in payload → 400, not silently ignored," enforced at the endpoint by inspecting
/// the raw request body before this type is even deserialized (System.Text.Json would
/// otherwise just drop an unrecognized property silently) — see
/// ReminderEndpoints.UpdateReminderAsync.
/// </summary>
public sealed record UpdateReminderRequest(string? Title, string? Recurrence, DateTime? StartDate, string? NotifyPreset);
