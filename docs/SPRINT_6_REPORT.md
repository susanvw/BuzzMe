# Sprint 6 Report — Buzz Delivery Pipeline

*Orchestration only, exactly as scoped: a Buzz can now travel `Scheduled → Generated → (Delivered | Failed)`, claimed safely by a polling `BackgroundService` under real concurrent access, with no real push provider anywhere in the loop. `Reminder → Occurrence → Buzz → Worker → Delivered` now exists end to end, verified against real MongoDB.*

---

## 1. Repository Changes

**Domain** — `src/BuzzMe.Domain/Buzzes/Buzz.cs` gained `ClaimForProcessing()` (`Scheduled → Generated`, increments `AttemptCount`, no event — see §4.1), `MarkDelivered(deliveredAt)` and `MarkFailed(failedAt)` (`Generated → Delivered`/`Failed`, each raising a new event). New events: `Events/BuzzDelivered.cs`, `Events/BuzzDeliveryFailed.cs`. New: `SeedWork/ConcurrencyConflictException.cs` (see §4.3). **No new `BuzzStatus` enum values were needed** — `Scheduled`/`Generated`/`Delivered`/`Failed` were all already modeled in Sprint 4; see §3.1 for why the brief's "Pending"/"Processing" map onto these directly rather than requiring new ones.

**`IBuzzRepository`/`BuzzRepository`** gained `ClaimPendingAsync(now, batchSize, ct)` — a loop of individual atomic MongoDB `FindOneAndUpdateAsync` calls (filter: `Status == Scheduled && ScheduledAt <= now`; update: `Status = Generated`, `AttemptCount += 1`, `Version += 1`) — and `UpdateAsync(buzz, ct)` — a full-document replace filtered on `Id` **and** `Version`, throwing `ConcurrencyConflictException` on a stale write. This is the first repository in the entire codebase to actually enforce `AggregateRoot.Version` as a real optimistic-concurrency gate (SPRINT_5_REPORT.md §4.3/§5.1 flagged this as still-open; closed here).

**Application** — `BuzzApplicationService` gained `ClaimPendingBuzzesAsync(batchSize, ct)`, `MarkDeliveredAsync(buzz, ct)`, `MarkFailedAsync(buzz, ct)`. No `Result<T>` wrapper on these: they're System/Internal calls with no authorization/business-failure branch, unlike every UI-facing method in this service.

**`INotificationDispatcher`** (`Application/Abstractions/`) — the Sprint 6 brief's temporary stand-in for real delivery; `Infrastructure/Messaging/Notifications/LoggingNotificationDispatcher.cs` is the default registration, always reporting success (deliberately the opposite default from `NullPushNotificationSender`/`NullEmailSender`/`NullSmsSender`, which return `false` — see those types' own doc comments for why honesty about non-delivery matters there but not here).

**`BuzzMe.Workers/Jobs/BuzzDeliveryWorker.cs`** — a `BackgroundService` using `PeriodicTimer` (5s poll, 20-item batch — both explicit operational parameters, not spec'd), claim → dispatch → mark outcome → continue, one DI scope per batch. Registered in `Workers/Program.cs` alongside `BuzzApplicationService`.

**Removed** (see §5): `InvitationApplicationService.ListPendingInvitationsAsync`, `IInvitationRepository.ListPendingByBoardAsync` and its Mongo/in-memory implementations, and their tests.

---

## 2. Test Results

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **94/94** (86 prior + 8 new: `ClaimForProcessing`/`MarkDelivered`/`MarkFailed` transitions, guards, and raised events) |
| `BuzzMe.Application.Tests` | **60/60** (57 prior − 2 removed `ListPendingInvitationsAsync` tests + 5 new: claim/mark orchestration against the in-memory fake) |
| `BuzzMe.Infrastructure.IntegrationTests` | **44/44** (33 prior − 2 removed `ListPendingByBoardAsync` tests + 7 new `BuzzRepositoryTests` (claim, batch size, **concurrent claim**, version-checked update, **stale-version conflict**) + 5 new `BuzzDeliveryWorkerTests` (claim→dispatch→mark, duplicate-processing prevention, not-yet-due, **end-to-end Reminder→Occurrence→Buzz→Worker→Delivered**) — real MongoDB throughout, only `INotificationDispatcher` mocked, exactly as instructed) |
| `BuzzMe.Api.IntegrationTests` | **25/25** (unchanged — this sprint added no endpoints) |

**223/223 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors.** No placeholder code. The full Infrastructure integration suite was run twice to confirm stability after the isolation fix in §2.1.

**Retry tests: none, deliberately.** "If applicable" — it isn't; see §4.2. No `RetryScheduled` transition exists to test.

### 2.1 A real test-isolation bug found and fixed while writing these tests

`ClaimPendingAsync`'s query is deliberately **global** — `Status == Scheduled && ScheduledAt <= now`, not scoped to any one caller's Board or Occurrence, because that's what a real work-queue claim has to be. Every prior Infrastructure integration test in this codebase happened to avoid ever needing this: each one queries by a randomly-generated ID (`ListByOccurrenceAsync(occurrenceId)`, `GetByIdAsync(id)`, ...), which is naturally immune to the fact that this whole test project shares **one MongoDB database per collection** (`MongoIntegrationTestFixture`, one container for every class using `MongoIntegrationTestCollection`). The first version of this sprint's claim tests used `Assert.Single(claimed)`/exact-count assertions and intermittently picked up leftover due Buzzes inserted by other tests in the same run — a real bug in the new tests, not in the claim logic itself. Fixed by asserting containment of each test's own known Buzz ID within a generously-batched result, never an exact total — the correct, general pattern for any test against a genuinely global query in a shared-database suite. No production code changed for this; §5 below is unrelated technical debt this incident makes more visible, not caused by it.

---

## 3. Architecture Observations

### 3.1 The brief's "Pending"/"Processing" vocabulary maps directly onto Sprint 4's existing `Scheduled`/`Generated` — no new states were needed

IMPLEMENTATION_SPEC.md's Buzz lifecycle (`Scheduled → Generated → (Delivered | Failed → Retried ... → Exhausted) → (Seen | Dismissed)`) already has a status sitting exactly where "claimed, about to attempt delivery" belongs: `Generated` reads as "ready to deliver," which is precisely what a worker-claimed Buzz is. Renaming it to "Processing," or adding a parallel "Processing" value alongside the existing "Generated," would have been the exact kind of invented, redundant state the brief explicitly forbids ("Do not invent additional states"). `Pending` maps onto `Scheduled` the same way — already established in Sprint 4's own `ListPendingByRecipientAsync` (`Status == Scheduled` was already what "pending" meant there). This sprint changed zero enum values; it only added the transition *methods* that move a Buzz between statuses already on the books.

### 3.2 `BuzzDeliveryWorker` is polling (`PeriodicTimer`), not the outbox-reactive pattern DEVELOPMENT_GUIDE.md's own process table lists — a deliberate, necessary divergence

DEVELOPMENT_GUIDE.md §7's process table classifies "Dispatch Push Notifications" as **reactive** work (triggered by `BuzzGenerated`, via `Workers/Jobs/OutboxDispatcherJob`) — category A, not category B (`BackgroundService` + `PeriodicTimer`, the category the Sprint 6 brief explicitly and repeatedly demands: "Infrastructure: BackgroundService, PeriodicTimer... Poll for Pending Buzzes"). These two instructions conflict, and the conflict is real, not cosmetic: Sprint 4 made `Buzz.Generate()` raise `BuzzGenerated` at Buzz **creation** time (when it first enters the queue, landing on `Status = Scheduled`), not at the moment its `ScheduledAt` lead time actually arrives — the canonical Event Storming model's own two-phase `BuzzScheduled`→(wait)→`BuzzGenerated` was deliberately collapsed into one step in Sprint 4 (SPRINT_4_REPORT.md §3.1). Reacting to `BuzzGenerated` via the outbox, as DEVELOPMENT_GUIDE.md's table describes, would therefore dispatch every Buzz **immediately upon creation** — hours or days before its actual intended delivery time, for any Reminder with a non-zero `NotifyPreset` lead time. That's not a viable implementation of "deliver at `ScheduledAt`," regardless of which document is followed literally.

**Resolution:** implement exactly what the brief explicitly asks for — a `PeriodicTimer`-driven poll against `Status == Scheduled && ScheduledAt <= now` — because it's the only one of the two instructions that can actually respect `ScheduledAt`. **Recommendation:** DEVELOPMENT_GUIDE.md §7's process table should be corrected: "Dispatch Push Notifications" belongs in category B (time-scheduled/`PeriodicTimer`) alongside "Retry Failed Notifications" and "Invitation Expiry," not category A. This is reported, not silently resolved — the same "stop and document rather than invent" instruction this sprint's own brief repeats.

### 3.3 The claim/mark split cleanly separates "safe under concurrency" from "business outcome," matching this codebase's established Domain/Infrastructure boundary

`ClaimForProcessing` (Domain) never touches Mongo; `ClaimPendingAsync` (Infrastructure) never touches domain invariants directly — it mutates the stored document via an atomic update and reconstructs the Domain object from the *result*. This mirrors the Buzz Generation Boundary Review's conclusion two sprints ago (Application orchestrates, Domain models, Infrastructure persists) applied to a new axis: concurrency safety belongs to the database operation (`FindOneAndUpdateAsync`'s atomicity), not to a lock, a queue, or a distributed coordination mechanism — appropriate for this system's actual scale and consistent with "No Quartz. No Hangfire."

### 3.4 One dead-letter gap, explicitly not solved

If `INotificationDispatcher.DispatchAsync` *throws* (rather than returning `false`), `BuzzDeliveryWorker` logs the error and moves on — but the Buzz stays claimed (`Generated`) forever, since neither `MarkDelivered` nor `MarkFailed` ever ran. There is no recovery sweep for a Buzz stuck in `Generated`. This is a real, known limitation, left unresolved deliberately: building a stuck-item recovery mechanism wasn't asked for ("implement only orchestration"), and inventing one would risk exactly the kind of unspecified retry/recovery logic §4.2 explains was intentionally left out. Recorded here so it isn't mistaken for an oversight.

---

## 4. Specification Gaps

### 4.1 No domain event for the claim transition

Every other state-changing Domain method in this codebase raises a matching event. `ClaimForProcessing` doesn't, because there is no unclaimed event name left for it: Sprint 4 already spent `BuzzGenerated` on Buzz *creation*. Inventing a new event name (e.g., `BuzzClaimed`) for this transition isn't grounded in any of the five specification documents — none of them describe a claim step as a business-significant fact in its own right (only the *outcome*, `BuzzDelivered`/`BuzzDeliveryFailed`, is ever named). Left un-raised rather than invented.

### 4.2 `RetryScheduled` is not implemented — no retry/backoff exists this sprint

The brief's own phrasing — "RetryScheduled (only if already specified)," "Retry scheduling (only if already specified)" — asked for exactly this check. Result: it isn't specified. IMPLEMENTATION_SPEC.md names `BuzzRetried` only as an **event** in a chain (`BuzzDeliveryFailed → BuzzRetried → (BuzzDelivered | BuzzDeliveryExhausted)`), never a resting **status**; and §6 explicitly flags the retry count/backoff curve as "an operational tuning parameter best set from real provider behavior during integration testing, not fixed here in advance" — i.e., deliberately unspecified, not merely undocumented. Building real bounded retry-with-backoff against an unspecified cadence would be inventing the exact parameter the spec itself declines to fix. `MarkFailed` is terminal this sprint: a failed Buzz simply stays `Failed`. This is the single largest piece of intentionally-deferred scope in this report — see §5.

### 4.3 `ConcurrencyConflictException` is a new abstraction, but a directly-required one

The brief explicitly asks for "Optimistic concurrency" (Infrastructure scope). `AggregateRoot<TId>.Version` has existed since Sprint 1 with exactly this documented purpose but was never enforced by any repository's write path until this sprint (SPRINT_5_REPORT.md §4.3/§5.1 named this directly as remaining work). A dedicated exception to signal a version mismatch is the minimal, necessary vocabulary for that enforcement to mean anything — not a speculative addition.

---

## 5. Recommendations for Replacing the Temporary Dispatcher

1. **Delete `LoggingNotificationDispatcher` and `INotificationDispatcher` outright** once a real dispatch step exists — both are explicitly temporary by design (per their own doc comments), not an abstraction meant to grow into the real one.
2. **The real replacement is not a drop-in implementation of the same interface** — it's a fan-out. `INotificationDispatcher.DispatchAsync(Buzz)` has no channel concept; a real send needs to resolve the recipient's device tokens/contact info and preferred channel(s), then call the *already-scaffolded* `IPushNotificationSender`/`IEmailSender`/`ISmsSender` (registered since Sprint 1's bootstrap, never yet consumed by anything) — each per-channel, potentially more than one per Buzz. Whatever replaces `BuzzDeliveryWorker`'s dispatch call should orchestrate across those three, not extend `INotificationDispatcher` with more parameters.
3. **§4.2's retry gap needs resolving before or alongside this**, since a real provider is precisely what determines the retry cadence IMPLEMENTATION_SPEC.md §6 defers to ("real provider behavior during integration testing"). Building retry/backoff and wiring the first real provider are naturally the same sprint's work, not two independent ones.
4. **§3.2's polling-vs-reactive tension should be resolved in the specification before the real provider sprint starts**, not carried forward silently a third time — whoever picks this up should not have to re-derive the same contradiction this report already worked through.
5. **The stuck-in-`Generated` gap (§3.4) should be closed by that same sprint**, likely via a second, slower `PeriodicTimer` sweep that reclaims Buzzes stuck in `Generated` past some staleness threshold — a natural, minimal extension of the polling pattern already in place, not a new mechanism.

---

## 6. `ListPendingInvitationsAsync` — Reviewed and Removed

Per this sprint's explicit review instruction, and using SPRINT_5_REPORT.md §3.1's own already-recorded finding as the evidence: `ListPendingInvitationsAsync` had **no specification basis anywhere** — no `APPLICATION_LAYER_SPEC.md` row, no `API_CONTRACT.md` endpoint, no `BUSINESS_BEHAVIOR_MODEL.md` scenario. This is a meaningfully weaker footing than `CancelInvitationAsync` (kept — grounded directly in `DOMAIN_MODEL.md`'s "the inviter may revoke it" and `EVENT_STORMING.md`'s explicit inviter-initiated `RevokeInvitation` command), which is why the two weren't treated the same way. Its backing repository method, `IInvitationRepository.ListPendingByBoardAsync`, was removed alongside it — nothing else called it, and its board-scoped shape wasn't even what a future global Expire-Invitations sweep would need, confirming it was speculative in both name and shape, not merely missing an API surface. Removed: the Application method, the repository method (interface + Mongo + in-memory implementations), and their dedicated tests (2 in `BuzzMe.Application.Tests`, 1 in `BuzzMe.Infrastructure.IntegrationTests`). The Mongo index that once backed it (`ix_invitations_boardId_status_id`, from Sprint 5's `CreateInvitationIndexes` migration) was left in place rather than editing an already-numbered, shipped migration to drop it — a genuinely unused index is a minor, low-cost cleanup item, not a correctness concern; noted here rather than silently left unexplained.
