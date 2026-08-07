# BuzzMe — Project Structure & Development Guide

*Answers one question: how should engineers organize and implement this solution? Builds on [IMPLEMENTATION_SPEC.md](./IMPLEMENTATION_SPEC.md), [APPLICATION_LAYER_SPEC.md](./APPLICATION_LAYER_SPEC.md), and [API_CONTRACT.md](./API_CONTRACT.md) without redesigning any of them — every project, folder, and rule below exists to give the architecture already specified a home, nothing more. Where a textbook Clean Architecture template would suggest more ceremony than BuzzMe's actual size and shape justify, this guide says so explicitly and picks the simpler option — consistent with the whole project's standing bias toward removing unnecessary complexity.*

---

## 1. Solution Structure

**Backend** — six .NET projects, each mapping cleanly to a documented layer:

```
BuzzMe.Domain
BuzzMe.Application
BuzzMe.Infrastructure
BuzzMe.Contracts
BuzzMe.Api
BuzzMe.Workers
```

Plus a test project per layer (§8). **No `BuzzMe.SharedKernel` project** — see §2's Domain entry for why: the handful of cross-aggregate primitives (base classes, a `Result<T>` type) live inside `BuzzMe.Domain/SeedWork` instead. A separate Shared Kernel project earns its place when multiple independently-deployed bounded contexts need to share primitives without depending on each other; BuzzMe's Event Storming document (§7) already concluded its bounded contexts are cohesive enough to be one Domain project with clear internal folders, not physically separate assemblies. Introducing one more project to hold three or four small classes would be the exact kind of unnecessary abstraction this guide is asked to avoid.

**Frontend** — a monorepo, not because BuzzMe needs one for its own sake, but because the API Contract (a single `/v1/` surface shared by Web and Mobile) is exactly the kind of thing that benefits from one shared, versioned client:

```
apps/web            (React)
apps/mobile          (React Native / Expo)
packages/api-client  (generated/hand-maintained TypeScript client matching API_CONTRACT.md exactly — one source of truth for both apps)
packages/domain-constants (the fixed enums that exist because the product is deliberately simple: recurrence options, notify presets, error codes — shared so neither app can drift from the other on what a "valid" value is)
```

No shared UI component package is prescribed — React and React Native render through different primitives, and forcing shared visual components tends to produce an abstraction that serves neither platform well. The Design System's *rules* (spacing, motion durations, color roles) are the thing worth sharing, as documentation and design tokens, not as shared component code.

---

## 2. Project Responsibilities

| Project | Responsibility | Allowed Dependencies | Forbidden Dependencies | Typical Contents |
|---|---|---|---|---|
| **Domain** | Aggregates, entities, value objects, domain events, invariants, repository *interfaces* | None (base .NET only) | MongoDB driver, ASP.NET Core, any other BuzzMe project | `Board`, `Reminder`, `Occurrence`, `Buzz`, `Invitation`, `User`, `Block`; `IBoardRepository`, `IReminderRepository`, etc.; `SeedWork/` (AggregateRoot, Entity, ValueObject, DomainEvent base types, `Result<T>`) |
| **Application** | Use cases (Application Layer Spec §3, one class per bounded-context area), Policies (the reactive glue from Application Layer Spec §7), capability interfaces the use cases need but don't own | Domain only | MongoDB driver, ASP.NET Core, Infrastructure, Api | `BoardApplicationService`, `ReminderApplicationService`, `Policies/ReassignOwnershipPolicy`, `Abstractions/IPushNotificationSender`, `IClock`, `ICurrentUserContext` |
| **Infrastructure** | Implements Domain's repository interfaces against MongoDB; implements Application's capability interfaces against real providers; the outbox dispatcher's technical machinery | Domain, Application | Api, Workers (Infrastructure is never aware of its hosts) | `Persistence/Mongo/BoardRepository`, `Messaging/Push/FcmPushSender`, `Time/SystemClock`, `Persistence/Migrations/` |
| **Contracts** | Wire-format request/response DTOs matching [API_CONTRACT.md](./API_CONTRACT.md) exactly, the standard envelope, the error code enum | None — deliberately zero dependency on any other BuzzMe project | Domain, Application, Infrastructure | `V1/Boards/CreateBoardRequest`, `V1/Common/ApiResponse<T>`, `V1/Common/ErrorCode` |
| **Api** | HTTP host: endpoint definitions matching API_CONTRACT.md 1:1, request→command mapping, auth middleware, the composition root | Application, Contracts, Infrastructure (**for DI wiring only** — see §4) | Domain (endpoint code never touches a Domain type directly — it goes through Application) | `Endpoints/BoardEndpoints`, `Middleware/ExceptionHandlingMiddleware`, `Program.cs` |
| **Workers** | Hosts the outbox dispatcher and every time-scheduled job (§7); thin — delegates all real behavior to Application | Application, Infrastructure (for DI wiring only) | Contracts (Workers has no HTTP surface), Domain (same rule as Api) | `Jobs/GenerateOccurrencesJob`, `Jobs/TransitionMissedRemindersJob`, `Program.cs` |

**Dependency direction, stated once:** `Api`/`Workers` → `Application` → `Domain`, with `Infrastructure` implementing interfaces defined in `Domain`/`Application` and being referenced only at the composition root (`Program.cs`) of `Api`/`Workers` — never from inside an endpoint or job class directly. This is the Dependency Inversion at the heart of Clean Architecture, applied without extra ceremony: there is no `IApiService` or similar indirection layer between `Api` and `Application` — `Api` calls `Application`'s classes directly, because there's no second implementation of `Application` ever expected to exist.

---

## 3. Folder Structure

### Domain
```
BuzzMe.Domain/
  SeedWork/               (AggregateRoot, Entity, ValueObject, DomainEvent, Result<T>)
  Identity/
    User.cs, Block.cs, Events/
  Boards/
    Board.cs, Membership.cs (entity within Board), Events/
  Reminders/
    Reminder.cs, RecurrenceRule.cs (value object), Events/
  Occurrences/
    Occurrence.cs, Events/
  Notifications/
    Buzz.cs, Events/
  Invitations/
    Invitation.cs, Events/
```
Domain events are colocated inside each aggregate's own folder (`Reminders/Events/ReminderCreated.cs`), not gathered into one flat top-level `Events/` folder — as the system grows, a feature's full vocabulary should be readable in one place rather than scattered by type.

### Application
```
BuzzMe.Application/
  Boards/           BoardApplicationService.cs, Models/ (request/result shapes internal to Application, distinct from Contracts' wire DTOs)
  Invitations/       InvitationApplicationService.cs, Models/
  Reminders/         ReminderApplicationService.cs, Models/
  Occurrences/       OccurrenceApplicationService.cs, Models/
  Account/           AccountApplicationService.cs, Models/
  Notifications/     NotificationApplicationService.cs, Models/
  Policies/          ReassignOwnershipPolicy.cs, GrantMembershipOnInvitationAcceptedPolicy.cs,
                     CancelPendingBuzzesPolicy.cs, RescheduleBuzzesOnNotifyPresetChangedPolicy.cs,
                     RevokePendingInvitationsOnBlockPolicy.cs
  Abstractions/      IPushNotificationSender.cs, IEmailSender.cs, ISmsSender.cs, IClock.cs, ICurrentUserContext.cs
  Common/            PagedResult.cs
```
**One Application Service class per bounded-context area, not one handler class per use case.** A generic mediator/command-handler-per-file pattern (e.g., a full MediatR pipeline) was considered and deliberately not recommended: BuzzMe's cross-cutting concerns (authorization-first, business validation, idempotency) are already fully specified per use case in the Application Layer Specification, so a pipeline-behavior abstraction would add indirection without a matching benefit at this size. `BoardApplicationService.LeaveBoardAsync(...)` is exactly as discoverable as an `IRequestHandler<LeaveBoardCommand>` and involves one fewer file and one fewer interface per use case. This is not a one-way door — if the team later wants uniform pipeline behavior across dozens of use cases, each method can become a handler without restructuring anything above it.

### Infrastructure
```
BuzzMe.Infrastructure/
  Persistence/
    Mongo/
      MongoContext.cs
      Repositories/    BoardRepository.cs, ReminderRepository.cs, OccurrenceRepository.cs, ...
      Documents/       BoardDocument.cs, ... (Mongo document shapes — see §6, never the same class as the Domain aggregate)
      Mappers/         BoardMapper.cs, ...
      Indexes/         (index-creation definitions, one file per collection)
    Migrations/        (numbered, idempotent scripts + a tracked `_migrations` collection)
    Outbox/             OutboxWriter.cs, OutboxDispatcher.cs (the technical mechanism; see §7)
  Messaging/
    Push/    FcmPushSender.cs, ApnsPushSender.cs
    Email/   EmailSender.cs
    Sms/     SmsSender.cs
  Time/      SystemClock.cs
  DependencyInjection/  ServiceCollectionExtensions.cs
```
**`ICurrentUserContext`'s implementation lives in `BuzzMe.Api`, not Infrastructure** — it's inherently tied to the ASP.NET Core `HttpContext`, and not every technical concern belongs in the project literally named "Infrastructure" just because it isn't Domain or Application. The interface is declared in `Application/Abstractions` (Application needs it); its one implementation reading claims off the current request belongs next to the request pipeline that produces it.

### Contracts
```
BuzzMe.Contracts/
  V1/
    Boards/ Invitations/ Reminders/ Occurrences/ Account/ Notifications/
    Common/  ApiResponse<T>.cs, ApiError.cs, PaginationInfo.cs, ErrorCode.cs
```
Versioned by folder, mirroring API_CONTRACT.md §9's URL versioning exactly — a `V2` folder, if it's ever needed, sits alongside `V1`, never replacing it.

### Api
```
BuzzMe.Api/
  Endpoints/   BoardEndpoints.cs, InvitationEndpoints.cs, ReminderEndpoints.cs, OccurrenceEndpoints.cs,
               AccountEndpoints.cs, NotificationEndpoints.cs, AuthEndpoints.cs
  Middleware/  ExceptionHandlingMiddleware.cs
  Mapping/     BoardMapping.cs, ... (Contracts DTO ↔ Application request/result — extension methods, not a generic mapper library)
  Configuration/
  Program.cs
```
**Minimal API endpoint groups, not MVC controllers.** For a surface this fully specified already (every route, verb, and response shape is fixed by API_CONTRACT.md), controller-base-class and attribute-routing ceremony adds nothing — a static `MapBoardEndpoints(this WebApplication app)` extension method per resource area is shorter, equally testable, and the more idiomatic ASP.NET Core style for a greenfield project on this version of the framework. If the team has a strong existing preference for Controllers, that's a legitimate substitution — the one rule that must not be broken is picking one style and never mixing the two, exactly as API_CONTRACT.md §10 insists on one naming convention with no mixed styles.

### Workers
```
BuzzMe.Workers/
  Jobs/
    GenerateOccurrencesJob.cs
    OutboxDispatcherJob.cs
    RetryFailedNotificationsJob.cs
    ExpireInvitationsJob.cs
    TransitionMissedRemindersJob.cs
    PurgeDeletedBoardsAndAccountsJob.cs
  Program.cs
```

---

## 4. Dependency Rules

- **Direction is one-way, always:** `Api`/`Workers` → `Application` → `Domain`. `Infrastructure` implements interfaces *declared* in `Domain` (repositories) and `Application` (capabilities) but is only *referenced* from the composition root of `Api`/`Workers` — never from a handler, endpoint, or job class.
- **No circular references are structurally possible** as long as the above holds — `Domain` references nothing else in the solution, which is what makes the rest of the graph acyclic by construction.
- **Domain may reference:** nothing but the .NET base class library. Not even a JSON serialization package — if a Domain type needs to be persisted, that mapping is Infrastructure's job (§6), not Domain's.
- **Infrastructure may reference:** `Domain` and `Application` (to implement their interfaces) and third-party packages (MongoDB driver, push/email/SMS SDKs). It may never be referenced by `Domain` or `Application` — the dependency points inward, per Clean Architecture, without exception.
- **DTOs live in `Contracts`, only.** No Domain aggregate, Application result model, or Infrastructure document type is ever serialized directly onto the wire. Mapping between them happens in `Api/Mapping` (Contracts ↔ Application) and `Infrastructure/Persistence/Mongo/Mappers` (Domain ↔ Mongo documents) — two distinct, deliberately separate mapping boundaries, never one shared "universal mapper."

---

## 5. Naming Standards

| Element | Convention | Example |
|---|---|---|
| Projects | `BuzzMe.{Layer}` | `BuzzMe.Application` |
| Namespaces | Mirror the folder path exactly | `BuzzMe.Domain.Reminders` |
| Aggregate/Entity classes | Singular noun, PascalCase | `Board`, `Reminder`, `Occurrence` |
| Value Objects | Singular noun or descriptive phrase, PascalCase, always a `record` | `RecurrenceRule`, `ReferenceTimezone` |
| Domain Events | Past-tense fact, PascalCase, suffix-free (no `Event` suffix — the folder already says what it is) | `ReminderCreated`, `BoardOwnershipReassigned` |
| Application Service methods (the "commands"/"queries" from prior specs) | `{Verb}{Noun}Async`, matching the use case name in Application Layer Spec exactly | `LeaveBoardAsync`, `ListBoardRemindersAsync` |
| Policies | `{Trigger}{Effect}Policy` or `{Effect}Policy` where the trigger is obvious from its folder | `ReassignOwnershipPolicy` |
| Repository interfaces/implementations | `I{Aggregate}Repository` / `{Aggregate}Repository` | `IReminderRepository`, `ReminderRepository` |
| Endpoint classes | `{Resource}Endpoints` | `BoardEndpoints` |
| Endpoint route names (for `MapGroup`/route naming) | Match API_CONTRACT.md's path exactly, kebab-case segments | `/v1/boards/{boardId}/members` |
| Mongo collections | Lowercase, plural, snake-free (single word where possible) | `boards`, `reminders`, `occurrences`, `buzzes`, `invitations`, `history`, `outbox` |
| Mongo indexes | `ix_{collection}_{fields}` | `ix_occurrences_reminderId_dueAt` |
| Configuration sections | PascalCase matching the bound Options class | `PushNotifications`, `MongoSettings` |

No element in this table has a second acceptable style — a PR introducing `snake_case` fields, a `-Handler` suffix, or a `tbl_` collection prefix is a naming violation, not a style preference.

---

## 6. MongoDB Organisation

**One aggregate root, one collection, one document — no aggregate is re-modeled to fit this rule; it already fits, because Implementation Spec §1 already drew the aggregate boundaries with exactly this in mind** (Occurrence and Buzz were split out from Reminder specifically so high-frequency writes wouldn't require touching a large parent document — precisely the shape MongoDB rewards).

| Collection | Contains | Notes |
|---|---|---|
| `users` | User aggregate | Includes `personalBoardId` |
| `boards` | Board aggregate, **with Membership embedded as a sub-document array** | Membership lives inside the Board aggregate at the domain layer (Implementation Spec §1) — embedding it is the direct, correct MongoDB translation of that decision, not a new one |
| `reminders` | Reminder aggregate | `referenceTimezone` stored as an IANA string field, per Implementation Spec §1 |
| `occurrences` | Occurrence aggregate | Separate collection — the reason Occurrence is its own aggregate root in the first place |
| `buzzes` | Buzz aggregate | One document per (Occurrence, recipient) pair |
| `invitations` | Invitation aggregate | |
| `blocks` | Block aggregate | |
| `history` | Append-only History/Activity ledger | Never updated in place, only inserted; denormalizes the Reminder's title and Board name at write time (see soft-delete note below) |
| `outbox` | Domain events awaiting dispatch to Policies/background jobs (§7) | Not a Domain concept — a purely technical Infrastructure mechanism |

**Indexes**, each one tied to a query pattern or invariant already specified rather than added speculatively:

| Collection | Index | Purpose |
|---|---|---|
| `users` | unique on `email`; unique, sparse on `phone` | Uniqueness invariant (Implementation Spec §5) |
| `boards` | multikey on `memberships.userId` | Backs "List Boards (mine)" |
| `reminders` | compound on `(boardId, createdAt)` | Backs List Board Reminders' default ordering |
| `occurrences` | **unique** compound on `(reminderId, dueAt)` | Directly enforces the generation idempotency key stated in Implementation Spec §1 at the database level, not just in application logic |
| `occurrences` | compound on `(status, dueAt)` | Backs the Missed-transition sweep and Buzz-scheduling queries |
| `buzzes` | **unique** compound on `(occurrenceId, recipientUserId)` | Enforces the Buzz idempotency key (Implementation Spec §5) at the database level |
| `invitations` | unique on `token`; compound on `(status, expiresAt)` | Token lookup; backs the Expire Invitations sweep |
| `blocks` | unique compound on `(blockerUserId, blockedUserId)` | Prevents duplicate Block rows |
| `history` | compound on `(reminderId, timestamp)` | Backs List Reminder History |
| `outbox` | compound on `(processed, availableAt)` | Backs the dispatcher's polling query |

**Optimistic concurrency:** every document carries a `version` integer. Every update targets `{ _id: x, version: expectedVersion }` and checks the modified-count is exactly 1; a modified-count of 0 is the concurrency conflict — this is the concrete mechanism behind Occurrence resolution's "already done by X" outcome (Implementation/Application Layer Specs' `expectedVersion` field) and Reminder's field-level idempotent-update behavior.

**Soft deletes:** `Board` and `User` carry a `deletedAt: DateTime?`, set at deletion and physically purged only after the 14-day grace window (Implementation Spec §6) by the Purge job. **`Reminder` does not carry the same grace period** — no undo-delete was ever specified for it, so its document is removed promptly on `ReminderDeleted`. This does **not** violate "History never deletes": `Occurrence` and `history` documents never cascade-delete when their parent `Reminder` disappears, and `history` denormalizes the Reminder's title and Board name at write time specifically so a History entry remains fully readable after the Reminder document it refers to is gone.

**Migration strategy:** MongoDB's schemaless nature doesn't remove the need for a disciplined process — numbered, idempotent scripts under `Infrastructure/Persistence/Migrations`, tracked in a `_migrations` collection, run at startup or as an explicit deploy step. No heavyweight migration framework is prescribed; the pattern matters more than the tool at this scale.

---

## 7. Background Processing Structure

Two genuinely different kinds of "background work" exist in this system, and conflating them is the most common mistake at this layer:

**A. Event-reactive work** (Application Layer Spec §7's Policies — cancel pending Buzzes, reassign ownership, grant Membership, revoke pending Invitations, reschedule Buzzes on a notify-preset change): these fire *because something just happened*, not on a clock. Implemented via a **transactional outbox**: when an aggregate's transaction commits, its raised domain events are written to the `outbox` collection **in the same MongoDB transaction** (MongoDB's multi-document ACID transactions, used here for exactly this one purpose — not as a general pattern). `OutboxDispatcherJob` (in `Workers`) polls `outbox` for unprocessed rows and invokes the matching Policy in `Application`, marking each row processed only on success — giving the "retried until success" guarantee every policy in the Application Layer Spec requires, without a message broker.

**B. Time-scheduled work** (Occurrence generation's rolling horizon, Missed-transition, Invitation expiry, Purge, Retry Failed Notifications): these run on a clock regardless of whether anything happened recently. Implemented as plain `BackgroundService` classes using `PeriodicTimer` — no external scheduler library is introduced for V1's job volume; if the number or complexity of scheduled jobs grows significantly later, a dedicated scheduler (Quartz.NET, Hangfire) can replace these without restructuring anything above `Workers`.

**Occurrence generation is genuinely both:** reactive (generate the first Occurrence immediately after `ReminderCreated`/`RecurrenceRuleUpdated`, via the outbox path) *and* scheduled (a periodic sweep keeps the rolling horizon topped up for Reminders nobody has touched recently, so the next Occurrence is never late just because nothing triggered it). Both paths call the same `ReminderApplicationService` generation method — there is exactly one place this logic lives, invoked from two different triggers.

| Process | Trigger Type | Hosted In |
|---|---|---|
| Generate Reminder Occurrences | Both (reactive + scheduled sweep) | `Workers/Jobs/GenerateOccurrencesJob` |
| Generate Buzzes | Reactive (via outbox, on `OccurrenceGenerated`) | `Workers/Jobs/OutboxDispatcherJob` |
| Dispatch Push Notifications | Reactive (via outbox, on `BuzzGenerated`) | `Workers/Jobs/OutboxDispatcherJob` |
| Retry Failed Notifications | Reactive trigger + delayed re-check (`availableAt` on the outbox row) | `Workers/Jobs/RetryFailedNotificationsJob` |
| Expire Invitations | Scheduled sweep | `Workers/Jobs/ExpireInvitationsJob` |
| Transition Missed Reminders | Scheduled sweep, hourly (Implementation Spec §6) | `Workers/Jobs/TransitionMissedRemindersJob` |
| Clean Up Deleted Boards/Accounts | Scheduled sweep, checks the 14-day grace window | `Workers/Jobs/PurgeDeletedBoardsAndAccountsJob` |

---

## 8. Testing Strategy

| Layer | What Belongs Here | Example |
|---|---|---|
| **Unit tests** (`BuzzMe.Domain.Tests`, `BuzzMe.Application.Tests`) | Aggregate invariants in isolation, no I/O; Application Service orchestration logic against fakes/mocks of `Domain` repository interfaces and `Application/Abstractions` capability interfaces | "Leaving a Board as sole Owner with other Members reassigns ownership in the same call" |
| **Application tests** (still `BuzzMe.Application.Tests`, a distinct suite from pure unit tests) | Each Application Service method tested against every row of its Application Layer Spec table — preconditions, validation, events emitted, side effects, idempotency — traceable one-to-one back to that document | Every row of Application Layer Spec §3 becomes at least one test case |
| **Integration tests** (`BuzzMe.Infrastructure.IntegrationTests`) | Repository implementations against a real MongoDB (ephemeral instance/Testcontainers) — index behavior (does the unique index actually reject a duplicate Buzz), the outbox dispatcher end-to-end | "A duplicate `(occurrenceId, recipientUserId)` Buzz insert fails at the database, not just in application logic" |
| **API tests** (`BuzzMe.Api.IntegrationTests`) | Every endpoint in API_CONTRACT.md §5, run against the real host (`WebApplicationFactory`), asserting exact status codes, envelope shape, and error codes | One test class per resource area, one test method per documented success/error case |
| **End-to-end tests** | A small, deliberately limited suite exercising the actual user journeys already documented (Product UX Specification §16 / Information Architecture §16) across the real stack | "Create Board → Invite → Accept → Create Reminder → Complete" as one continuous test |

The guiding rule: **the higher up this table, the more tests are expected; the lower down, the fewer.** End-to-end tests exist to catch integration mistakes the layers above couldn't, not to re-verify business rules already covered by unit and Application tests.

---

## 9. Development Standards

| Concern | Standard |
|---|---|
| **Validation** | Format/shape validation (a title isn't empty, an enum value is valid) lives at the `Api` boundary using FluentValidation against `Contracts` request types. Business validation (Application Layer Spec §4) lives in `Application`/`Domain` — never duplicated in the `Api` layer beyond format checks. |
| **Error handling** | A `Result<T>` type (in `Domain/SeedWork`) is returned for every *expected* business outcome (rejections, conflicts, no-ops) — never an exception. Exceptions are reserved for genuinely unexpected faults (a database connection drop, a bug), caught once by `ExceptionHandlingMiddleware` and mapped to `500 SERVER_ERROR`. Every other error code in API_CONTRACT.md §6 maps from a specific `Result` failure type, never from a caught exception. |
| **Logging** | Structured logging via `ILogger<T>`; a correlation ID generated per request and carried into any outbox row it produces, so a background job's log line can be traced back to the request that caused it. No email, phone number, or display name is ever written to a log line — only IDs — consistent with the product's own privacy stance. |
| **Configuration** | Strongly-typed `IOptions<T>`, bound once at startup; no `IConfiguration["..."]` string-indexed reads scattered through business code. |
| **Feature flags** | Used sparingly, for operational rollout only (e.g., switching push providers) — never to toggle product behavior, which would directly contradict the product's own "one obvious way" principle carried through every prior document. |
| **Dependency injection** | Constructor injection only. Scoped lifetime for anything request-bound (repositories, `ICurrentUserContext`); singleton for stateless infrastructure clients; transient is rarely needed and should prompt a second look if reached for. |
| **Time handling** | All time is read through `IClock` — never `DateTime.Now`/`UtcNow` directly inside `Domain` or `Application`. This is not a style preference; it's the mechanism that makes the `referenceTimezone` correctness work in Implementation Spec §1 actually testable. |
| **ID generation** | Time-sortable IDs (e.g., GUIDv7/ULID), not random GUIDv4 — this makes natural MongoDB insertion order meaningful and gives cursor pagination (API_CONTRACT.md §7) a simple, correct cursor value for free. |
| **Transactions** | MongoDB multi-document transactions are used in exactly one place: writing an aggregate's change and its outbox event together (§7). Nowhere else in this system needs one, because "one aggregate, one document" already removes the usual reasons to reach for one. |
| **Serialization** | camelCase JSON, matching API_CONTRACT.md's field naming exactly; enums serialize as strings, never integers; all date/times as ISO 8601 UTC. |
| **Versioning** | Already fully specified by API_CONTRACT.md §9; `Contracts`' `V1/` folder is its direct implementation — no separate decision needed here. |

---

## 10. Coding Standards

- **File size:** no hard limit, but a file exceeding roughly 300 lines is a signal to ask whether it's doing more than one job, not a rule to mechanically split against.
- **Method size:** small and single-purpose; a method that's grown past ~30–40 lines or needs a comment to explain its "sections" should usually become two methods.
- **Class responsibilities:** one Application Service per bounded-context area (§3); one class per aggregate; no `*Manager`/`*Helper` classes that accumulate unrelated behavior over time.
- **Constructor injection, always** — no property injection, no service-locator calls anywhere in the codebase.
- **Immutability:** `Domain` entities and aggregates expose behavior through methods that enforce invariants, never public setters. Value Objects, `Contracts` DTOs, and Domain Events are immutable `record` types.
- **Records vs. classes:** `record` for anything that should have value equality and no independent identity (Value Objects, Events, DTOs). `class` for anything with identity that persists across state changes (Aggregates, Entities) — giving these `record`'s structural equality by default would actively misrepresent what they are.
- **Exceptions vs. Result objects:** covered in §9 — Result for expected outcomes, exceptions for genuine faults only. A method that throws for "cannot remove yourself" is a standards violation, not a style choice.
- **Async guidelines:** async all the way down for anything doing I/O; `ConfigureAwait(false)` inside `Domain`/`Application`/`Infrastructure` (no `SynchronizationContext` concerns there); no `async void` outside a top-level event handler; no blocking `.Result`/`.Wait()` anywhere.
- **Cancellation tokens:** every async method performing I/O accepts and forwards one, sourced from the ASP.NET Core request or the Worker's stopping token — never `CancellationToken.None` as a habit.
- **Nullable reference types:** enabled project-wide, no exceptions. The null-forgiving operator (`!`) requires an inline comment explaining why the compiler's warning doesn't apply — an unexplained `!` fails review.

---

*This guide, together with [IMPLEMENTATION_SPEC.md](./IMPLEMENTATION_SPEC.md), [APPLICATION_LAYER_SPEC.md](./APPLICATION_LAYER_SPEC.md), and [API_CONTRACT.md](./API_CONTRACT.md), is sufficient for a new engineer to clone the repository and know where the next piece of code belongs before writing it. Where this guide recommends a specific pattern over a textbook alternative — one Application Service per area instead of per-use-case handlers, Minimal APIs instead of Controllers, `BackgroundService` instead of a scheduler library — the reasoning is stated inline precisely so a future team can revisit it deliberately, not accidentally.*
