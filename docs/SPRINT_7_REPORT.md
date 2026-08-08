# Sprint 7 Report — Real Notification Preferences

*"Real notification preferences," per the specifications' own precision level, turned out to be narrower than the name suggests: exactly one dimension of Notification Preference — Board mute — is specified precisely enough to implement (a command, an authorization rule, an idempotency rule, two API endpoints, all already on the books). The other two named dimensions — channel and quiet hours — are named at the conceptual level only, with zero implementation-precision detail anywhere. This sprint builds the first and documents the second and third as genuine gaps, rather than inventing field shapes to fill them.*

---

## 1. Repository Changes

**A specification contradiction, resolved before writing code.** BUSINESS_BEHAVIOR_MODEL.md's NOT-02 scenario describes Board mute as living inside a separate Notification Preferences aggregate ("Objects Updated: Notification Preferences (and, for a Board mute, the Board-scoped override within it)"). APPLICATION_LAYER_SPEC.md §3.4 — more precise, and explicitly authoritative for implementation alongside IMPLEMENTATION_SPEC.md — states plainly that Mute/Unmute is *"a single-aggregate transaction (Board), updating... the requester's own Membership's `muted` flag."* These describe different aggregate ownership for the same piece of state and cannot both be literally true. Resolved in favor of APPLICATION_LAYER_SPEC.md's more precise statement: **Board mute lives on `Membership`, inside the `Board` aggregate — there is no separate Notification Preference aggregate in this codebase, and none was created this sprint.**

**Domain** — `src/BuzzMe.Domain/Boards/Membership.cs` gained `bool Muted { get; private set; }` (defaults `false`) and an `internal SetMuted(bool)` mutator, callable only from within `Board` (same assembly). `Board.cs` gained `MuteBoard(Guid userId, DateTimeOffset mutedAt)` / `UnmuteBoard(...)` — idempotent (a no-op, no event, when already in the target state — APPLICATION_LAYER_SPEC.md §3.4's own stated rule), throwing if `userId` isn't a Member at all (a genuine invariant violation, matching `Buzz`'s/`Invitation`'s existing defensive-guard convention — never expected to trigger in practice since every Application Service already checks membership first). New events: `Events/BoardMuted.cs`, `Events/BoardUnmuted.cs`.

**Application** — `BoardApplicationService` gained `MuteBoardAsync`/`UnmuteBoardAsync(requestingUserId, boardId, ct)`. No target-user parameter, by design — APPLICATION_LAYER_SPEC.md §3.4: *"acting only on their own Membership — never another person's."* Checks the current mute state before touching the aggregate or the database, matching this codebase's established idempotent-write pattern (skip the domain call and the persistence call together when there's nothing to do).

**Infrastructure** — `MembershipDocument` gained `required bool Muted`. `BoardMapper` maps it both ways. `IBoardRepository`/`BoardRepository` gained `SetMembershipMutedAsync(boardId, userId, muted, ct)` — a targeted MongoDB update using the positional `$` operator (filtered by `Id` + `ElemMatch` on `UserId`), not a full-document replace, matching `AddMemberAsync`'s existing targeted-update precedent. No Version/optimistic-concurrency check needed, same reasoning as `AddMemberAsync`: two different Users muting concurrently are two independent, naturally-atomic single-document updates.

**Api** — `POST /v1/boards/{boardId}/mute` and `POST /v1/boards/{boardId}/unmute`, already named in API_CONTRACT.md's endpoint catalogue but never implemented until now (`BoardEndpoints.cs`'s own doc comment previously read "exactly: Create, Get, List. No additional endpoints" — updated). `204` on success (per API_CONTRACT.md's own success table), `404` for a non-Member (never `403` — matches this codebase's universal "404 not 403 for invisible resources" principle; no case actually produces `403` for these two endpoints despite that code appearing in the contract's shared error-code column for the row).

**No new aggregate, no new Contracts DTOs, no new collection/index.** Mute is a field on an existing entity within an existing aggregate — there was nothing else to add.

---

## 2. Test Results

| Project | Result |
|---|---|
| `BuzzMe.Domain.Tests` | **102/102** (94 prior + 8 new: `MuteBoard`/`UnmuteBoard` transitions, idempotency, raised events, not-a-Member guard) |
| `BuzzMe.Application.Tests` | **67/67** (60 prior + 7 new: `MuteBoardAsync`/`UnmuteBoardAsync` orchestration, authorization, idempotency) |
| `BuzzMe.Infrastructure.IntegrationTests` | **47/47** (44 prior + 3 new: default-unmuted-on-create, targeted mute of one Member's Membership without affecting another's, clearing mute back to false — real MongoDB) |
| `BuzzMe.Api.IntegrationTests` | **32/32** (25 prior + 7 new: `204` on mute/unmute, idempotent replay, `404` for non-Members, `401` unauthenticated — real host + real MongoDB) |

**248/248 total.** `dotnet build BuzzMe.sln` → **0 Warnings, 0 Errors.** No placeholder code.

---

## 3. Specification Gaps

### 3.1 Channel preference and quiet hours have no implementation-precision specification anywhere

DOMAIN_MODEL.md names Notification Preference's purpose as *"channel, quiet hours, mute state, and per-Board overrides"* and BUSINESS_BEHAVIOR_MODEL.md's NOT-02 names the same three user-facing actions (*"mutes a specific Board, sets quiet hours, changes preferred channel"*). Beyond that: **zero** field shapes, enum values, time formats, or validation rules exist for channel or quiet hours in any of the five specification documents. Confirmed by direct search: `APPLICATION_LAYER_SPEC.md` contains no `NotificationPreference`/`quiet hour` command row at all (only the fully-specified `MuteBoard`/`UnmuteBoard`); `API_CONTRACT.md`'s `User` resource field list (`id`, `displayName`, `photoUrl`, `email`, `phone`, `status`, `personalBoardId`) has no preference fields, and no dedicated preferences endpoint exists anywhere — only the generic `PATCH /v1/users/me` (Update Profile), whose own request body is never shown to include them. Building either now would mean inventing a data shape — exactly what this codebase's practice, repeated across every sprint, has consistently avoided. Not built; recorded here as the reason.

### 3.2 IMPLEMENTATION_SPEC.md never actually scoped Notification Preference into V1 at all

IMPLEMENTATION_SPEC.md's own aggregate catalogue (§1) lists exactly `User, Board, Membership, Reminder, Occurrence, Buzz, Invitation, Block` — and its explicit out-of-scope line names `Entity, Guest-role Membership, Report/Moderation, and every AI/Draft-related aggregate`. **Notification Preference appears in neither list.** This is different from Entity/Guest/Report, which were explicitly considered and explicitly excluded — Notification Preference was simply never addressed when this document restated the system at implementation precision. The User section's own Responsibilities line (*"identity, authentication state, minimal profile (name, photo, email/phone), and the reference to its own Personal Board"*) doesn't mention it either, consistent with User/Account/Auth never having been implemented in this codebase at all (confirmed repeatedly since Sprint 5 — only `ICurrentUserContext`, a JWT-claim reader, exists). Given `NotificationPreferencesInitialized` is specified (EVENT_STORMING.md) as part of an account-provisioning policy that itself was never built, there is no natural hook to create a Notification Preference record at all right now — matching exactly why Board mute was implementable (it hangs off `Membership`, which already exists) while channel/quiet-hours are not (they would need their own aggregate, owned by a `User` concept this codebase represents only as a bare `Guid`, same as everywhere else).

### 3.3 Mute is still not consulted anywhere in the Buzz delivery pipeline

Per DELIVERY_PIPELINE_REVIEW.md §1.5's own conclusion (confirmed again in this session's follow-up refinement), the mute check belongs at **dispatch time**, inside a future `BuzzApplicationService` method that also performs real channel selection — not at generation time, and not wired into `BuzzDeliveryWorker`'s current claim/mark loop, which still only talks to the temporary `INotificationDispatcher` stub. Building that check now, ahead of real dispatch orchestration, would mean guessing at how a muted delivery attempt should resolve status-wise (`Failed`? A new, unspecified status? Silently `Delivered`?) — none of which is named anywhere. Left exactly where the last review said it belongs: wired in when real channel selection is built, not before.

---

## 4. Architecture Observations

1. **`Membership` remains a plain entity, not a second aggregate root, and that was the correct call to preserve.** Adding `Muted` to it (rather than reaching for a new aggregate) keeps `Board`'s existing "always exactly one Owner, transactionally" invariant machinery untouched — precisely the reason DOMAIN_MODEL.md/IMPLEMENTATION_SPEC.md put Membership inside Board in the first place, now reused for a second, unrelated invariant (per-person mute) without disturbing the first.
2. **The positional-array `$` update is a new pattern for this repository, not previously needed.** `AddMemberAsync` (Sprint 5) only ever appended to the array; `SetMembershipMutedAsync` is the first write that targets one existing element within it. Tested directly against real MongoDB with a two-Member Board to confirm the positional filter only ever touches the matched element, never the whole array.
3. **This sprint is a clean illustration of the "implement precisely what's specified" discipline paying off differently than usual.** Every prior sprint's "gap" was a thing named but under-specified (Block, User/Profile, RetryScheduled). This one is subtler: the *feature name* the sprint brief used ("notification preferences") is broader than what the specifications actually back with implementation precision — the correctly-scoped sprint turned out to be about a third the size the name implied, once the specs were read literally rather than by their title.

---

## 5. Remaining Notification Preference Work

1. **Channel preference and quiet hours** (§3.1) — need their own implementation-precision specification (field shapes, enum values, a time format, validation rules) before any code should be written against them, per this codebase's consistent practice.
2. **A `NotificationPreference` aggregate, if and when channel/quiet-hours are specified** — likely owned by a bare `UserId` (matching every other "belongs to a User" reference in this codebase, none of which depend on an actual `User` aggregate existing), with a lazy-default read pattern (no record ⇒ implicit sensible defaults) rather than requiring an account-provisioning hook this codebase doesn't have (§3.2) — mirroring exactly how Reminder's soft-delete and Board's implicitly-active Membership already treat absence as a meaningful default.
3. **Wiring the mute check into real Buzz dispatch** (§3.3) — belongs to whichever sprint builds real channel selection per DELIVERY_PIPELINE_REVIEW.md §1.5, inside a new `BuzzApplicationService` method, not before.
4. **A User/Profile/Account domain** — the root gap behind both §3.1 and §3.2, already named in SPRINT_5_REPORT.md §3.2 and SPRINT_6_REPORT.md §5, still the largest piece of unaddressed scope this whole session has repeatedly surfaced.
