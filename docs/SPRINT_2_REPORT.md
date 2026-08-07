# Sprint 2 Report — Create, Retrieve and Delete Reminders

*The next vertical slice: a Board Member can create a Reminder, retrieve it, list a Board's Reminders, and delete one. No Occurrence, no Buzz, no background processing, no History, no outbox — exactly as scoped.*

---

## 1. Repository Changes

**New files** — Domain (`src/BuzzMe.Domain/Reminders/`): `ReminderId.cs`, `ReminderTitle.cs`, `Recurrence.cs`, `NotifyPreset.cs`, `ReminderSchedule.cs`, `Reminder.cs`, `IReminderRepository.cs`, `Events/{ReminderCreated,ReminderDeleted}.cs`

**Application** (`src/BuzzMe.Application/Reminders/`): `ReminderApplicationService.cs`, `Models/ReminderResult.cs`

**Contracts** (`src/BuzzMe.Contracts/V1/Reminders/`): `CreateReminderRequest.cs`, `ReminderResponse.cs`

**Infrastructure** (`src/BuzzMe.Infrastructure/Persistence/Mongo/Reminders/`): `ReminderDocument.cs`, `Mappers/ReminderMapper.cs`, `ReminderRepository.cs`, plus `Persistence/Migrations/Steps/CreateReminderIndexes.cs`

**Api**: `Endpoints/ReminderEndpoints.cs`, `Validation/CreateReminderRequestValidator.cs`, `Mapping/ReminderMapping.cs`

**Tests**: `BuzzMe.Domain.Tests/Reminders/ReminderTests.cs` (18 tests) · `BuzzMe.Application.Tests/Reminders/ReminderApplicationServiceTests.cs` (9 tests) + `TestDoubles/InMemoryReminderRepository.cs` · `BuzzMe.Infrastructure.IntegrationTests/Reminders/ReminderRepositoryTests.cs` (5 tests) · `BuzzMe.Api.IntegrationTests/Reminders/ReminderEndpointsTests.cs` (8 tests)

**Existing files touched**: `InfrastructureServiceCollectionExtensions.cs` (registered `IReminderRepository` and `CreateReminderIndexes`), `Program.cs` (registered `ReminderApplicationService`, mapped `ReminderEndpoints`). Sprint 1's Board code was not touched — see §3.4 for why one validation rule that would have required changing it was deliberately left unimplemented instead.

---

## 2. Test Results

All green, run for real against Docker-backed MongoDB, same as Sprint 1:

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **25/25** (7 Board + 18 Reminder) |
| `BuzzMe.Application.Tests` | **15/15** (6 Board + 9 Reminder, in-memory fakes) |
| `BuzzMe.Infrastructure.IntegrationTests` | **9/9** (4 Board + 5 Reminder, real ephemeral MongoDB) |
| `BuzzMe.Api.IntegrationTests` | **14/14** (6 Board + 8 Reminder, real host + real MongoDB + real JWTs) |

**63/63 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors.** `grep` across every `.cs` file in the repo for `TODO`, `FIXME`, `NotImplementedException` → **none found.**

The API suite exercises the full acceptance scenario directly: create → persisted, belongs to the Board, appears in List, retrievable by id, deletable, and confirmed gone from both Get and List afterward (`DeleteReminder_RemovesItAndItIsNoLongerReturned`). No Occurrence, Buzz, or background-worker code exists anywhere in the solution to have run.

---

## 3. Architecture Observations

1. **Two repositories in one Application Service is the right shape, not a smell.** `ReminderApplicationService` depends on both `IReminderRepository` and `IBoardRepository`, because Board-membership — the authorization gate for every Reminder action — lives entirely on the Board aggregate (Sprint 1). This is normal Application-layer orchestration across aggregate boundaries, not a layering violation.
2. **A minor, deliberate deviation from DEVELOPMENT_GUIDE.md §6's literal index description.** That document specified a `(boardId, createdAt)` index for `reminders`; this sprint built `(boardId, _id)` instead, to stay consistent with Sprint 1's `Id`-based cursor pattern (`IBoardRepository.ListByMemberAsync`). Since `Id` is a time-sortable GUIDv7, the two orderings are functionally equivalent — this is a consistency choice, not a behavioral gap, but worth recording since it doesn't match that document's literal wording.
3. **Delete is a real document removal, not a soft-delete flag.** `Reminder` carries no `IsDeleted`/`DeletedAt` field. Nothing in the current codebase (no History, no Occurrence) needs a deleted Reminder's data to survive yet — adding a soft-delete flag now, with nothing to query around it, would itself have been the "speculative infrastructure" this sprint was told to avoid. This is a real, consequential decision a future sprint (the one introducing History) will need to revisit, not an oversight — recorded again in §5.
4. **`Reminder.Delete()` still raises a `ReminderDeleted` domain event, even though nothing drains it** (the outbox isn't wired this sprint, same as Sprint 1's `BoardCreated`/`MembershipGranted`). This is consistent with Sprint 1's own precedent: an aggregate's job is to raise the facts about what happened; whether Infrastructure consumes them yet is a separate, already-correctly-deferred decision. The event is real, tested domain behavior (`ReminderTests.Delete_RaisesReminderDeleted`), not dead code.

---

## 4. Specification Gaps Discovered

Four found. Per instruction: documented, options given, one recommended, and — where a working slice still had to ship this sprint — the most conservative option was taken and flagged rather than silently invented.

### 4.1 — Endpoint paths: the sprint brief's prose contradicts the existing API Contract

The brief lists `GET /boards/{boardId}/reminders/{reminderId}` and `DELETE /boards/{boardId}/reminders/{reminderId}` (nested under Board) but also says "Exactly matching the API Contract." [API_CONTRACT.md](./API_CONTRACT.md) §5 already specifies these as **flat, top-level** paths — `GET /reminders/{reminderId}`, `DELETE /reminders/{reminderId}` — per its own Principle 3 (a resource is created/listed under its parent, then read/updated/deleted via its own id). These two instructions cannot both be followed literally.

**Options:** (a) follow the brief's nested prose, changing the already-established contract; (b) follow API_CONTRACT.md's existing flat paths, treating the brief's wording as imprecise.

**Recommended and implemented: (b).** "Exactly matching the API Contract" makes that document the stated source of truth, and changing it now would itself be the redesign this sprint explicitly forbids. Implemented as flat `/v1/reminders/{reminderId}` for Get/Delete, nested `/v1/boards/{boardId}/reminders` for Create/List — confirmed working end-to-end by the API integration suite.

### 4.2 — "NotificationPreset collection" vs. four prior documents' singular `notifyPreset`

The brief's Domain scope lists "NotificationPreset collection." [IMPLEMENTATION_SPEC.md](./IMPLEMENTATION_SPEC.md), [APPLICATION_LAYER_SPEC.md](./APPLICATION_LAYER_SPEC.md), [API_CONTRACT.md](./API_CONTRACT.md), and [DEVELOPMENT_GUIDE.md](./DEVELOPMENT_GUIDE.md) all consistently model `notifyPreset` as **one** value per Reminder, from a fixed six-value vocabulary.

**Options:** (a) singular, matching all four prior documents; (b) a genuine multi-value field on Reminder (e.g., both "1 day before" and "1 hour before" simultaneously) — a real, if arguably reasonable, product feature nobody has specified anywhere else.

**Recommended and implemented: (a).** "Collection" is read as referring to the fixed set of valid preset *values* (a closed vocabulary — which `NotifyPresetCodes` in Domain represents), not a request to store more than one per Reminder. Changing the cardinality now would be a real redesign against overwhelming, consistent prior specification, not a minor implementation detail.

### 4.3 — `referenceTimezone` has no source in the Create request contract

IMPLEMENTATION_SPEC.md §1 says `referenceTimezone` is "captured automatically from the creating device at creation." API_CONTRACT.md's `CreateReminder` request body is `{ title, recurrence, startDate, notifyPreset }` — no timezone field. No session, profile, or header-based timezone mechanism has ever been specified anywhere in this project.

**Options:** (a) default to `"UTC"` for every Reminder until a real mechanism is decided; (b) add a request header not currently in the contract; (c) extend the request body with an explicit `referenceTimezone` field (a contract change).

**Recommended and implemented for this sprint: (a), explicitly flagged, not silently chosen.** Every Reminder created this sprint has `referenceTimezone = "UTC"`. This is a known, load-bearing gap — real recurrence-across-timezones behavior (the entire reason Implementation Spec's Critical 1.2 fix exists) cannot be tested or trusted until a future sprint resolves how the timezone is actually captured. Recommend (c) — an explicit request field — as the more durable fix over a header, since it keeps the value visible and testable in the same place every other Create field is.

### 4.4 — `nextOccurrence` and Board-deletion validation both depend on concepts this sprint doesn't have

Two smaller, related gaps: **(a)** API_CONTRACT.md's `Reminder` resource always includes `nextOccurrence`, but Occurrence is explicitly out of scope. Resolved by keeping the field (always `null` this sprint) rather than breaking the documented response shape — see `ReminderResponse.NextOccurrence`. **(b)** The brief's own validation example "Board must not be deleted" cannot be implemented: Sprint 1 never built `DeleteBoard` (out of its own scope), so `Board` has no deleted state to check today, and adding one now would violate this sprint's explicit "do not revisit completed work." This rule is simply not implementable yet, not silently skipped — it becomes real the sprint that adds Board deletion.

---

## 5. Technical Debt Introduced

Named plainly, each with the sprint that should resolve it:

1. **`referenceTimezone` is hardcoded to `"UTC"`.** Every Reminder created before this is fixed has an incorrect or at least unconfirmed timezone anchor. Must be resolved before Occurrence generation (which depends entirely on this value) is implemented.
2. **Delete is a hard removal, not a soft-delete.** Once History exists and needs to reference a deleted Reminder's title/details, this will need to change to a soft-delete-then-purge pattern (mirroring how Board/Account deletion already works per IMPLEMENTATION_SPEC.md §6) — a real, anticipated migration, not just a note.
3. **No Idempotency-Key handling on `POST /boards/{boardId}/reminders`**, for the same reason as Sprint 1's Board creation (Sprint 1 Report §5.2) — unresolved for the same reasons, not re-litigated here.
4. **The outbox is still never drained.** `ReminderCreated`/`ReminderDeleted` are raised and discarded, exactly like Sprint 1's Board events. Still correctly deferred, not a new debt, but the pile of undrained event types is growing and should be addressed in one sprint rather than piecemeal once a first real consumer exists.

No other debt. Everything else in this sprint — the aggregate shape, the four use cases, the four endpoints, the repository, the indexes — matches the already-established specifications exactly, resolved contradictions notwithstanding.
