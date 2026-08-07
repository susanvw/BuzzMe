# Sprint 4 Report — Buzz Generation

*The domain that turns "an Occurrence is due" into "a persistent, per-recipient queue of pending notifications." No dispatch, no push provider, no retry — this sprint's output is the `buzzes` collection itself: one row per (Occurrence, recipient) pair, `Status = Scheduled`, waiting for a future delivery sprint to pick it up. `Reminder → Occurrence → Buzz` now all exist, in that order, in MongoDB.*

---

## 1. Repository Changes

**Domain** (`src/BuzzMe.Domain/Buzzes/`): `BuzzId.cs`, `BuzzStatus.cs` (full spec lifecycle enum — see §3.1), `Buzz.cs` (aggregate root), `IBuzzRepository.cs`, `Events/BuzzGenerated.cs`, `NotifyPresetLeadTimeExtensions.cs` (see §3.2).

**Application** (`src/BuzzMe.Application/Buzzes/`): `BuzzApplicationService.cs` (`GenerateBuzzesAsync`, `ListPendingBuzzesAsync`, `GetBuzzAsync`), `Models/BuzzResult.cs`.

**Infrastructure** (`src/BuzzMe.Infrastructure/Persistence/Mongo/Buzzes/`): `BuzzDocument.cs`, `Mappers/BuzzMapper.cs`, `BuzzRepository.cs`, plus `Persistence/Migrations/Steps/CreateBuzzIndexes.cs` (migration version 4).

**No Contracts DTOs, no Api endpoints, no Program.cs changes, no background worker.** Matches the brief's own scope list exactly — API_CONTRACT.md defines no plain Get/List Buzz endpoints (only the unrelated, delivery-side "Notification" in-app-fallback endpoint, which is explicitly out of scope: "does NOT deliver notifications"). `GenerateBuzzes`/`ListPendingBuzzes`/`GetBuzz` exist purely as `BuzzApplicationService` methods, exercised by tests only — same posture as Sprint 3's Occurrence application service.

**One existing file touched, additively only**: `InfrastructureServiceCollectionExtensions.cs` gained three registrations (`IMongoMigration, CreateBuzzIndexes`; `IBuzzRepository, BuzzRepository`) — the same unavoidable, purely-additive DI wiring every prior sprint has made to this exact file. No other Sprint 1–3.1 file was modified.

### 1.1 Generation algorithm

`GenerateBuzzesAsync(requestingUserId, occurrenceId)`:
1. Load the Occurrence (`occurrenceRepository.GetByIdAsync`) — not found → `NOT_FOUND`.
2. Load the owning Reminder via the Occurrence's `ReminderId`, using the **soft-delete-filtering** `GetByIdAsync` (Sprint 3.1) — a deleted Reminder is not found here, for free, with no new code. This is the "Reminder exists and is not deleted" rule.
3. Load the Board via the Reminder's `BoardId` — not found, or requester isn't a Member → `NOT_FOUND` (same privacy-preserving default used everywhere else in this codebase).
4. Read every existing Buzz for this Occurrence (`buzzRepository.ListByOccurrenceAsync`) and collect their recipient IDs.
5. For every current `Board.Membership` not already in that set, generate one Buzz and persist it.

Step 5 is what makes this idempotent and duplicate-free without a special "has this Occurrence already generated Buzzes at all" flag: on a second call with no membership change, every recipient is already in the existing-recipient set, so nothing is generated — a true no-op. If a new Member had joined in between (not actually possible yet — see §4.2), the same call would correctly backfill only their Buzz, which is what "generate exactly one Buzz per active Member" requires, not merely "generate once and never again."

---

## 2. Test Results

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **60/60** (43 prior + 17 `BuzzTests`: construction, `BuzzGenerated` event, full `BuzzStatusCodes` round-trip, `NotifyPreset.ToLeadTime()` for all six presets) |
| `BuzzMe.Application.Tests` | **36/36** (26 prior + 10 `BuzzApplicationServiceTests`: generation, idempotency, `ScheduledAt` computation, unknown-Occurrence, deleted-Reminder, non-member requester, `GetBuzz` recipient-only access, `ListPendingBuzzes` per-recipient isolation) |
| `BuzzMe.Infrastructure.IntegrationTests` | **25/25** (18 prior + 7 `BuzzRepositoryTests`, real ephemeral MongoDB via Testcontainers) |
| `BuzzMe.Api.IntegrationTests` | **14/14** (unchanged — no new endpoints exist to test, correctly, per scope) |

**135/135 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors.** No placeholder code (`TODO`/`FIXME`/`NotImplementedException` grep across the new files returns nothing but false-positive substring matches inside `ToDocument`/`ToDomain`).

Idempotency and duplicate-generation are each tested at two independent levels, same pattern as Sprint 3's Occurrence generation:
- **Application algorithm** (`BuzzApplicationServiceTests.GenerateBuzzesAsync_CalledAgain_IsIdempotent`): a second call returns an empty list.
- **Database constraint** (`BuzzRepositoryTests.AddAsync_RejectsADuplicateOccurrenceIdAndRecipientUserIdCombination`): a real `MongoWriteException` from the unique `(occurrenceId, recipientUserId)` index — the actual enforcement mechanism the brief asked for, not just trusted application logic.

Membership and deleted-Reminder edge cases are both covered (`GenerateBuzzesAsync_ReturnsNotFoundForSomeoneWhoIsNotAMemberOfTheOwningBoard`, `GenerateBuzzesAsync_ReturnsNotFoundWhenTheOwningReminderIsSoftDeleted`). **Deleted-Board edge case: not implemented, correctly.** Per the brief's own "(only if already implemented)" qualifier — grepped and confirmed no `DeleteBoard` command exists anywhere in this codebase (Board has no delete path at all, in any sprint to date), so there is nothing to test.

---

## 3. Architecture Observations

### 3.1 `BuzzStatus` models the full spec lifecycle, only `Scheduled` is ever constructed

Same precedent Sprint 3 set for `OccurrenceStatus`: IMPLEMENTATION_SPEC.md §1 already gives Buzz's complete lifecycle (`Scheduled → Generated → (Delivered | Failed → Retried ... → Exhausted) → (Seen | Dismissed)`) — not speculative, so it's modeled in full now rather than partially. `Buzz.Generate()` always sets `Status = Scheduled` and raises `BuzzGenerated` (the event name matches the Application command's verb, exactly mirroring `Occurrence.Generate()` → `OccurrenceGenerated`), leaving `Generated`/`Delivered`/`Failed`/`Retried`/`Exhausted`/`Seen`/`Dismissed` unreachable until a future delivery sprint adds the transitions. "Pending" (as in `ListPendingBuzzes`) is therefore not a separate stored status — it's defined as `Status == Scheduled`, which today is the only status any Buzz can ever have.

### 3.2 `ScheduledAt` operationalizes NotifyPreset for the first time

No prior sprint ever computed a delivery time from a Reminder's `NotifyPreset` — Sprint 2 only ever stored the value. `ScheduledAt = Occurrence.DueAt − NotifyPreset.ToLeadTime()` is the mechanical, non-speculative translation of already-fully-specified data (`AtTime` → zero offset, `FifteenMinutesBefore` → 15 minutes, ... `OneWeekBefore` → 7 days) — not a new design decision, just the first code path that needed it. Kept as a standalone extension method in the `Buzzes` namespace (`NotifyPresetLeadTimeExtensions.cs`) rather than added to `NotifyPreset.cs` itself, so this sprint touches zero Sprint 2 files, in keeping with "do not revisit previous implementation."

### 3.3 The unique index is per-(Occurrence, recipient), deliberately not per-Occurrence

A unique index on `occurrenceId` alone would make it impossible to ever add a second Buzz for the same Occurrence — which is wrong, since a shared Reminder needs one Buzz per Member. `(occurrenceId, recipientUserId)` is the correct compound key, tested directly (`AddAsync_AllowsTheSameOccurrenceForDifferentRecipients` alongside the duplicate-rejection test) to confirm both halves of that invariant.

### 3.4 Buzz generation deliberately doesn't touch the outbox

`BuzzGenerated` is raised (and tested at the Domain level) but never drained — the same, now four-times-consistent pattern as `BoardCreated`/`MembershipGranted` (Sprint 1), `ReminderCreated`/`ReminderDeleted` (Sprint 2), and `OccurrenceGenerated` (Sprint 3). Still a single, deliberately-deferred pass once a real event consumer exists (Sprint 2 Report §5.4, Sprint 3 Report §5.3).

---

## 4. Specification Gaps Discovered

Per the brief's own instruction ("stop and explain rather than inventing behaviour"), these are reported, not silently resolved:

### 4.1 "The Member is not blocked" cannot be checked — no Block concept exists in this codebase

The Sprint 4 brief's Generation Rules require confirming a recipient "is not blocked" before generating their Buzz. Confirmed by direct search: **no `Block` aggregate, repository, or field exists anywhere in `src/`**, in any sprint to date. MVP_SCOPE.md marks Remove Member and Block as non-negotiable *product* requirements, but neither has ever been implemented — Membership itself has no status field at all (`Membership.cs`'s own doc comment: "every Membership on a Board is implicitly active by construction... Remove Member/Leave Board... is exactly what will introduce that state").

This sprint's Domain scope is Buzz only ("Do NOT redesign the domain"), so implementing Block now would be out of scope regardless of how the gap is resolved. **Resolution taken: the block check is simply not performed** — `GenerateBuzzesAsync` generates a Buzz for every current Board Member, because "not blocked" is, today, vacuously true for all of them (there is no data source that could ever say otherwise). This is not a silent gap: it's recorded here, in the code's own doc comments on `BuzzApplicationService.GenerateBuzzesAsync`, and should be treated as a hard prerequisite for whichever future sprint implements Block — at that point, this method needs a real check added, not before.

### 4.2 "The Member is active" is also unverifiable independent of "the Member is not blocked" — for the same reason

Same root cause as §4.1: `Membership` has no status field, so every entry in `Board.Memberships` is active by construction. This one is genuinely benign (there's no *other* state a Membership could be in, so "is active" and "is present" are actually equivalent today, not just assumed equivalent) — but it's the same underlying gap, worth naming once rather than twice.

### 4.3 Board cannot be constructed with more than one Member anywhere in this codebase

Discovered while writing this sprint's tests. `Board.Create` only ever adds a single Membership (the creator, as Owner) — no `InviteMember`/`AcceptInvitation`/`AddMember` command has been implemented in any sprint to date, and `Board.Rehydrate` is `internal`, visible only to `BuzzMe.Infrastructure` and `BuzzMe.Domain.Tests`, not `BuzzMe.Application.Tests` or `BuzzMe.Infrastructure.IntegrationTests`. This means **"generate exactly one Buzz per active Member" could only be tested against the N=1 case** — the generation algorithm itself is written generically (it loops over `board.Memberships`, with no special-casing for a single member), but multi-recipient generation has not been, and currently cannot be, exercised by a test anywhere in this repository without inventing Board behavior that's out of this sprint's Domain scope. Flagged here rather than worked around by bypassing the aggregate (e.g., hand-writing extra membership sub-documents directly into MongoDB) — that would test a Board shape the system cannot actually produce, which is its own form of inventing behavior.

---

## 5. Technical Debt Introduced

1. **Block-check gap (§4.1)** — the most consequential item in this report. Once Block is implemented, `GenerateBuzzesAsync` must be revisited to add the check; until then, every current Board Member receives a Buzz unconditionally.
2. **Multi-Member generation is untested (§4.3)** — a direct consequence of Board's existing single-Member limitation, not something introduced by this sprint. Whichever sprint adds Invite/Accept (or any other path to a second Membership) should add a multi-recipient `GenerateBuzzesAsync` test at that point — the algorithm doesn't need to change, only the test's ability to set up its precondition does.
3. **No caching or batching for `ListByOccurrenceAsync`'s existing-recipient check** — one extra repository read per generation call, same acceptable-for-now / worth-reconsidering-at-scale posture Sprint 3 Report §5.4 already recorded for Occurrence generation's own repository reads.

No other debt. The aggregate, the repository, and the indexes all match what this sprint's brief and IMPLEMENTATION_SPEC.md §1's Buzz responsibilities describe, once §4's two genuine specification gaps are treated as reported rather than silently worked around.
