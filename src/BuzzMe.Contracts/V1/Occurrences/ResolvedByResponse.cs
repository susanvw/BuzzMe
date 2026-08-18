namespace BuzzMe.Contracts.V1.Occurrences;

/// <summary>API_CONTRACT.md §3's nested `resolvedBy` shape on the Occurrence resource — `userId`, `displayName`.</summary>
public sealed record ResolvedByResponse(Guid UserId, string? DisplayName);
