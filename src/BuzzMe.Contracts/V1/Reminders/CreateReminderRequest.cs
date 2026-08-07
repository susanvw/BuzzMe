namespace BuzzMe.Contracts.V1.Reminders;

/// <summary>API_CONTRACT.md §5 — POST /v1/boards/{boardId}/reminders request body. Recurrence/NotifyPreset are plain strings, not C# enums, matching the wire codes exactly (ErrorCode's precedent, Sprint 1).</summary>
public sealed record CreateReminderRequest(string Title, string Recurrence, DateTime StartDate, string NotifyPreset);
