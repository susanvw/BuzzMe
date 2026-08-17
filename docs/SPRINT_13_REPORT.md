# Sprint 13 Report — DeleteBoard

*Sprint 12's own gap 5.3 named this exactly: `Board.Delete()` already existed, built as `DeleteAccountAsync`'s internal fallback for a sole-owner Board with no other Members, but had never been exposed as its own authorized, standalone endpoint. This sprint wires up the Owner-only authorization and idempotency around that existing domain capability — no new Domain behavior beyond one repository method, since `Board.Delete()` itself was already correct.*

---

## 1. Repository Changes

**Domain** — `IBoardRepository` gained `GetByIdIncludingDeletedAsync(BoardId, CancellationToken)` — mirrors `IReminderRepository`'s exact Sprint 3.1 pattern. No change to `Board` itself: `Delete(deletedAt)` (Sprint 12) already has the exact idempotent, no-authorization-check shape this use case needs — same aggregate/use-case division of responsibility as `RemoveMember`, where the aggregate defends its own invariants and the calling Application Service owns authorization.

**Infrastructure** — `BoardRepository.GetByIdIncludingDeletedAsync` — finds by `Id` alone, deliberately omitting the `NotDeletedFilter` every other read method applies, so it can find a Board regardless of its `DeletedAt` state. `InMemoryBoardRepository` (Application-layer test double) got the matching addition.

**Application** — `BoardApplicationService.DeleteBoardAsync(requestingUserId, boardId, cancellationToken)`: loads via `GetByIdIncludingDeletedAsync` (not the deleted-excluding `GetByIdAsync` every other method here uses) — necessary because the idempotency requirement ("deleting an already-deleted Board is a no-op → success") can't be told apart from "this Board never existed" (→ not-found) using a filter that already excludes deleted Boards. Existence + membership checked first (`board is null || !board.HasMember(requestingUserId)` → `NotFound`), then Owner authorization (`board.OwnerUserId != requestingUserId` → `Forbidden`), then the `IsDeleted` idempotency short-circuit, then `board.Delete()` + `UpdateAsync`. Authorization is checked *before* the idempotency check — same ordering precedent as `RemoveMemberAsync`'s Sprint 10 shape — so a non-Owner Member of an already-deleted Board learns nothing different from a non-Owner Member of a live one; both get `Forbidden`, never a state-revealing 200/204 vs 403 split.

**Api** — `DELETE /v1/boards/{boardId}` in `BoardEndpoints.cs`, no request body — API_CONTRACT.md §5's Delete Board row states this explicitly: confirmation UX is handled client-side before the call; the path ID plus the server-side Owner check is the safeguard, unlike DeleteAccount's `{ confirmation: true }` body. Returns `204 No Content` on success, matching every other no-body write in this codebase (`Mute`/`Unmute`/`RemoveMember`).

---

## 2. Test Results

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **157/157** (no Domain changes this sprint — `Board.Delete` was already covered by Sprint 12's own tests) |
| `BuzzMe.Application.Tests` | **126/126** (121 prior + 5 new: `DeleteBoardAsync` — Owner succeeds, idempotent on repeat, non-Owner returns `Forbidden`, not-found for a non-member, not-found for a nonexistent Board) |
| `BuzzMe.Infrastructure.IntegrationTests` | **77/77** (74 prior + 3 new: `GetByIdIncludingDeletedAsync` returns a deleted Board, returns a non-deleted Board too, returns null for an unknown id — real MongoDB) |
| `BuzzMe.Api.IntegrationTests` | **80/80** (74 prior + 6 new: `DELETE /v1/boards/{boardId}` end to end — success/204 followed by a 404 on subsequent Get, idempotent repeat, non-Owner forbidden, not-found for a non-member, not-found for a nonexistent Board, unauthenticated — real host + real MongoDB) |

**440/440 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors** (confirmed the pre-existing `NU1903` SSH.NET/Testcontainers advisory noted since SPRINT_9_REPORT.md is unchanged by diffing against a stash of this sprint's own changes — not introduced here).

---

## 3. Specification Interpretation Notes

### 3.1 No request body, unlike DeleteAccount's `{ confirmation: true }`

IMPLEMENTATION_SPEC.md §2 names DeleteBoard's own UX precondition as "explicit confirmation... naming the Board" — the same phrasing Sprint 12's report (§4.1) noted for DeleteAccount. But API_CONTRACT.md §5's literal Delete Board row is explicit that this is client-side only: no body, no confirmation field, the path ID and the server-side Owner check are the whole safeguard. Favored the more implementation-precise document, same rule this session has applied every time these two specs' precision levels disagreed (Sprint 9's Personal Board timing, Sprint 10's 403-vs-409).

### 3.2 Authorization-before-idempotency ordering

Neither spec states the check order explicitly. Resolved by reusing `RemoveMemberAsync`'s established Sprint 10 shape: Owner authorization is checked before the `IsDeleted` short-circuit, so a non-Owner Member can never distinguish "this Board is still live" from "this Board was already deleted" by the shape of the response they get — both are `Forbidden`. Getting this order backwards would leak state to someone who isn't authorized to see it.

---

## 4. Specification Gaps

### 4.1 No cascading cleanup of the Board's own Reminders/Occurrences/Buzzes

IMPLEMENTATION_SPEC.md's own words are explicit that DeleteBoard's side effects are "none synchronous beyond the state change... the Purge background process runs after the grace window." `DeleteBoardAsync` therefore does not touch any Reminder/Occurrence/Buzz belonging to the deleted Board — this is not an oversight, it matches the spec's own stated deferral to the (still nonexistent) async Purge worker, the same gap Sprint 12's report named in §5.2 for DeleteAccount and never new to this sprint.

### 4.2 No async Purge background worker exists, for any aggregate

Same standing gap as Sprint 12 §5.2, restated because this sprint adds a second production path (alongside DeleteAccount's sole-owner fallback) that now relies on it: a deleted Board's `DeletedAt` is set, but nothing ever actually erases its data. No sprint has built this worker yet.

### 4.3 No RenameBoard endpoint

Surfaced by this sprint's own research pass into `BoardEndpoints.cs` and confirmed via the same Board-APIs table DeleteBoard's own row came from: `PATCH /v1/boards/{boardId}` (rename) has no Application Service method, no Contracts DTO, and no route mapping anywhere in this codebase. Out of scope — the user asked specifically for DeleteBoard — but flagged here since it's the other unbuilt use case in the same table, not merely a hypothetical.
