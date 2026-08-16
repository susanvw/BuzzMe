# Sprint 8 Report — User/Profile Domain

*The specifications describe a full account lifecycle — Register, Verify, Login, Forgot/Reset Password, Refresh Token — none of which this codebase has any infrastructure for (no password storage, no verification-code pipeline, no JWT issuance; only JWT *validation*, via `HttpCurrentUserContext` reading an externally-issued token's claims). This sprint builds the slice that's actually buildable on top of that reality: a `User` aggregate, a same-request Personal Board provisioning flow for an already-authenticated caller, and the two `/v1/users/me` endpoints the API contract itself backs with real field shapes. Everything credential-shaped is named as a gap rather than invented.*

---

## 1. Repository Changes

**Domain** — `src/BuzzMe.Domain/Users/`: `UserId` (thin `Guid` wrapper, matching every other aggregate's id type), `UserStatus` (`PendingVerification`, `Active`, `Deactivated`, `Suspended`, `Deleted` — the full enum DOMAIN_MODEL.md names, even though only `Active` is reachable this sprint, matching the `OccurrenceStatus`/`BuzzStatus` precedent of modeling the whole enum now and constructing only what's reachable), `DisplayName` (a non-empty, trimmed value object, mirroring `BoardName` exactly), and the `User` aggregate itself. `User.Provision(...)` requires at least one of Email/Phone (IMPLEMENTATION_SPEC.md §1's own invariant, enforced in the constructor via `ArgumentException` — a genuine precondition violation, never expected to trigger since the Application layer validates first) and raises `UserAccountProvisioned`. `UpdateProfile(...)` is a partial update — each parameter `null` means "leave unchanged" — that raises `ProfileUpdated` only when something actually changed, matching this codebase's established idempotent-write discipline. `IUserRepository` defines `AddAsync`/`GetByIdAsync`/`UpdateAsync`/`ExistsWithEmailOrPhoneAsync`.

**Application** — `UserApplicationService.ProvisionAccountAsync(requestingUserId, email, phone, displayName, ct)` collapses Register→Verify→account-creation into one operation: the new `User`'s id comes directly from the caller's own JWT `sub` claim (`requestingUserId`), never a server-generated id, because there is no separate registration step in which a not-yet-authenticated identity could be assigned one. It lands directly at `Active` — there is no email/SMS verification-code infrastructure to gate on `PendingVerification` with. It creates a `Board.Create(..., new BoardName("Personal"), ...)` for the new User in the same operation, per DOMAIN_MODEL.md's "every User has exactly one Personal Board, created alongside the account." Idempotent: replaying with the same `requestingUserId` returns the existing User rather than erroring or duplicating the Personal Board. `GetCurrentUserAsync`/`UpdateProfileAsync` are the straightforward reads/writes behind the two endpoints below; `UpdateProfileAsync` re-checks email/phone uniqueness only when the value is actually changing (re-asserting a User's own current email is a no-op, not a self-conflict).

**Infrastructure** — `UserDocument`/`UserMapper`/`UserRepository` follow the established one-aggregate-one-document, hand-written-repository pattern. `CreateUserIndexes` (migration version 6) creates `ux_users_email`/`ux_users_phone` as **unique, sparse** indexes, enforcing IMPLEMENTATION_SPEC.md §1's global email/phone uniqueness at the database level. **A real bug was caught while writing the integration tests for this, not before**: `UserDocument.Email`/`Phone` were declared as plain `string?` with no `[BsonIgnoreIfNull]`. The MongoDB driver serializes a null `string?` property as an explicit `Email: null` field in the BSON document — present, just null — rather than omitting the field. A "sparse" index only skips documents that are *missing* the field entirely; a document with the field present-but-null is indexed with value `null` like any other value, so two phone-only Users (both with `Email` present-and-null) collided on `ux_users_email` the moment a second one was inserted. Fixed by adding `[BsonIgnoreIfNull]` to both properties, which makes the driver omit the field outright when the C# value is null — confirmed by a new integration test (`AddAsync_AllowsTwoUsersWithNoEmailAtAll`) that failed before the fix and passes after.

**Contracts** — `UserResponse` (matches API_CONTRACT.md's User resource: `id`, `displayName`, `photoUrl`, `email`, `phone`, `status`, `personalBoardId`) and `UpdateProfileRequest` (all four fields optional, PATCH semantics).

**Api** — `GET /v1/users/me` and `PATCH /v1/users/me`, the only two User endpoints API_CONTRACT.md §5 backs with a real request/response shape. `ProvisionAccountAsync` has **no HTTP endpoint** — matching the "capability exists at the Application layer, no endpoint, because the spec's own endpoint requires infrastructure this codebase doesn't build" precedent already used for Occurrence/Buzz reads (Sprint 3/4) and `CancelInvitation`/`ListPendingInvitations` (Sprint 5): the spec's own `POST /auth/register` is unauthenticated and gates on password + verification-code delivery, neither of which exists.

---

## 2. Test Results

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **121/121** (102 prior + 19 new: `User.Provision` field-stamping and event-raising, phone-only construction, the neither-Email-nor-Phone guard, `UpdateProfile` partial-change/no-op/event-raising, `DisplayName` validation, `UserStatusCodes` round-trip) |
| `BuzzMe.Application.Tests` | **77/77** (67 prior + 10 new: `ProvisionAccountAsync` creation/idempotent-replay/Personal-Board-creation/email-and-phone-conflict, `GetCurrentUserAsync` found/not-found, `UpdateProfileAsync` partial-update/not-found/email-conflict/self-reassert-is-fine) |
| `BuzzMe.Infrastructure.IntegrationTests` | **56/56** (47 prior + 9 new: persist-at-version-zero, unknown-id lookup, duplicate-email/duplicate-phone rejected by the real sparse unique index, two no-email Users coexisting — the regression test for §1's bug — `ExistsWithEmailOrPhoneAsync` found/not-found/excluding-self, profile-update persistence — real MongoDB) |
| `BuzzMe.Api.IntegrationTests` | **39/39** (32 prior + 7 new: `GET`/`PATCH /v1/users/me` success, not-found for a never-provisioned caller, `401` unauthenticated, validation error on empty display name, `409` on changing to an already-registered email — real host + real MongoDB, seeded via direct `UserApplicationService` resolution since no HTTP-reachable provisioning endpoint exists) |

**293/293 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors.**

---

## 3. Specification Gaps

### 3.1 The full credential/auth lifecycle has no buildable infrastructure in this codebase

Register, VerifyAccount, Login, ForgotPassword, ResetPassword, and RefreshToken are all named in the specifications, but none of password storage/hashing, verification-code generation and delivery, or JWT *issuance* exist anywhere in this codebase — only JWT *validation* (`HttpCurrentUserContext`, reading an already-issued token's claims). This isn't a gap this sprint could close without inventing an entire authentication subsystem (hashing scheme, code TTLs, token signing keys and rotation) with zero specification backing for any of those choices. `ProvisionAccountAsync` is the maximal buildable substitute: it assumes a caller who is already authenticated by *something upstream* and folds Register+Verify into landing a `User` document at `Active`.

### 3.2 DeleteAccount was not built

DOMAIN_MODEL.md's account lifecycle includes deletion, but a real `DeleteAccountAsync` would need to resolve what happens to Boards the User owns — `ReassignOwnership`, `RemoveMember`, and `LeaveBoard` are all named in the specifications but none exist in this codebase in any prior sprint (Board's only membership-mutating methods remain `AddMemberAsync`/mute-unmute). Building `DeleteAccountAsync` now would mean either silently orphaning owned Boards or inventing ownership-transfer semantics with no specification to build against. Left unbuilt.

### 3.3 Privacy Settings and Notification Preferences initialization were skipped

EVENT_STORMING.md's account-provisioning policy names both as side effects of account creation. Notification Preferences was already resolved in SPRINT_7_REPORT.md §3.2: it isn't scoped into IMPLEMENTATION_SPEC.md's V1 aggregate catalogue at all, and channel/quiet-hours have no field-level specification anywhere. Privacy Settings doesn't appear in that catalogue either, and — like Notification Preferences before this sprint — has never been named with any field shape in any of the five specification documents. `ProvisionAccountAsync` creates exactly the User and Personal Board; nothing else.

### 3.4 The Personal-Board-is-never-invitable invariant is still not enforced

DOMAIN_MODEL.md states a Personal Board is "never invitable." Sprint 5's `InviteMemberAsync` has no check against this — it was a pre-existing gap, newly visible now that this sprint actually creates Personal Boards for the first time as a side effect of a real flow, rather than as ad-hoc test fixtures. Not retroactively fixed this sprint, to avoid scope creep into Sprint 5's code under an unrelated sprint's banner; recorded here as the place to fix it next.

### 3.5 `User.UpdateAsync` has no optimistic-concurrency check, unlike Buzz

`UserRepository.UpdateAsync` is a plain `ReplaceOneAsync` keyed only on `Id` — it doesn't check or bump `Version` the way `BuzzRepository`'s claim/mark-delivered path does (Sprint 6's `$inc`-and-version-filter pattern). Two concurrent `PATCH /v1/users/me` calls for the same User would silently last-write-wins rather than conflict. Not fixed this sprint: nothing in the specifications marks concurrent profile edits as a scenario requiring a conflict response (contrast with Buzz claiming, where APPLICATION_LAYER_SPEC.md explicitly requires it), so adding one now would be inventing a rule, not implementing one. Recorded here in case profile-edit concurrency is ever specified.

---

## 4. Architecture Observations

1. **The sparse-index bug (§1) is a reminder that "sparse" in MongoDB means "field absent," not "field null," and the .NET driver doesn't infer that gap for you.** Every other nullable field already in this codebase (`Board.PhotoUrl`-equivalents, `Reminder.DeletedAt`) either isn't behind a unique index or is never actually written as an explicit null in a context that matters. `User` is the first aggregate where a nullable field participates in a *uniqueness* invariant, and that's exactly where the gap surfaced — caught by a same-sprint integration test, not downstream.
2. **`ProvisionAccountAsync` is the first Application Service method in this codebase whose own identity source is the caller's JWT rather than a server-generated id.** Every prior `Create*Async` (Board, Reminder, Invitation) mints a fresh id via `IIdGenerator`. This one is deliberately different because the entity it creates *is* the authenticated identity itself — there's no reasonable sense in which the server assigns a User a different id than the one already asserted in their own token.
3. **This sprint's gap list (§3) is almost entirely "named lifecycle steps with no backing infrastructure," a different shape than most prior sprints' gaps.** Sprints 1–7's gaps were mostly "specified but under-detailed" (Block, channel preference). This sprint's are "named at the conceptual level in DOMAIN_MODEL.md, but their prerequisite subsystems (hashing, ownership transfer, token issuance) were never built in any sprint" — closer to SPRINT_5_REPORT.md's and SPRINT_6_REPORT.md's repeated flagging of "a real User/Account domain" as the largest unaddressed piece of this whole session's scope. This sprint is the direct, deliberately-narrowed answer to that flag, not a full close of it.

---

## 5. Remaining User/Account Work

1. **Real authentication infrastructure** (§3.1) — password hashing, verification-code delivery, JWT issuance and refresh — needed before Register/Login/ForgotPassword/RefreshToken can be built as anything but a guess. The largest remaining piece of unaddressed scope in this whole session.
2. **Board ownership transfer** (§3.2) — `ReassignOwnership`/`RemoveMember`/`LeaveBoard` — needed before `DeleteAccountAsync` can be built without silently orphaning Boards.
3. **Privacy Settings specification** (§3.3) — needs its own field-level specification, same as Notification Preferences' channel/quiet-hours (SPRINT_7_REPORT.md §5.1), before any code should target it.
4. **Enforce "Personal Board is never invitable"** (§3.4) — a small, targeted fix to `BoardApplicationService.InviteMemberAsync`, independent of any of the above.
5. **Optimistic concurrency on User profile updates** (§3.5) — only worth adding if/when the specifications actually name concurrent-edit conflict as a real scenario for Update Profile.
