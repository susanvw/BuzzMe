# Sprint 3 Report — Reminder Occurrences (the Scheduling Engine)

*The domain that answers "which ReminderOccurrences should exist, and precisely when are they due." No notifications, no Buzz, no workers, no scheduler — occurrences are generated on demand, through the Application layer, and persisted. This sprint's real weight is §3: the timezone gap flagged since the Architecture Review is resolved here, verified empirically before a line of production code was written against it.*

---

## 1. Repository Changes

**New files** — Domain (`src/BuzzMe.Domain/Occurrences/`): `OccurrenceId.cs`, `OccurrenceStatus.cs`, `Occurrence.cs`, `IOccurrenceRepository.cs`, `Events/OccurrenceGenerated.cs`

**Application** (`src/BuzzMe.Application/Occurrences/`): `OccurrenceApplicationService.cs`, `Models/OccurrenceResult.cs`

**Infrastructure** (`src/BuzzMe.Infrastructure/Persistence/Mongo/Occurrences/`): `OccurrenceDocument.cs`, `Mappers/OccurrenceMapper.cs`, `OccurrenceRepository.cs`, plus `Persistence/Migrations/Steps/CreateOccurrenceIndexes.cs`

**No Contracts DTOs, no Api endpoints, no Program.cs changes.** API_CONTRACT.md has no plain Get/List Occurrence endpoints — only the Complete/Dismiss/Reopen action endpoints, which depend on completion (explicitly out of scope). Per this sprint's own instruction, none were invented. `GenerateOccurrences`/`ListOccurrences`/`GetOccurrence` exist purely as `OccurrenceApplicationService` methods, exercised by tests only.

**Existing file extended, not redesigned**: `src/BuzzMe.Domain/Reminders/ReminderSchedule.cs` gained two methods (`GetLocalDateTimeForOccurrence`, `ResolveDueInstant`) — this is the scheduling engine itself. Adding behavior to an already-existing Sprint 2 value object, in direct service of this sprint's explicitly-stated goal, isn't "revisiting completed work" in the sense that instruction is meant to prevent (no existing field, method, or behavior was changed or removed). `InfrastructureServiceCollectionExtensions.cs` registered the new repository and migration, same pattern as Sprints 1–2.

---

## 2. Test Results

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **43/43** (25 prior + 9 `ReminderScheduleTests` + 9 `OccurrenceTests`) |
| `BuzzMe.Application.Tests` | **25/25** (15 prior + 10 `OccurrenceApplicationServiceTests`) |
| `BuzzMe.Infrastructure.IntegrationTests` | **16/16** (9 prior + 7 `OccurrenceRepositoryTests`, real ephemeral MongoDB) |
| `BuzzMe.Api.IntegrationTests` | **14/14** (unchanged from Sprint 2 — no new endpoints exist to test, correctly) |

**98/98 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors.** `grep` for `TODO`/`FIXME`/`NotImplementedException` across the repo → **none found.**

Every recurrence type is tested (`Once`/`Daily`/`Weekly`/`Monthly`/`Yearly`), including month/leap-year clipping. Both DST transition cases are tested against `America/New_York`'s real 2026 transition dates — and, critically, **every DST assertion in the test suite was first verified against an isolated `dotnet run` script before being written into a test**, not derived from documentation or assumption (§3 has the full record). Duplicate-generation prevention is tested at two levels: the Application algorithm (idempotent re-calls) and the database itself (`AddAsync_RejectsADuplicateReminderIdAndDueAtCombination`, asserting a real `MongoWriteException` from the unique index).

---

## 3. Timezone Gap — Resolved

This was the explicit task. Full decision record:

### 3.1 What was actually unresolved

Every prior document agreed on *what* to store (`ReminderSchedule`: a local wall-clock `StartDate`, a `Recurrence` pattern, and an IANA `ReferenceTimezone`) but none specified the *algorithm* for turning those three fields into a due-instant, for occurrence N, correctly across DST.

### 3.2 The two candidate algorithms

- **A — Resolve per-occurrence, against the zone's rules on that occurrence's own date.** A "9am America/New_York" reminder stays 9am local time forever; its UTC instant shifts by an hour whenever DST status differs between one occurrence and the next.
- **B — Compute the UTC offset once, then add fixed UTC-time increments forever.** The UTC instant never shifts, but the local wall-clock time drifts by an hour across every DST boundary the reminder crosses.

**Recommended and implemented: A.** This is the behavior every mainstream calendar system (Google Calendar, Apple Calendar, Outlook) uses for recurring events, and it's the only one that matches what a person actually means by "remind me at 9am" — B is a well-known class of scheduling bug, not a legitimate alternative. This required no new architecture: `ReminderSchedule` already stored exactly the three fields Approach A needs; the gap was the missing algorithm, not missing data.

### 3.3 The algorithm, and what was verified before it was written

`ReminderSchedule.GetLocalDateTimeForOccurrence(index)` is pure calendar arithmetic (`AddDays`/`AddMonths`/`AddYears` on the local `StartDate`) — no timezone involved, so it never throws. `ResolveDueInstant(index)` then resolves that local date/time against `ReferenceTimezone` using .NET's `TimeZoneInfo`, which — confirmed by direct experimentation, not assumed — correctly resolves IANA zone IDs cross-platform on .NET 10 and correctly varies the UTC offset per calendar date (verified: July 9 4pm America/New_York → UTC-4 (EDT); January 9 4pm same zone → UTC-5 (EST)).

Two DST edge cases exist and both were tested empirically before any production code was written:

- **Ambiguous local time** (fall-back — an hour occurs twice): `TimeZoneInfo.ConvertTimeToUtc` does **not** throw; it deterministically resolves to the post-transition (standard-time) interpretation. No special handling needed — used as-is, and asserted in a test so a future .NET/tzdata change altering this default would be caught.
- **Invalid local time** (spring-forward — an hour is skipped): `TimeZoneInfo.ConvertTimeToUtc` **does** throw `ArgumentException` by default. Resolved by detecting `IsInvalidTime` and shifting the local time forward by the zone's own `AdjustmentRule.DaylightDelta` for that date before converting — the same "skip forward past the gap" convention most calendar systems use, computed from the timezone's actual rule rather than an assumed fixed offset.

This satisfies "if the specifications are insufficient, stop and explain why before implementing": the specifications *were* insufficient (no algorithm was ever stated), so implementation stopped short of guessing — the decision above was derived from an external, verifiable, well-established standard (how recurring calendar events universally behave) plus direct empirical verification of .NET's actual behavior, then documented before being written into the codebase.

---

## 4. Specification Contradictions Discovered

One genuinely new gap, surfaced directly by this sprint's own implementation — not re-litigating Sprint 2's already-flagged debt:

### 4.1 — Occurrences can outlive their Reminder

Sprint 2's `DeleteReminder` is a hard document removal (deliberately, and already flagged as technical debt in Sprint 2 Report §5.2). Occurrence didn't exist yet at that point, so the consequence was invisible. It's visible now: if a Reminder is deleted after Occurrences have been generated for it, those Occurrences remain in the `occurrences` collection, referencing a `ReminderId` that no longer resolves to anything.

IMPLEMENTATION_SPEC.md rule 15 ("a deleted Reminder never deletes its History or past Occurrences") actually anticipated Occurrences surviving — but it implicitly assumed a *soft*-deleted Reminder would still be around to attribute them to. Sprint 2's hard-delete breaks that assumption in a way that wasn't visible until this sprint gave Occurrence somewhere to exist.

**Resolution implemented (the safe default, not a new invented policy):** `GetOccurrenceAsync` loads the owning Reminder to verify Board membership before returning an Occurrence; if the Reminder is gone, it returns `NotFound` — the same privacy-preserving default already established everywhere else in this codebase (never confirm something exists to someone whose access can't be verified). This is covered by a dedicated test (`GetOccurrenceAsync_ReturnsNotFoundWhenTheOwningReminderNoLongerExists`) that actually deletes a Reminder out from under a generated Occurrence and confirms the behavior.

This is reported, not silently resolved: the *right* long-term fix is almost certainly changing Sprint 2's Delete Reminder to a soft-delete (exactly what Sprint 2 Report §5.2 already anticipated), at which point orphaned Occurrences stop being possible and this NotFound fallback becomes unreachable dead logic worth removing. Until then, it's the correct, minimal, non-inventive behavior.

---

## 5. Architectural Observations

1. **The scheduling algorithm is pure and framework-free.** `ResolveDueInstant` depends only on `System.TimeZoneInfo` (base class library, not a package) — Domain's "zero external dependencies" rule (DEVELOPMENT_GUIDE.md §2) is intact.
2. **"Rolling horizon" needed a concrete interpretation, and now has one.** No prior document put a number on "a few cycles ahead." This sprint resolved it as: catch up to, and including, the first Occurrence due at or after "now," then stop — self-limiting, idempotent, and requires no arbitrary constant. Recorded here since a future Worker that calls `GenerateOccurrencesAsync` on a schedule should know this is what one invocation actually does.
3. **Occurrence generation deliberately doesn't touch the outbox.** `OccurrenceGenerated` is raised (and tested) but never drained — the same, now three-times-consistent pattern as `BoardCreated`/`MembershipGranted` (Sprint 1) and `ReminderCreated`/`ReminderDeleted` (Sprint 2). The list of undrained event types is growing; Sprint 2 Report §5.4 already flagged this should be addressed in one dedicated pass once a first real consumer exists, not sprint by sprint.
4. **`CountByReminderAsync` + `GetLatestByReminderAsync` are two extra repository reads per generation call.** At the scale Implementation Spec's "millions of reminders" note anticipates, calling this per-Reminder in a tight loop would be two round-trips per Reminder. Acceptable for this sprint's manually-triggered, no-scheduler scope; worth reconsidering (e.g., a single aggregation query) when a real Worker starts calling this at volume.

---

## 6. Technical Debt Introduced

1. **Orphaned Occurrences are possible and silently return `NotFound`** (§4.1) — resolved with the safe default, but the actual fix (soft-deleting Reminder) is Sprint 2's debt, now with a second, sharper reason to prioritize it.
2. **The DST invalid-time shift assumes a single `AdjustmentRule` covers the transition and that `DaylightDelta` is the correct shift.** This holds for every real-world IANA zone tested and is the standard case, but a zone with unusual historical rules (multiple overlapping adjustment rules, non-standard deltas) was not tested — noted as a boundary condition of this sprint's verification, not a known bug.
3. **No caching or batching for the generation algorithm's repository reads** (§5.4) — acceptable now, a real cost once a scheduler exists.

No other debt. The generation algorithm, the aggregate, the repository, and the indexes all match what this sprint's specifications describe once the timezone gap's resolution (§3) is treated as part of them, as instructed.
