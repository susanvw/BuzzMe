# Sprint 16 Report — Update Reminder

*Create/Get/List/Delete Reminder have existed since Sprint 2; the one Reminder mutation nothing ever built was the edit itself. Smaller in surface area than Sprint 15, but the literal `boardId`-rejection requirement in API_CONTRACT.md's own words ("not silently ignored") turned out to need a genuine mechanism, not just a missing DTO field.*

---

## 1. Repository Changes

**Domain** — `Reminder.Update(title, schedule, notifyPreset, updatedAt)`: the Application layer resolves the caller's partial request into fully-resolved target values first (falling back to the current value for anything omitted), so this method only ever sees complete values and decides which of three independent field groups changed — title/date share one event (`ReminderUpdated`), recurrence gets its own (`RecurrenceRuleUpdated`), notify preset its own (`NotifyPresetUpdated`), since each drives a different downstream policy per IMPLEMENTATION_SPEC.md §1/APPLICATION_LAYER_SPEC.md §7. Re-applying identical values raises nothing and leaves `UpdatedAt` untouched — a full no-op, matching API_CONTRACT.md §5's own stated idempotency rule.

**Infrastructure** — `IReminderRepository.UpdateAsync`, a version-checked full-aggregate replace throwing `ConcurrencyConflictException` on a stale write — the same shape Board/Occurrence/Buzz's own `UpdateAsync` already established, and the first time this codebase has ever needed a full Reminder replace (every prior Reminder write path was either an insert or the single-field `MarkDeletedAsync`).

**Application** — `ReminderApplicationService.UpdateReminderAsync`: loads the Reminder (deleted-excluding, same as `GetReminderAsync`), then the Board **including deleted**, with an explicit `board.IsDeleted` check returning `409 Conflict` — not the deleted-excluding `GetByIdAsync` every other Reminder method here uses, because APPLICATION_LAYER_SPEC.md §0's own gap-closing note is explicit that a deleted Board's Membership rows may still read as Active during the soft-delete grace window, so "Board not found" (404) and "Board deleted" (409) must stay distinguishable. Each nullable field is parsed/validated only when present; `ReferenceTimezone` is always carried through unchanged (it's not one of the four editable fields).

**Contracts/Api** — `UpdateReminderRequest { title?, recurrence?, startDate?, notifyPreset? }`, deliberately with no `BoardId` property. `PATCH /v1/reminders/{reminderId}` in `ReminderEndpoints.cs`, returning `200` with the updated Reminder — matches `GetReminder`'s own response shape.

---

## 2. The `boardId`-in-body Requirement — a Real Mechanism, Not Just an Absent Field

API_CONTRACT.md §5's Update Reminder row is explicit: *"`boardId` is not an accepted field; present in payload → `400`, not silently ignored."* Omitting `BoardId` from `UpdateReminderRequest` alone doesn't satisfy this — System.Text.Json's default behavior for an unmapped JSON property is to drop it silently during normal typed-body binding, which is exactly the "silently ignored" outcome the spec rules out. Two mechanisms were considered:

- `[JsonUnmappedMemberHandling(Disallow)]` on the DTO, relying on ASP.NET Core's built-in "invalid JSON body → `400`" handling for Minimal API complex-body parameters. Rejected: the resulting `400` would come from the framework's own ProblemDetails-shaped error path, not this codebase's single `ApiResponse<T>` envelope (API_CONTRACT.md §3's own "every error response... uses the single envelope" rule), and it would also reject *any* unrecognized field, not specifically `boardId`.
- Parsing the raw request body as a `JsonDocument` before typed deserialization, checking case-insensitively for a `boardId` property, and only then deserializing the same JSON into `UpdateReminderRequest` (using the app's own registered `JsonSerializerOptions`, already available via DI). This is what shipped — explicit, keeps the app's own envelope for the error response, and was verified end to end by `UpdateReminder_WithABoardIdInTheBody_ReturnsValidationError` (an anonymous object with both `boardId` and `title` sent as one real HTTP request against the real host).

---

## 3. Test Results

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **174/174** (168 prior + 6 new: `Update` — title/date change raises `ReminderUpdated`, recurrence change raises only `RecurrenceRuleUpdated`, notify-preset change raises only `NotifyPresetUpdated`, all three together raise all three, identical values are a full no-op) |
| `BuzzMe.Application.Tests` | **156/156** (146 prior + 9 new: `UpdateReminderAsync` — each field editable independently, no-fields-provided no-op, invalid recurrence/notify-preset validation, not-found for a nonexistent Reminder and for a non-member, `409` when the owning Board is deleted) |
| `BuzzMe.Infrastructure.IntegrationTests` | **82/82** (80 prior + 2 new: `UpdateAsync` persists an edit and increments Version, throws `ConcurrencyConflictException` on a stale write — real MongoDB) |
| `BuzzMe.Api.IntegrationTests` | **103/103** (96 prior + 7 new: `PATCH /v1/reminders/{reminderId}` end to end — title-only update, no-op, the `boardId`-in-body `400` mechanism itself, invalid recurrence, non-member `404`, deleted-Board `409`, unauthenticated — real host + real MongoDB) |

**515/515 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors** (the pre-existing `NU1903` SSH.NET/Testcontainers advisory, unrelated to this sprint, remains unchanged since SPRINT_9_REPORT.md).

---

## 4. Specification Interpretation Notes

### 4.1 Board-Deleted is a pre-existing gap in `CreateReminderAsync`, not fixed here

API_CONTRACT.md's Create Reminder row lists the identical `409 (Board Deleted)` error, governed by the same APPLICATION_LAYER_SPEC.md §3.7 row as Update. `CreateReminderAsync` (Sprint 2) never implemented this distinction — it uses the deleted-excluding `GetByIdAsync`, so a deleted Board with stale-Active Membership rows currently produces a plain `404`, not the spec's own `409`. This sprint's own selection was specifically Update Reminder; fixing Create's identical, pre-existing gap was left alone rather than silently expanded into scope — flagged here as a real, confirmed defect for a future sprint, not a hypothetical one.

### 4.2 `ReferenceTimezone` stays out of Update entirely

Neither IMPLEMENTATION_SPEC.md nor API_CONTRACT.md's `{ title?, recurrence?, startDate?, notifyPreset? }` body lists `referenceTimezone` as editable — `ReminderSchedule`'s own doc comment already calls it "immutable after creation." `UpdateReminderAsync` always carries the Reminder's existing `ReferenceTimezone` through unchanged when constructing the new `ReminderSchedule`, never accepting it from the request.

---

## 5. Specification Gaps

### 5.1 Occurrence regeneration and Buzz rescheduling on Update are not implemented

IMPLEMENTATION_SPEC.md §1 states: if `recurrence` changed, regenerate only not-yet-generated future Occurrences; if `notifyPreset` changed, reschedule every not-yet-delivered Buzz for every not-yet-resolved Occurrence immediately. Both are named as separate, policy-driven steps (§7), not part of Update's own transaction. `RecurrenceRuleUpdated`/`NotifyPresetUpdated` are raised correctly and are ready for a future dispatcher to consume — but, as Sprint 15's own report (§5.3) already established, no domain-event dispatcher exists anywhere in this codebase yet, so nothing currently reacts to either event.

### 5.2 Board-Deleted 409 remains unimplemented for Create Reminder (restated from §4.1)

Grouped here as the standing gap it now provably is, not merely a hypothetical inconsistency.
