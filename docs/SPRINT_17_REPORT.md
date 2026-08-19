# Sprint 17 Report — Transactional Outbox + Buzz Cancellation Policies

*Selected as "dispatcher infrastructure plus the two most self-contained Buzz-cancellation policies," deliberately smaller than "all four named policies." What actually shipped is bigger than that scope suggests, for one reason: the dispatcher this codebase's own architecture document specifies is not a simple pub/sub relay — it's a transactional outbox, named collection, index, and background job included. Building the honest, correctly-scoped version of "dispatcher infrastructure" turned out to mean building the real thing DEVELOPMENT_GUIDE.md §6/§7 already described, not a smaller stand-in for it.*

---

## 1. The Scope Discovery, and Why It Changed the Plan Mid-Sprint

The initial plan was a simple in-process dispatcher: an Application Service calls a dispatcher directly after a successful save, the dispatcher resolves and invokes matching handlers by reflection, exceptions get logged and swallowed. That design was abandoned after reading `DEVELOPMENT_GUIDE.md` §6/§7 in full, which turned out to already specify, in concrete detail: an `outbox` MongoDB collection (already named in §6's collection table, alongside its own index), a **transactional** write — domain events land in `outbox` in the *same MongoDB transaction* as the aggregate's own document write — and a separate `OutboxDispatcherJob` background worker (also named, in §7's own process table) that polls `outbox` and marks each row processed only on success, which is what actually gives "retried until success," not a same-request try/catch. Scaffolding for the writer half already existed from the foundational sprint (`IOutboxWriter`/`MongoOutboxWriter`/`OutboxMessage`, all unused by any repository until now) — confirming this was always the intended design, just never wired up.

Presented with this, the choice was to build the documented architecture rather than a smaller substitute for it — larger than the originally-scoped option, but the one this codebase's own architecture document actually specifies, and the two chosen policies now sit on infrastructure that will still be correct once a future sprint adds the other two.

---

## 2. Repository Changes

**Domain** — `BuzzStatus` gained `Cancelled` (not part of IMPLEMENTATION_SPEC.md §1's original lifecycle enumeration, but required by name, repeatedly, by the "cancel pending Buzzes" policy both specs describe — no other status value fits). `Buzz.Cancel(cancelledAt)`: transitions from `Scheduled`/`Generated` only — any other status (already Delivered/Failed/Cancelled, or the still-unreachable Retried/Exhausted/Seen/Dismissed) is a no-op, matching IMPLEMENTATION_SPEC.md §4's own words, "cancelling an already-cancelled or already-delivered Buzz is a no-op," generalized to any non-pending status. New `BuzzCancelled` event — raised, not currently dispatched (see §5.1). `IPolicy<TEvent>` — DEVELOPMENT_GUIDE.md §2's own naming for "the reactive glue from Application Layer Spec §7" — declared in `Domain.SeedWork` alongside `IDomainEvent`, so both Application (implements) and Infrastructure (resolves reflectively) can reference it.

**Infrastructure** — `OccurrenceRepository.UpdateAsync` and `ReminderRepository.MarkDeletedAsync` are now the first two write paths in this codebase using a real MongoDB session/transaction: the aggregate's own document write and the outbox insert commit together via `IOutboxWriter.WriteAsync(session, ...)`, or neither does. A version conflict aborts the transaction before anything reaches the outbox — a losing race must never leave a stray "already done by X" event behind for a change that was itself rejected (verified by `OccurrenceRepositoryTests.UpdateAsync_OnVersionConflict_LeavesNoOutboxRowBehind`). `IReminderRepository.MarkDeletedAsync`'s signature changed from `(ReminderId, DateTimeOffset)` to `(Reminder)` — it now needs the aggregate itself to reach its raised events, not just the two scalar values the old signature carried; every call site (one real caller, several test seeding helpers) was updated to call `reminder.Delete(now)` before passing the aggregate through. `OutboxDispatcher` (implements the new `IOutboxDispatcher` Application abstraction): claims a batch via the same atomic `FindOneAndUpdate` pattern `BuzzRepository.ClaimPendingAsync` already established (each claim also pushes `AvailableAt` forward by a 30-second backoff — a crash mid-processing self-heals without a separate heartbeat), resolves each row's `IPolicy<TEvent>` implementations by the event's own runtime type via reflection (there's no other way to route a heterogeneous `IDomainEvent` payload to a closed generic interface at compile time), and marks the row processed on success. `OutboxEventTypeRegistry` — an explicit `EventType`-name-to-CLR-`Type` map, listing only the event types a write path in this codebase actually puts in the outbox today (`OccurrenceCompleted`/`Dismissed`/`Undone`, `ReminderDeleted`) — matching this codebase's established preference for an explicit table over reflection-scanning a whole assembly (RecurrenceCodes/NotifyPresetCodes' own precedent). A row whose type isn't in the registry — meaning nothing consumes it yet — is marked processed immediately rather than retried forever; that's a permanent, expected state, not a transient failure. New migration: `CreateOutboxIndexes` (`processedAt`+`availableAt`, DEVELOPMENT_GUIDE.md §6's own named index).

**Application** — `BuzzApplicationService` gained `CancelBuzzesForOccurrenceAsync`/`CancelBuzzesForReminderAsync` (the shared internal "CancelBuzzes" use case APPLICATION_LAYER_SPEC.md §3 names). New `Policies/` namespace: `CancelBuzzesOnOccurrenceResolvedPolicy` (handles both `OccurrenceCompleted` and `OccurrenceDismissed` — the side effect is identical either way) and `CancelBuzzesOnReminderDeletedPolicy`.

**Workers** — `OutboxDispatcherJob`, a plain `BackgroundService` + `PeriodicTimer` (5-second poll, 20-row batches) — the exact shape `BuzzDeliveryWorker` already established, for the same reason: outbox rows carry their own `availableAt` retry-backoff, so polling is sufficient without a message broker.

**DI** — `IOutboxDispatcher`/`OutboxDispatcher` and both Policies are registered in the *shared* `InfrastructureServiceCollectionExtensions` (used by both `Api` and `Workers`), which meant `BuzzApplicationService` — previously registered only in `Workers/Program.cs`, since Buzz has no HTTP endpoints — now also needs registering in `Api/Program.cs`, because the Policies (which the Api host's own DI container now validates at startup) depend on it.

---

## 3. A Real Deployment Requirement This Sprint Introduces

**MongoDB must run as a replica set** (even a single-node one) for any of this to work — a standalone `mongod` rejects `session.StartTransaction()` outright with `NotSupportedException: Standalone servers do not support transactions`. This was caught empirically, not assumed: both Testcontainers-based test fixtures (`MongoIntegrationTestFixture`, `BuzzMeApiFactory`) were using a plain standalone `MongoDbBuilder("mongo:7.0").Build()`, which failed the moment `OccurrenceRepository.UpdateAsync`/`ReminderRepository.MarkDeletedAsync` tried to open a transaction against it — fixed by adding `.WithReplicaSet()` to both. **This is not just a test-fixture footnote** — any real deployment of this application from this sprint forward needs MongoDB configured as a replica set (even single-node), or `CompleteOccurrenceAsync`/`DismissOccurrenceAsync`/`ReopenOccurrenceAsync`/`DeleteReminderAsync` will all fail outright at the transaction-start step. No infrastructure-as-code or deployment doc exists in this repository to update with this requirement — flagged here as the authoritative record of it, and worth a line in whatever deployment runbook exists outside this repository.

---

## 4. Test Results

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **179/179** (168 prior + `Buzz.Cancel` — from Scheduled, from Generated, no-op when already Delivered, idempotent when already Cancelled — plus the `cancelled` code added to the existing round-trip theory) |
| `BuzzMe.Application.Tests` | **162/162** (156 prior + `CancelBuzzesForOccurrenceAsync`/`CancelBuzzesForReminderAsync` — cancels a pending Buzz, leaves an already-delivered one alone, only touches not-yet-resolved Occurrences across a Reminder — plus both Policies' own `HandleAsync` tests, exercised against real `BuzzApplicationService` and in-memory repositories, not mocks) |
| `BuzzMe.Infrastructure.IntegrationTests` | **90/90** (82 prior + the transactional-outbox-write tests on both repositories, the version-conflict-leaves-no-outbox-row test, three `OutboxDispatcher` tests — invokes a registered Policy and marks processed, skips an unrecognized event type without retrying it forever, leaves a row for retry when a Policy throws — and two full end-to-end pipeline tests: Complete an Occurrence → one dispatcher poll → its Buzz is Cancelled; Delete a Reminder → one dispatcher poll → pending Buzzes across its Occurrences are Cancelled — real MongoDB, real transactions, real Policies, only the outer `PeriodicTimer` bypassed, same precedent `BuzzDeliveryWorkerTests` already set) |
| `BuzzMe.Api.IntegrationTests` | **103/103** (unchanged in count — this sprint added no new endpoints; every existing test still passes against the replica-set-enabled test host) |

**534/534 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors** (the pre-existing `NU1903` SSH.NET/Testcontainers advisory, unrelated to this sprint, remains unchanged since SPRINT_9_REPORT.md).

---

## 5. Specification Interpretation Notes

### 5.1 A direct contradiction between IMPLEMENTATION_SPEC.md and APPLICATION_LAYER_SPEC.md on synchronous-vs-policy-driven cancellation

IMPLEMENTATION_SPEC.md §4's "Delete-Cascade Cancellation" row states cancellation happens "in the same operation" as the halt-generation step, and separately marks it "Not applicable — synchronous." APPLICATION_LAYER_SPEC.md §7's own classification table, for the identical "DeleteReminder → cancel pending Buzzes" row, states the opposite explicitly: "Eventually consistent workflow, policy-driven... Reminder's own transaction commits `ReminderDeleted` first; a policy then cancels pending Buzzes... as a retried-to-completion follow-on step, not bundled into the same transaction." Resolved by favoring APPLICATION_LAYER_SPEC.md §7 — its entire purpose is this exact synchronous-vs-eventually-consistent classification, stated with more precision than IMPLEMENTATION_SPEC.md's more general "in the same operation" phrasing — the same "more implementation-precise document wins" rule this session has applied at every prior contradiction. This reading is also the only one consistent with Buzz being a genuinely separate aggregate root (this codebase's own established boundary since Sprint 4) and with Sprint 12's own `DeleteAccountAsync`, which already never attempted synchronous Buzz cancellation when deleting Boards.

### 5.2 `IReminderRepository.MarkDeletedAsync`'s signature change was required, not optional

The outbox write needs `reminder.DomainEvents`, which only the aggregate itself carries — the old `(ReminderId, DateTimeOffset)` signature had nowhere to get them from. This is the one interface-shape change this sprint made to an already-shipped method, and it rippled through every existing call site (one real caller, several test-seeding helpers across three test projects) — a deliberate, contained change, not a sign the original design was wrong; `OccurrenceRepository.UpdateAsync` needed no equivalent change, since it already took the full aggregate.

---

## 6. Specification Gaps

### 6.1 Occurrence regeneration and Buzz rescheduling remain unimplemented

The two policies this sprint deliberately left for later — "regenerate not-yet-generated future Occurrences on a recurrence change" and "reschedule not-yet-delivered Buzzes on a notify-preset change" — now have a correct, real dispatcher to land on. `RecurrenceRuleUpdated`/`NotifyPresetUpdated` are raised by `Reminder.Update` (Sprint 16) but never written to the outbox — `ReminderRepository.UpdateAsync` (the write path that raises them) wasn't touched this sprint, deliberately, matching the approved scope. A future sprint building these two policies needs to both write the matching Policy classes *and* wire `ReminderRepository.UpdateAsync` into the transactional-outbox pattern this sprint established — the second half doesn't happen automatically.

### 6.2 `BuzzCancelled` is raised but never dispatched

Unlike `OccurrenceCompleted`/`Dismissed`/`ReminderDeleted`, nothing writes `BuzzCancelled` to the outbox — `BuzzRepository.UpdateAsync` (the write path `Buzz.Cancel()`'s persistence goes through) wasn't wired into the transactional-outbox pattern, since nothing in this codebase currently needs to react to a Buzz being cancelled. Consistent with every other undispatched event in this system (Board's `MembershipGranted`, User's `AccountDeleted`, etc.) — not a new category of gap, just one more entry in it.

### 6.3 No dead-lettering or maximum-attempt cutoff for outbox rows

A row whose Policy keeps throwing is retried forever, every 30 seconds, with no cap and no alerting. Same honestly-scoped category as Sprint 6's own unbuilt Buzz retry/backoff (`SPRINT_6_REPORT.md`) — a real production gap, named rather than silently accepted, not a blocker for this sprint's own two policies (which have no realistic permanent-failure mode: cancelling a Buzz that no longer exists is itself a no-op, not an exception).

### 6.4 No deployment documentation exists to record the new replica-set requirement

Restated from §3 — this repository has no infrastructure-as-code, Docker Compose file, or deployment runbook for this backend that could be updated with "MongoDB must be a replica set." This report is the only place that requirement is currently written down.
