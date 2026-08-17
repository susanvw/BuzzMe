# Sprint 11 Report — List Members

*`MembershipResult`'s own doc comment, since Sprint 5, has read: "`displayName`/`photoUrl` need a User/Profile domain this codebase doesn't have; `muted`/`joinedAt` need fields Membership.cs itself has never carried." All four are now real — `muted` since Sprint 7, `joinedAt` since Sprint 10 (built for ReassignOwnership's own "longest-standing Member" need), and a real User domain since Sprint 8/9. This sprint is the one that finally reads all four together and ships the endpoint that was always the reason they were asked for.*

---

## 1. Repository Changes

**Application** — `MembershipResult` gained `DisplayName`, `PhotoUrl`, `Muted`, `JoinedAt`. `FromDomain(boardId, membership, user)` takes an optional `User?` — `DisplayName`/`PhotoUrl` are `null` when it's omitted (the existing `InvitationApplicationService.AcceptInvitationAsync` call site, unchanged this sprint — see §3) or when no matching User record exists (a data-integrity anomaly nothing in this codebase can currently produce, but nothing enforces it across the two aggregates either, so the read degrades gracefully rather than failing). `BoardApplicationService.ListMembersAsync` — Board-Member authorization, cursor/limit pagination mirroring `ListBoardsAsync` exactly, just applied to an in-memory collection: a Board's Memberships are already loaded whole as part of the aggregate, and DOMAIN_MODEL.md's family/team scale never approaches the size where that matters. Sorted and paginated by UserId, not JoinedAt, for the same reason every other list endpoint in this codebase sorts by Id — one stable, always-unique key (unique among a Board's *Active* Memberships specifically, which is exactly the set this endpoint returns — see IMPLEMENTATION_SPEC.md §1's "at most one Active Membership per (Board, User) pair"). Removed/Left historical rows are excluded, same Active-only filtering established in Sprint 10.

**Contracts** — `MembershipResponse` gained the same four fields, now matching API_CONTRACT.md §3's Membership resource exactly (`userId, displayName, photoUrl, role, muted, joinedAt`, plus this codebase's own `boardId` carried since Sprint 5).

**Api** — `GET /v1/boards/{boardId}/members`, `200` with the standard `cursor`/`limit`/`pagination.nextCursor` list shape (API_CONTRACT.md §10: "every list endpoint uses the identical shape... verified across Boards, Members, Board Reminders, Reminder History, and Notifications" — Members' own turn, this sprint). Default limit 20, max 100, same clamp as every other list endpoint.

**No Domain or Infrastructure changes.** Every field this endpoint needed already existed on `Membership`/`User` as of Sprint 10 — this sprint is purely Application/Contracts/Api, reading data that was already there.

---

## 2. Test Results

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **153/153** (unchanged — no Domain changes this sprint) |
| `BuzzMe.Application.Tests` | **114/114** (108 prior + 6 new: full field population, excludes Removed/Left, graceful null when no User record exists, cursor/limit pagination, not-a-Member/nonexistent-Board) |
| `BuzzMe.Infrastructure.IntegrationTests` | **70/70** (unchanged — no new repository method; List Members reuses `IBoardRepository.GetByIdAsync`/`IUserRepository.GetByIdAsync`, both already covered) |
| `BuzzMe.Api.IntegrationTests` | **68/68** (64 prior + 4 new: real HTTP + real MongoDB + real seeded Users, confirming `displayName`/`role` come through correctly for both an Owner and a Member, excludes a Member who Left, authorization, unauthenticated) |

**405/405 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors** (SSH.NET's `NU1903` advisory remains the same pre-existing, unrelated Testcontainers transitive dependency noted in SPRINT_9_REPORT.md/SPRINT_10_REPORT.md).

---

## 3. Specification Gaps and Notes

### 3.1 AcceptInvitation's own Membership response still doesn't carry displayName/photoUrl

API_CONTRACT.md §5 gives Accept Invitation the identical `Membership` response shape List Members now fully populates, but `InvitationApplicationService.AcceptInvitationAsync` wasn't touched this sprint — it still calls `MembershipResult.FromDomain(board.Id, membership)` without a `User`, so its own response's `displayName`/`photoUrl` remain `null`. This is a small, real inconsistency across two endpoints sharing one resource shape, deliberately left alone: fixing it means injecting `IUserRepository` into `InvitationApplicationService` and looking up the accepting User's own record (a user who, by definition, already exists and is already authenticated — so unlike List Members' defensive nullability, this lookup would always succeed in practice). Small and low-risk, but adjacent to Invitation's own sprint, not List Members' — recorded here rather than done opportunistically while this file was already open.

### 3.2 `role` is still cased `Owner`/`Member`, not `owner`/`member`

API_CONTRACT.md §3 states the Membership resource's `role` field as `owner`|`member` (lowercase). `Membership.Role.ToString()` — unchanged since Sprint 5, and exercised by this sprint's own new tests exactly as it already was by `InvitationEndpointsTests`/`InvitationApplicationServiceTests` — produces `"Owner"`/`"Member"` (the C# enum's own casing). This is a pre-existing deviation from the literal contract text, not something List Members introduces; noted here because writing this sprint's own assertions (`Assert.Equal("Owner", owner.Role)`) made it newly visible in a second place. Not changed: correcting it would be a breaking response-shape change affecting Invitation's already-shipped, already-tested behavior too, not a List-Members-scoped fix.

---

## 4. Architecture Observations

1. **This sprint is the clearest example yet of a gap closing itself as a side effect of unrelated work.** Sprint 7 added `Muted` for Board mute, not for List Members. Sprint 10 added `JoinedAt` for ReassignOwnership's Member-selection, not for List Members. Sprint 8/9 built a real User domain for authentication, not for List Members. By the time this sprint asked "what does List Members need," three of its four missing fields were already sitting on the domain, built for entirely different reasons — the sprint itself was almost entirely plumbing.
2. **Pagination over an in-memory collection (§1) is a deliberate, scale-aware choice, not a shortcut.** Every other list endpoint in this codebase queries Mongo directly because its underlying collection can be arbitrarily large (Boards a User belongs to, a Board's Reminders). A Board's own Membership list is bounded by DOMAIN_MODEL.md's own stated scale (a family, a team) — loading the whole aggregate and paginating the array in memory is the right call here specifically because that scale assumption holds, not a general pattern to reach for elsewhere.
