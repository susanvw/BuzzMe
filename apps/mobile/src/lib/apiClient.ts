import { BuzzMeApiClient } from "@buzzme/api-client";

/**
 * The one shared client instance the app uses. Token storage/refresh wiring (via
 * expo-secure-store) is added alongside the auth screens — not part of the foundation.
 */
export const apiClient = new BuzzMeApiClient({
  baseUrl: process.env.EXPO_PUBLIC_API_BASE_URL ?? "http://localhost:5000/v1",
  getAccessToken: () => null,
});
