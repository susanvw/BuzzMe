namespace BuzzMe.Contracts.V1.Occurrences;

/// <summary>
/// API_CONTRACT.md §3's Occurrence resource: `id`, `reminderId`, `dueAt`, `status`,
/// `resolvedBy` (nested, nullable), `resolvedAt` (nullable). `Version` is not part of that
/// field list, but is required for the `{ expectedVersion }` mechanism API_CONTRACT.md §5
/// itself specifies for Complete/Dismiss/Reopen — see SPRINT_15_REPORT.md.
/// </summary>
public sealed record OccurrenceResponse(
    Guid Id, Guid ReminderId, DateTimeOffset DueAt, string Status, ResolvedByResponse? ResolvedBy, DateTimeOffset? ResolvedAt, long Version);
