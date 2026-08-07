import type { ErrorCode } from "@buzzme/domain-constants";

/** API_CONTRACT.md §3 — the one error shape every endpoint returns, no exceptions. */
export interface ApiError {
  code: ErrorCode;
  message: string;
  details?: string[];
}

/** API_CONTRACT.md §3 — the single-resource success/error envelope. */
export interface ApiResponse<TData> {
  data: TData | null;
  error: ApiError | null;
}

/** API_CONTRACT.md §7 — the cursor-pagination shape, identical on every list endpoint. */
export interface PaginationInfo {
  nextCursor: string | null;
}

/** API_CONTRACT.md §3/§7 — the list-response envelope. */
export interface ApiListResponse<TItem> {
  data: TItem[] | null;
  pagination: PaginationInfo | null;
  error: ApiError | null;
}
