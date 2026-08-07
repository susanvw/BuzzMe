# Sprint 5 Report — Membership & Invitations

*The first true collaboration capability: a Board Owner or Member can invite another person, the invitee can accept, and — as Sprint 4's Buzz Generation Boundary Review anticipated but could never actually exercise — the Board's Buzz generation now runs against a real, more-than-one-Member Board for the first time. `Reminder → Occurrence → Buzz` (Sprint 4) meets `Membership → Invitation → Acceptance` (this sprint) at exactly the seam the boundary review identified.*

---

## 1. Repository Changes

**Domain** (`src/BuzzMe.Domain/Invitations/`): `InvitationId.cs`, `InvitationToken.cs`, `InvitationStatus.cs`, `InvitationChannel.cs`, `Invitation.cs` (aggregate root), `IInvitationRepository.cs`, `IInvitationTokenGenerator.cs`, `Events/InvitationSent.cs`, `InvitationAccepted.cs`, `InvitationDeclined.cs`, `InvitationRevoked.cs`.

**`src/BuzzMe.Domain/Boards/Board.cs`** — gained `GrantMembership(Guid userId, DateTimeOffset grantedAt)`, Sprint 5's own explicitly-scoped "Membership activation." Idempotent by the aggregate's own invariant (a no-op if the user is already a Member), reuses the existing `MembershipGranted` event unchanged. This is the one addition to a pre-existing aggregate this sprint made — squarely in scope, not a refactor: Board previously had no way to gain a Member beyond its creator.

**`src/BuzzMe.Domain/Boards/IBoardRepository.cs`** / **`BoardRepository.cs`** — gained `AddMemberAsync`, a targeted MongoDB `$push` of one new Membership sub-document. `BoardRepository.AddAsync`'s own Sprint 1 comment ("no Update method with no caller yet") anticipated exactly this addition.

**Application** (`src/BuzzMe.Application/Invitations/`): `InvitationApplicationService.cs` — `InviteMemberAsync`, `ValidateInvitationAsync`, `AcceptInvitationAsync`, `DeclineInvitationAsync`, `CancelInvitationAsync`, `ListPendingInvitationsAsync`. `Models/InvitationResult.cs`. `src/BuzzMe.Application/Boards/Models/MembershipResult.cs` (new — Board had no Application-layer read model for a single Membership before this sprint).

**Contracts** (`src/BuzzMe.Contracts/V1/Invitations/`): `InviteMemberRequest.cs`, `InvitationResponse.cs`, `ValidateInvitationResponse.cs`, `DeclineInvitationResponse.cs`. `src/BuzzMe.Contracts/V1/Boards/MembershipResponse.cs`.

**Infrastructure** (`src/BuzzMe.Infrastructure/Persistence/Mongo/Invitations/`): `InvitationDocument.cs`, `Mappers/InvitationMapper.cs`, `InvitationRepository.cs`, plus `Persistence/Migrations/Steps/CreateInvitationIndexes.cs` (migration version 5 — unique `token` index, `boardId+status+_id` list index) and `Ids/SecureInvitationTokenGenerator.cs` (see §4.1 for why this is a distinct generator from every other aggregate's `IIdGenerator`).

**Api** (`src/BuzzMe.Api/`): `Endpoints/InvitationEndpoints.cs` (exactly API_CONTRACT.md's four Invitation endpoints — Invite Member, Validate, Accept, Decline), `Mapping/InvitationMapping.cs`, `BoardMapping.cs` (gained a `MembershipResult → MembershipResponse` mapping), `Validation/InviteMemberRequestValidator.cs`. `Program.cs` registered `InvitationApplicationService` and `MapInvitationEndpoints()`.

**No Cancel/List Invitation endpoints** — see §3.1. Both Application capabilities exist and are tested; neither has an HTTP surface, same posture as Sprint 3/4's read-only Occurrence/Buzz methods.

**Documentation** — DOMAIN_MODEL.md (§2 Reminder, rule 4, the Aggregate Root table), EVENT_STORMING.md (§B4, §D3), and BUSINESS_BEHAVIOR_MODEL.md (RMD-01, RMD-02, RMD-03, RMD-06, NOT-01) had their stale "Participants are a subset of Board Members" language corrected to match the already-authoritative MVP_SCOPE.md rule — see §5.

---

## 2. Test Results

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **86/86** (60 prior + 3 `Board.GrantMembership` tests + 23 `InvitationTests`) |
| `BuzzMe.Application.Tests` | **57/57** (36 prior + 20 `InvitationApplicationServiceTests` + 1 `CollaborationAcceptanceTests`) |
| `BuzzMe.Infrastructure.IntegrationTests` | **33/33** (25 prior + 1 `BoardRepositoryTests.AddMemberAsync` + 7 `InvitationRepositoryTests`, real ephemeral MongoDB) |
| `BuzzMe.Api.IntegrationTests` | **25/25** (14 prior + 11 `InvitationEndpointsTests`, real host + real MongoDB) |

**201/201 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors.** No placeholder code.

**The Sprint 5 acceptance scenario** (`CollaborationAcceptanceTests.OwnerInvitesAMember_BothReceiveExactlyOneBuzzEach_AndRegeneratingCreatesNoDuplicates`, `tests/BuzzMe.Application.Tests/Acceptance/`) runs Owner-creates-Board → Owner-invites-User-A → User-A-accepts → Board has two Members → an existing Reminder's existing Occurrence generates exactly two Buzzes → a second `GenerateBuzzes()` call creates none, entirely through the real Application Services (`BoardApplicationService`, `InvitationApplicationService`, `ReminderApplicationService`, `OccurrenceApplicationService`, `BuzzApplicationService`) wired together, not a single isolated unit. It passed on the first run: Sprint 4's `BuzzApplicationService.GenerateBuzzesAsync` already iterates `board.Memberships` generically, with no hidden assumption about there being exactly one — the Boundary Review's §5.3 risk ("multi-recipient generation is still functionally unverified... the first multi-member Board Sprint 5 produces is also the first real exercise of this code path") is now closed, cleanly.

Idempotency is exercised at every layer this sprint touches: `AcceptInvitationAsync` re-accepted by the same user (Application + Api), `DeclineInvitationAsync`/`CancelInvitationAsync` re-called on an already-resolved Invitation (Application), `Board.GrantMembership` called twice for the same user (Domain + Infrastructure), and the Invitation token's uniqueness enforced as a real database constraint (`AddAsync_RejectsADuplicateToken`, asserting a real `MongoWriteException`) — same "trust the database, not just application logic" pattern established in Sprints 3–4.

---

## 3. Specification Gaps Discovered

Per this sprint's own instruction ("stop and document them rather than inventing behaviour"):

### 3.1 `CancelInvitation` and `ListPendingInvitations` have no API_CONTRACT.md endpoint

API_CONTRACT.md's full endpoint catalogue (§4) defines exactly four Invitation-related routes: Invite Member, Validate, Accept, Decline. There is no `DELETE`/cancel route and no `GET /v1/boards/{boardId}/invitations` list route anywhere in the document. Both capabilities are, however, real: `DOMAIN_MODEL.md` states plainly *"the inviter may revoke it"*, and `EVENT_STORMING.md` §B4 names a distinct, inviter-initiated `RevokeInvitation` command (separate from the block-triggered, system-issued one at the same section) — this is exactly Sprint 5's `CancelInvitation`. `ListPendingInvitations` has no equivalent grounding anywhere — no Application Layer Spec row, no Business Behavior scenario, nothing. **Resolution:** both are implemented as Application Service methods only (`InvitationApplicationService.CancelInvitationAsync`/`ListPendingInvitationsAsync`), fully tested, with no HTTP endpoint — the same "capability exists, no API surface yet" posture Sprint 3 used for `GenerateOccurrencesAsync` and Sprint 4 used for the entire Buzz service. `ListPendingInvitationsAsync`'s authorization (Board Member) isn't independently specified anywhere either; it reuses the one uniform Board-scoped-read default this codebase has applied without exception since Sprint 1, rather than inventing a new rule.

### 3.2 No User/Profile/Account domain exists anywhere in this codebase

Confirmed by direct search: `src/` contains no `Users/`, `Accounts/`, or `Auth/` domain folder — only `ICurrentUserContext`, which extracts an already-authenticated caller's ID from the JWT, nothing more. This is the single root cause behind three separate, related gaps:

- **`inviterDisplayName`** (API_CONTRACT.md's Invitation resource field) cannot be populated — there is no mechanism anywhere to resolve a UserId to a display name. Omitted from `InvitationResult`/`InvitationResponse` entirely, not filled with a placeholder.
- **The Membership resource's `displayName`/`photoUrl`** (API_CONTRACT.md's Membership resource) are unavailable for the same reason. `MembershipResult`/`MembershipResponse` expose only `boardId`, `userId`, `role`.
- **Targeted-invitation identity matching** — API_CONTRACT.md's Accept authorization is *"the invitation's resolved invitee where one was specified"*, meaning an `email`/`sms` invitation should only be acceptable by the person that contact resolves to. With no mechanism to resolve an email/phone string to a UserId, this is not enforced: any authenticated User may accept any Invitation, regardless of channel or target contact. The `link` channel (identity resolved only at acceptance, by design — DOMAIN_MODEL.md) is unaffected by this gap; `email`/`sms` are affected. This is also why **"Cannot invite existing Members" and "Cannot invite blocked users"** (this sprint's own validation examples) are not enforced for any channel — there is no identity to check either rule against.

None of this was invented around: the `channel`/`targetContact` fields are still modeled and stored exactly as API_CONTRACT.md's request body specifies, and the gap is documented at each call site (`InvitationApplicationService`'s own doc comments) rather than silently absorbed.

### 3.3 `Membership` has no `joinedAt` (or `muted`) field

Independent of §3.2: even with a User/Profile domain, `Membership.cs` (Sprint 1) has never carried a timestamp. API_CONTRACT.md's Membership resource wants `joinedAt`; nothing in any sprint's scope — including this one — has added it. Noted here because it compounds with §3.2 rather than being caused by it.

### 3.4 Block remains entirely unimplemented — documented exactly as Sprint 4 did

Per this sprint's own instruction, re-confirmed by direct search: no `Block` aggregate, repository, or field exists anywhere in `src/`. "Cannot invite blocked users" is unenforced for the same reason it was unenforced for Buzz generation in Sprint 4 (SPRINT_4_REPORT.md §4.1) — nothing to check against. No new Block-shaped code was written this sprint.

---

## 4. Architecture Observations

### 4.1 `InvitationToken` is deliberately not a GUIDv7, unlike every other identifier in this codebase

Every aggregate ID in BuzzMe (`BoardId`, `ReminderId`, `OccurrenceId`, `BuzzId`, and `InvitationId` itself) is a GUIDv7 — time-sortable by design (DEVELOPMENT_GUIDE.md §9), which is exactly the wrong property for `InvitationToken`: a bearer credential that is emailed, texted, and put in links. Sortability would leak issuance order and shrink the effective guessing space. `InvitationToken` is a separate value object, generated by a new `IInvitationTokenGenerator` → `SecureInvitationTokenGenerator` (256 bits from `RandomNumberGenerator`, Infrastructure) — deliberately not reusing `IIdGenerator`. This is the one place this sprint introduced a second identifier-generation abstraction; it's justified by a real security property, not a stylistic preference.

### 4.2 `Invitation` and `Board` never load each other directly

`InvitationApplicationService` — not the `Invitation` aggregate, and not the `Board` aggregate — is what loads both and orchestrates between them, exactly the boundary the Buzz Generation Boundary Review confirmed for `GenerateBuzzes` two sprints ago, now reused for a second, structurally identical case: two separate Aggregate Roots (`DOMAIN_MODEL.md` §6: *"neither of those aggregates should need to be locked to expire or revoke an Invitation"*), coordinated only at the Application layer, referenced only by ID. `AcceptInvitationAsync`'s two-step shape (`Invitation.Accept()` + `invitationRepository.UpdateAsync()`, then `Board.GrantMembership()` + `boardRepository.AddMemberAsync()`) mirrors APPLICATION_LAYER_SPEC.md §3.5's own description precisely: *"Invitation transitions to Accepted in its own transaction; MembershipGranted on the Board is a separate, second transaction... an eventually-consistent step, not one atomic operation."*

### 4.3 The EVENT_STORMING-flagged accept/revoke race is not resolved at the database layer

EVENT_STORMING.md §B4 names this directly: *"AcceptInvitation and RevokeInvitation arriving near-simultaneously must resolve deterministically... enforced at the aggregate's consistency boundary (optimistic concurrency / compare-and-set)."* `InvitationRepository.UpdateAsync` is a plain `ReplaceOneAsync` keyed only by `Id`, with no `Version`-checked filter — consistent with, not a regression from, every other repository in this codebase (no sprint to date has ever exercised `AggregateRoot.Version` as an actual optimistic-concurrency gate; `BoardRepository`'s own Sprint 1 comment says as much). Given the low probability and the explicit "do not introduce new abstractions" instruction, this was left as-is rather than being the sprint that first wires up real compare-and-set — but it's the correct, narrowly-scoped next step, not a silently-ignored gap. See §5.

---

## 5. Remaining Collaboration Work

1. **Real optimistic concurrency for Invitation (and, by extension, every other mutable aggregate)** — §4.3. `AggregateRoot.Version` has been present and documented since Sprint 1 but never enforced by any repository's write path. Invitation's accept/revoke race is the first place this sprint touched where that gap is concretely reachable (two people, or an inviter and an invitee, racing on the same document); worth being the first repository to close it.
2. **`RemoveMember`/`LeaveBoard`** — not in this sprint's scope. `Membership` still has no way to leave the Board once granted; IMPLEMENTATION_SPEC.md's invariant 6 ("a Membership once Removed or Left is never reactivated") remains unreachable/untestable until one of these exists, same as it was before this sprint.
3. **Block** — §3.4. The single biggest unblock for tightening Invitation's own validation rules ("cannot invite blocked users") once it exists.
4. **A User/Profile domain** — §3.2/§3.3. Needed for `inviterDisplayName`, Membership's `displayName`/`photoUrl`/`joinedAt`, and real targeted-invitation identity matching. The largest of the remaining gaps by scope; everything else in this report traces back to it in some form.
5. **`Expire Invitations` background sweep** — explicitly out of scope this sprint ("No background cleanup worker yet"). `Invitation.IsExpired`'s lazy-expiration design means this is additive when it arrives: a future sweep can start physically transitioning `Status` to `Expired` without any existing caller needing to change, since every caller already treats a past-`ExpiresAt` Pending Invitation as expired regardless of its stored status.
6. **Cancel/List Invitation API endpoints** — §3.1, if and when API_CONTRACT.md is updated to define them; the Application capability is ready and tested.
