namespace BuzzMe.Contracts.V1.Common;

/// <summary>The `pagination` object on every list response — API_CONTRACT.md §7. Cursor-based, no exceptions.</summary>
public sealed record PaginationInfo(string? NextCursor);
