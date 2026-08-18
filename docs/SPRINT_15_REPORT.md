# Sprint 15 Report — Complete / Dismiss / Reopen Occurrence

*Every sprint since 3 that touched Occurrence named the same boundary: generation and reads existed, but "the core 'mark as done' loop of the whole app" (the very reason a reminder app exists) had no Application methods and no endpoints — `OccurrenceApplicationService`'s own doc comment called it out by name as out of scope. This sprint closes it: three new domain transitions, one new repository write path with real optimistic concurrency, and the first three Occurrence endpoints this codebase has ever exposed.*

---

## 1. Repository Changes

**Domain** — `Occurrence` gained `Complete(userId, resolvedAt)`, `Dismiss(userId, resolvedAt)`, `Undo(undoneAt)`, and a derived `IsResolved` property. Complete/Dismiss share one idempotency rule: "already resolved" (by either action, by anyone) is a no-op — the first valid resolution wins, and a later, *different* resolution attempt never overwrites it. `Undo` restores `Due` or `Scheduled` depending on whether `DueAt` has passed "now," since nothing tracks which of the two an Occurrence was in before being resolved. Three new events: `OccurrenceCompleted`, `OccurrenceDismissed`, `OccurrenceUndone`.

**Infrastructure** — `IOccurrenceRepository.UpdateAsync`, a version-checked full-aggregate replace throwing `ConcurrencyConflictException` on a stale write — the exact same shape `BuzzRepository.UpdateAsync` established in Sprint 6, now Occurrence's second real exercise of `AggregateRoot.Version` as an enforced optimistic-concurrency gate.

**Application** — `OccurrenceApplicationService` gained `CompleteOccurrenceAsync`, `DismissOccurrenceAsync`, `ReopenOccurrenceAsync`, sharing a `LoadResolvableOccurrenceAsync` helper (existence + Board-membership → `404`, parent-Reminder-Deleted → `410 Gone`, and — new this sprint — the path's outer `reminderId` segment must match the Occurrence's actual `ReminderId` or it's treated as `404` too, since API_CONTRACT.md §1 itself notes the nesting exists for URL readability, not because the Occurrence aggregate boundary requires it). `expectedVersion` mismatches (whether caught before the write or via a genuine `ConcurrencyConflictException` race during it) are modeled as a `Result` **success** carrying a new `OccurrenceResolutionResult(Occurrence, VersionConflict)` — APPLICATION_LAYER_SPEC.md §3.8's own words call this outcome "not an error," even though the wire-level status is `409`; a plain `Result<T>` failure has no way to carry a value, which is why this couldn't just be `Error.Conflict`. Reopen's own rejections (not-currently-resolved, past the 24-hour grace window) are genuine `Result` failures — APPLICATION_LAYER_SPEC.md §3.8's Idempotency row names only "a second Complete/Dismiss call," conspicuously not Reopen, so no idempotent-no-op treatment was invented for it.

**Contracts/Api** — `ResolveOccurrenceRequest { expectedVersion }` (shared by all three actions, matching API_CONTRACT.md §5's literal body), `OccurrenceResponse` (`id`, `reminderId`, `dueAt`, `status`, nested `resolvedBy { userId, displayName }`, `resolvedAt`, plus `version` — see §3 below), new `OccurrenceEndpoints.cs` mapping `POST /v1/reminders/{reminderId}/occurrences/{occurrenceId}/{complete,dismiss,reopen}`, nested two levels deep exactly as API_CONTRACT.md §1 specifies. `200` for a genuine transition or a same-version idempotent replay; `409` (with the resolved Occurrence still in the body) for the version-mismatch race; `410` for a deleted parent Reminder.

---

## 2. A Bug Caught Before It Shipped

### 2.1 The returned `version` was one behind what was actually persisted

`OccurrenceRepository.UpdateAsync` increments `Version` only on the *stored document* — the domain `Occurrence` instance handed to it is never mutated back (repositories don't reach into `AggregateRoot`'s protected setter, same reasoning `BuzzRepository.UpdateAsync` already lived with, just never surfaced since nothing ever read `Buzz.Version` back out through a response). The first draft of `ResolveOccurrenceAsync`/`ReopenOccurrenceAsync` built their success response from that same stale in-memory instance — a client completing an Occurrence at `version 0` would be told the new state was still `version 0`, and their very next call (a natural idempotent retry using the version they were just given) would immediately fail as a stale-version conflict against the real database, which had already moved to `version 1`. Caught by `OccurrenceEndpointsTests.CompleteOccurrence_MarksItCompletedAndReturnsTheUpdatedOccurrence`'s own version assertion, not discovered later. Fixed by re-fetching the Occurrence after a successful write and building the response from that copy instead.

---

## 3. Test Results

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **168/168** (159 prior + 9 new: `Complete`/`Dismiss` set state and raise events, idempotent against each other and themselves, `Undo` restores `Due`/`Scheduled` correctly and throws when nothing is resolved, `IsResolved`) |
| `BuzzMe.Application.Tests` | **146/146** (131 prior + 15 new: Complete/Dismiss/Reopen — success, idempotent replay, a Complete after a Dismiss doesn't override it, stale-version conflict, `410` for a deleted parent Reminder, `404` for a non-member/nonexistent Occurrence/mismatched `reminderId`, Reopen's own `409`/`403`/grace-window cases) |
| `BuzzMe.Infrastructure.IntegrationTests` | **80/80** (78 prior + 2 new: `UpdateAsync` persists a resolution and increments Version, throws `ConcurrencyConflictException` on a stale write — real MongoDB, mirroring `BuzzRepositoryTests`' own Sprint 6 pattern exactly) |
| `BuzzMe.Api.IntegrationTests` | **96/96** (86 prior + 10 new: all three actions end to end — success, idempotent repeat, version-conflict-returns-current-state, not-resolved-cannot-reopen, deleted-parent-Reminder `410`, non-member `404`, mismatched-path `404`, unauthenticated — real host + real MongoDB) |

**490/490 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors** (the pre-existing `NU1903` SSH.NET/Testcontainers advisory, unrelated to this sprint, remains unchanged since SPRINT_9_REPORT.md).

---

## 4. Specification Interpretation Notes

### 4.1 `version` is not in API_CONTRACT.md §3's own Occurrence field list, but the mechanism it specifies needs it anyway

§3 lists the Occurrence resource as `id, reminderId, dueAt, status, resolvedBy, resolvedAt` — no `version`. But §5's Complete/Dismiss/Reopen row requires the client to send `{ expectedVersion }`, and gives no other channel for the client to have learned the current version in the first place. Resolved by adding `version` to `OccurrenceResponse` anyway: the alternative — a documented mechanism the client has no way to actually invoke past the very first call — would make §5's own literal requirement unusable. Favored the more implementation-necessary reading over the incomplete field list, same reasoning this session has applied to other table-vs-mechanism gaps (Sprint 14's Rename Board `400`).

### 4.2 Reopen restores `Due` or `Scheduled` by recomputing from `DueAt`, not from a stored prior state

Neither spec says what status Reopen should restore. `Occurrence` never recorded which of `Scheduled`/`Due` it was in before being resolved (nothing in this codebase transitions `Scheduled → Due` at all yet — see gap 5.2). Resolved by computing it fresh at Undo time: `Due` if `now >= DueAt`, otherwise `Scheduled` — self-consistent, requires no new stored field, and matches "what this Occurrence's status would be right now if it weren't resolved."

### 4.3 `Missed` is left unhandled by Complete/Dismiss, because nothing can produce it yet

Neither spec states whether a `Missed` Occurrence can still be Completed/Dismissed after the fact. Not special-cased here: the Missed-transition sweep worker (§5.2) doesn't exist in this codebase, so `Status` can never actually be `Missed` in production today. Complete/Dismiss's own "already resolved" guard checks only `Completed`/`Dismissed`; a hypothetical `Missed` Occurrence would fall through to a normal fresh transition, which is a reasonable default (late-completing a missed reminder is ordinary product behavior) but genuinely untested, since there's no path to construct that state.

---

## 5. Specification Gaps

### 5.1 No plain Get/List Occurrence endpoints — unchanged since Sprint 3

`GetOccurrenceAsync`/`ListOccurrencesAsync` still have no route; API_CONTRACT.md's endpoint catalogue has never named one. Not addressed here — this sprint's own selection was specifically the three action endpoints, and a client can render an Occurrence entirely from a Reminder's own `nextOccurrence` field or from a Complete/Dismiss/Reopen response body.

### 5.2 No Scheduled → Due transition exists, and no Missed-transition sweep worker exists

Both flagged since Sprint 3's own doc comments; still true. An Occurrence generated today stays `Scheduled` forever unless resolved — nothing in this codebase ever flips it to `Due` as its `DueAt` passes, and nothing ever flips an unresolved, past-grace-window Occurrence to `Missed`. IMPLEMENTATION_SPEC.md §1 names this as its own periodic sweep (§5's 24-hour grace window, hourly check) — a genuinely new background worker, out of scope for this sprint.

### 5.3 Cancelling a still-pending Buzz on resolution is not implemented

APPLICATION_LAYER_SPEC.md §3.8 names this as a "separate, policy-driven step" — eventually consistent, not part of the same transaction. No domain-event dispatcher exists anywhere in this codebase yet (`AggregateRoot.DomainEvents` is collected but never published, confirmed by grep across Application/Infrastructure/Workers) — the same standing gap Sprint 6's own Buzz-cancellation notes and Sprint 10's report both already named for other event-driven policies. `OccurrenceCompleted`/`OccurrenceDismissed` are raised correctly and are ready for a future dispatcher to consume; nothing consumes them today.
