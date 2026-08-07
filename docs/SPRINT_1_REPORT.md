# Sprint 1 Report — Create and Retrieve Boards

*The smallest complete vertical slice: a developer can start MongoDB, start the API, create a Board, retrieve it, and see it in their list. Nothing more was built.*

---

## 1. Repository Changes

Existing files touched (all from the bootstrap; none redesigned, only extended to support Boards):

| File | Change |
|---|---|
| `src/BuzzMe.Domain/BuzzMe.Domain.csproj` | Added `InternalsVisibleTo` for `BuzzMe.Infrastructure` and `BuzzMe.Domain.Tests`, needed for aggregate rehydration (§3). |
| `src/BuzzMe.Api/BuzzMe.Api.csproj` | Added a project reference to `BuzzMe.Domain` and the `FluentValidation.DependencyInjectionExtensions` package — see §5's flagged deviation. |
| `src/BuzzMe.Infrastructure/Persistence/Mongo/MongoContext.cs` | Added a static constructor registering `GuidSerializer(GuidRepresentation.Standard)` — see §5's flagged contradiction; this is a real bug fix, not a design change. |
| `src/BuzzMe.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` | Registered `IBoardRepository → BoardRepository` and `IMongoMigration → CreateBoardIndexes`. |
| `src/BuzzMe.Api/Program.cs` | Registered `BoardApplicationService`, FluentValidation validators, and mapped `BoardEndpoints`. |
| `tests/BuzzMe.Application.Tests/BuzzMe.Application.Tests.csproj` | Added a project reference to `BuzzMe.Domain` (needed by the in-memory repository test double). |
| `tests/BuzzMe.Api.IntegrationTests/BuzzMe.Api.IntegrationTests.csproj` | Added `Testcontainers.MongoDb` and `System.IdentityModel.Tokens.Jwt`. |
| `tests/BuzzMe.Api.IntegrationTests/BuzzMeApiFactory.cs` | Rewritten to boot a real, ephemeral Mongo container per test run (bootstrap's version pointed at whatever `Mongo:ConnectionString` happened to be configured, with no real database behind it). |

## 2. New Files

**Domain** (`src/BuzzMe.Domain/Boards/`): `BoardId.cs`, `BoardName.cs`, `MembershipRole.cs`, `Membership.cs`, `Board.cs`, `IBoardRepository.cs`, `Events/BoardCreated.cs`, `Events/MembershipGranted.cs`

**Application** (`src/BuzzMe.Application/Boards/`): `BoardApplicationService.cs`, `Models/BoardResult.cs`

**Contracts** (`src/BuzzMe.Contracts/V1/Boards/`): `CreateBoardRequest.cs`, `BoardResponse.cs`

**Infrastructure** (`src/BuzzMe.Infrastructure/Persistence/Mongo/Boards/`): `BoardDocument.cs`, `MembershipDocument.cs`, `Mappers/BoardMapper.cs`, `BoardRepository.cs`, plus `Persistence/Migrations/Steps/CreateBoardIndexes.cs`

**Api**: `Endpoints/BoardEndpoints.cs`, `Validation/CreateBoardRequestValidator.cs`, `Mapping/ErrorMapping.cs`, `Mapping/BoardMapping.cs`

**Tests**: `BuzzMe.Domain.Tests/Boards/BoardTests.cs` (7 tests) · `BuzzMe.Application.Tests/Boards/BoardApplicationServiceTests.cs` (6 tests) + `TestDoubles/{FakeClock,FakeCurrentUserContext,FakeIdGenerator,InMemoryBoardRepository,RecordingMessageSenders}.cs` · `BuzzMe.Infrastructure.IntegrationTests/Boards/BoardRepositoryTests.cs` (4 tests) · `BuzzMe.Api.IntegrationTests/{TestJwtTokenFactory.cs, Boards/BoardEndpointsTests.cs}` (6 tests)

23 new tests total, across all four layers specified.

---

## 3. Architectural Decisions

1. **`Membership` has no status field.** Nothing in this sprint transitions a Membership away from existing — Remove Member and Leave Board don't exist yet. Adding a `Status` enum with exactly one reachable value (`Active`) would be exactly the "placeholder architecture" the sprint forbids. Every Membership present on a Board is implicitly active by construction; the field will be added in the sprint that actually introduces removal.
2. **`BoardId` is a `readonly record struct`, not a class inheriting the Domain's `ValueObject` base.** The `ValueObject` base exists for multi-component structural equality; a single-`Guid` wrapper gets everything it needs from a record struct with far less ceremony. `BoardName` *does* use a validating constructor (not the `ValueObject` base either — it doesn't need multi-component equality, just a guard).
3. **Format vs. business validation for the Board name is split exactly where DEVELOPMENT_GUIDE.md §9 says it should be.** "A title isn't empty" is that document's own example of *format* validation → enforced by `CreateBoardRequestValidator` (FluentValidation) at the Api boundary, producing a clean `400 VALIDATION_ERROR`. `BoardName`'s constructor also guards against an empty value, but throws — per §9, an empty name reaching the Domain despite upstream validation is exactly the "genuinely unexpected fault" case exceptions are reserved for, not a second copy of the same business rule.
4. **`Board.Rehydrate` and `Membership`'s constructor are `internal`, exposed to `BuzzMe.Infrastructure` via `InternalsVisibleTo`.** This is the standard DDD answer to "how does a Mapper reconstruct an aggregate without going through its business factory and re-raising creation events" — encapsulation is preserved from every caller except the one layer trusted to have persisted the aggregate in the first place.
5. **Pagination cursors are the last returned Board's `Id` (a time-sortable GUIDv7) as a string, with no dedicated cursor-encoding type.** `IBoardRepository` never returns anything but plain Domain types — no `PagedResult<T>` (an Application-layer type) leaks into the Domain-layer interface. The Application Service builds the next cursor itself by checking whether a full page came back.
6. **`AddAsync` is insert-only; there is no `UpdateAsync`.** Nothing in this sprint updates an existing Board, and a method with zero callers and zero possible tests would itself be a placeholder. `Version` is still correctly stamped at `0` on every insert, so the field and the document shape are exactly ready for the update path a future sprint adds — this sprint just doesn't write that path prematurely.

---

## 4. Test Results

All green, run for real — nothing here is asserted from memory:

| Project | Result | Against |
|---|---|---|
| `BuzzMe.Domain.Tests` | **7/7 passed** | In-memory, no I/O |
| `BuzzMe.Application.Tests` | **6/6 passed** | In-memory fake repository (per Sprint 1's own instruction: mock only external infrastructure, not MongoDB) |
| `BuzzMe.Infrastructure.IntegrationTests` | **4/4 passed** | A real, ephemeral MongoDB via Testcontainers |
| `BuzzMe.Api.IntegrationTests` | **6/6 passed** | The real host (`WebApplicationFactory<Program>`) + a real, ephemeral MongoDB, real JWT bearer authentication |

**23/23 tests passed.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors.** Confirmed via a `grep` sweep across every new file: no `TODO`, no `FIXME`, no `NotImplementedException`, anywhere in the sprint's code or the repository as a whole.

The API integration suite exercises the full acceptance scenario directly: `CreateBoard_PersistsTheBoardAndMakesTheCreatorOwnerAndMember`, `GetBoard_RetrievesTheBoardJustCreated`, `ListBoards_IncludesTheBoardJustCreated` — plus three deliberately-added edge cases (unauthenticated → `401`, someone else's board → `404`, empty name → `400`) that weren't strictly required by the acceptance criteria but were free to add given the same test infrastructure and directly protect the privacy rule (§1 Principle 6) and format-validation split (§9) called out above.

---

## 5. Where This Sprint Differs From the Specifications

Two genuine contradictions found and resolved, exactly as instructed — stopped and explained rather than silently invented:

### 5.1 — A real bug: the MongoDB driver rejects `Guid` serialization by default

`MongoDB.Driver` 3.x no longer assumes a `GuidRepresentation` — every `Guid`-typed field (which is most of this system's identity fields) throws `BsonSerializationException` on first write unless a representation is registered explicitly. This isn't a specification contradiction so much as a gap no prior document could have caught (it's a driver-version detail, not an architectural one) — noted here because it's exactly the kind of thing "stop and explain" is for. Fixed with one global registration in `MongoContext`'s static constructor (`GuidSerializer(GuidRepresentation.Standard)`) rather than annotating every property on every future document — confirmed working via the real Testcontainers-backed integration tests.

### 5.2 — A real, unresolved gap: `Idempotency-Key` on `POST /boards`

[API_CONTRACT.md](./API_CONTRACT.md) §5 states Create Board "requires a client idempotency key; a retried call with the same key returns the original result, never a second Board." **This sprint does not implement that.** Building real deduplication requires either:
- a new abstraction (an idempotency-key store) — explicitly forbidden this sprint ("do not introduce new abstractions"), or
- deriving `BoardId` deterministically from the client's key instead of generating it fresh — which would contradict [DEVELOPMENT_GUIDE.md](./DEVELOPMENT_GUIDE.md) §9's time-sortable-ID standard (a hashed, key-derived ID isn't time-sortable).

Rather than inventing a resolution to that tension, `POST /boards` in this sprint does not read or require an `Idempotency-Key` header at all — a retried request creates a second Board. This needs a deliberate decision (most likely: a dedicated idempotency-key store, introduced as its own small piece of infrastructure) before it can ship, not a workaround bolted onto this sprint.

### 5.3 — A necessary, narrow clarification to a stated dependency rule (not a contradiction — flagged for completeness)

DEVELOPMENT_GUIDE.md §2/§4 states "Api may reference: Application, Contracts, Infrastructure (DI wiring only) — **Forbidden: Domain**." Implementing real endpoints surfaced that this needs one narrow, deliberate exception: `Result<T>`/`Error` (`BuzzMe.Domain.SeedWork`) are generic outcome-wrapper primitives, not business objects, and DEVELOPMENT_GUIDE.md §9 itself already assumed they'd reach the Api boundary ("every error code in API_CONTRACT.md §6 maps from a specific Result failure type"). `BuzzMe.Api` now references `BuzzMe.Domain` for exactly this — `ErrorMapping.cs` is the one place it happens. No endpoint touches `Board` or any other aggregate directly; the rule's actual intent is fully intact.

### 5.4 — The outbox mechanism was deliberately not wired up for Board creation

`Board.Create` raises `BoardCreated` and `MembershipGranted` domain events (as designed), but `BoardRepository.AddAsync` does not drain them into the outbox this sprint. Two reasons: first, nothing consumes them yet — no Policy, no `OutboxDispatcherJob` exists, so writing rows nobody reads would be inert, untestable code, again bordering on placeholder. Second, and more importantly: the outbox's transactional-write pattern (aggregate document + outbox row, same MongoDB transaction) requires a **replica-set** MongoDB deployment — multi-document transactions don't work against a plain standalone `mongod`. The sprint's acceptance criteria ("start MongoDB, start the API") implies exactly that plain standalone case. Wiring transactional outbox writes now would either silently fail against the simple setup a new developer would reach for, or would require documenting a replica-set requirement nothing has asked for yet. Deferred, cleanly, to the sprint that introduces the first real event consumer — at which point the MongoDB deployment requirement needs to be decided and documented explicitly, not assumed.

No other deviations. Every other piece of this sprint — the aggregate shape, the three use cases, the three endpoints, the repository, the indexes — was built exactly as the four prior specification documents already described.

---

## 6. Deployability

The repository builds and its full test suite passes from a clean checkout, given Docker (for the integration test containers) and the .NET 10 SDK. `BuzzMe.Api` starts against any MongoDB reachable at the configured connection string — standalone is sufficient for everything this sprint ships, per §5.4 above.
