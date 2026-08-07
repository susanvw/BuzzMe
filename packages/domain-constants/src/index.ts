/**
 * The fixed vocabulary IMPLEMENTATION_SPEC.md §1 and API_CONTRACT.md §3 already decided on.
 * Not business logic — just the one place both apps read these values from, so neither can
 * quietly drift from the other on what counts as valid.
 */

export const RECURRENCE_OPTIONS = ["once", "daily", "weekly", "monthly", "yearly"] as const;
export type Recurrence = (typeof RECURRENCE_OPTIONS)[number];

export const NOTIFY_PRESETS = [
  "atTime",
  "15MinBefore",
  "1HourBefore",
  "8HoursBefore",
  "1DayBefore",
  "1WeekBefore",
] as const;
export type NotifyPreset = (typeof NOTIFY_PRESETS)[number];

export const MEMBERSHIP_ROLES = ["owner", "member"] as const;
export type MembershipRole = (typeof MEMBERSHIP_ROLES)[number];

export const OCCURRENCE_STATUSES = ["scheduled", "due", "completed", "dismissed", "missed"] as const;
export type OccurrenceStatus = (typeof OCCURRENCE_STATUSES)[number];

/** API_CONTRACT.md §6 — the full, closed vocabulary of `error.code` wire values. */
export const ERROR_CODES = {
  VALIDATION_ERROR: "VALIDATION_ERROR",
  UNAUTHORIZED: "UNAUTHORIZED",
  FORBIDDEN: "FORBIDDEN",
  NOT_FOUND: "NOT_FOUND",
  CONFLICT: "CONFLICT",
  GONE: "GONE",
  RATE_LIMITED: "RATE_LIMITED",
  SERVER_ERROR: "SERVER_ERROR",
} as const;
export type ErrorCode = (typeof ERROR_CODES)[keyof typeof ERROR_CODES];
