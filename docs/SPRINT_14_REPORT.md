# Sprint 14 Report — RenameBoard

*The other unbuilt row in the same Board APIs table DeleteBoard's own Sprint 13 gap 4.3 pointed at. The smallest of the three candidates surfaced after Sprint 13 — no new aggregate, no new lifecycle state, a single-field change with the same shape as MuteBoard/UnmuteBoard.*

---

## 1. Repository Changes

**Domain** — `Board.Rename(BoardName name, DateTimeOffset renamedAt)` — same idempotent-no-op shape as `MuteBoard`/`UnmuteBoard`: a re-applied, already-current name (compared via `BoardName`'s record equality) raises nothing; a genuine change updates `Name` and raises the new `BoardRenamed` event. Owner authorization is not checked here, matching every other Board aggregate method's own division of responsibility — the calling Application Service owns it.

**Application** — `BoardApplicationService.RenameBoardAsync(requestingUserId, boardId, name, cancellationToken)`: existence + membership check (`NotFound`), then Owner authorization (`Forbidden`), then `board.Rename(...)` + `UpdateAsync`, always — no early-return idempotency check here, since the aggregate's own no-op already makes a repeated call and a genuine rename observably identical from this method's point of view (unlike `RemoveMemberAsync`, which must skip an entire repository write). Returns the updated `BoardResult` on success, matching `CreateBoardAsync`'s own return shape — the client needs the new name back, same as every other single-resource-returning Board endpoint.

**Contracts/Api** — `RenameBoardRequest { name }`, matching API_CONTRACT.md §5's literal `{ name }` body (same shape as `CreateBoardRequest`). `RenameBoardRequestValidator` — "Name required," identical rule to `CreateBoardRequestValidator`. `PATCH /v1/boards/{boardId}` in `BoardEndpoints.cs`, returning `200` with the updated Board on success — matches `GetBoard`'s own response shape, per API_CONTRACT.md §5's Rename Board row (`200`, Board).

---

## 2. Test Results

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **159/159** (157 prior + 2 new: `Board.Rename` changes the name and raises `BoardRenamed`, idempotent on the already-current name) |
| `BuzzMe.Application.Tests` | **131/131** (126 prior + 5 new: `RenameBoardAsync` — Owner succeeds, idempotent on the current name, non-Owner `Forbidden`, not-found for a non-member, not-found for a nonexistent Board) |
| `BuzzMe.Infrastructure.IntegrationTests` | **78/78** (77 prior + 1 new: `UpdateAsync` persists a rename — real MongoDB) |
| `BuzzMe.Api.IntegrationTests` | **86/86** (80 prior + 6 new: `PATCH /v1/boards/{boardId}` end to end — success returns the updated Board, idempotent repeat, empty-name validation error, non-Owner forbidden, not-found for a non-member, unauthenticated — real host + real MongoDB) |

**454/454 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors** (the pre-existing `NU1903` SSH.NET/Testcontainers advisory, unrelated to this sprint, remains unchanged since SPRINT_9_REPORT.md).

---

## 3. Specification Interpretation Notes

### 3.1 API_CONTRACT.md's Errors row omits `400` for Rename Board, despite its own "Name required" Validation cell

The Board APIs table's `Validation` row states "Name required" for Rename Board — identical wording to Create Board's own cell, which *does* list `400` in its Errors row. Rename Board's Errors row lists only `401`, `403`, `404`. Treated this as a documentation gap rather than a deliberate omission: the same `BoardName` value object rejects an empty name identically regardless of which use case constructs it, so returning `400` from an equivalent `RenameBoardRequestValidator` (mirroring `CreateBoardRequestValidator` exactly) is consistent behavior, not an invented rule — the alternative (silently accepting an empty name on Rename while rejecting it on Create) would be the actual inconsistency.

### 3.2 No Application-layer idempotency short-circuit, unlike Mute/RemoveMember

APPLICATION_LAYER_SPEC.md §3.4 states re-applying the current name "is a no-op, not an error" — identical wording to Mute/Unmute's own idempotency rule, which `MuteBoardAsync`/`UnmuteBoardAsync` (Sprint 7) implement with an early-return *before* touching the aggregate, specifically to skip a targeted `SetMembershipMutedAsync` repository call. `RenameBoardAsync` has no equivalent write to skip — it always calls the same `UpdateAsync` full-aggregate-replace either way — so the idempotency guarantee is satisfied entirely inside `Board.Rename`'s own no-op, with no Application-layer check needed. A narrower implementation than Mute's, deliberately, because the underlying operation is narrower (one full-document write either way, not a conditional targeted one).

---

## 4. Specification Gaps

No new gaps surfaced by this sprint. The two remaining items from SPRINT_13_REPORT.md §4 (no Purge background worker; no cascading Reminder/Occurrence/Buzz cleanup on Board deletion) are unaffected by RenameBoard and remain open.
