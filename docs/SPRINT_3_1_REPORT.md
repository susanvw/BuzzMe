# Sprint 3.1 Report — Reminder Soft Delete (Architectural Alignment)

*Not a new feature. This sprint corrects Sprint 2's `DeleteReminder` from a hard document removal to the soft delete (`DeletedAt` marker) that [REMINDER_LIFECYCLE_REVIEW.md](REMINDER_LIFECYCLE_REVIEW.md) and [SOFT_DELETE_IMPACT_REVIEW.md](SOFT_DELETE_IMPACT_REVIEW.md) independently concluded was always the specified architecture — DEVELOPMENT_GUIDE.md §6 was the one place that had drifted from it. No product decisions were made here; both reviews already made them. This report covers only what changed to implement them.*

---

## 1. Repository Changes

**`src/BuzzMe.Domain/Reminders/Reminder.cs`** — added `DateTimeOffset? DeletedAt` and `bool IsDeleted => DeletedAt is not null`. `Delete(DateTimeOffset deletedAt)` now sets `DeletedAt` and raises `ReminderDeleted` instead of being a request to remove the document. `Rehydrate` takes the persisted `DeletedAt`.

**`src/BuzzMe.Domain/Reminders/IReminderRepository.cs`** — `DeleteAsync` replaced with `MarkDeletedAsync(id, deletedAt, ct)`, a targeted update, not a removal. Added `GetByIdIncludingDeletedAsync(id, ct)` for the two call sites that must find a Reminder regardless of `DeletedAt` (Occurrence historical reads, and Delete's own idempotency check). `GetByIdAsync`/`ListByBoardAsync` doc comments now state plainly that they exclude soft-deleted Reminders.

**`src/BuzzMe.Infrastructure/Persistence/Mongo/Reminders/ReminderDocument.cs`** — added `DateTimeOffset? DeletedAt` (nullable, additive).

**`src/BuzzMe.Infrastructure/Persistence/Mongo/Reminders/Mappers/ReminderMapper.cs`** — maps `DeletedAt` both directions.

**`src/BuzzMe.Infrastructure/Persistence/Mongo/Reminders/ReminderRepository.cs`** — `GetByIdAsync`/`ListByBoardAsync` now filter `DeletedAt == null`. `MarkDeletedAsync` uses `UpdateOneAsync` with `Set(d => d.DeletedAt, deletedAt)` — the document is never removed. `GetByIdIncludingDeletedAsync` added, unfiltered.

**`src/BuzzMe.Application/Reminders/ReminderApplicationService.cs`** — `DeleteReminderAsync` now: loads the Reminder via `GetByIdIncludingDeletedAsync` (so an already-deleted Reminder is found, not reported missing); checks Board membership as before; if already deleted, returns `Result.Success()` as a no-op — API_CONTRACT.md §5's specified "already-deleted → 204" idempotency; otherwise calls `reminder.Delete(clock.UtcNow)` then `MarkDeletedAsync`. `CreateReminderAsync`, `GetReminderAsync`, `ListBoardRemindersAsync` needed no changes — they already call the (now-filtering) `GetByIdAsync`/`ListByBoardAsync`.

**`src/BuzzMe.Application/Occurrences/OccurrenceApplicationService.cs`** — `GetOccurrenceAsync` and `ListOccurrencesAsync` now load the owning Reminder via `GetByIdIncludingDeletedAsync`, so reading a historical Occurrence no longer fails just because its Reminder was later deleted — this directly closes the orphaned-Occurrence gap SPRINT_3_REPORT.md §3 flagged under Sprint 2's hard delete. `GenerateOccurrencesAsync` was deliberately left unchanged: it still calls the filtering `GetByIdAsync`, so a soft-deleted Reminder correctly stops producing new Occurrences without any new logic — the existing filter does this for free.

**Writes review (Update/Complete/Dismiss/Reopen/Occurrence-resolution actions):** none of these exist in any sprint yet — there is nothing to correct for them. Stated explicitly so this is recorded as checked, not overlooked.

---

## 2. Migration

**None required.** `DeletedAt` is an additive nullable field on an existing document shape; Mongo has no schema to migrate, and every pre-existing document is implicitly "active" (`DeletedAt` absent, which the `d.DeletedAt == null` filter and the C# `Reminder.IsDeleted` both treat identically to an explicit `null`). The existing indexes (`ReminderId`, `BoardId`) remain valid unchanged — no index was defined over `DeletedAt`, and none is needed at this data volume (`Find` + `.Eq(..., null)` is a normal filtered query, not a missing-index scan concern worth addressing yet). There is no production data to backfill — this is pre-launch.

---

## 3. Updated Tests

| File | Change |
|---|---|
| `tests/BuzzMe.Application.Tests/TestDoubles/InMemoryReminderRepository.cs` | Rewritten to mirror the real repository: `GetByIdAsync`/`ListByBoardAsync` filter out `IsDeleted` Reminders; `MarkDeletedAsync` replaces `DeleteAsync`, added `GetByIdIncludingDeletedAsync`. |
| `tests/BuzzMe.Application.Tests/Occurrences/OccurrenceApplicationServiceTests.cs` | `GetOccurrenceAsync_ReturnsNotFoundWhenTheOwningReminderNoLongerExists` renamed to `GetOccurrenceAsync_StillReturnsTheOccurrenceWhenTheOwningReminderIsSoftDeleted` and its expected outcome flipped from failure/`NOT_FOUND` to success — this is the one intentionally-changed test outcome in this sprint, and it changes because the underlying behavior is now correct, not because an assertion was weakened: reading a historical Occurrence must not fail just because its Reminder was later deleted, per both prior reviews. |
| `tests/BuzzMe.Application.Tests/Reminders/ReminderApplicationServiceTests.cs` | Added `DeleteReminderAsync_SucceedsAsANoOpWhenTheReminderIsAlreadyDeleted` — the already-deleted → success idempotency case had no prior test. `DeleteReminderAsync_RemovesTheReminder` and `_ReturnsNotFoundForAReminderThatDoesNotExist` needed no changes — their assertions (delete succeeds, then `GetReminderAsync` returns `NOT_FOUND`) hold identically under soft delete. |
| `tests/BuzzMe.Infrastructure.IntegrationTests/Reminders/ReminderRepositoryTests.cs` | `DeleteAsync_RemovesTheReminder` renamed to `MarkDeletedAsync_ExcludesTheReminderFromNormalReadsButPreservesTheDocument` and extended: still asserts `GetByIdAsync` returns null, and now also asserts `GetByIdIncludingDeletedAsync` finds the document with `DeletedAt` set — proving this is exclusion, not removal. Added `MarkDeletedAsync_IsIdempotentWhenCalledAgainOnAnAlreadyDeletedReminder` and `ListByBoardAsync_ExcludesSoftDeletedReminders`, both previously untested. |
| `tests/BuzzMe.Api.IntegrationTests/Reminders/ReminderEndpointsTests.cs` | No changes. Read and confirmed: `DeleteReminder_RemovesItAndItIsNoLongerReturned` asserts `204` on delete, `404` on subsequent GET, and exclusion from the list — all true identically under soft delete, since the API surface was never distinguishing hard vs. soft delete in the first place. |

No test had an assertion weakened. The one changed outcome is explained above.

---

## 4. Regression Results

```
BuzzMe.Domain.Tests                 43/43
BuzzMe.Application.Tests            26/26  (25 prior + 1 new: idempotent re-delete)
BuzzMe.Infrastructure.IntegrationTests  18/18  (16 prior + 2 new: idempotent MarkDeletedAsync, ListByBoardAsync excludes soft-deleted)
BuzzMe.Api.IntegrationTests         14/14  (unchanged)
```

**101/101 total** (98 prior + 3 new tests; the orphan-Occurrence test's outcome flip does not change the total). `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors**. Integration tests ran against real, ephemeral MongoDB via Testcontainers — no repository was mocked.

---

## 5. Remaining Implementation Differences from the Architecture

None found. Domain, Application, Infrastructure, and their tests now match what IMPLEMENTATION_SPEC.md §1, API_CONTRACT.md §5–6, and both review documents describe.

One documentation-only inconsistency remains, outside this sprint's "implementation only" scope: **DEVELOPMENT_GUIDE.md §6** still states that Reminder does *not* get a soft-delete grace period like Board/User — this is the exact statement REMINDER_LIFECYCLE_REVIEW.md identified as the point where the architecture's documented description drifted from its own specified intent. The code and tests now correctly reflect the original intent; §6's wording does not. Flagging it here rather than editing it, since correcting product-facing documentation wasn't part of this sprint's brief.
