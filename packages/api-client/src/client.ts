import type { ApiListResponse, ApiResponse } from "./envelopes";

export interface BuzzMeApiClientConfig {
  /** e.g. https://api.buzzme.app/v1 — versioning per API_CONTRACT.md §9. */
  baseUrl: string;
  /** Supplies the current Bearer access token; returns null when unauthenticated. */
  getAccessToken?: () => string | null;
}

/**
 * The one place every request is built and every envelope is parsed
 * (DEVELOPMENT_GUIDE.md §1 — "one shared, versioned client" is the reason this package
 * exists at all). Per-resource calls (createBoard, listBoards, ...) are added here as
 * each endpoint in API_CONTRACT.md §5 is implemented — none exist yet.
 */
export class BuzzMeApiClient {
  private readonly config: BuzzMeApiClientConfig;

  constructor(config: BuzzMeApiClientConfig) {
    this.config = config;
  }

  async get<TData>(path: string): Promise<ApiResponse<TData>> {
    return this.request<TData>(path, { method: "GET" });
  }

  async getList<TItem>(path: string): Promise<ApiListResponse<TItem>> {
    const response = await this.rawRequest(path, { method: "GET" });
    return (await response.json()) as ApiListResponse<TItem>;
  }

  async post<TData>(path: string, body?: unknown): Promise<ApiResponse<TData>> {
    return this.request<TData>(path, {
      method: "POST",
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  }

  async patch<TData>(path: string, body: unknown): Promise<ApiResponse<TData>> {
    return this.request<TData>(path, { method: "PATCH", body: JSON.stringify(body) });
  }

  async delete<TData>(path: string): Promise<ApiResponse<TData>> {
    return this.request<TData>(path, { method: "DELETE" });
  }

  private async request<TData>(path: string, init: RequestInit): Promise<ApiResponse<TData>> {
    const response = await this.rawRequest(path, init);
    return (await response.json()) as ApiResponse<TData>;
  }

  private async rawRequest(path: string, init: RequestInit): Promise<Response> {
    const token = this.config.getAccessToken?.();
    const headers: Record<string, string> = { "Content-Type": "application/json" };
    if (token) headers.Authorization = `Bearer ${token}`;

    return fetch(`${this.config.baseUrl}${path}`, { ...init, headers: { ...headers, ...init.headers } });
  }
}
