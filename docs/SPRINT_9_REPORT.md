# Sprint 9 Report — Real Authentication Infrastructure

*Every prior sprint that touched User treated "no credential/JWT-issuance infrastructure" as the wall it couldn't build past — Sprint 8's own `ProvisionAccountAsync` was explicitly named as a stand-in for exactly this. This sprint builds the real thing: password hashing, a two-phase Register → VerifyAccount lifecycle, Login, rotating refresh tokens, and password recovery — and removes the Sprint 8 stand-in it supersedes. DeleteAccount stays out (it needs Board ownership reassignment, which still doesn't exist anywhere in this codebase), and it's named here again as the largest remaining piece of account-lifecycle scope.*

---

## 1. Repository Changes

**A contradiction, resolved before writing code.** DOMAIN_MODEL.md's User invariant reads "a User has exactly one Personal Board, created atomically with the account." IMPLEMENTATION_SPEC.md §1 is more precise and disagrees: `personalBoardId` is "set exactly once, at account provisioning" — and APPLICATION_LAYER_SPEC.md §7 places Account Provisioning as the policy that runs *after* `VerifyAccount`, not at `RegisterAccount`. A newly-registered, not-yet-verified User cannot have a Personal Board yet under the more precise document. Resolved the same way as every prior contradiction this session: favor IMPLEMENTATION_SPEC.md/APPLICATION_LAYER_SPEC.md. `User.PersonalBoardId` is now `BoardId?`, null until `VerifyAccount` completes.

**Domain** — `User` rewritten: `PasswordHash` (opaque, hashed — Domain never sees a plaintext password), `VerificationCode`/`VerificationCodeExpiresAt`, `PasswordResetTokenHash`/`PasswordResetTokenExpiresAt` (a hash, not the plaintext token — same at-rest discipline as the password). New factory `Register` replaces Sprint 8's `Provision`: starts `PendingVerification`, no Personal Board, and — unlike `Provision` — mints its own server-generated `Id` via `IIdGenerator`, because there is no authenticated caller yet for an id to be sourced from (Register is the step that creates the identity). New transitions: `Verify` (→ `Active`, clears the code, throws if not `PendingVerification` — a defensive guard mirroring `Invitation.EnsurePending`), `CompleteProvisioning` (sets `PersonalBoardId` once, idempotent), `RequestPasswordReset`/`ResetPassword` (the reset token is consumed on use — "a used token must not work twice," API_CONTRACT.md §5). Four new events (`AccountRegistered`, `AccountVerified`, `AccountRecoveryRequested`, `AccountRecovered`), matching IMPLEMENTATION_SPEC.md §2's names exactly; `UserAccountProvisioned` (Sprint 8's own, purpose-built for `Provision`) is removed along with it.

**New aggregate — `RefreshToken`** (`BuzzMe.Domain/Auth/`), deliberately separate from `User`: APPLICATION_LAYER_SPEC.md §3.10 states Refresh Token's transaction is "session reissuance only," never touching User, and a User may hold many concurrently (one per device). Only `TokenHash` is ever persisted — the plaintext bearer value is generated once, returned to the caller, and never stored, same discipline as a password. `IsValid`/`Revoke` mirror `Invitation`'s lazy-expiration pattern; rotation happens by revoking the presented token before issuing its replacement (`AuthApplicationService.RefreshTokenAsync`), so a given bearer value can never be exchanged twice.

**New Domain abstractions** — `ISecureTokenGenerator` (`SeedWork`, generalizing `SecureInvitationTokenGenerator`'s construction for RefreshToken/password-reset tokens) and `IVerificationCodeGenerator` (`Users`, a short human-typeable code — deliberately not the same shape as a bearer token).

**Application** — new `AuthApplicationService` (`BuzzMe.Application/Auth/`) implementing all six of APPLICATION_LAYER_SPEC.md §3.10's Auth use cases: `RegisterAsync`, `VerifyAccountAsync`, `LoginAsync`, `RefreshTokenAsync`, `ForgotPasswordAsync`, `ResetPasswordAsync`. `VerifyAccountAsync` runs Account Provisioning (Personal Board creation) as a sequential same-request follow-on, not a literal saga — this codebase has no outbox-driven retry mechanism wired up anywhere (`IOutboxWriter` is registered but confirmed uncalled by any repository, session-wide, not new to this sprint), so "multi-step orchestrated workflow, retried to completion" is implemented the same way Sprint 5's `AcceptInvitation` two-step workflow already was. `Register`/`ForgotPassword` call `IEmailSender`/`ISmsSender` — Sprint 6 built both, with a doc comment naming "verification codes, recovery tokens" as their exact purpose, but nothing had ever called them until now. `UserApplicationService.ProvisionAccountAsync` (Sprint 8's stand-in) is removed; `UserApplicationService` now holds only `GetCurrentUserAsync`/`UpdateProfileAsync`. New Application.Abstractions: `IPasswordHasher`, `IAccessTokenIssuer`.

**Infrastructure** — `Pbkdf2PasswordHasher` (PBKDF2-HMAC-SHA256 via .NET's built-in `Rfc2898DeriveBytes`, no new external dependency — 210,000 iterations/128-bit salt/256-bit key, OWASP's 2023 recommendation, an implementation choice IMPLEMENTATION_SPEC.md §2 leaves fully open ("password meets minimum policy," no algorithm named)); `SecureTokenGenerator`/`NumericVerificationCodeGenerator`; `JwtAccessTokenIssuer` (real JWT issuance, finally implementing what `JwtOptions.AccessTokenLifetimeMinutes`/`RefreshTokenLifetimeDays` were already scaffolded for since the original Api host setup — same signing scheme and `ClaimTypes.NameIdentifier` claim shape as `HttpCurrentUserContext` already expects and `TestJwtTokenFactory` already stood in for). `JwtIssuerOptions` is a second binding of the "Jwt" config section, owned by Infrastructure rather than reusing Api's `JwtOptions` — Infrastructure must never depend on Api (DEVELOPMENT_GUIDE.md §2), so this mirrors `MongoOptions`'s existing pattern instead. `UserDocument`/`UserMapper`/`UserRepository` extended (`PasswordHash`, nullable `PersonalBoardId`, verification/reset fields — `[BsonIgnoreIfNull]` applied to the new sparse-indexed fields up front, applying Sprint 8's own lesson before it could repeat); new `GetByEmailOrPhoneAsync`/`GetByPasswordResetTokenHashAsync`. New `RefreshTokenDocument`/`RefreshTokenMapper`/`RefreshTokenRepository`. Two new migrations, each its own step rather than editing a shipped one (`CreateUserIndexes`, Version 6, is left untouched): `CreateUserPasswordResetIndex` (Version 7, sparse unique) and `CreateRefreshTokenIndexes` (Version 8, unique on `TokenHash`).

**Api** — `AuthEndpoints.cs`: the six explicitly-unauthenticated endpoints API_CONTRACT.md §2 names (`register`, `verify`, `login`, `refresh-token`, `forgot-password`, `reset-password`) — no `.RequireAuthorization()` group, unlike every other endpoint file, because these are the endpoints that establish authentication. Six new request validators (format-only — password minimum length, "at least one of email/phone," matching the existing UpdateProfileRequestValidator convention). `UserResponse.PersonalBoardId` is now `Guid?`.

---

## 2. Test Results

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **142/142** (121 prior + 16 rewritten for `User.Register`/`Verify`/`CompleteProvisioning`/`RequestPasswordReset`/`ResetPassword` replacing Sprint 8's `Provision` tests, + 5 new `RefreshTokenTests`) |
| `BuzzMe.Application.Tests` | **95/95** (77 prior, 7 rewritten in `UserApplicationServiceTests` after `ProvisionAccountAsync`'s removal, + 22 new `AuthApplicationServiceTests`) |
| `BuzzMe.Infrastructure.IntegrationTests` | **67/67** (56 prior + 11 new: `GetByEmailOrPhoneAsync`/`GetByPasswordResetTokenHashAsync`, the sparse-index-allows-no-outstanding-token regression case, and a full `RefreshTokenRepositoryTests` — real MongoDB) |
| `BuzzMe.Api.IntegrationTests` | **54/54** (39 prior + 15 new: `AuthEndpointsTests` exercising Register→Verify→Login→Refresh and Forgot→Reset-Password end to end against the real host, reading the real (never-invented) verification code and reset token back out of MongoDB/a substituted `RecordingEmailSender` rather than guessing at them) |

**358/358 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors** (the SSH.NET `NU1903` advisory some test projects report is Testcontainers' own pre-existing transitive dependency, unrelated to and untouched by this sprint).

---

## 3. Specification Interpretation Notes

*Places a judgment call was needed to turn a precisely-stated rule into code — recorded here the same way Sprint 7's contradiction resolution was, so the reasoning is inspectable later.*

### 3.1 Login's non-Active status handling, beyond the two IMPLEMENTATION_SPEC.md names

IMPLEMENTATION_SPEC.md §2's Login row explicitly names two special cases: `Suspended` → a distinct rejection (`403`), `Deactivated` → succeeds anyway (routes the client to a "Reactivate?" prompt, which is a client-side concern this sprint's response shape doesn't need to encode specially). Neither `PendingVerification` nor `Deleted` is named. Both are treated as the same generic `401` a wrong password produces — API_CONTRACT.md §5's own stated privacy rule for Login ("generic — never confirms which field was wrong") extends naturally to "which non-Active reason," not just "which field." Credentials are checked *before* status for the same reason: checking status first would let a wrong-password guess distinguish a Suspended account from a nonexistent one.

### 3.2 Password hashing, code format, and every TTL are implementation choices, not spec derivations

None of PBKDF2/210,000 iterations, a 6-digit numeric code, a 15-minute code lifetime, a 1-hour reset-token lifetime, or a 30-day refresh-token lifetime are named in any specification document — IMPLEMENTATION_SPEC.md §2 only says "password meets minimum policy" for Register and never states a verification-code format at all. Same category as `InvitationApplicationService.TokenLifetime`'s own documented 7-day default: an engineering choice using established convention (OWASP's PBKDF2 guidance; the 30-day refresh lifetime literally matches `JwtOptions.RefreshTokenLifetimeDays`'s default, already present in this codebase's config since the original Api scaffold), not something derived from a BuzzMe-specific requirement.

---

## 4. Specification Gaps

### 4.1 DeleteAccount was not built

Unchanged from SPRINT_8_REPORT.md §3.2: `ReassignOwnership`/`RemoveMember`/`LeaveBoard` still don't exist in this codebase in any sprint. This sprint's Auth work doesn't touch it — "authentication infrastructure" is a real subset of the full account lifecycle, and DeleteAccount belongs to the latter.

### 4.2 Idempotency-Key handling is still nowhere in this codebase — now including Register

API_CONTRACT.md §5 requires an `Idempotency-Key` header on Register, same as it already did for CreateBoard (Sprint 1) and CreateReminder (Sprint 2) — and, like both of those, no endpoint in this codebase actually reads or enforces one. This is a pre-existing, session-wide gap this sprint doesn't fix in isolation for just one more endpoint (a half-implemented idempotency-key story — enforced on one POST, silently absent on two others — would be worse than the current, at-least-consistent absence). Recorded here as the gap widening, not a new one.

### 4.3 RefreshTokenAsync doesn't re-check the owning User's Status

A Suspended or Deleted User's still-valid refresh tokens can still be exchanged for a new access token. IMPLEMENTATION_SPEC.md §2 names exactly one error case for Refresh Token ("expired/revoked refresh token") and no document states a cross-check against `User.Status` — adding one now would be inventing a rule IMPLEMENTATION_SPEC.md conspicuously didn't state, given how precisely it enumerated Login's own status handling one row above. Left as specified, recorded as a real operational gap: a platform-level Suspend action doesn't actually cut off an already-issued session.

### 4.4 No rate limiting

EVENT_STORMING.md names a policy — "on `LoginFailed` (repeated) → `ApplyRateLimit`" — and API_CONTRACT.md §6 reserves `429 RATE_LIMITED` for exactly this. No rate-limiting store, sliding window, or middleware exists anywhere in this codebase, and building one from scratch (what counts as "repeated," per-account or per-IP, what window) would mean inventing the policy's parameters wholesale. Not built; every failed Login attempt is currently unlimited.

### 4.5 `LoginSucceeded`/`LoginFailed` are not raised as domain events

Every event in this codebase is raised by an aggregate mutation (`Raise()` inside a factory/transition method) and — in principle, though never actually wired — drained to the outbox by whatever persists that aggregate. Login is read-only by IMPLEMENTATION_SPEC.md §2's own design ("Read-only check against User"): there is no aggregate mutation for either event to attach to. Not raised. Not a regression specific to this sprint — the outbox itself is unwired for every other aggregate's events too (§1) — but Login is the first use case in this codebase whose own specified events have no possible home in the existing event-raising mechanism at all, mutation or none.

### 4.6 Privacy Settings / Notification Preferences initialization still isn't part of Account Provisioning

Unchanged from SPRINT_7_REPORT.md §3.2 and SPRINT_8_REPORT.md §3.3: neither is a buildable aggregate in this codebase (no field-level specification exists for either). `VerifyAccountAsync`'s provisioning step creates only the Personal Board.

---

## 5. Architecture Observations

1. **`RefreshToken` is the first aggregate in this codebase whose entire reason to exist is session/credential state, not a domain concept from DOMAIN_MODEL.md.** It still follows every other aggregate's shape (an `AggregateRoot<TId>`, a repository interface in Domain, a hand-written Mongo repository in Infrastructure) — the pattern generalized cleanly to something DOMAIN_MODEL.md never named, which is a reasonable signal the pattern itself is sound, not over-fit to the nine documents it was built against.
2. **`User.Register` mints its own Id; Sprint 8's `Provision` sourced one from the caller's JWT.** Both are correct for what each could see: `ProvisionAccountAsync` only ever ran for an already-authenticated caller (there was no other way to create a User at all), so reusing their established identity was the only sound choice. `Register` runs *before* any identity exists, so the server is the only party that can assign one. Neither approach was a shortcut relative to the other — they answer different questions.
3. **Two `IEmailSender`/`ISmsSender` calls is a small thing that closes a three-sprint-old loop.** Both interfaces, and their Null implementations, were built in Sprint 6 with doc comments already naming "verification codes, recovery tokens" as their purpose — and then sat completely uncalled through Sprints 7 and 8. This sprint is the first to actually need them, and they needed no changes at all to fit.
4. **The `JwtOptions` duplication (Api vs. Infrastructure) is a direct, visible cost of the dependency-direction rule this codebase has held all session** (DEVELOPMENT_GUIDE.md §2: Infrastructure must never depend on Api). It's a small, explicit cost — two four-property classes bound from the same config section — traded for keeping Infrastructure genuinely host-agnostic (BuzzMe.Workers, which never touches JWT validation at all, still doesn't have to reference anything Api-shaped to pull in `AddBuzzMeInfrastructure`).

---

## 6. Remaining Auth/Account Work

1. **DeleteAccount** (§4.1) — blocked on Board ownership reassignment, the same gap named in SPRINT_8_REPORT.md and every sprint before it that touched Boards.
2. **A real Idempotency-Key mechanism** (§4.2) — a generic, cross-cutting concern (a dedup store keyed by header value + endpoint, some retention window) that would retroactively make Register, CreateBoard, and CreateReminder's already-specified idempotency real, rather than adding a fourth inconsistent partial implementation.
3. **Re-checking User.Status on refresh** (§4.3) and **session revocation on Suspend** — both point at the same underlying gap: there is no "kill all of this User's sessions" operation anywhere, and DeleteAccount's own "revokes sessions" side effect (IMPLEMENTATION_SPEC.md §2) can't be built until one exists.
4. **Rate limiting** (§4.4) — needs its own specification (what's a "repeated" failure, what window, per-what) before it can be built as anything but a guess.
5. **Privacy Settings / Notification Preferences** (§4.6) — still blocked on their own field-level specifications, unchanged since Sprint 7.
