namespace BuzzMe.Contracts.V1.Common;

/// <summary>
/// The single-resource response envelope used by every endpoint in API_CONTRACT.md §5 —
/// exactly one shape for success (`Data` set, `Error` null) or failure (`Error` set,
/// `Data` null). A client never needs a different parser for a different endpoint.
/// </summary>
public sealed record ApiResponse<TData>(TData? Data, ApiError? Error)
{
    public static ApiResponse<TData> Ok(TData data) => new(data, null);

    public static ApiResponse<TData> Fail(ApiError error) => new(default, error);
}

/// <summary>The list-response envelope — API_CONTRACT.md §3/§7. Same success/error shape, plus pagination.</summary>
public sealed record ApiListResponse<TItem>(IReadOnlyList<TItem>? Data, PaginationInfo? Pagination, ApiError? Error)
{
    public static ApiListResponse<TItem> Ok(IReadOnlyList<TItem> data, PaginationInfo pagination) =>
        new(data, pagination, null);

    public static ApiListResponse<TItem> Fail(ApiError error) => new(null, null, error);
}
