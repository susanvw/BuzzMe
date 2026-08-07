# BuzzMe — Repository Bootstrap

*The production-ready repository skeleton, built exactly to [DEVELOPMENT_GUIDE.md](./DEVELOPMENT_GUIDE.md). Every project compiles, every dependency is correct, and every check below was actually run against the real repository — not asserted from a plan. No business logic, no domain classes, no API endpoints exist yet; this document is the map for adding them.*

**Verified, not assumed:** `dotnet build BuzzMe.sln` → 0 warnings, 0 errors, 10/10 projects. `dotnet test` → all four test projects execute (0 tests, by design). `npm install && npm run typecheck && npm run lint` → clean across all four workspace packages. `npm run build --workspace=@buzzme/web` → real production bundle, 190KB.

---

## 1. Repository Tree

```
BuzzMe/
├── .github/workflows/
│   ├── backend-ci.yml
│   └── frontend-ci.yml
├── .gitignore
├── global.json                          (pins the .NET SDK to 10.0.100)
├── BuzzMe.sln
├── package.json                         (npm workspaces root)
│
├── docs/                                (all 14 prior architecture documents)
│
├── src/
│   ├── BuzzMe.Domain/
│   │   └── SeedWork/                    Entity, AggregateRoot, ValueObject, IDomainEvent, Error, Result, IIdGenerator
│   ├── BuzzMe.Application/
│   │   ├── Abstractions/                IClock, ICurrentUserContext, IPushNotificationSender, IEmailSender, ISmsSender
│   │   └── Common/                      PagedResult
│   ├── BuzzMe.Infrastructure/
│   │   ├── DependencyInjection/         InfrastructureServiceCollectionExtensions
│   │   ├── Ids/                         TimeSortableIdGenerator
│   │   ├── Messaging/{Push,Email,Sms}/  Null* senders (log, don't deliver, until a real provider is wired)
│   │   ├── Persistence/
│   │   │   ├── Mongo/                   MongoOptions, MongoContext, MongoHealthCheck
│   │   │   ├── Outbox/                  OutboxMessage, IOutboxWriter, MongoOutboxWriter
│   │   │   └── Migrations/              IMongoMigration, MongoMigrationRecord, MongoMigrationRunner
│   │   └── Time/                        SystemClock
│   ├── BuzzMe.Contracts/
│   │   └── V1/Common/                   ApiResponse, ApiListResponse, ApiError, PaginationInfo, ErrorCode
│   ├── BuzzMe.Api/
│   │   ├── Configuration/               JwtOptions
│   │   ├── Identity/                    HttpCurrentUserContext
│   │   ├── Middleware/                  ExceptionHandlingMiddleware
│   │   ├── Program.cs
│   │   └── appsettings*.json
│   └── BuzzMe.Workers/
│       ├── Program.cs
│       └── appsettings*.json
│
├── tests/
│   ├── BuzzMe.Domain.Tests/
│   ├── BuzzMe.Application.Tests/
│   │   └── TestDoubles/                 FakeClock, FakeCurrentUserContext, Recording{Push,Email,Sms}Sender
│   ├── BuzzMe.Infrastructure.IntegrationTests/
│   │   └── MongoIntegrationTestFixture.cs   (real MongoDB via Testcontainers)
│   └── BuzzMe.Api.IntegrationTests/
│       └── BuzzMeApiFactory.cs          (real host via WebApplicationFactory<Program>)
│
├── apps/
│   ├── web/                             React + Vite + TypeScript
│   │   └── src/{lib,App.tsx,main.tsx}
│   └── mobile/                          React Native + Expo + TypeScript
│       └── src/lib/
│
└── packages/
    ├── api-client/                      One shared TypeScript client — envelopes + a generic fetch wrapper, no per-resource calls yet
    └── domain-constants/                Recurrence/NotifyPreset/Role/ErrorCode — the same fixed vocabulary as the C# side
```

---

## 2. Solution Layout

`BuzzMe.sln` at the repository root references all ten .NET projects — six under `src/`, four under `tests/` — exactly as named in [DEVELOPMENT_GUIDE.md](./DEVELOPMENT_GUIDE.md) §1. `global.json` pins the SDK to `10.0.100` with `latestMinor` roll-forward, so every machine and CI runner builds against the same compiler regardless of what else is installed.

The frontend is a separate, sibling concern — an npm-workspaces monorepo rooted at the same repository root, declared in the top-level `package.json`'s `workspaces` field (`apps/*`, `packages/*`). The two toolchains don't reference each other; they meet only at the API contract (`docs/API_CONTRACT.md`) that `packages/api-client` implements against.

---

## 3. Project References

Exactly as specified — verified by `dotnet build` succeeding with these references in place, not just declared:

```
BuzzMe.Application     → BuzzMe.Domain
BuzzMe.Infrastructure  → BuzzMe.Domain, BuzzMe.Application
BuzzMe.Api             → BuzzMe.Application, BuzzMe.Contracts, BuzzMe.Infrastructure   (composition root only)
BuzzMe.Workers         → BuzzMe.Application, BuzzMe.Infrastructure                     (composition root only)
BuzzMe.Contracts       → (none)

BuzzMe.Domain.Tests                 → BuzzMe.Domain
BuzzMe.Application.Tests            → BuzzMe.Application
BuzzMe.Infrastructure.IntegrationTests → BuzzMe.Infrastructure, BuzzMe.Domain
BuzzMe.Api.IntegrationTests         → BuzzMe.Api
```

No project references anything that would create a cycle — `Domain` references nothing else in the solution, which is what makes the rest of the graph acyclic by construction (DEVELOPMENT_GUIDE.md §4).

---

## 4. Startup Sequence

Identical in `BuzzMe.Api` and `BuzzMe.Workers`, because both are composition roots over the same `AddBuzzMeInfrastructure` call:

1. **Configuration binds** — `MongoOptions`, `JwtOptions` (Api only) from `appsettings.json` / environment.
2. **Serialization configured** — camelCase, enums as strings (Api only; Workers has no HTTP surface).
3. **`AddBuzzMeInfrastructure`** registers Mongo, the outbox writer, the migration runner, `IClock`, `IIdGenerator`, and the three (currently no-op, logging) messaging senders.
4. **Authentication/Authorization configured** (Api only) — JWT Bearer, validated against `JwtOptions`.
5. **Health checks registered** — `"ready"` (Mongo-dependent, from Infrastructure) and `"live"` (Api only, always healthy — "is the process up").
6. **Host built.**
7. **Migrations run**, once, before the app accepts traffic or a job starts — `MongoMigrationRunner.RunAsync`, awaited synchronously. An environment with no reachable MongoDB fails fast here, by design — starting in a half-working state is worse than not starting.
8. **Middleware pipeline** (Api only): `ExceptionHandlingMiddleware` → `UseAuthentication` → `UseAuthorization`.
9. **Health endpoints mapped**: `/health/live`, `/health/ready`.
10. **Feature endpoints/jobs are mapped here** — currently nothing, by design (see §9).
11. **`Run()`.**

---

## 5. Dependency Injection Registration

One extension method is the single source of truth for Infrastructure's own registrations — `InfrastructureServiceCollectionExtensions.AddBuzzMeInfrastructure`, called identically from both hosts:

| Registered | Lifetime | Notes |
|---|---|---|
| `MongoContext` | Singleton | One Mongo connection per process |
| Mongo health check | — | Tagged `"ready"` |
| `IClock` → `SystemClock` | Singleton | Stateless |
| `IIdGenerator` → `TimeSortableIdGenerator` | Singleton | Guid v7, stateless |
| `IOutboxWriter` → `MongoOutboxWriter` | Singleton | Stateless (writes are always scoped to a passed-in session) |
| `MongoMigrationRunner` | Singleton | Run once at startup |
| `IPushNotificationSender` → `NullPushNotificationSender` | Singleton | Logs, doesn't deliver — swap in a real adapter later, same lifetime |
| `IEmailSender` → `NullEmailSender` | Singleton | Same pattern |
| `ISmsSender` → `NullSmsSender` | Singleton | Same pattern |
| Repositories | — | `AddRepositories()` exists and is called, deliberately empty — the designated home for every future `IBoardRepository`-style registration |

`BuzzMe.Api` additionally registers, itself (not delegated to Infrastructure, since both are inherently request-pipeline concerns per DEVELOPMENT_GUIDE.md §3):

| Registered | Lifetime |
|---|---|
| `ICurrentUserContext` → `HttpCurrentUserContext` | Scoped |
| `IHttpContextAccessor` | Singleton (framework default) |
| JWT Bearer authentication handler | — |

---

## 6. Configuration Structure

Strongly-typed `Options` only, bound once — no scattered `IConfiguration["..."]` reads exist anywhere in the codebase (DEVELOPMENT_GUIDE.md §9):

- `MongoOptions` (`Mongo` section) — `ConnectionString`, `DatabaseName`. Present in both `BuzzMe.Api` and `BuzzMe.Workers`' `appsettings.json`.
- `JwtOptions` (`Jwt` section) — `Issuer`, `Audience`, `SigningKey`, token lifetimes. `BuzzMe.Api` only.

**Secrets are never committed.** `appsettings.Development.json` carries an explicitly-labeled `local-development-signing-key-not-for-production-use-only` — real environments supply `Jwt:SigningKey` and provider credentials via environment variables or the hosting platform's secret store, never a checked-in file. `.gitignore` explicitly excludes `.env*` and any `appsettings.*.local.json` as a second line of defense.

---

## 7. Testing Structure

Four projects, one per layer named in DEVELOPMENT_GUIDE.md §8 (the fifth layer, End-to-end, is a future addition against a deployed environment, not a project in this repository):

| Project | What's already there | What isn't (yet) |
|---|---|---|
| `BuzzMe.Domain.Tests` | Project + reference wired | No aggregates exist to test |
| `BuzzMe.Application.Tests` | `TestDoubles/` — `FakeClock`, `FakeCurrentUserContext`, `RecordingPushNotificationSender`/`RecordingEmailSender`/`RecordingSmsSender` (spies, for asserting external side effects separately from domain state — DEVELOPMENT_GUIDE.md §8) | No Application Services exist to test |
| `BuzzMe.Infrastructure.IntegrationTests` | `MongoIntegrationTestFixture` — a real, ephemeral MongoDB via Testcontainers, one per test collection | No repositories exist to test against it |
| `BuzzMe.Api.IntegrationTests` | `BuzzMeApiFactory` — a real host via `WebApplicationFactory<Program>`, with test-only Jwt/Mongo config overrides | No endpoints exist to assert against |

**Test naming convention** (applied from the first real test onward): `MethodOrScenario_Condition_ExpectedOutcome`, e.g. `LeaveBoard_SoleOwnerWithOtherMembers_ReassignsOwnership`. One test class per Application Service or aggregate, mirroring the class it tests — not one enormous shared test class per project.

No actual `[Fact]` exists anywhere, as instructed; `dotnet test` confirms all four projects execute and report zero tests, which is the correct state for a foundation with no business logic yet.

---

## 8. CI/CD Structure

Two independent GitHub Actions workflows, each path-filtered so a backend-only or frontend-only change doesn't run the other:

**`backend-ci.yml`** — `build-and-test` (restore → build → unit/Application tests → integration tests against a real, Testcontainers-provisioned MongoDB, since Docker is available on GitHub-hosted runners by default) → `publish` (main branch only: `dotnet publish` for `BuzzMe.Api` and `BuzzMe.Workers`, artifacts uploaded — no deployment step is prescribed here, since target hosting wasn't specified).

**`frontend-ci.yml`** — install (`npm ci`, whole workspace) → lint → typecheck → build `apps/web` (main branch only: dist uploaded as an artifact). `apps/mobile` is deliberately not built here — Expo apps ship via EAS Build/Submit, a separate pipeline outside plain GitHub Actions compute, added when the mobile app has something to publish.

**Environment configuration & secrets:** neither workflow embeds a connection string, signing key, or provider credential. Real values belong in GitHub Actions repository/environment secrets (`Settings → Secrets and variables → Actions`), injected as env vars at the publish step — this repository's checked-in config only ever contains safe, clearly-labeled local-development defaults (§6).

---

## 9. First Implementation Order

The sequence that lets each step be tested against something real, rather than against a mock of a mock:

1. **`Board` aggregate** (Domain) — the simplest aggregate, no dependencies on any other. Implement it, its `IBoardRepository` interface (Domain), and `BoardRepository` (Infrastructure) together, so the first real MongoDB read/write path exists end to end.
2. **`CreateBoard` / `RenameBoard` / `DeleteBoard`** (Application's `BoardApplicationService`) against that repository — the first Application-layer tests become real.
3. **`BuzzMe.Api`'s `BoardEndpoints`** — the first two real HTTP endpoints, tested against `BuzzMeApiFactory`, proving the whole stack (Contracts → Api → Application → Domain → Infrastructure → Mongo) end to end for the simplest possible slice.
4. **`User` aggregate + Register/Verify/Login** — unblocks real authentication for every endpoint after this point (everything through step 3 can be built and tested with a fake `ICurrentUserContext`, but nothing past it can ship without real auth).
5. **`Membership` (inside `Board`) + Invite/Accept/Remove/Leave** — the second-most-load-bearing slice, and the one that exercises the two-step `AcceptInvitation` workflow and the outbox for the first time.
6. **`Reminder` + `Occurrence` + `Buzz` aggregates**, in that order, each with its repository — this is the largest remaining slice, and the reason Boards/Membership came first: by this point the outbox, the migration runner, and the Api/Application testing patterns are all already proven.
7. **`GenerateOccurrencesJob`, `OutboxDispatcherJob`** (`BuzzMe.Workers`) — the first real background jobs, calling into the Application Services built in step 6.
8. **Everything else in `DEVELOPMENT_GUIDE.md` §7's process table**, in any order, since by this point each is an incremental addition to an already-proven pattern.

---

## 10. Remaining Implementation Checklist

Everything this bootstrap deliberately left out, grouped by where it lands:

**Domain** — every aggregate (`User`, `Board`, `Reminder`, `Occurrence`, `Buzz`, `Invitation`, `Block`) and their repository interfaces; domain events per IMPLEMENTATION_SPEC.md §3.

**Application** — every Application Service and its use-case methods (APPLICATION_LAYER_SPEC.md §3); every Policy (§7 of that document); the concrete request/result models each service needs.

**Infrastructure** — every `I{Aggregate}Repository` implementation and its Mongo document/mapper pair; every `IMongoMigration` (index creation, per DEVELOPMENT_GUIDE.md §6's index table); a real push/email/SMS provider adapter to replace each `Null*Sender`.

**Contracts** — the `V1/Boards`, `V1/Reminders`, `V1/Invitations`, `V1/Account`, `V1/Notifications` request/response DTOs, one folder per API_CONTRACT.md §5's resource groups.

**Api** — every `*Endpoints.cs` file and its `Map*Endpoints` registration in `Program.cs`; FluentValidation validators per Contracts request type; the auth token-issuance logic itself (Register/Login/RefreshToken currently have no endpoint at all).

**Workers** — every job class in DEVELOPMENT_GUIDE.md §7's table, each an `AddHostedService<TJob>()` registration in `Program.cs`.

**Frontend** — every screen named in INFORMATION_ARCHITECTURE.md's tree, starting with `apps/web/src/screens/` and `apps/mobile/src/screens/`; the per-resource methods on `BuzzMeApiClient` (`createBoard`, `listBoards`, ...) as each backend endpoint ships; real token storage/refresh in both `apiClient.ts` files, replacing `getAccessToken: () => null`.

**Testing** — the first real test in each of the four projects, written alongside the first real aggregate/service/endpoint from §9, not batched up afterward.

Nothing in this list requires revisiting a decision already made in this document or [DEVELOPMENT_GUIDE.md](./DEVELOPMENT_GUIDE.md) — it's additive work inside a structure that already compiles, tests, lints, and builds.
