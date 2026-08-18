namespace BuzzMe.Contracts.V1.Occurrences;

/// <summary>API_CONTRACT.md §5 — the shared request body for Complete/Dismiss/Reopen: `{ expectedVersion }`, the optimistic-concurrency check named in the Implementation/Application Layer specs.</summary>
public sealed record ResolveOccurrenceRequest(long ExpectedVersion);
