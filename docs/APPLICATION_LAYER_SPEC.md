# BuzzMe — Application Layer Specification

*Answers one question: when the UI requests something, exactly what happens? This sits directly on top of [IMPLEMENTATION_SPEC.md](./IMPLEMENTATION_SPEC.md) — it does not redefine any aggregate, invariant, or command precondition already established there. It adds the layer those commands are orchestrated from: authorization, transaction sequencing, and the separation between domain changes and external side effects.*

*Per instruction, genuine gaps this finer-grained pass surfaced are reported explicitly in §0, not silently patched into the domain layer's documents.*

---

## 0. Gaps Found During This Pass

Three real gaps, closed here at the application layer rather than left unspecified — flagged rather than silently absorbed:

1. **`LeaveBoard`'s precondition in [IMPLEMENTATION_SPEC.md](./IMPLEMENTATION_SPEC.md) never explicitly excluded the Personal Board**, even though the Domain Model states the Personal Board is "never leavable." In practice the Information Architecture already prevents this by never listing the Personal Board anywhere reachable — but the *application service* had no explicit guard of its own, meaning a direct call to `LeaveBoard` (bypassing the UI) was not actually blocked by anything. Closed in §3.2 below with an explicit business validation rule.
2. **Neither `CreateReminder` nor `UpdateReminder` explicitly checked that the target Board is not Deleted**, only that the requester holds an Active Membership. During the 14-day soft-delete grace window (before the Purge background process runs), Membership rows are not necessarily invalidated yet, so this check does not fall out "for free" — it needs to be explicit. Closed in §3.6/§3.7.
3. **No rule stated whether an Occurrence remains completable after its parent Reminder is Deleted.** [IMPLEMENTATION_SPEC.md](./IMPLEMENTATION_SPEC.md) specified that deleting a Reminder cancels *pending* Buzzes, but not whether an already-`Due` Occurrence can still be Completed/Dismissed/Undone afterward. Resolved here: it cannot — once the parent Reminder is Deleted, all of its Occurrences become read-only historical facts immediately. Closed in §3.8.

No other inconsistencies were found between this pass and the Product Vision, Domain Model, Business Behavior Model, Event Storming, Architecture Review, or Implementation Specification.

---

## 1. Application Layer Principles

1. **One use case, one intent.** Every application service corresponds to exactly one thing the UI asked for — never a bundle of unrelated intents behind one call.
2. **Application services orchestrate; they do not contain business rules.** Invariants live in the aggregates (Implementation Spec §1/§5). This layer's job is authorization, sequencing, and translating a UI request into the right domain command(s) — never re-deciding what the domain already decided.
3. **Authorization is checked here, first, before any domain interaction.** A rejected authorization check never reaches the aggregate.
4. **Business validation is checked here or by the aggregate — never in the UI.** Format/shape validation (a title isn't empty, an email looks like an email) is a UI-layer and command-input concern, not listed in this document (see §4's scope note).
5. **Cross-aggregate consistency is achieved by sequencing separate, retried transactions — never a distributed transaction.** Where a use case touches more than one aggregate root, this document states the order and which step is compensated or retried, not a hypothetical two-phase commit.
6. **Every use case separates its domain effects from its external side effects.** A push notification, an email, or an analytics event is never bundled into the same transaction as a domain state change (§8).
7. **Idempotency is a first-class part of every use case's contract, not an afterthought.** Every use case below states what happens on a retried or duplicate call.

---

## 2. Application Service Catalogue

| Area | Use Cases |
|---|---|
| **Account** | Register, VerifyAccount, Login, ForgotPassword (RequestAccountRecovery), ResetPassword (CompleteAccountRecovery), UpdateProfile, DeleteAccount |
| **Board** | CreateBoard, RenameBoard, DeleteBoard, LeaveBoard, InviteMember, AcceptInvitation, DeclineInvitation, RemoveMember, MuteBoard, UnmuteBoard |
| **Membership** | *(JoinBoard has no separate service — see §3.5)*, BlockUser, UnblockUser |
| **Reminder** | CreateReminder, UpdateReminder, DeleteReminder, CompleteReminder, DismissReminder, ReopenReminder |
| **Internal / System-invoked** | GenerateRecurrences, GenerateBuzzes, CancelBuzzes (reused by several use cases below, never called directly by the UI) |
| **Background Processes** | Generate Reminder Occurrences, Generate Buzzes, Dispatch Push Notifications, Retry Failed Notifications, Expire Invitations, Transition Missed Reminders, Clean Up Deleted Boards/Accounts |

---

## 3. Use Case Specifications

### 3.1 CreateBoard

| Field | Detail |
|---|---|
| Purpose | Establish a new shared space. |
| Inputs | Requesting User ID, Board name. |
| Validation (business) | None beyond the name existing — see §4 for the full separation of business vs. format validation. |
| Authorization | Authenticated User. |
| Domain interaction | Creates the Board aggregate with the requester as its sole Owner Membership. |
| Events emitted | `BoardCreated`, `MembershipGranted`. |
| Side effects | Domain: none beyond the above. External: none. |
| Result returned | The new Board's identity and the requester's Owner Membership. |
| Failure conditions | None beyond generic infrastructure failure. |
| Idempotency | Requires a client-supplied idempotency key; a retried call with the same key returns the original result, never a second Board. |

### 3.2 LeaveBoard

| Field | Detail |
|---|---|
| Purpose | End a person's own Membership on a Board. |
| Inputs | Requesting User ID, Board ID. |
| Validation (business) | **Cannot leave your Personal Board** (gap closed in §0 — the Board being targeted must not equal `User.personalBoardId`). If the requester is the sole Member of the Board entirely (not just sole Owner), Leave is rejected in favor of Delete Board (a Board is never left "empty from the inside" — it's deleted). |
| Authorization | Board Member. |
| Domain interaction | If requester is sole Owner with other Active Members remaining: `ReassignOwnership` runs first, in the same Board-aggregate transaction (Membership lives inside Board — this is a single-aggregate operation, not a saga). Then the requester's Membership transitions to `Left`. |
| Events emitted | `MemberLeft`, and `BoardOwnershipReassigned` if applicable — both from the same transaction. |
| Side effects | Domain: none further. External: none directly (Buzz cancellation is a separate, policy-driven step — see §7). |
| Result returned | Confirmation; if ownership was reassigned, the new Owner's identity. |
| Failure conditions | Personal Board target → rejected. Sole Member (not just sole Owner) → rejected, redirected to Delete Board. |
| Idempotency | A second Leave call against an already-`Left` Membership is a no-op, not an error. |

### 3.3 DeleteBoard

| Field | Detail |
|---|---|
| Purpose | Permanently remove a Board for everyone. |
| Inputs | Requesting User ID, Board ID, explicit confirmation (naming the Board). |
| Validation (business) | **Cannot delete a Board you don't own.** |
| Authorization | Board Owner. |
| Domain interaction | Board transitions to `Deleted` (soft) in a single-aggregate transaction. |
| Events emitted | `BoardDeleted`. |
| Side effects | Domain: none synchronous beyond the state change. External: none synchronous — the Purge background process (§6) runs after the grace window. |
| Result returned | Confirmation. |
| Failure conditions | Non-Owner → rejected. |
| Idempotency | A second Delete call against an already-`Deleted` Board is a no-op. |

### 3.4 RenameBoard / MuteBoard / UnmuteBoard

| Field | Detail |
|---|---|
| Purpose | Rename: change the Board's name. Mute/Unmute: change one person's own delivery preference for this Board. |
| Inputs | Rename: Requesting User ID, Board ID, new name. Mute/Unmute: Requesting User ID, Board ID. |
| Validation (business) | None beyond authorization. |
| Authorization | Rename: Board Owner. Mute/Unmute: Board Member (acting only on their own Membership — never another person's). |
| Domain interaction | Single-aggregate transaction (Board), updating the name field or the requester's own Membership's `muted` flag. |
| Events emitted | `BoardRenamed` / `BoardMuted` / `BoardUnmuted`. |
| Side effects | Domain: none further. External: none. |
| Result returned | Confirmation. |
| Failure conditions | Rename by a non-Owner → rejected. |
| Idempotency | Setting an already-current name or an already-current mute state is a no-op, not an error. |

### 3.5 InviteMember / AcceptInvitation / DeclineInvitation

| Field | Detail |
|---|---|
| Purpose | Offer, accept, or decline Membership on a Board. |
| Inputs | InviteMember: Requesting User ID, Board ID, target contact or link/QR request. Accept/Decline: Invitation token, acting User ID (may be a brand-new account — see nested flow below). |
| Validation (business) | **Cannot invite blocked users** — checked both directions (requester blocked by target, or target blocked by requester). |
| Authorization | InviteMember: Board Member (any Active Member — no configurable policy, per Implementation Spec §5's resolution). Accept/Decline: Authenticated User (the invitation's resolved invitee). |
| Domain interaction | InviteMember: single-aggregate transaction (Invitation), which only *reads* Board Membership to check the requester is Active — it does not write to Board. AcceptInvitation: Invitation transitions to `Accepted` in its own transaction; **`MembershipGranted` on the Board is a separate, second transaction**, since Invitation and Board are different aggregate roots — see §7 for why this is an eventually-consistent step, not one atomic operation. DeclineInvitation: single-aggregate transaction (Invitation only). |
| Events emitted | `InvitationSent` / `InvitationAccepted` (+ `MembershipGranted`, second step) / `InvitationDeclined`. |
| Side effects | Domain: as above. External: Invitation delivery (email/SMS/link) — see §8. |
| Result returned | InviteMember: the Invitation's shareable token/link. Accept: the new Membership. Decline: confirmation. |
| Failure conditions | Blocked relationship → rejected at InviteMember. Expired/revoked/already-resolved Invitation → rejected at Accept/Decline with a clear reason. |
| Idempotency | Accepting an already-Accepted Invitation is a no-op returning the existing Membership, never a duplicate. |
| Reconciliation note | **"Join Board" has no separate application service.** It is the direct effect of `AcceptInvitation` — there is no self-service, invitation-free join path in V1, so specifying a distinct "JoinBoard" use case would describe a path that doesn't exist. |

### 3.6 RemoveMember

| Field | Detail |
|---|---|
| Purpose | End someone else's Membership on a Board. |
| Inputs | Requesting User ID, Board ID, target User ID. |
| Validation (business) | **Cannot remove yourself** (use Leave instead). |
| Authorization | Board Owner. |
| Domain interaction | Single-aggregate transaction (Board), transitioning the target's Membership to `Removed`. |
| Events emitted | `MemberRemoved`. |
| Side effects | Domain: none further in this transaction. External: none. Buzz cancellation for the removed person is a separate, policy-driven step (§7), not part of this transaction. |
| Result returned | Confirmation. |
| Failure conditions | Non-Owner → rejected. Target is requester → rejected. Target already Removed/Left → no-op. |
| Idempotency | Removing an already-Removed Membership is a no-op. |

### 3.7 CreateReminder / UpdateReminder

| Field | Detail |
|---|---|
| Purpose | Create or edit a Reminder's definition. |
| Inputs | Create: Requesting User ID, Board ID, title, recurrence, notify preset. Update: Requesting User ID, Reminder ID, any of title/date-recurrence/notify preset (never Board — structurally absent as a field, per Implementation Spec §2). |
| Validation (business) | **Cannot create (or edit) reminders in a Deleted Board** (gap closed in §0 — checked explicitly, not assumed from Membership status alone, since Membership rows may still read as Active during the soft-delete grace window). |
| Authorization | Board Member (any Active Member — matches Edit's shared-responsibility default). |
| Domain interaction | Single-aggregate transaction (Reminder). Update may additionally flag whether recurrence or notify-preset changed, determining which follow-on policy fires (see §7). |
| Events emitted | `ReminderCreated` / `ReminderUpdated` and/or `RecurrenceRuleUpdated` and/or `NotifyPresetUpdated`, as applicable. |
| Side effects | Domain: none further in this transaction. External: none. Occurrence (re)generation and Buzz rescheduling are separate, policy-driven steps (§7). |
| Result returned | The Reminder's current state. |
| Failure conditions | Target Board Deleted → rejected. Non-Member → rejected. Attempted Board change on Update → rejected outright (not a validation error — the field doesn't exist on this command). |
| Idempotency | Create requires a client idempotency key. Update re-applying identical field values is a no-op. |

### 3.8 CompleteReminder / DismissReminder / ReopenReminder

*Reopen Reminder is supported, and maps exactly to `UndoOccurrenceResolution` at the domain layer (Implementation Spec §1) — there is no reminder-level "undelete"; only an Occurrence's resolution can be reopened, within its grace window.*

| Field | Detail |
|---|---|
| Purpose | Mark an Occurrence resolved (Complete/Dismiss), or reverse that resolution (Reopen) within the grace window. |
| Inputs | Requesting User ID, Occurrence ID, an expected version (for optimistic concurrency). |
| Validation (business) | **Cannot complete/dismiss/reopen an Occurrence whose parent Reminder has been Deleted** (gap closed in §0 — once a Reminder is Deleted, all of its Occurrences become read-only immediately, regardless of their own state at that moment). Reopen additionally requires the grace window not to have elapsed. |
| Authorization | Board Member. |
| Domain interaction | Single-aggregate transaction (Occurrence), with a version check — the first valid call wins. |
| Events emitted | `OccurrenceCompleted` / `OccurrenceDismissed` / `OccurrenceUndone`. |
| Side effects | Domain: none further in this transaction. External: none. Cancelling any still-pending Buzz for this specific Occurrence is a separate, policy-driven step (§7). |
| Result returned | The Occurrence's new state, including who resolved it (for the shared "✓ Done — Name" credit). |
| Failure conditions | Parent Reminder Deleted → rejected. Version mismatch (someone else already resolved it) → returns the existing resolution as a no-op, not an error. Reopen past the grace window → rejected. |
| Idempotency | A second Complete/Dismiss call after the first succeeded is a no-op returning the existing outcome — this is the mechanism behind "already done by X." |

### 3.9 BlockUser / UnblockUser

| Field | Detail |
|---|---|
| Purpose | Set or clear a directional safety restriction between two people. |
| Inputs | Requesting User ID, target User ID. |
| Validation (business) | Target cannot be the requester. |
| Authorization | Authenticated User. |
| Domain interaction | Single-aggregate transaction (Block). |
| Events emitted | `UserBlocked` / `UserUnblocked`. |
| Side effects | Domain: none further in this transaction. External: none. Revoking pending Invitations between the two parties is a separate, policy-driven step (§7) — never touches existing Board Membership. |
| Result returned | Confirmation. |
| Failure conditions | Target is self → rejected. |
| Idempotency | Blocking an already-blocked person, or unblocking an already-unblocked one, is a no-op. |

### 3.10 Account Use Cases

| Use Case | Purpose | Authorization | Key Domain Interaction | Events | Idempotency |
|---|---|---|---|---|---|
| **Register** | Create a new, unverified account | Unauthenticated | Single-aggregate transaction (User) | `AccountRegistered` | Duplicate email/phone → rejected, not a second account |
| **VerifyAccount** | Confirm ownership of email/phone | Unauthenticated (holds a valid code) | User transitions to `Active`; triggers Account Provisioning (multi-step, see §7) | `AccountVerified` | Re-verifying an already-Active account is a no-op |
| **Login** | Establish a session | Unauthenticated (holds credentials) | Read-only check against User | `LoginSucceeded`/`LoginFailed` | Not applicable — each attempt is independent |
| **ForgotPassword** (`RequestAccountRecovery`) | Begin password recovery | Unauthenticated | Issues a token; never confirms account existence either way | `AccountRecoveryRequested` | A second request invalidates any prior outstanding token, doesn't stack |
| **ResetPassword** (`CompleteAccountRecovery`) | Complete recovery with a new password | Unauthenticated (holds a valid token) | Single-aggregate transaction (User) | `AccountRecovered` | Reusing an already-consumed token → rejected |
| **UpdateProfile** | Change name/photo/email/phone | Authenticated User (self only) | Single-aggregate transaction (User); email/phone change requires re-verification, following the same nested flow as Register/Verify | `ProfileUpdated` | Re-applying identical values is a no-op |
| **DeleteAccount** | Permanently remove an account | Authenticated User (self only) | Multi-aggregate orchestrated workflow — see §7 | `AccountDeletionRequested` → (per-Board `ReassignOwnership` as needed) → `AccountDeleted` | Re-confirming an already-Deleted account is a no-op |

*`UpdateProfile` was not previously specified as a distinct command in the Implementation Specification's V1 command table — it existed only as an event (`ProfileUpdated`) with no defined use case around it. Filled in here as a normal, consistent addition, not a design change.*

---

## 4. Validation Rules (Business Only)

*Format/shape checks — a title isn't empty, an email looks like an email, a name is under some character limit — are UI-layer and command-input concerns and are deliberately excluded here. Everything below is a rule about the state of the business, not the shape of a field.*

| Rule | Enforced In |
|---|---|
| Cannot leave your Personal Board | `LeaveBoard` (§3.2 — gap closed) |
| A Board is never left "from the inside" by its last remaining Member — Delete replaces Leave in that case | `LeaveBoard` |
| Cannot remove yourself from a Board | `RemoveMember` |
| Cannot delete a Board you don't own | `DeleteBoard` |
| Cannot invite a user you've blocked, or who has blocked you | `InviteMember` |
| Cannot create or edit a Reminder in a Deleted Board | `CreateReminder`/`UpdateReminder` (§3.7 — gap closed) |
| Cannot change a Reminder's Board after creation | `UpdateReminder` (field doesn't exist on the command) |
| Cannot complete, dismiss, or reopen an Occurrence whose parent Reminder has been Deleted | `CompleteReminder`/`DismissReminder`/`ReopenReminder` (§3.8 — gap closed) |
| Cannot reopen an Occurrence past its grace window | `ReopenReminder` |
| A Board must retain exactly one Active Owner at all times — enforced by requiring reassignment or dissolution in the same operation as any Owner-ending action | `LeaveBoard`, `DeleteAccount` |
| A blocked relationship never automatically ends existing Board Membership — that remains a separate, Owner-only `RemoveMember` action | `BlockUser` |

---

## 5. Authorization Rules

*Exactly four levels exist. No additional roles are introduced.*

| Use Case | Authorization |
|---|---|
| Register, Login, ForgotPassword, ResetPassword | Unauthenticated (the person proves identity as part of the use case itself) |
| VerifyAccount | Unauthenticated, holding a valid code |
| CreateBoard, UpdateProfile, DeleteAccount, BlockUser, UnblockUser | Authenticated User |
| LeaveBoard, InviteMember, MuteBoard, UnmuteBoard, CreateReminder, UpdateReminder, DeleteReminder, CompleteReminder, DismissReminder, ReopenReminder | Board Member |
| RenameBoard, DeleteBoard, RemoveMember | Board Owner |
| AcceptInvitation, DeclineInvitation | Authenticated User (specifically, the Invitation's resolved invitee) |
| GenerateRecurrences, GenerateBuzzes, CancelBuzzes, all Background Processes | System Process |

---

## 6. Background Processes

| Process | Trigger | Frequency | Inputs | Outputs | Failure Behavior | Retry | Idempotency |
|---|---|---|---|---|---|---|---|
| **Generate Reminder Occurrences** | A Reminder is `Active` | Continuous, rolling horizon (materializes a few cycles ahead, never the full future) | Active Reminders and their Recurrence Rules | `OccurrenceGenerated` events | Must not silently stall — monitored for generation lag | Retried until successful | Per (`reminderId`, resolved due-date) — safe to re-run |
| **Generate Buzzes** | `OccurrenceGenerated`, or a lead-time/due-instant is reached | Continuous, event/time-driven | Occurrences, current Board Membership | `BuzzScheduled`/`BuzzGenerated` | Must not silently stall | Retried until successful | Per (`occurrenceId`, `recipientId`) |
| **Dispatch Push Notifications** | `BuzzGenerated` | Continuous | Generated Buzzes | `BuzzDelivered`/`BuzzDeliveryFailed` | Provider outage handled by the retry process below, never silently dropped | See Retry Failed Notifications | Per Buzz ID |
| **Retry Failed Notifications** | `BuzzDeliveryFailed` | Bounded retry with backoff (exact cadence is an open operational parameter, per Implementation Spec §6) | Failed Buzzes | `BuzzRetried` → `BuzzDelivered` or `BuzzDeliveryExhausted` | On exhaustion, falls back to guaranteed in-app visibility | Bounded, then stops | Per Buzz ID, per attempt |
| **Expire Invitations** | Token TTL elapses | Periodic sweep | Pending Invitations | `InvitationExpired` | None of note | Retried on next sweep if the job itself fails | Per Invitation — already-expired ones are skipped |
| **Transition Missed Reminders** | A `Due` Occurrence passes its grace window (24 hours, per Implementation Spec §6) with no action | Hourly sweep | Due, unresolved Occurrences | `OccurrenceMissed` | None of note | Retried on next sweep | Already-Missed Occurrences are skipped |
| **Clean Up Deleted Boards/Accounts** (Purge) | `BoardDeleted`/`AccountDeleted`, after the 14-day grace window | Periodic sweep, chunked | Soft-deleted Boards/Accounts past their grace window | Actual cascade delete of Reminders/Occurrences/History | Must be chunked — never one giant transaction for a large Board | Resumable from last completed chunk | Re-running against already-purged data is a no-op |

---

## 7. Transaction Boundaries

| Use Case | Boundary Type | Detail |
|---|---|---|
| CreateBoard, RenameBoard, DeleteBoard, LeaveBoard (incl. same-transaction reassignment), MuteBoard, UnmuteBoard, RemoveMember | **Single aggregate transaction** | Membership lives inside the Board aggregate, so even Owner reassignment during Leave is one atomic Board-aggregate transaction, not a saga. |
| CreateReminder, UpdateReminder, DeleteReminder, CompleteReminder, DismissReminder, ReopenReminder | **Single aggregate transaction** | Each acts on exactly one Reminder or one Occurrence aggregate. |
| BlockUser, UnblockUser | **Single aggregate transaction** | Acts only on the Block aggregate itself. |
| InviteMember, DeclineInvitation | **Single aggregate transaction** | Acts only on Invitation; reads (does not write) Board Membership for the authorization check. |
| **AcceptInvitation** | **Eventually consistent, two-step workflow** | Step 1 (own transaction): Invitation → `Accepted`. Step 2 (own transaction): `MembershipGranted` on Board. Step 2 is retried until it succeeds — Invitation and Board are different aggregate roots, so this is not one atomic operation, and no distributed transaction is used. |
| **DeleteReminder → cancel pending Buzzes** | **Eventually consistent workflow, policy-driven** | Reminder's own transaction commits `ReminderDeleted` first; a policy then cancels pending Buzzes (a different aggregate root) as a retried-to-completion follow-on step, not bundled into the same transaction. |
| **RemoveMember/LeaveBoard → cancel pending Buzzes** | **Eventually consistent workflow, policy-driven** | Same pattern as above — Board's transaction commits first; Buzz cancellation follows as a separate, retried step. |
| **CompleteReminder/DismissReminder → cancel the Occurrence's own pending Buzz** | **Eventually consistent workflow, policy-driven** | Same pattern — the Occurrence's own transaction commits first. |
| **UserBlocked → revoke pending Invitations** | **Eventually consistent workflow, policy-driven** | Block's transaction commits first; Invitation revocation follows as a separate step. |
| **VerifyAccount → Account Provisioning** | **Multi-step orchestrated workflow, retried to completion** | Creating the Personal Board, setting `personalBoardId`, and initializing defaults must all complete before the account is usable — orchestrated as a sequence of small transactions retried until every step succeeds, not one transaction spanning multiple aggregates. |
| **DeleteAccount** | **Multi-aggregate orchestrated workflow** | For each Board where the requester is sole Owner: run `ReassignOwnership` (its own Board-aggregate transaction) — repeated per affected Board. Then mark the User `Deleted` (its own transaction). Then the async Purge process runs after the grace window. No single transaction spans the User and all their Boards; the orchestration is sequential, and any step that fails is retried before the next one proceeds. |
| **CreateReminder → first Occurrence generation** | **Eventually consistent workflow, policy-driven** | `ReminderCreated` commits first; Occurrence generation is a separate, immediately-following step (fast enough to feel instant, but not the same transaction), consistent with Occurrence being its own aggregate root. |

**Compensating actions:** none of the above require a true compensating (rollback-the-first-step) action, because every multi-step workflow above is designed as *forward-only, retry-to-completion* — a failed second step is retried until it succeeds rather than undoing the first step. This is a deliberate simplicity choice consistent with the rest of the architecture: the only place a "failure" is genuinely terminal rather than retried is a rejected authorization or business-validation check, which happens *before* any transaction begins.

---

## 8. External Side Effects

*Every domain change is listed once, above. This section lists only what happens outside the domain model as a consequence.*

| Use Case / Process | Domain Change | External Side Effect |
|---|---|---|
| Register | `AccountRegistered` | Send a verification code (email/SMS) |
| InviteMember | `InvitationSent` | Deliver the invitation (email/SMS/link generation) |
| ForgotPassword | `AccountRecoveryRequested` | Send a recovery token (email/SMS) |
| Dispatch Push Notifications | `BuzzDelivered`/`Failed` | Call the push provider (APNs/FCM) or SMS/email channel |
| Every state-changing use case | Its own event(s) | **Audit/History:** an append-only History entry recording actor, action, and timestamp (Implementation Spec §4's Search/History Projection policy) |
| Every state-changing use case | Its own event(s) | **Analytics:** none specified in this product — BuzzMe does not track engagement metrics as part of its core behavior; if analytics are added later, they must remain read-only observers of events, never a dependency any use case's success relies on |

No use case's success or failure depends on an external side effect completing — a push-provider outage, for instance, never blocks or fails the underlying domain transaction; it only affects delivery, which has its own retry and fallback path (§6).

---

## 9. Development Guidelines

- Build each use case above as its own application service method — resist the urge to generalize several of them behind one generic "update" handler; each has a distinct authorization and validation shape.
- Authorization checks always run first, before touching any aggregate — a rejected check should never produce a partial domain read, let alone a write.
- Every multi-aggregate workflow in §7 is forward-only and retried-to-completion — do not implement a two-phase-commit or a rollback/undo mechanism for any of them; none are specified to need one.
- Idempotency keys and version checks (§3, §5 of Implementation Spec) are not optional hardening — they are part of each use case's correctness contract from day one.
- External side effects (§8) must never be awaited synchronously inside the same transaction as a domain state change — dispatch them after commit, and let their own retry/fallback mechanisms (§6) handle failure.
- If a future use case doesn't fit cleanly into "Authenticated User / Board Member / Board Owner / System Process," that's a signal to revisit the request against the Domain Model, not to invent a fifth authorization level here.

---

*This document, together with [IMPLEMENTATION_SPEC.md](./IMPLEMENTATION_SPEC.md), is sufficient to begin building BuzzMe's application services. Every use case, background process, and transaction boundary above is stated precisely enough that no further behavioral decision should be needed during implementation — where one was needed to get here, it's recorded in §0, not left implicit.*
