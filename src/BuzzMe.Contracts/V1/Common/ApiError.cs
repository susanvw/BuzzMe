namespace BuzzMe.Contracts.V1.Common;

/// <summary>The `error` object inside every non-success response envelope — API_CONTRACT.md §3.</summary>
public sealed record ApiError(string Code, string Message, IReadOnlyList<string>? Details = null);
