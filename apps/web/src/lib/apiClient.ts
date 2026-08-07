import { BuzzMeApiClient } from "@buzzme/api-client";

/**
 * The one shared client instance the app uses (INFORMATION_ARCHITECTURE.md's IA is one
 * app talking to one /v1 surface). Token storage/refresh wiring is added alongside the
 * auth screens — not part of the foundation.
 */
export const apiClient = new BuzzMeApiClient({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000/v1",
  getAccessToken: () => null,
});
