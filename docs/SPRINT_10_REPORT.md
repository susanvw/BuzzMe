# Sprint 10 Report — Board Ownership Reassignment

*Membership.cs's own doc comment, since Sprint 1, has said "no lifecycle status field yet — Remove Member / Leave Board, a future sprint, is what will introduce that state." This sprint is that sprint: a real `MembershipStatus` lifecycle, `LeaveBoard` with the inline, same-transaction `ReassignOwnership` policy IMPLEMENTATION_SPEC.md §4 specifies (auto-selecting the longest-standing other Active Member, no offer/accept step — that's an explicit V1 simplification, not a shortcut this sprint took), and `RemoveMember`. DeleteAccount itself stays out — see §5 for exactly how much closer this leaves it, and what's still missing.*

---

## 1. Repository Changes

**Domain** — new `MembershipStatus` enum (`Active`, `Removed`, `Left`, matching IMPLEMENTATION_SPEC.md §1's lifecycle exactly). `Membership` gained `Status` and `JoinedAt` (the latter needed for ReassignOwnership's "longest-standing" selection — never carried before this sprint), plus internal `SetRole`/`MarkLeft`/`MarkRemoved`. `Board.HasMember`/`OwnerUserId` are now Active-filtered — a Board may hold more than one historical row for the same UserId once someone can leave and later rejoin (IMPLEMENTATION_SPEC.md §1: "a subsequent Invitation acceptance by the same User creates a new Membership record"), so raw `.Memberships.First(userId)`-style lookups are no longer safe anywhere. New public `Board.FindActiveMembership(userId)` replaces every such call site. `Board.Leave(userId, leftAt)` runs the ReassignOwnership policy inline when the departing person is the sole Owner with other Active Members remaining (demotes them to Member, promotes the longest-standing other Active Member, both in the same transaction — APPLICATION_LAYER_SPEC.md §3.2/§7's "single-aggregate operation, not a saga"), then marks the Membership `Left`; returns the new Owner's UserId or null. `Board.RemoveMember(targetUserId, removedAt, removedByUserId)` never touches ownership — the caller (an Owner, per authorization) can never target themselves, so the target is always a non-Owner Member. Three new events: `MemberLeft`, `MemberRemoved`, `BoardOwnershipReassigned` (IMPLEMENTATION_SPEC.md §5: the future Offer/Accept/Decline transfer sequence is collapsed to this one system-triggered event for V1).

**Application** — `BoardApplicationService` gained `LeaveBoardAsync`/`RemoveMemberAsync` and now depends on `IUserRepository` (for LeaveBoardAsync's "cannot leave your Personal Board" check, APPLICATION_LAYER_SPEC.md §3.2). Business validation (Personal Board, sole-Member-entirely) is checked here, before the aggregate; `Board.Leave` itself only defends the genuine invariant beneath both (never zero Active Memberships). Two pre-existing call sites — `BoardApplicationService.MuteBoardAsync`/`UnmuteBoardAsync` and `InvitationApplicationService.AcceptInvitationAsync` — used raw `.Memberships.First(userId)` lookups that this sprint's Membership-status change made unsafe; all switched to `FindActiveMembership`.

**Infrastructure** — `MembershipDocument` gained `Status`/`JoinedAt`; `BoardMapper` maps both ways. `IBoardRepository`/`BoardRepository` gained `UpdateAsync` (a full-document replace, unlike the existing targeted `$`-operator updates — Leave-with-reassignment changes two Membership rows in one transaction, which a positional update can't express; mirrors `UserRepository.UpdateAsync`'s own plain-replace shape, no Version check, same category). `AddMemberAsync` now takes a `joinedAt` parameter. `ListByMemberAsync` and `SetMembershipMutedAsync`'s Mongo filters are now Status-filtered (see §2 — the latter was a real, caught bug).

**Api** — `POST /v1/boards/{boardId}/leave` (`200, { reassignedOwnerUserId }`) and `DELETE /v1/boards/{boardId}/members/{userId}` (`204`), matching API_CONTRACT.md §5 exactly, including its error-code grouping (see §2).

---

## 2. Bugs Caught Before They Shipped

Two genuine correctness bugs surfaced while implementing this sprint — both are direct consequences of Membership rows no longer being "active by construction," an assumption several pieces of already-shipped code depended on without stating it.

### 2.1 `BuzzApplicationService.GenerateBuzzesAsync` would have re-buzzed removed/left Members

Its own doc comment (since Sprint 4) read: *"every Membership present on Board.Memberships is active by construction."* That was true until this sprint. The method iterated `board.Memberships` directly to decide who to generate a Buzz for — once a Board can hold historical `Removed`/`Left` rows, that loop would have generated Buzzes for people no longer on the Board. Fixed: filtered to `Status == Active`; the stale comment is corrected in place.

### 2.2 `BoardRepository.SetMembershipMutedAsync`'s Mongo filter matched by UserId alone

MongoDB's positional `$` update operator updates the *first* array element matching the filter. Once a Board can hold a historical `Removed`/`Left` row for someone who later rejoined (a fresh `Active` row), a filter matching only `UserId` could target the wrong row depending on array order — silently muting/unmuting a dead historical record instead of the real one. Fixed: the filter now also requires `Status == "Active"`.

Both were found by a background research pass auditing every `HasMember`/`Memberships` call site before this sprint's Domain changes were finalized, cross-checked against the actual implementation as it was written.

---

## 3. Test Results

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **153/153** (142 prior + 11 new: `Leave`'s non-Owner/sole-Owner-reassignment/sole-Owner-no-others/not-a-Member cases, the longest-standing-selection rule, `RemoveMember`, `GrantMembership`-after-Leave creating a new row rather than reactivating the old one, `FindActiveMembership`) |
| `BuzzMe.Application.Tests` | **108/108** (95 prior + 13 new: `LeaveBoardAsync`/`RemoveMemberAsync` full coverage — Personal Board, sole-Member, idempotency, authorization, self-targeting) |
| `BuzzMe.Infrastructure.IntegrationTests` | **70/70** (67 prior + 3 new: `UpdateAsync` persisting a Leave-with-reassignment and a Removal, `ListByMemberAsync` excluding a Board the caller has Left — real MongoDB) |
| `BuzzMe.Api.IntegrationTests` | **64/64** (54 prior + 10 new: Leave/RemoveMember over real HTTP + real MongoDB, including the reassignment case and both idempotency paths) |

**395/395 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors** (the SSH.NET `NU1903` advisory some test projects report is Testcontainers' own pre-existing transitive dependency, unrelated to this sprint, same note as SPRINT_9_REPORT.md's).

---

## 4. Specification Interpretation Notes

### 4.1 The sole-Member-leaving case is `403`, not `409`

API_CONTRACT.md §5's Leave Board row groups it with the Personal Board case under a single `403 (Personal Board / sole-Member — see App Layer §3.2)`. My first implementation used `409 CONFLICT` for the sole-Member case, reasoning by the general error catalogue's own definitions ("a business-rule conflict on an otherwise well-formed request" reads like a natural fit) — that was a plausible-but-wrong guess corrected once I re-read the literal table cell. Both cases now return `403`.

### 4.2 RemoveMember's target-not-found vs. idempotent-no-op distinction

API_CONTRACT.md states "already `Removed` → no-op," but a target who was *never* a Member at all is a different case, and the two shouldn't collapse to the same response — a stranger's random GUID shouldn't silently succeed. Resolved by checking whether the target has *any* Membership row at all (`404` if none) before checking whether their most recent one is Active (`204` no-op if it's already Removed/Left, proceed if Active) — the same two-step shape LeaveBoardAsync's own idempotency check needed for the identical reason (§4.3).

### 4.3 LeaveBoardAsync's own idempotency check needed the same two-step shape

The first implementation checked `board.HasMember(requestingUserId)` (now Active-only) to decide "board not found or not a Member" — but that's also true of someone who *already left*, so a second Leave call hit `404` instead of the intended idempotent `200`. Fixed by checking "has any Membership record at all" for existence, then separately checking "is it still Active" for the idempotency short-circuit — caught by `LeaveBoardAsync_CalledAgain_IsIdempotent` failing on first run, not by inspection.

---

## 5. Specification Gaps

### 5.1 DeleteAccount is closer, but still not unblocked

The core ReassignOwnership *policy* this sprint built is the same one IMPLEMENTATION_SPEC.md §4 names as DeleteAccount's own prerequisite ("if the account is the sole Owner of any shared Board, the command... triggers ReassignOwnership inline, as the first step of the same operation"). But `Board.Leave` bundles reassignment together with *also* marking the actor's own Membership `Left` — DeleteAccount needs reassignment-without-a-Leave (the User is being deleted outright, not leaving one Board at a time), which is a related but distinct domain operation this sprint didn't build. DeleteAccount also still needs: revoking all of a User's RefreshTokens (no "revoke all for User" method exists on `IRefreshTokenRepository` — Sprint 9 only built single-token revocation), anonymizing authorship on shared Boards' History (no History/audit-trail entity exists anywhere in this codebase), and purging the Personal Board's content. Each is its own real piece of work — recorded here rather than attempted under this sprint's narrower banner.

### 5.2 Buzz cancellation on Leave/RemoveMember was not built — and neither was it built for three earlier use cases that needed it first

APPLICATION_LAYER_SPEC.md §3.2/§3.6 specify "cancels pending Buzzes" as a side effect of both LeaveBoard and RemoveMember. Investigating this surfaced that **no Buzz-cancellation mechanism exists anywhere in this codebase** — `DeleteReminder` (Sprint 4) and `CompleteReminder`/`DismissReminder` (Sprint 6) carry the identical specified side effect and never built it either, with no prior sprint report flagging it. This isn't a gap this sprint introduced; it's a pre-existing, previously-unflagged one this sprint's own research happened to surface. Building it now, only for Leave/RemoveMember, would leave the codebase in a more inconsistent state than today's uniform (if incomplete) absence — a real `Buzz.Cancel()` domain method plus the repository query shape it needs (by-Occurrence-and-recipient, by-Board-and-recipient) is a legitimate, cross-cutting sprint of its own, touching Reminder/Occurrence/Board alike, not something to build piecemeal under one use case's name.

### 5.3 List Members has no endpoint

API_CONTRACT.md §5 names `GET /boards/{boardId}/members` in the same table as Leave/RemoveMember, but it wasn't part of the scope chosen for this sprint (ownership reassignment specifically) and RemoveMember's own `{userId}` path parameter doesn't require it to exist. `MembershipResult`/`MembershipResponse` also still omit `muted`/`joinedAt` per SPRINT_5_REPORT.md's original gap — worth noting that both fields now genuinely exist on the domain (`Muted` since Sprint 7, `JoinedAt` since this sprint), so closing that DTO gap is now a small, low-risk follow-up rather than one still blocked on missing data.

---

## 6. Architecture Observations

1. **A Board's Memberships collection changed shape from "always the current member list" to "an append-only history with a current-state filter."** Every future piece of code that reads `Board.Memberships` needs to reason about this the way `FindActiveMembership`'s own doc comment now states explicitly — this sprint's two caught bugs (§2) are exactly what happens when that reasoning is skipped.
2. **`Board.Leave`'s reassignment logic reuses `JoinedAt`, a field that existed for exactly one reason before this sprint: none.** It was added specifically to make "longest-standing other Active Member" answerable — a reminder that IMPLEMENTATION_SPEC.md §4's precision (naming *which* Member gets promoted, not just *that* one does) already implied a piece of state no prior sprint had needed yet.
3. **The single-aggregate-transaction design (Membership living inside Board) is what makes Leave-with-reassignment atomic without any saga machinery.** Two Membership rows change role/status together, in one `ReplaceOneAsync`, with no possibility of a partial state where a Board ends up with zero or two Owners mid-operation — exactly the property APPLICATION_LAYER_SPEC.md §7 called out this design choice for.
