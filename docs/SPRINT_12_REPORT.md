# Sprint 12 Report — DeleteAccount

*Every sprint since 8 that touched User or Board named the same blocker: DeleteAccount needs Board ownership reassignment. Sprint 10 built that. This sprint is the one that finally spends it — plus two genuinely new pieces (Board's own soft-delete, bulk RefreshToken revocation) that turned out to be real, unavoidable prerequisites, not optional extras.*

---

## 1. Repository Changes

**Domain** — `Board` gained `DeletedAt`/`IsDeleted`/`Delete(deletedAt)`, mirroring `Reminder.DeletedAt`'s exact Sprint 3.1 pattern (nullable timestamp, no new lifecycle enum, idempotent no-op on a second call) — IMPLEMENTATION_SPEC.md §1's own words for Board: "Deleted is internally soft... but user-facing behavior is simply 'gone.'" New `BoardDeleted` event. `Board.Delete()` carries no Owner-authorization check itself — same division of responsibility as `RemoveMember`: the aggregate defends its own invariants, the calling use case (this sprint, `DeleteAccountAsync`'s own orchestration; a future `DeleteBoardAsync` would check separately) owns authorization. `User` gained `Delete(deletedAt)` — the terminal `→ Deleted` transition `UserStatus.cs` has documented as unreachable since Sprint 8. New `AccountDeleted` event. `IRefreshTokenRepository` gained `RevokeAllForUserAsync` — a direct bulk operation, not a load-then-revoke-then-save loop over every token, since (unlike every other RefreshToken write) there's no single in-memory aggregate whose own `Revoke()` needs to run first.

**Infrastructure** — `BoardDocument.DeletedAt` + `BoardMapper` both ways. `BoardRepository.GetByIdAsync`/`ListByMemberAsync` gained a `NotDeletedFilter`, mirroring `ReminderRepository`'s own named-filter pattern exactly — a soft-deleted Board now reads as gone everywhere a live one would be found, not merely absent from its own Memberships. `RefreshTokenRepository.RevokeAllForUserAsync` — one `UpdateManyAsync`, filtered to `UserId` + `RevokedAt == null`.

**Application** — `AuthApplicationService.DeleteAccountAsync`: for every Board the requester belongs to, resolves their Membership one of three ways — sole Owner with other Active Members → reuses `Board.Leave` (reassign to the longest-standing other Member, exactly Sprint 10's own mechanism); sole Owner with none → `Board.Delete()` (IMPLEMENTATION_SPEC.md §4's ReassignOwnership policy's own stated fallback: *"if no other Active Member exists, this policy does not run — the Board is deleted instead, not left ownerless"*); non-Owner Member → `Board.RemoveMember` (self-targeted, since a Membership must still end somehow, and the spec never distinguishes this case from an Owner-initiated removal in its end effect). Each Board is persisted independently before the next is touched — the same sequential, no-saga-infrastructure shape as `VerifyAccountAsync`'s own Account Provisioning step. Then every outstanding RefreshToken is revoked, then the User is marked `Deleted`. Idempotent: re-confirming an already-`Deleted` account is a no-op, checked before any of the above runs.

**Contracts/Api** — `DeleteAccountRequest { confirmation: bool }`, matching API_CONTRACT.md §5's literal request body. `DELETE /v1/users/me` in `UserEndpoints.cs`, calling `AuthApplicationService` (not `UserApplicationService`) — DeleteAccount is grouped with the rest of the account-lifecycle use cases there per Sprint 9's own split, even though its route sits under `/users/me` alongside Profile's two endpoints.

---

## 2. Bugs Caught Before They Shipped

### 2.1 Minimal APIs don't infer a request body for `DELETE`

`DeleteAccountAsync`'s handler took a `DeleteAccountRequest request` parameter the same way every `POST`/`PATCH` handler in this codebase already does — and every integration test in `AuthEndpointsTests.cs` failed at host-startup with *"Body was inferred but the method does not allow inferred body parameters."* ASP.NET Core's Minimal API body-inference conventions treat `DELETE` (along with `GET`/`HEAD`) as verbs that don't usually carry a body, and refuse to guess. Fixed with an explicit `[FromBody]` attribute — the one endpoint in this codebase that needs it, since `DELETE /v1/boards/{boardId}/members/{userId}` (Sprint 10) and every other `DELETE`-shaped route here has no body at all. Caught immediately by the integration test suite, not discovered later.

### 2.2 `InMemoryBoardRepository` (Application-layer test double) didn't respect `IsDeleted`

Sprint 10 added the real `BoardRepository`'s `NotDeletedFilter`, but the in-memory fake `BoardApplicationServiceTests`/`AuthApplicationServiceTests` run against was never updated to match — it would have kept returning soft-deleted Boards, silently diverging from production behavior and potentially masking a real bug in this exact sprint's own `DeleteAccountAsync` tests (e.g. `DeleteAccountAsync_DeletesThePersonalBoard` could have passed against the fake while failing against real MongoDB). Fixed proactively, before writing this sprint's own Application-layer tests, once the gap was noticed.

---

## 3. Test Results

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **157/157** (153 prior + 4 new: `Board.Delete`/idempotency, `User.Delete`/idempotency) |
| `BuzzMe.Application.Tests` | **121/121** (114 prior + 7 new: `DeleteAccountAsync` — marks User Deleted, deletes the Personal Board, reassigns a shared Board with other Members, removes a non-owned Membership, revokes RefreshTokens, idempotent, not-found) |
| `BuzzMe.Infrastructure.IntegrationTests` | **74/74** (70 prior + 4 new: `Board.Delete` persistence + exclusion from reads, `RevokeAllForUserAsync` scoped correctly to one User and not another, doesn't clobber an already-revoked token's original timestamp — real MongoDB) |
| `BuzzMe.Api.IntegrationTests` | **74/74** (68 prior + 6 new: `DELETE /v1/users/me` end to end — success, missing confirmation, blocks future Login, revokes the RefreshToken, idempotent, unauthenticated — real host + real MongoDB) |

**426/426 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors** (SSH.NET's `NU1903` advisory remains the same pre-existing, unrelated Testcontainers dependency noted since SPRINT_9_REPORT.md).

---

## 4. Specification Interpretation Notes

### 4.1 "Valid re-authentication" vs. `{ confirmation: true }`

IMPLEMENTATION_SPEC.md §2 names ConfirmAccountDeletion's precondition as "valid re-authentication." API_CONTRACT.md §5's literal request body for `DELETE /users/me` is `{ confirmation: true }` — a boolean, no password field. These describe the same operation at two different precision tiers, not two different mechanisms: resolved by treating the endpoint's own already-required, short-lived (15-minute, Sprint 9) Bearer token as the "re-authentication" IMPLEMENTATION_SPEC.md names, and `confirmation` as the explicit "yes I mean it" acknowledgment — the same role IMPLEMENTATION_SPEC.md §2 gives DeleteBoard's "explicit confirmation token... naming the Board," simplified to a boolean since an account has no name to type back. No password re-entry was built; API_CONTRACT.md's literal wire shape is what this codebase's Api layer must match exactly, per every prior sprint's own practice.

### 4.2 Non-Owner Membership resolution during DeleteAccount

Neither IMPLEMENTATION_SPEC.md nor APPLICATION_LAYER_SPEC.md states what happens to a Board where the deleting User is a Member but not the Owner — both documents' attention is entirely on the sole-Owner case ReassignOwnership handles. Resolved by reusing `RemoveMember` (self-targeted): the account is being permanently removed, so its Membership must end somehow, and nothing distinguishes this from an Owner-initiated removal in its actual effect (the Board is untouched, the Membership becomes non-Active). A defensible, minimal inference — not left unhandled, but not a business rule invented from nothing either.

---

## 5. Specification Gaps

### 5.1 No History/audit-trail entity exists — authorship on shared Boards is not anonymized

IMPLEMENTATION_SPEC.md §2 names "anonymizes authorship on shared Boards' History" as a ConfirmAccountDeletion side effect. Confirmed by direct search: no `History`/`AuditLog`/`AuditEntry` type exists anywhere in this codebase, in any bounded context, despite being referenced by name in doc comments since Sprint 3/4 (Reminder's own "History survives" notes). Building an audit-trail aggregate spanning every action across Reminders/Occurrences/Boards is a large, cross-cutting feature of its own — squarely out of scope for DeleteAccount specifically, same reasoning as Sprint 10's Buzz-cancellation gap.

### 5.2 No async Purge background worker — a deleted Board's content is soft-deleted, never actually erased

IMPLEMENTATION_SPEC.md §2 also names "purges the Personal Board's content." `Board.Delete()` (this sprint) does the soft-delete half; the actual data-erasure half is APPLICATION_LAYER_SPEC.md §6's own separately-named "Clean Up Deleted Boards/Accounts (Purge)" background process — chunked, running after a 14-day grace window. No sprint has ever built this worker, for any aggregate (Reminder's own `DeletedAt`, three sprints old, has never been purged either). Not new to this sprint; DeleteAccount now produces exactly the same "soft-deleted, never hard-deleted" state every other soft-delete in this codebase already does.

### 5.3 No standalone DeleteBoard endpoint

`Board.Delete()` exists only as an internal capability `DeleteAccountAsync`'s own orchestration calls when a sole-owner Board has no other Members. IMPLEMENTATION_SPEC.md §2's own DeleteBoard use case — Owner-only authorization, its own "explicit confirmation token... naming the Board" UX — was not built as a public, standalone `DELETE /v1/boards/{boardId}` endpoint. Out of scope for a sprint named DeleteAccount; the domain capability it would need already exists, waiting for its own sprint to wire up the authorization and confirmation flow around it.

### 5.4 A deleted account's existing access token still authenticates until it naturally expires

RefreshToken revocation (this sprint) stops a deleted account from ever obtaining a *new* access token, but the short-lived (15-minute) access token it already held at the moment of deletion is a stateless JWT — its own signature and expiry are all that's checked per request (`HttpCurrentUserContext`), with no per-request re-check against `User.Status`. A deleted account's existing session can therefore still read data for up to 15 minutes after deletion. Not addressed here: closing it would mean either a token-blacklist (new infrastructure, a real architectural addition) or checking `User.Status` on every authenticated request (a meaningful latency/complexity cost across every endpoint, for a narrow window). Recorded as a real, bounded gap rather than silently accepted.
