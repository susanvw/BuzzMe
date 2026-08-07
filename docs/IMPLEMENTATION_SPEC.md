# BuzzMe — Implementation Specification

*This document closes every gap [ARCHITECTURE_REVIEW.md](./ARCHITECTURE_REVIEW.md) found, and restates the whole system at implementation precision. It supersedes any prior document on behavioral detail where they conflict — where the Product UX Specification, Domain Model, or Event Storming documents describe something at odds with what's written here (e.g., Entities, Guest role, Board Archive, People Search, the AI-first creation flow, an explicit Transfer Ownership UI), **this document is authoritative for V1**; those are out of scope and excluded from every catalogue below. No product redesign, no new features, no further simplification — this is the same architecture, made unambiguous.*

---

## 1. Aggregate Implementation Notes

### User

- **Responsibilities:** identity, authentication state, minimal profile (name, photo, email/phone), and the reference to its own Personal Board.
- **Invariants:** email/phone unique across all Users; exactly one `personalBoardId` set exactly once, at account provisioning, never changed afterward by any command (this is the resolution to Architecture Review Critical 1.1 — see §5).
- **Lifecycle:** `PendingVerification → Active ⇄ Deactivated`; `Active|Deactivated → Suspended` (platform-only, not self-service); any non-Deleted state `→ Deleted` (terminal).
- **Ownership:** root aggregate, no parent.
- **Relationships:** owns exactly one Personal Board (by reference, not containment — the Personal Board is still an ordinary Board aggregate); holds zero or more Memberships on other Boards (by reference, held on the Board/Membership side, not duplicated here).

### Board

- **Responsibilities:** holds its own Membership list; enforces the single-Owner invariant transactionally.
- **Invariants:** exactly one Active Membership with `role = Owner` at all times, no exceptions, including mid-transaction; at least one Active Membership of any role; `name` non-empty.
- **Lifecycle:** `Created → Active → Deleted`. **There is no Archived state for Board in V1** — confirmed removed per [MVP_SCOPE.md](./MVP_SCOPE.md); `Deleted` is internally soft (see §4, Purge policy) but user-facing behavior is simply "gone."
- **Ownership:** root aggregate.
- **Relationships:** contains Memberships as child entities within its own consistency boundary (a Membership change and the Owner invariant must commit atomically together). Reminders reference a Board by ID; a Board does not contain Reminders as child entities — Reminder is its own aggregate root.

### Membership (entity within Board)

- **Responsibilities:** a single User's standing relationship to a single Board.
- **Invariants:** at most one **Active** Membership per (Board, User) pair at any time; role is exactly one of `Owner`, `Member` — no other value exists in V1 (Guest is future-only and must not appear as an implementable value yet); a Membership once `Removed` or `Left` is **never reactivated** — a subsequent Invitation acceptance by the same User creates a **new** Membership record, preserving the fact that they left/were removed once (this closes a gap the Architecture Review didn't explicitly name but that fell directly out of specifying Membership's lifecycle precisely).
- **Lifecycle:** `Active → (Removed | Left)`, terminal per record. `muted: boolean` is a mutable attribute of an Active Membership, settable only by the Member it belongs to, for themselves — never by anyone else (this is the "Mute Board" affordance carried over from the simplification pass; it is not a separate aggregate).

### Reminder

- **Responsibilities:** the definition — title, recurrence, notification timing, and its one Board.
- **Invariants:**
  - `boardId` is set at creation and is **immutable for the life of the Reminder** — this is the resolution to Architecture Review Medium 2.2. Moving a Reminder's intent to a different Board is done by deleting it and creating a new one on the target Board; there is no "move" operation, and Edit never accepts a Board change.
  - `recurrence` is exactly one of `Once | Daily | Weekly | Monthly | Yearly` — no custom rule engine.
  - `notifyPreset` is exactly one of `AtTime | 15MinBefore | 1HourBefore | 8HoursBefore | 1DayBefore | 1WeekBefore`, defaulted to `AtTime`.
  - `referenceTimezone` (IANA zone name, e.g. `Africa/Johannesburg`) is captured automatically from the creating device at creation and is **immutable afterward** — this is the resolution to Architecture Review Critical 1.2 (see §5).
  - Editable fields, ever: `title`, the date/recurrence pattern, `notifyPreset`. Not editable, ever: `boardId`.
- **Lifecycle:** `Created → Active → (Edited)* → Deleted`. No Archive state (removed per MVP simplification).
- **Ownership:** references its Board by ID; not contained by it.
- **Relationships:** generates Occurrence aggregates (by reference, not containment — see Domain Model §1.3's original reasoning, unaffected by this pass).

### Occurrence

- **Responsibilities:** one concrete, dated instance of a Reminder becoming due.
- **Invariants:** references exactly one Reminder by ID, immutably; its due-instant is computed **once**, at generation time, using the parent Reminder's `referenceTimezone` at that moment, and stored as an absolute instant — **never recomputed per viewer's local timezone**; resolution (Complete/Dismiss) is idempotent — the first valid resolving command wins, every subsequent one is a no-op that returns the existing resolution, never a duplicate or an error.
- **Lifecycle:** `Scheduled → Due → (Completed | Dismissed | Missed)`. `Undo` does not create a new Occurrence — it flips the same Occurrence's resolution state back and produces a new History entry recording the reversal (the prior entry is never erased).
- **Concurrency control:** requires optimistic concurrency (a version/compare-and-set check) on every resolving command — this is the mechanism, not just the policy, behind the "already done by X" outcome.

### Buzz (Notification)

- **Responsibilities:** one delivery, to one recipient, about one Occurrence.
- **Invariants:** always references an existing Occurrence (never an Occurrence that's already been Completed/Dismissed at generation time — see §4's cancellation policy) and an existing User; delivery is gated by a **re-check, at the moment of send**, that the recipient still holds an Active, non-muted Membership on the Reminder's Board and has not blocked/been blocked by anyone relevant — not just the state captured when the Buzz was scheduled.
- **Lifecycle:** `Scheduled → Generated → (Delivered | Failed → Retried ... → Exhausted) → (Seen | Dismissed)`. Cancellable at any point before `Delivered`.

### Invitation

- **Responsibilities:** an offer of Membership on one Board.
- **Invariants:** `issuer` must hold an Active Membership on the target Board at issuance time — **this is the entire invitation policy in V1**, no configurable enum (this is the resolution to Architecture Review Medium 2.1: the Domain Model's "Board Invitation Policy" field is removed — see §5); carries an expiring, single-use token; a blocked relationship between issuer and any resolved invitee identity invalidates it immediately.
- **Lifecycle:** `Created → Pending → (Accepted | Declined | Revoked | Expired)`.

### Block

- **Responsibilities:** a directional safety record between two Users.
- **Invariants:** unaffected by this pass — confirmed correct in the Architecture Review (§4 of that document). Kept deliberately separate from Board Membership removal.

*Entity, Guest-role Membership, Report/Moderation, and every AI/Draft-related aggregate are **not implemented in V1** — they remain accurately described in the Domain Model as future extensibility points, but no code should be written against them yet.*

---

## 2. Command Implementation Notes

| Command | Preconditions | Validation | Events Emitted | Failure Conditions | Side Effects |
|---|---|---|---|---|---|
| `RegisterAccount` | Email/phone not already registered | Valid email/phone format, password meets minimum policy | `AccountRegistered` | Duplicate account → rejected with a generic conflict, routes to Login | None beyond the event — provisioning waits for verification |
| `VerifyAccount` | A valid, unexpired verification code exists for this account | Code matches | `AccountVerified` | Wrong/expired code → rejected, account stays `PendingVerification` | Triggers the Account Provisioning policy (§4) — must complete before the account is usable |
| `Login` | Account is `Active` (not `Suspended`/`Deleted`) | Credentials match | `LoginSucceeded` / `LoginFailed` | `Suspended` → distinct rejection message; `Deactivated` → succeeds but routes to a "Reactivate?" prompt | None |
| `RequestAccountRecovery` | None (always accepted, to avoid confirming account existence) | — | `AccountRecoveryRequested` | Never fails visibly | Issues a token; a second request invalidates any prior outstanding token |
| `ConfirmAccountDeletion` | No **unresolved** sole-Owner situation remains — see next column | Valid re-authentication | `AccountDeleted` | If the account is the sole Owner of any shared Board, the command **does not block waiting for a separate action** — it triggers `ReassignOwnership` (§4) inline, as the first step of the same operation, then proceeds. There is no user-facing "resolve this first" dead end in V1. | Anonymizes authorship on shared Boards' History; purges the Personal Board's content; revokes sessions |
| `CreateBoard` | Requester is `Active` | Name non-empty | `BoardCreated`, `MembershipGranted` (self, Owner) | Duplicate double-submit → idempotency key required, second attempt is a no-op returning the first result | Creator becomes sole Owner atomically |
| `RenameBoard` | Requester holds `Owner` Membership | Name non-empty | `BoardRenamed` | Non-Owner attempt → rejected | None |
| `DeleteBoard` | Requester holds `Owner` Membership | Explicit confirmation token from the UI (naming the Board) | `BoardDeleted` | Non-Owner attempt → rejected | Soft-deletes immediately (user-facing: gone); schedules the async Purge policy (§4) |
| `LeaveBoard` | Requester holds an Active Membership | — | `MemberLeft`, and if the requester was the sole Owner with other Members remaining, also `BoardOwnershipReassigned` (§4) in the same operation, inline, no separate confirmation step | If requester is the *only* Member at all (not just sole Owner) → this is equivalent to Delete; the UI must offer Delete instead of a no-op Leave | Cancels any of this person's own pending Buzzes on this Board |
| `RemoveMember` | Requester holds `Owner` Membership; target is not the requester | — | `MemberRemoved` | Non-Owner attempt → rejected; attempting to remove oneself → rejected (use `LeaveBoard`) | Cancels the removed person's pending Buzzes on this Board; their authored content and History are untouched |
| `SendInvitation` | Requester holds an Active Membership on the target Board (**any Member, no policy check beyond this** — resolution of Architecture Review 2.1) | Target not currently blocked by/blocking the requester | `InvitationSent` | Blocked relationship → rejected with a generic, non-specific message | Issues an expiring, single-use token |
| `AcceptInvitation` | Invitation is `Pending` and unexpired; if the invitee has no account, account creation/verification must complete first (nested flow, not a precondition failure) | Invitee not blocked by the issuer | `InvitationAccepted`, `MembershipGranted` | Already-accepted → no-op, returns existing Membership, not an error; expired/revoked → rejected | None beyond Membership creation |
| `MuteBoard` / `UnmuteBoard` | Requester holds an Active Membership on the Board | — | `BoardMuted` / `BoardUnmuted` | None of note | Affects only this person's own future Buzz delivery — never anyone else's, never the Home/Board reminder list itself |
| `CreateReminder` | Requester holds an Active Membership on the target Board | Title non-empty; `recurrence` and `notifyPreset` are valid enum values | `ReminderCreated` | Double-submit → idempotency key required | Captures `referenceTimezone` from the device automatically; triggers first `GenerateNextOccurrence` |
| `UpdateReminder` | Requester holds an Active Membership on the Reminder's Board | Same field validation as Create; **`boardId` is not an accepted field on this command at all** — not merely rejected, structurally absent | `ReminderUpdated` (title/date change) and/or `RecurrenceRuleUpdated` and/or `NotifyPresetUpdated`, as applicable — see note below | Attempting to change Board → rejected outright (Board immutability, §1) | If recurrence changed: regenerates only not-yet-generated future Occurrences (past/current ones untouched). If `notifyPreset` changed: reschedules every not-yet-delivered Buzz for every not-yet-resolved Occurrence to the new preset immediately (resolution of Architecture Review Medium 2.4 — see §5 for why this differs from the recurrence-change rule) |
| `DeleteReminder` | Requester holds an Active Membership on the Reminder's Board (**any Member — resolution of Architecture Review Medium 2.3**, matching Edit's default) | Explicit confirmation naming the Reminder | `ReminderDeleted` | None beyond the permission check | Halts future Occurrence generation **and** cancels every pending, not-yet-delivered Buzz for every not-yet-resolved Occurrence of this Reminder, in the same operation (resolution of Architecture Review Critical 1.3 — see §5). History and past Occurrences are untouched. |
| `CompleteOccurrence` / `DismissOccurrence` | Requester holds an Active Membership on the Occurrence's Reminder's Board; Occurrence is in a resolvable state | Version/compare-and-set match | `OccurrenceCompleted` / `OccurrenceDismissed` | Already resolved by someone else → no-op, returns "already done by X," not an error | Cancels any still-pending Buzzes for this specific Occurrence |
| `UndoOccurrenceResolution` | Within the grace window (§5); requester holds an Active Membership | — | `OccurrenceUndone` | Outside grace window → rejected, direct edit not offered as a workaround | Produces a new History entry; does not erase the prior one |
| `BlockUser` / `UnblockUser` | — | Target is not self | `UserBlocked` / `UserUnblocked` | None of note | Blocking revokes any pending Invitations between the two parties; never touches existing Board Membership |

**On commands "doing too much":** `UpdateReminder` deliberately accepts multiple fields in one command rather than being split into `UpdateReminderTitle`/`UpdateRecurrenceRule`/`UpdateNotifyPreset`, because the product exposes exactly one "Save" action per edit (§ Design System, one primary action per screen) — splitting it would fragment a single user action into multiple round-trips for no behavioral benefit. Its side effects are conditional on which fields actually changed, which is the correct place for that branching to live, not a sign the command is overloaded.

**Missing commands identified and added above, not previously named explicitly:** `MuteBoard`/`UnmuteBoard` (existed as a described affordance but had no named command), and the system-internal `ReassignOwnership` (§4) replacing the deferred, future-only `OfferOwnershipTransfer`/`AcceptOwnershipTransfer` pair for V1's automatic-handoff behavior.

---

## 3. Event Implementation Notes

*Two events removed as redundant, one three-event chain collapsed to one, per the instruction to eliminate duplication:*

- **`OccurrenceDue` removed.** It was already flagged in the Event Storming document as "a useful internal signal... may not always need to be a durably-stored event." Confirmed: it adds nothing `BuzzScheduled`/`BuzzGenerated` don't already carry. The scheduler's "time reached" check is an internal trigger condition, not a fact worth persisting.
- **`BuzzDeliveryAttempted` removed.** `BuzzGenerated` followed by either `BuzzDelivered` or `BuzzDeliveryFailed` already carries every bit of information `BuzzDeliveryAttempted` would have added; retry counting is carried by `BuzzRetried` instead.
- **`BoardOwnershipTransferOffered`/`Accepted`/`Declined` collapsed to a single `BoardOwnershipReassigned` for V1.** The offer/accept dance is a *future* feature (explicit Transfer Ownership UI, deferred per MVP Scope); V1 only ever needs one system-triggered event recording that ownership moved automatically, with no counterpart offer/accept step to model yet.

| Event | Why it exists | Emitting Aggregate | Reacting Policies | Read Models Updated |
|---|---|---|---|---|
| `AccountRegistered` | New identity exists, unverified | User | Send-verification policy | None (not yet visible anywhere) |
| `AccountVerified` | Identity confirmed | User | Account Provisioning policy | None directly (provisioning's own events update read models) |
| `BoardCreated` | New shared space | Board | Search-index policy | Boards list |
| `BoardRenamed` | Name changed | Board | Search-index policy | Boards list, Board Detail header |
| `BoardDeleted` | Board is gone (soft) | Board | Purge policy (async, after grace window) | Boards list (removed immediately), Home (that Board's items removed) |
| `MembershipGranted` | A User now belongs to a Board | Board | — | Boards list, Board Members, Home (that Board's items now included) |
| `MemberRemoved` / `MemberLeft` | Access ended, by different means, both meaningful for History | Board | Cancel-pending-Buzzes policy | Boards list, Board Members, Home |
| `BoardOwnershipReassigned` | Ownership moved without an explicit offer/accept step (V1's only ownership-change event) | Board | — | Board Members (role badge) |
| `BoardMuted` / `BoardUnmuted` | One person's delivery preference for one Board | Board (on the Membership entity) | Buzz-delivery-time-recheck policy | None visible beyond the mute indicator on that person's own view |
| `InvitationSent` | An offer exists | Invitation | Delivery policy (email/SMS/link) | None |
| `InvitationAccepted` / `Declined` / `Revoked` / `Expired` | Terminal outcomes of the offer | Invitation | `MembershipGranted` is produced by Accepted; none by the others | Boards list, Board Members (on Accepted only) |
| `ReminderCreated` | New reminder definition | Reminder | Occurrence-generation policy | Home, Board Detail, Search |
| `ReminderUpdated` | Title/date changed | Reminder | Occurrence-regeneration policy (if recurrence changed) | Home, Board Detail, Search |
| `NotifyPresetUpdated` | Notification timing changed — kept **distinct** from `ReminderUpdated` because it triggers a different policy (reschedule pending Buzzes) than a title/date change does | Reminder | Buzz-reschedule policy | None visible beyond the Reminder's own detail view |
| `ReminderDeleted` | Reminder retired; History survives | Reminder | Halt-generation-and-cancel-pending-Buzzes policy | Home, Board Detail, Search (removed) |
| `OccurrenceGenerated` | A concrete due instance now exists | Occurrence | Buzz-scheduling policy | Home, Board Detail |
| `OccurrenceCompleted` / `Dismissed` / `Undone` / `Missed` | Resolution state changes, each independently meaningful for History | Occurrence | Cancel-pending-Buzzes (on Completed/Dismissed); History-append policy (all) | Home, Board Detail, Reminder History |
| `BuzzScheduled` | A future delivery is planned | Buzz | Dispatch policy | None (not user-visible until Generated) |
| `BuzzGenerated` | Ready to deliver | Buzz | Delivery policy | None |
| `BuzzDelivered` / `BuzzDeliveryFailed` → `BuzzRetried` → (`BuzzDelivered` \| `BuzzDeliveryExhausted`) | Delivery outcome, with bounded retry | Buzz | Retry policy; fallback-to-in-app policy on Exhausted | In-app Buzz fallback list (on Exhausted only) |
| `BuzzSeen` / `Dismissed` | Recipient acknowledged | Buzz | May trigger `CompleteOccurrence`/`DismissOccurrence` if acted on via a native action button | In-app Buzz fallback list |
| `UserBlocked` / `Unblocked` | Safety state changed | Block | Revoke-pending-Invitations policy (on Blocked) | Search, Invitation eligibility |

*Every AI/Draft event (`ReminderDraftProposed`/`Confirmed`/`Discarded`, `TextParsed`, etc.) and every V2 Imports event are intentionally excluded from this V1 catalogue — they are correctly specified elsewhere but are not in scope for the team about to start coding.*

---

## 4. Policy Implementation Notes

| Policy | Trigger | Behavior | Idempotency | Failure Handling | Retry |
|---|---|---|---|---|---|
| **Account Provisioning** | `AccountVerified` | Creates the Personal Board, sets `User.personalBoardId`, initializes minimal privacy/notification defaults — as one atomic unit | Re-running against an already-provisioned account is a no-op (checked via `personalBoardId` already being set) | Must not leave an account half-provisioned; retried automatically until complete, account unusable until it is | Retry until success; alert if not resolved within a short bounded time |
| **Sole-Owner Reassignment** (`ReassignOwnership`) | `LeaveBoard` or `ConfirmAccountDeletion` where the actor is the sole Owner and other Active Members exist | Selects the longest-standing other Active Member, grants them `Owner`, demotes the departing actor, all in one transaction | Idempotent per Board — re-running against a Board that already has a different Owner is a no-op | If no other Active Member exists, this policy does not run — the Board is deleted instead, not left ownerless | Not applicable — synchronous, transactional |
| **Occurrence Generation** | Reminder is `Active` (continuous, rolling horizon) | Computes the next Occurrence's due-instant using the Reminder's stored `referenceTimezone`, materializing a few cycles ahead, never the full future | Idempotent per (`reminderId`, resolved due-date) — safe to re-run without duplicating | A generation failure must not silently stop future generation — monitored, retried | Retried until successful; alerted if generation falls behind a defined lag threshold |
| **Buzz Scheduling & Delivery** | `OccurrenceGenerated`, then the scheduled lead-time or due-instant arrives | Generates one Buzz per current Active, non-muted, non-blocked Member; **re-checks Membership/Mute/Block at the moment of send, not at scheduling** | Idempotent per (`occurrenceId`, `recipientId`) | Bounded retry (see §6 for the open parameter), then falls back to guaranteed in-app visibility — never silently dropped | Bounded retry with backoff, then fallback |
| **Delete-Cascade Cancellation** | `ReminderDeleted` | Halts future generation **and**, in the same operation, cancels every not-yet-delivered Buzz for every not-yet-resolved Occurrence of this Reminder | Idempotent — cancelling an already-cancelled or already-delivered Buzz is a no-op | None of note — this is the direct fix for Architecture Review Critical 1.3 | Not applicable — synchronous |
| **Notify-Preset Reschedule** | `NotifyPresetUpdated` | Immediately recomputes and reschedules every not-yet-delivered Buzz for every not-yet-resolved Occurrence of this Reminder to the new preset | Idempotent — recomputing against an unchanged preset is a no-op | None of note | Not applicable — synchronous |
| **Missed Transition** | A `Due` Occurrence passes its grace window with no action | Transitions it to `Missed`, generates a quiet History entry | Idempotent — an already-Missed Occurrence is skipped | Sweep frequency must keep "missed" feeling timely (see §5 for the resolved grace window and sweep interval) | Retried on next sweep if the job itself fails |
| **Invitation Expiry** | Token TTL elapses | Marks the Invitation `Expired` | Idempotent | None of note | Retried on next sweep |
| **Block Revokes Pending Invitations** | `UserBlocked` | Revokes any Invitation currently Pending between the two parties | Idempotent | None of note | Not applicable — synchronous |
| **Board/Account Purge** | `BoardDeleted` / `AccountDeleted`, after the grace window | Performs the actual cascade delete of Reminders/Occurrences/History rows, chunked, resumable | Idempotent — re-running against already-purged data is a no-op | Must be chunked, never a single giant transaction, for large Boards | Retried, resumable from last completed chunk |
| **Search/History Projection** | Broad subscription across most events | Maintains eventually-consistent read models | Must de-duplicate by event ID under at-least-once delivery | Lag here is acceptable and should not be over-engineered toward synchronous (confirmed in the Event Storming document, unaffected by this pass) | Standard at-least-once consumer retry |

---

## 5. Invariant Catalogue

*Every invariant in the system, consolidated. Items marked **NEW** close a gap the Architecture Review found; items marked **TIGHTENED** restate an existing rule with the ambiguity removed.*

1. Every Reminder belongs to exactly one Board.
2. **NEW / TIGHTENED:** A Reminder's Board reference is set at creation and is immutable for its entire lifetime — there is no "move to another Board" operation.
3. Every Board has exactly one Active Membership with role `Owner`, at all times, enforced transactionally — never zero, never more than one.
4. **TIGHTENED:** The system must never allow the last Owner's Membership to transition to `Removed`/`Left` without, in the same transaction, either reassigning ownership to another Active Member or deleting the Board entirely if none remain.
5. **NEW:** A User holds at most one Active Membership per Board at any time.
6. **NEW:** A Membership, once `Removed` or `Left`, is never reactivated — a later Invitation acceptance creates a new Membership record.
7. Every Membership belongs to exactly one Board.
8. A Buzz must always reference an existing Occurrence.
9. **NEW:** A Buzz is never generated for an Occurrence that has already been resolved (Completed/Dismissed/Undone-then-re-resolved) at generation time, and delivery re-checks the recipient's current Membership/Mute/Block state at send time, not just at scheduling time.
10. **NEW:** Every User has exactly one `personalBoardId`, set exactly once at account provisioning, never changed afterward.
11. **NEW:** A Reminder's `referenceTimezone` is captured once, automatically, at creation, and never recalculated against a viewer's local timezone.
12. **NEW:** Deleting a Reminder cancels every pending (not-yet-delivered) Buzz for every not-yet-resolved Occurrence of that Reminder, in the same operation that halts future generation.
13. An Occurrence's due-instant, once computed, is immutable; only not-yet-generated future Occurrences are affected by a later Recurrence Rule change.
14. Occurrence resolution (Complete/Dismiss) is idempotent — the first valid command wins; every subsequent one returns the existing outcome, never a duplicate or an error.
15. A deleted or deleted-Board's Reminder never deletes its History or past Occurrences.
16. A blocked relationship prevents Invitations in both directions and auto-revokes any pending one between the two parties, but never automatically removes existing shared Board Membership — that remains a separate, Owner-only action.
17. Recurrence is always exactly one of a fixed five values; Notification timing is always exactly one of a fixed six values — no open-ended rule or custom timing exists anywhere in V1.
18. Membership role is always exactly one of `Owner`/`Member` in V1 — no other value is implementable yet.

---

## 6. Open Implementation Questions

*Kept intentionally short — every gap the Architecture Review raised has a resolved answer above. What remains is genuinely a tuning parameter, not a behavioral ambiguity:*

1. **Buzz delivery retry count/backoff curve** (e.g., "3 attempts over 15 minutes" vs. some other cadence) is an operational tuning parameter best set from real provider behavior during integration testing, not fixed here in advance.
2. **Board/Account deletion grace period before hard purge:** resolved to **14 days**, matching the figure already proposed in the Business Behavior Model — stated here as final rather than left open, since there was no reason to leave an already-reasonable number undecided.
3. **Missed-transition grace window and sweep interval:** resolved to a **24-hour** grace window after the due-instant, checked by an **hourly** sweep — concrete defaults, tunable later if real usage suggests otherwise, but not blocking implementation now.

---

*An engineer should be able to build any V1 command, event, or policy directly from this document without asking a product question first. Where this document's behavioral detail differs from an earlier document, this one wins for implementation purposes — the earlier documents remain valuable for the reasoning behind each decision, not for the exact mechanics of it.*
