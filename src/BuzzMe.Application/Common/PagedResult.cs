namespace BuzzMe.Application.Common;

/// <summary>
/// The Application-layer shape behind every cursor-paginated list use case
/// (API_CONTRACT.md §7). Mapped onto Contracts' wire-level pagination envelope at the
/// Api boundary — this type never crosses that boundary itself.
/// </summary>
public sealed record PagedResult<TItem>(IReadOnlyList<TItem> Items, string? NextCursor);
