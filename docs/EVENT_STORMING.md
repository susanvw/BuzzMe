# BuzzMe — Event Storming Blueprint

*Builds on the finalized [PRODUCT_VISION.md](./PRODUCT_VISION.md), [DOMAIN_MODEL.md](./DOMAIN_MODEL.md), [BUSINESS_BEHAVIOR_MODEL.md](./BUSINESS_BEHAVIOR_MODEL.md), and [INFORMATION_ARCHITECTURE.md](./INFORMATION_ARCHITECTURE.md). None of those are redesigned here. This document re-views the same business through a single lens — the flow of events through time — to produce the behavioral blueprint for an event-driven backend: what triggers what, what reacts automatically, where consistency is eventual, and where the real distributed-systems risk lives.*

*No APIs. No databases. No code. Commands and events only, plus the actors, policies, read models, sagas, and external systems that connect them.*

---

## 0. Notation

| Symbol | Concept | Meaning |
|---|---|---|
| 🟧 | **Domain Event** | A fact that already happened, named in the past tense. The unit of truth everything else reacts to. |
| 🟦 | **Command** | An intent to do something, named as an imperative. May be accepted (produces one or more Events) or rejected. |
| 🟨 | **Actor** | Who or what issues a Command — a person in a role (Owner, Member, Guest, prospective User), or the System acting on a timer/policy. |
| 🟪 | **Read Model** | A query-optimized view built from Events, consumed by the product surfaces defined in the Information Architecture. Never written to directly. |
| 🟩 | **Policy** | The automatic glue: *"Whenever [Event] happens, issue [Command]."* This is where reactive, asynchronous behavior lives. |
| ⚫ | **External System** | A third-party dependency BuzzMe does not own or control. |
| 🔴 | **Hotspot** | A flagged risk: race condition, ambiguity, consistency boundary, or open design question that needs deliberate handling. |

Each flow below is a continuous "storm" — commands and events in the order they actually occur, including their policy-triggered continuations — rather than an isolated list, because that's what an engineering team actually has to build: chains, not islands.

---

## A. IDENTITY

### A1 · Registration & Verification

🟨 Actor: Prospective person
🟦 `RegisterAccount` → 🟧 **AccountRegistered**
🟩 Policy: on `AccountRegistered` → 🟦 `SendVerificationCode` → ⚫ Email/SMS provider → 🟧 **AccountVerificationRequested**
🟨 Actor: Prospective person (enters code)
🟦 `VerifyAccount` → 🟧 **AccountVerified**
🟩 Policy: on `AccountVerified` → 🟦 `ProvisionPersonalBoard`, `InitializePrivacySettings`, `InitializeNotificationPreferences` → 🟧 **BoardCreated** (Personal), **MembershipGranted** (self, Owner), **PrivacySettingsInitialized**, **NotificationPreferencesInitialized**
🟪 Read Models touched: none yet — Home is still empty until the person's first Reminder.
🔴 **Hotspot:** the four provisioning commands triggered by `AccountVerified` must be treated as one atomic unit from the User's point of view — if `ProvisionPersonalBoard` succeeds but `InitializePrivacySettings` fails, the account must not be left half-provisioned. This needs to be a small saga (§C) with a retry-until-complete guarantee, not four independent fire-and-forget policies.
- **Idempotency:** `VerifyAccount` with an already-consumed code is a safe no-op, not an error that confuses the user.
- **Timeout:** verification code expires (short TTL); a background sweeper (§F) raises no event for silent expiry — the account simply stays `PendingVerification` until a new code is requested.

### A2 · Login

🟨 Actor: Registered person
🟦 `Login` → 🟧 **LoginSucceeded** / **LoginFailed**
🟩 Policy: on `LoginFailed` (repeated) → 🟦 `ApplyRateLimit` (Trust & Safety adjacent, not user-visible as an event).
🔴 **Hotspot:** none structurally — login is stateless per attempt and has no cross-context fan-out. Flagged here only to confirm it deliberately has *no* saga, no read-model update beyond a last-seen timestamp, and no retry semantics of its own — a good example of a flow that should stay this simple.

### A3 · Account Recovery

🟨 Actor: Person who lost access
🟦 `RequestAccountRecovery` → 🟧 **AccountRecoveryRequested**
🟩 Policy: on `AccountRecoveryRequested` → 🟦 `SendRecoveryToken` → ⚫ Email/SMS provider
🟦 `CompleteAccountRecovery` (with token) → 🟧 **AccountRecovered**
- **Idempotency:** a second token issued invalidates the first implicitly (§B4 in the Business Behavior Model already specifies this) — the recovery saga carries at most one live token at a time.
- **Retry:** token delivery failure retries exactly like any other transactional message (§F, background delivery retry).

### A4 · Account Suspension

🟨 Actor: Moderator (privileged, not a peer User)
🟦 `SuspendAccount` (issued as the resolution of `TS-04`, never directly) → 🟧 **AccountSuspended**
🟩 Policy: on `AccountSuspended` → 🟦 `InvalidateAllSessions`, `CancelPendingBuzzes`
🟩 Policy: on `AccountSuspended` → 🟦 `CheckSoleOwnership` → if the suspended person is the sole Owner of a shared Board with other active Members → 🟦 `ForceOwnershipTransfer` (see Ownership Transfer saga, §C2, forced-path variant) → 🟧 **BoardOwnershipTransferred** (system-forced, Members notified after the fact).
🔴 **Hotspot:** this sole-ownership check is the exact gap identified in the Business Behavior Model (§I, item 9). From an event-storming lens it is unambiguously a **policy that must exist** — without it, `AccountSuspended` leaves a Board in an undefined state, which is precisely the kind of silent gap Event Storming exists to surface.

### A5 · Account Deletion (Saga — see §C6 for full detail)

🟨 Actor: The person themselves
🟦 `RequestAccountDeletion` → 🟧 **AccountDeletionRequested**
🟩 Policy: on `AccountDeletionRequested` → 🟦 `CheckSoleOwnership` → if blocking, deletion halts pending `BoardOwnershipTransferred` or `BoardDeleted` for each affected Board.
🟦 `ConfirmAccountDeletion` (only reachable once the precondition clears) → 🟧 **AccountDeleted**
🟩 Policy: on `AccountDeleted` → 🟦 `AnonymizeAuthorshipOnSharedBoards`, `PurgePersonalBoardContent`, `RemoveFromSearchIndex`, `RevokeAllSessions`

---

## B. SHARED SPACES (Boards & Membership)

### B1 · Board Created / Renamed / Visibility Changed

🟨 Actor: Any authenticated person (create) / Owner or permitted Member (settings)
🟦 `CreateBoard` → 🟧 **BoardCreated**, **MembershipGranted** (self, Owner)
🟦 `RenameBoard` → 🟧 **BoardRenamed**
🟦 `ChangeBoardVisibility` → 🟧 **BoardVisibilityChanged**
🟩 Policy: on any of the above → 🟦 `UpdateSearchIndex` (async, §F)
🟪 Read Models touched: Boards list, Search index.
- **Idempotency:** `CreateBoard` needs a client-supplied idempotency key — a double-tap must not create two Boards (flagged already in the Business Behavior Model; restated here because it's a textbook Command-idempotency case).

### B2 · Board Archived / Deleted

🟨 Actor: Owner
🟦 `ArchiveBoard` → 🟧 **BoardArchived**
🟩 Policy: on `BoardArchived` → 🟦 `HaltOccurrenceGeneration` for every Reminder on this Board (no new **OccurrenceGenerated** events until unarchived).
🟦 `DeleteBoard` → 🟧 **BoardDeleted**
🟩 Policy: on `BoardDeleted` → 🟦 `PurgeBoardContent` (async, deferred — see Hotspot below).
🔴 **Hotspot — synchronous cascade risk:** a literal implementation would have `BoardDeleted` trigger an immediate, synchronous cascade delete of every Reminder, Occurrence, Entity, and History row on that Board. For a long-lived family Board, that could be years of data deleted in one transactional burst. **Recommendation (carried into §H.2):** `BoardDeleted` should mark the Board as deleted/hidden *immediately* (fast, user-facing), while the actual heavy cascade runs as an asynchronous background purge job after a short grace window — decoupling the instant "it's gone" experience from the expensive cleanup, and incidentally opening the door to an undo window the Information Architecture doesn't currently offer (also flagged there, §18.11 equivalent).

### B3 · Ownership Transfer (Saga)

🟨 Actor: Current Owner
🟦 `OfferOwnershipTransfer` (target: a specific Member) → 🟧 **BoardOwnershipTransferOffered**
🟨 Actor: Target Member
🟦 `AcceptOwnershipTransfer` → 🟧 **BoardOwnershipTransferred** *(atomic: old Owner → Member, target → Owner, in the same transaction)*
🟦 `DeclineOwnershipTransfer` → 🟧 **BoardOwnershipTransferDeclined**
🟩 Policy — **forced path** (used only by A4's suspension flow and BRD-06's urgent-leave escape hatch, both recommended additions from the Business Behavior Model): on `AccountSuspended` or `LeaveBoard` from a sole Owner with no accepted offer outstanding → 🟦 `ForceOwnershipTransfer` (auto-target: longest-standing active Member) → 🟧 **BoardOwnershipTransferred** (flagged as system-forced in its payload) → notify the new Owner after the fact rather than requiring prior acceptance.
🔴 **Hotspot:** two transfer offers issued in quick succession by the same Owner — the second must implicitly supersede the first (only one live offer per Board at a time), otherwise two Members could both attempt to accept and race for the Owner slot.
- **Timeout:** an unaccepted offer has no hard expiry by default (it's a low-frequency, low-risk pending state) — but if the Owner later wants to leave and the offer is still unaccepted, the forced path above takes over rather than leaving the Owner stuck.

### B4 · Invitation Lifecycle (Saga)

🟨 Actor: Inviting Member (per Board's Invitation Policy)
🟦 `SendInvitation` → 🟧 **InvitationSent**
🟩 Policy: on `InvitationSent` → 🟦 `DeliverInvitation` → ⚫ Email / SMS provider, or in-app link/QR generation (no external system).
🟨 Actor: Recipient
🟦 `AcceptInvitation` → 🟧 **InvitationAccepted** → 🟩 Policy: → 🟦 `GrantMembership` → 🟧 **MembershipGranted**
🟦 `DeclineInvitation` → 🟧 **InvitationDeclined**
🟨 Actor: Inviting Member
🟦 `RevokeInvitation` → 🟧 **InvitationRevoked**
🟨 Actor: System (timer)
🟩 Policy: token TTL elapses with no action → 🟦 `ExpireInvitation` → 🟧 **InvitationExpired**
🟩 Policy: on `UserBlocked` where a pending Invitation exists between the two parties → 🟦 `RevokeInvitation` (system-issued) → 🟧 **InvitationRevoked**
🔴 **Hotspot — accept/revoke race:** `AcceptInvitation` and `RevokeInvitation` arriving near-simultaneously must resolve deterministically (first to reach the Invitation's consistency boundary wins; the loser gets an honest "already revoked" / "already accepted" response, never a silent failure or a duplicate Membership).
🔴 **Hotspot — unregistered invitee:** if the recipient has no account yet, `AcceptInvitation` cannot be issued until `AccountVerified` (§A1) completes first — this is a genuine saga-of-sagas: Invitation Acceptance nested inside Registration. Engineering must carry the Invitation's identity through the registration flow rather than treating them as unrelated.
- **Idempotency:** accepting an already-accepted Invitation is a safe no-op (Business Behavior Model rule already states this — restated here as a Command-level idempotency requirement, not just a business rule).

### B5 · Member Removed / Leaves / Role Changed

🟨 Actor: Owner
🟦 `RemoveMember` → 🟧 **MemberRemoved**
🟦 `ChangeMemberRole` → 🟧 **MemberRoleChanged**
🟨 Actor: Any Member
🟦 `LeaveBoard` → 🟧 **MemberLeft** (blocked by a precondition check if sole Owner — see B3's forced path)
🟩 Policy: on `MemberRemoved` / `MemberLeft` → 🟦 `CancelPendingBuzzesForMember` (that person should not receive Buzzes for a Board they no longer belong to) and `RemoveFromBoardReadModel` (their view of this Board's future Occurrences stops).
🔴 **Hotspot:** a Buzz already generated (but not yet delivered) for someone who is removed between generation and delivery — see §H.1 (delivery-time re-check).

---

## C. REMINDER LIFECYCLE

### C1 · Reminder Drafted → Confirmed → Created (manual and AI converge here)

🟨 Actor: Member (manual path)
🟦 `CreateReminder` → 🟧 **ReminderCreated** → 🟩 Policy: → 🟦 `GenerateNextOccurrence` → 🟧 **OccurrenceGenerated**

🟨 Actor: Member (AI-assisted path — see §E for the parsing detail)
🟦 `RequestReminderDraft` (natural language, any modality) → 🟧 **ReminderDraftProposed**
🟨 Actor: Member
🟦 `ConfirmReminderDraft` → 🟧 **ReminderDraftConfirmed** → 🟩 Policy: → 🟦 `CreateReminder` (using the draft's fields) → *joins the same happy path as the manual command above.*
🟦 `DiscardReminderDraft` → 🟧 **ReminderDraftDiscarded**
🟨 Actor: System (timer)
🟩 Policy: Draft unconfirmed past its TTL → 🟦 `ExpireReminderDraft` → 🟧 **ReminderDraftDiscarded** (system-issued)

🟪 Read Models touched: Home (Today/Coming Up), the target Board's Reminders list, Search index (async).
🔴 **Hotspot — duplicate submission:** both `CreateReminder` and `ConfirmReminderDraft` need idempotency keys; a retried network call on a flaky connection must never produce two Reminders for one user intent.
🔴 **Hotspot — AI duplicate detection:** `RequestReminderDraft` triggers a **Policy**: on **ReminderDraftProposed** → 🟦 `CheckForDuplicateReminder` (compares against the target Board's active Reminders) → if a strong match is found, the Draft carries a `possibleDuplicateOf` reference shown to the user at confirmation time. This never blocks — it informs — consistent with the "AI proposes, humans decide" principle already established.

### C2 · Reminder Updated / Recurrence Changed

🟨 Actor: Entitled Member
🟦 `UpdateReminder` → 🟧 **ReminderUpdated**
🟦 `ChangeRecurrenceRule` → 🟧 **RecurrenceRuleUpdated**
🟩 Policy: on `RecurrenceRuleUpdated` → 🟦 `RegenerateFutureOccurrences` (only Occurrences not yet generated/due are affected — see Domain Model challenge on timezone, restated as a Hotspot below).
🔴 **Hotspot — timezone anchor:** `RegenerateFutureOccurrences` cannot resolve a deterministic due-instant without a fixed reference timezone on the Reminder (the gap identified in the Business Behavior Model, §I.4). This is not just a domain-modeling nicety from the event-storming view — it is the literal input `GenerateNextOccurrence` needs to compute a due-instant at all. **This must be resolved before Occurrence generation can be built.**
🔴 **Hotspot — in-flight Occurrence vs. rule change:** an Occurrence already generated under the old rule, currently `Due`, must complete its lifecycle unaffected by a `RecurrenceRuleUpdated` that arrives mid-flight — the policy only acts on not-yet-generated future Occurrences, never retroactively touching an existing one.

### C3 · Reminder Archived / Deleted

🟨 Actor: Entitled Member
🟦 `ArchiveReminder` → 🟧 **ReminderArchived**
🟦 `DeleteReminder` → 🟧 **ReminderDeleted**
🟩 Policy: on either → 🟦 `HaltOccurrenceGeneration` for this Reminder (no new **OccurrenceGenerated**); History and past Occurrences are explicitly **not** touched by this policy — silence here is intentional, not an oversight.

---

## D. OCCURRENCE LIFECYCLE

### D1 · Occurrence Generated (background process — see §F1)

🟨 Actor: System (scheduler, not a person)
🟦 `GenerateNextOccurrence` (per active Recurrence Rule, rolling horizon) → 🟧 **OccurrenceGenerated**
🟩 Policy: on `OccurrenceGenerated` → 🟦 `ScheduleBuzz` for each current Board Member → 🟧 **BuzzScheduled** (see §E1)
- **Idempotency key:** (ReminderId, resolved due-date) — the scheduler must be safe to re-run without ever producing two Occurrences for the same due moment.

### D2 · Occurrence Due → Buzzed

🟨 Actor: System (scheduler, time-based)
🟩 Policy: an Occurrence's due-instant (or its configured lead time) arrives → 🟦 `TriggerBuzz` → *feeds into the Notification Lifecycle, §E.*
🟧 **OccurrenceDue** is a useful internal signal even though it may not always need to be a durably-stored event in its own right — it's the trigger, not typically something other contexts subscribe to directly (they subscribe to the Buzz events it causes instead). Named here for completeness since the prompt calls for it explicitly.

### D3 · Occurrence Completed / Dismissed / Missed / Skipped (future)

🟨 Actor: Any current Board Member
🟦 `CompleteOccurrence` → 🟧 **OccurrenceCompleted**
🟦 `DismissOccurrence` → 🟧 **OccurrenceDismissed**
🟦 `UndoOccurrenceResolution` → 🟧 **OccurrenceUndone**
🟦 `SkipOccurrence` *(future — "snooze")* → 🟧 **OccurrenceSkipped**
🟨 Actor: System (timer)
🟩 Policy: a `Due` Occurrence passes its grace window with no action → 🟦 `MarkOccurrenceMissed` → 🟧 **OccurrenceMissed**
🟩 Policy: on any of Completed/Dismissed/Undone/Skipped/Missed → 🟦 `CancelPendingBuzzes` for this Occurrence (a resolved Occurrence shouldn't keep buzzing) and `RecordHistoryEntry` (§I).
🔴 **Hotspot — concurrent completion (the canonical Event Storming race case):** two Board Members issue `CompleteOccurrence` for the same shared Occurrence within milliseconds of each other. **Resolution:** the Occurrence aggregate accepts the first Command that reaches it and produces exactly one **OccurrenceCompleted**; the second Command is rejected as a no-op that returns "already completed by [name]" rather than erroring or double-firing the event. This must be enforced at the aggregate's consistency boundary (optimistic concurrency / compare-and-set on the Occurrence's version), not client-side.
🔴 **Hotspot — Undo ping-pong:** rapid alternating Complete/Undo from two devices (e.g., both queued offline and replayed on reconnect) — every transition is still recorded in History even though only the *latest* state is authoritative, so no information is lost even under a race; this is a deliberate design choice, not a gap.

### D4 · Occurrence Archived (implicit)

No direct Command — Occurrences move into a permanently-retained historical state as a *consequence* of `ReminderArchived`, `ReminderDeleted`, or natural resolution (D3). No new event type is needed beyond what D3 and C3 already produce; flagged here only to confirm this is a deliberate simplification, not a missing event.

---

## E. NOTIFICATION LIFECYCLE

### E1 · Buzz Scheduled → Generated → Delivered / Failed → Retried

🟨 Actor: System (policy-triggered, per D1/D2)
🟧 **BuzzScheduled** (one per (Occurrence, Recipient) pair, at generation time)
🟩 Policy: at the scheduled moment → 🟦 `GenerateBuzz` → 🟧 **BuzzGenerated**
🟩 Policy: on `BuzzGenerated` → 🟦 `DeliverBuzz` → ⚫ APNs / FCM / SMS / Email provider (per recipient's Notification Preferences) → 🟧 **BuzzDelivered** or **BuzzDeliveryFailed**
🟩 Policy: on `BuzzDeliveryFailed` → 🟦 `RetryBuzzDelivery` (bounded: e.g., a fixed number of attempts over a fixed window) → 🟧 **BuzzRetried** → eventually either **BuzzDelivered** or, after exhausting retries, 🟧 **BuzzDeliveryExhausted**
🟩 Policy: on `BuzzDeliveryExhausted` → 🟦 `FallbackToAlternateChannel` (if configured) or `SurfaceInAppOnly` — the Buzz must always remain visible in-app even if every push/SMS/email attempt failed (Business Behavior Model rule, restated here as the terminal compensation step for this saga).
🔴 **Hotspot — the delivery-time membership/mute re-check:** `DeliverBuzz` must re-verify, *at the moment of delivery, not just at generation time*, that the recipient is still an active, non-blocked Member of the Board and hasn't muted it since scheduling. A Buzz generated minutes or hours before delivery (for a lead-time reminder) could otherwise be delivered to someone who left the Board, was removed, or muted it in the interim. **This is a new, concrete recommendation surfaced only by tracing the actual time gap between generation and delivery events** — worth feeding back into the Business Behavior Model (see §H.3).
🔴 **Hotspot — duplicate Buzzes:** the (Occurrence, Recipient) pair is the mandatory idempotency key for the entire chain above — `RetryBuzzDelivery` must act on the *same* Buzz identity, never spawn a new one, or a person could receive two lock-screen notifications for one shared reminder, directly damaging the "friendly, not spammy" promise.
🔴 **Hotspot — cross-device delivery:** a Buzz delivered to three of a person's devices must be marked `Seen` everywhere the instant any one device reports it seen — this is a fan-out-then-converge pattern, not three independent Buzz lifecycles.

### E2 · Buzz Opened / Dismissed

🟨 Actor: Recipient
🟦 `OpenBuzz` → 🟧 **BuzzSeen**
🟦 `DismissBuzz` → 🟧 **BuzzDismissed**
🟩 Policy: on `OpenBuzz` (via a native notification action) → may directly issue `CompleteOccurrence` or `DismissOccurrence` (§D3) if the person acted from the notification's action buttons rather than opening the app — the Buzz and Occurrence lifecycles converge here by design (Information Architecture §9's "resolve without opening the app" requirement).

---

## F. AI LIFECYCLE

### F1 · Text / Voice / Photo / Email Parsed → Draft Generated

🟨 Actor: Member
🟦 `SubmitTextForParsing` → 🟧 **TextParsed** → 🟩 Policy: → 🟦 `RequestReminderDraft` *(joins C1)*
🟦 `SubmitVoiceForParsing` (future) → ⚫ Speech-to-text provider → 🟧 **VoiceTranscribed** → 🟧 **TextParsed** (transcription feeds the identical text pipeline — no separate parsing logic) → same policy chain.
🟦 `SubmitPhotoForParsing` (future) → ⚫ OCR/vision provider → 🟧 **PhotoParsed** → 🟧 **TextParsed** (extracted text feeds the same pipeline) → same policy chain.
🟦 `SubmitEmailForParsing` (future) → 🟧 **EmailParsed** → same policy chain.
🟩 Policy: on `TextParsed` → 🟦 `ExtractReminderFields` (via ⚫ LLM/NLU provider) → 🟧 **ReminderDraftProposed** *(this is where F1 hands off into C1 — Draft confirmation and creation are a single concept regardless of which channel produced the Draft)*
🔴 **Hotspot — LLM latency vs. the 10-second promise:** `ExtractReminderFields` depends on an external, variable-latency provider. The Information Architecture's confirmation card must handle a genuine "thinking" window gracefully (a fast, friendly loading state under ~1–2 seconds expected, with a defined worse-case fallback to the manual form if parsing takes too long) — this is a concrete UX requirement generated directly by this event chain, not previously specified anywhere. **Recommendation carried into §H.4.**
🔴 **Hotspot — provider failure:** if `ExtractReminderFields` fails outright (provider outage, malformed response), the policy must degrade to offering the manual-entry fallback immediately — never leave the person staring at a hung request.

---

## G. ENTITY LIFECYCLE

### G1 · Entity Created / Archived, Reminder Linked / Removed

🟨 Actor: Entitled Member
🟦 `CreateEntity` → 🟧 **EntityCreated**
🟦 `ArchiveEntity` → 🟧 **EntityArchived** *(referencing Reminders are explicitly untouched — no cascading policy fires)*
🟦 `LinkReminderToEntity` → 🟧 **ReminderLinked**
🟦 `UnlinkReminderFromEntity` → 🟧 **ReminderRemoved** *(from the Entity, not deleted)*
🟪 Read Models touched: the Entity screen (Information Architecture §3), which is simply a filtered projection of Reminders/Occurrences by `EntityId` — no separate storage concept needed beyond the link itself.
- No hotspots of note — this is a deliberately low-complexity flow with no timing sensitivity or external dependency, which is itself worth confirming explicitly rather than manufacturing risk where none exists.

---

## H. TRUST & SAFETY

### H1 · User Blocked / Unblocked

🟨 Actor: Any person
🟦 `BlockUser` → 🟧 **UserBlocked**
🟩 Policy: on `UserBlocked` → 🟦 `RevokePendingInvitationsBetweenParties` *(feeds §B4)*
🟦 `UnblockUser` → 🟧 **UserUnblocked** *(does not resurrect any revoked Invitation — a new one must be sent)*
🔴 **Hotspot:** a `BlockUser` Command racing against an in-flight `AcceptInvitation` between the same two parties — the block must win (the acceptance is rejected), since safety takes precedence over a not-yet-finalized action, exactly as specified in the Business Behavior Model.

### H2 · Report Submitted → Reviewed → Moderation Action

🟨 Actor: Any person
🟦 `SubmitReport` → 🟧 **ReportSubmitted**
🟨 Actor: Moderator
🟦 `ResolveReport` → 🟧 **ReportResolved**
🟩 Policy: on `ReportResolved` (actioned) → 🟦 `TakeModerationAction` → 🟧 **ModerationActionTaken** → may itself trigger `SuspendAccount` (§A4), `RemoveMember` (§B5), or content removal.
🟪 Read Model touched: a Moderation Queue (admin-facing, not part of the consumer Information Architecture) fed by `ReportSubmitted` / `ReportResolved`.

---

## I. SEARCH & HISTORY (Cross-Cutting Projections)

These two contexts are structurally different from everything above: they issue no Commands of their own and raise no primary business events. They are pure **consumers** — policies that subscribe broadly and maintain read models. Modeling them as anything more than that would be over-engineering.

🟩 Policy: on `ProfileUpdated`, `PrivacySettingsChanged`, `BoardCreated/Renamed/VisibilityChanged`, `ReminderCreated/Updated/Archived/Deleted`, `EntityCreated/Archived`, `AccountDeactivated/Suspended/Deleted` → 🟦 `UpdateSearchIndex` → 🟪 **Search Index** (read model)

🟩 Policy: on *(effectively everything in §B, §C, §D — every Membership, Reminder, and Occurrence event)* → 🟦 `AppendHistoryEntry` → 🟪 **History / Activity Projection** (read model)

🔴 **Hotspot — eventual consistency is fine here, and should be left alone:** unlike Home's Today feed (§J), Search and History have no user expectation of instant, read-your-own-write freshness — a few seconds of lag before a newly renamed Board appears in search results is invisible in practice. **This is flagged as a hotspot in the positive sense**: the temptation to over-engineer synchronous updates here should be resisted; it would add cost and complexity for a UX improvement nobody will perceive.

---

## J. Long-Running Processes — Saga Summary

*(Full detail already given inline above; consolidated here for traceability, as requested.)*

| Saga | Start Event | Intermediate Events | Timeout Behavior | Failure Recovery | Completion |
|---|---|---|---|---|---|
| **Account Provisioning** | `AccountVerified` | Personal Board + Privacy + Notification Preferences created | N/A (must complete, not time-limited) | Retry each unfinished step until all succeed; account is not usable until complete | All four provisioning events fired |
| **Invitation Acceptance** | `InvitationSent` | `InvitationAccepted`\|`Declined`, possibly nested `AccountRegistered`→`AccountVerified` if invitee is new | Token TTL elapses | Auto-`InvitationExpired`; no manual recovery needed | `MembershipGranted` or terminal decline/expiry |
| **Ownership Transfer** | `BoardOwnershipTransferOffered` | `AcceptOwnershipTransfer`\|`DeclineOwnershipTransfer` | No hard timeout on the voluntary path; forced path triggers immediately on `LeaveBoard`/`AccountSuspended` deadlock | Forced-transfer fallback to longest-standing Member | `BoardOwnershipTransferred` |
| **Recurring Occurrence Generation** | Continuous (Reminder is `Active`) | `OccurrenceGenerated` on a rolling horizon | N/A — an ongoing process, not a single completable saga | Re-run is always safe (idempotent per Reminder+due-date) | Never "completes" while the Reminder is Active; halts on Archive/Delete |
| **Buzz Scheduling & Delivery** | `BuzzScheduled` | `BuzzGenerated`→`BuzzDeliveryAttempted`→(`Delivered`\|`Failed`→`Retried`)* | Bounded retry window, then `BuzzDeliveryExhausted` | Fallback channel, then guaranteed in-app visibility | `BuzzDelivered` or `BuzzDeliveryExhausted`-with-fallback |
| **Account Deletion** | `AccountDeletionRequested` | Nested Ownership Transfer saga if needed, then `AccountDeleted` | N/A — blocked, not timed out, until precondition clears | User retries `ConfirmAccountDeletion` once the precondition is resolved | `AccountDeleted` + anonymization/purge policies fired |
| **AI Parsing → Draft** | `TextParsed`\|`VoiceTranscribed`\|`PhotoParsed`\|`EmailParsed` | `ReminderDraftProposed` | Draft TTL elapses unconfirmed | Auto-`ReminderDraftDiscarded`; person can simply re-submit | `ReminderDraftConfirmed`→joins Reminder Created, or `ReminderDraftDiscarded` |
| **Board/Account Deletion Purge** *(new, recommended)* | `BoardDeleted`\|`AccountDeleted` | Grace-period timer | Grace window elapses | N/A | Background `PurgeBoardContent`/`PurgePersonalBoardContent` completes |

---

## K. Read Models

| Read Model | Product Surface | Updated By |
|---|---|---|
| **Home Feed** (Today / Coming Up / Missed) | Home tab | `OccurrenceGenerated`, `OccurrenceCompleted`, `OccurrenceDismissed`, `OccurrenceUndone`, `OccurrenceMissed`, `OccurrenceSkipped`, `ReminderArchived`/`Deleted`, `MembershipGranted`/`MemberRemoved`/`MemberLeft`, `BoardArchived`/`Deleted`. **Not** updated by `BoardMuted` — muting affects Buzz delivery only, never the Home list, per Business Behavior Model rule 8. |
| **Board Screen** | Boards tab → a Board | `ReminderCreated`/`Updated`/`Archived`/`Deleted`, all Occurrence events scoped to this Board's Reminders, `MembershipGranted`/`MemberRemoved`/`MemberLeft`/`MemberRoleChanged` |
| **Entity Screen** | Board Options → Entities | `EntityCreated`/`Archived`, `ReminderLinked`/`Removed`, plus derived Occurrence status for linked Reminders |
| **History / Activity Projection** | Board Options → History, Reminder Detail → History | Broad subscription — see §I |
| **Search Index** | Search | See §I |
| **Notification / Buzz List** | Lock screen, in-app fallback list | `BuzzGenerated`/`Delivered`/`Failed`/`Seen`/`Dismissed` |
| **Profile** | Profile | `ProfileUpdated`, `PrivacySettingsChanged`, `NotificationPreferencesChanged`; "My Boards" shortcut by `MembershipGranted`/`MemberLeft`/`MemberRemoved`/`BoardArchived`/`Deleted` |
| **Moderation Queue** | (admin surface, outside consumer IA) | `ReportSubmitted`, `ReportResolved`, `ModerationActionTaken` |

🔴 **Hotspot — read-your-own-writes on Home:** Home is the one read model with a real UX expectation of *instant* consistency for the acting user's own action (complete an Occurrence, see it update immediately). Recommendation: the client applies an optimistic local update the instant the Command is issued, while the durable projection catches up asynchronously (typically sub-second, but not guaranteed synchronous) for other devices and other Board Members. This is an architecture requirement, not just a UX nicety — see §M.3.

---

## L. Background Processing

| Job | Purpose | Idempotency / Retry Notes |
|---|---|---|
| **Occurrence Generation Scheduler** | Materializes the next Occurrence(s) per Active Reminder on a rolling horizon | Idempotent per (ReminderId, due-date); safe to re-run |
| **Buzz Dispatcher** | Turns `OccurrenceDue`/lead-time triggers into `BuzzGenerated` per recipient | Idempotent per (OccurrenceId, RecipientId) |
| **Buzz Delivery Retry Worker** | Bounded retry with backoff on `BuzzDeliveryFailed`, then fallback channel | Retries act on the same Buzz identity — never spawns a new one |
| **Search Indexer** | Async projector from domain events to the Search read model | At-least-once consumption tolerated; eventually consistent by design |
| **History / Activity Projector** | Async, append-only projector | Must de-duplicate on event ID to avoid duplicate ledger entries under at-least-once delivery |
| **Invitation Expiry Sweeper** | Raises `InvitationExpired` when a token's TTL elapses | Pure time-based scan; idempotent (an already-expired Invitation is skipped) |
| **Reminder Draft Expiry Sweeper** | Raises `ReminderDraftDiscarded` (system) when a Draft's TTL elapses | Same pattern as above |
| **Missed-Occurrence Transition Job** | Transitions `Due` Occurrences past their grace window into `Missed` | Must run frequently enough that "missed" feels timely, not stale |
| **Board/Account Deletion Purge Job** *(new, recommended — see §M.2)* | Performs the actual heavy cascade delete after the grace window on `BoardDeleted`/`AccountDeleted` | Must be resumable/chunked for very large Boards — never a single giant transaction |
| **Deactivated/Suspended Account Housekeeping** | Ensures no Buzz generation, no Search visibility, for non-Active accounts | Belongs to the delivery-time re-check (§H.1) more than a standalone sweep — listed here for completeness |

---

## M. External Systems

| System | Used For | Failure Implication |
|---|---|---|
| **APNs** (iOS push) | Buzz delivery to iOS devices | Falls back to alternate channel / in-app-only per §E1's exhaustion policy |
| **FCM** (Android/cross-platform push) | Buzz delivery to Android and Web | Same fallback pattern |
| **Transactional Email provider** | Verification codes, recovery tokens, email-channel Invitations and Buzzes | Delay here directly delays registration/recovery — treat as higher-priority than marketing email infrastructure |
| **SMS provider** | Phone verification, SMS-channel Invitations and Buzzes | Cost-sensitive at scale; failures must fall back gracefully, not silently drop |
| **Speech-to-text provider** *(future)* | Voice reminder input | Failure degrades to "please type instead," never a dead end |
| **OCR / vision provider** *(future)* | Photo import | Same graceful-degradation principle |
| **LLM / NLU provider** | Natural-language parsing — this is core today, not future, per the Information Architecture's AI-first creation flow | The single most latency- and availability-sensitive external dependency in the whole system, given the <10-second promise; needs a hard timeout with instant fallback to the manual form (§F1 hotspot) |
| **Calendar export** *(future, outbound adapter)* | Letting Occurrences appear in a person's external calendar | Outbound only — no impact on BuzzMe's own event flow if unavailable |
| **WhatsApp detection** *(future, inbound adapter)* | Suggesting Reminders from message content | Feeds the same `TextParsed` pipeline as any other input channel |

---

## N. Event Catalogue

*Grouped by context. Ordering requirement notes whether strict per-aggregate ordering matters (it always does, per aggregate ID) versus cross-aggregate ordering being irrelevant. Idempotency notes the natural de-duplication key. Retry strategy notes whether the producing side needs at-least-once delivery guarantees.*

### Identity
| Event | Producer | Key Consumers | Payload (meaning) | Ordering | Idempotency Key | Retry |
|---|---|---|---|---|---|---|
| `AccountRegistered` | Identity | Account Provisioning saga | New person, unverified | Per-account | Account ID | At-least-once, consumer-deduplicated |
| `AccountVerified` | Identity | Account Provisioning saga, Search | Identity confirmed | Per-account | Account ID | At-least-once |
| `LoginSucceeded`/`Failed` | Identity | Trust & Safety (rate limiting) | Access attempt outcome | None (independent facts) | N/A — not replayed | Best-effort |
| `AccountRecoveryRequested`/`Recovered` | Identity | Notification (token delivery) | Access restoration | Per-account | Account ID + token ID | At-least-once |
| `AccountDeactivated`/`Reactivated` | Identity | Search, Notification Engine | Self-paused/resumed | Per-account | Account ID | At-least-once |
| `AccountSuspended` | Trust & Safety | Identity, Shared Spaces (ownership check), Notification Engine | Platform-enforced restriction | Per-account | Account ID | At-least-once, must not be lost |
| `AccountDeletionRequested`/`Deleted` | Identity | Shared Spaces, History, Search | Irreversible removal | Per-account | Account ID | At-least-once, must not be lost |
| `ProfileUpdated` | Identity | Search | Display info changed | Per-account | Account ID + version | At-least-once |
| `PrivacySettingsChanged` | Identity | Search, Invitations, Mentions | Discoverability/reachability changed | Per-account | Account ID + version | At-least-once |
| `NotificationPreferencesChanged` | Identity | Notification Engine | Delivery rules changed | Per-account | Account ID + version | At-least-once |

### Shared Spaces
| Event | Producer | Key Consumers | Payload (meaning) | Ordering | Idempotency Key | Retry |
|---|---|---|---|---|---|---|
| `BoardCreated` | Shared Spaces | Search, History | New shared space | Per-board | Board ID | At-least-once |
| `BoardRenamed`/`SettingsUpdated`/`VisibilityChanged` | Shared Spaces | Search, History | Board metadata changed | Per-board, must apply in order | Board ID + version | At-least-once |
| `BoardArchived`/`Unarchived` | Shared Spaces | Reminder Management (halt generation) | Board paused/resumed | Per-board, ordered | Board ID + version | At-least-once |
| `BoardDeleted` | Shared Spaces | Everything referencing this Board | Permanent removal (see purge hotspot) | Per-board, terminal | Board ID | At-least-once, must not be lost |
| `BoardOwnershipTransferOffered`/`Transferred`/`Declined` | Shared Spaces | History, Notification | Owner role change | Per-board, strictly ordered | Board ID + offer ID | At-least-once |
| `MemberLeft`/`Removed`/`RoleChanged` | Shared Spaces | Notification Engine (cancel pending), History | Membership change | Per-(board, member) | Board ID + Member ID + version | At-least-once |
| `MembershipGranted` | Shared Spaces | Home/Board read models, Notification Engine | New active membership | Per-(board, member) | Board ID + Member ID | At-least-once |
| `InvitationSent`/`Accepted`/`Declined`/`Revoked`/`Expired` | Shared Spaces | Identity (nested registration), History | Invitation lifecycle stage | Per-invitation, strictly ordered | Invitation ID + stage | At-least-once |

### Reminder Management
| Event | Producer | Key Consumers | Payload (meaning) | Ordering | Idempotency Key | Retry |
|---|---|---|---|---|---|---|
| `ReminderDraftProposed`/`Confirmed`/`Discarded` | AI/Reminder Management | Home (draft never shown there), Reminder Management | AI-assisted creation stage | Per-draft | Draft ID | At-least-once |
| `ReminderCreated` | Reminder Management | Home, Board screen, Search, Notification (schedule) | New reminder definition | Per-reminder | Reminder ID | At-least-once |
| `ReminderUpdated`/`RecurrenceRuleUpdated` | Reminder Management | Notification (reschedule), Search | Definition changed | Per-reminder, ordered | Reminder ID + version | At-least-once |
| `ReminderArchived`/`Deleted` | Reminder Management | Occurrence generation (halt) | Retired (history survives) | Per-reminder, terminal-ish | Reminder ID | At-least-once |
| `OccurrenceGenerated` | Reminder Management (scheduler) | Notification Engine, Home, Board screen | New due instance | Per-occurrence | (Reminder ID, due-date) | At-least-once, must be exactly-once *effectively* via the idempotency key |
| `OccurrenceCompleted`/`Dismissed`/`Undone`/`Skipped`/`Missed` | Reminder Management | Notification (cancel pending), Home, History | Occurrence resolved/reversed | Per-occurrence, strictly ordered | Occurrence ID + action + actor + timestamp | At-least-once, consumer-deduplicated |
| `EntityCreated`/`Archived` | Reminder Management | Search, Entity screen | Real-world subject lifecycle | Per-entity | Entity ID | At-least-once |
| `ReminderLinked`/`Removed` (Entity) | Reminder Management | Entity screen | Association change | Per-(reminder, entity) | Reminder ID + Entity ID | At-least-once |
| `AttachmentAdded` | Reminder Management | Reminder detail read model | Supporting file added | Per-reminder | Attachment ID | At-least-once |

### Notification Engine
| Event | Producer | Key Consumers | Payload (meaning) | Ordering | Idempotency Key | Retry |
|---|---|---|---|---|---|---|
| `BuzzScheduled` | Notification Engine | Buzz Dispatcher | A future delivery is planned | Per-(occurrence, recipient) | Occurrence ID + Recipient ID | At-least-once |
| `BuzzGenerated` | Notification Engine | Delivery worker | Ready to deliver | Per-(occurrence, recipient) | Occurrence ID + Recipient ID | At-least-once |
| `BuzzDeliveryAttempted`/`Delivered`/`DeliveryFailed`/`Retried`/`DeliveryExhausted` | Notification Engine | Buzz list read model | Delivery outcome per attempt | Per-buzz, strictly ordered | Buzz ID + attempt number | At-least-once, must tolerate provider-side duplicate sends without user-visible duplication |
| `BuzzSeen`/`Dismissed` | Notification Engine | Buzz list, Occurrence (if action-triggered) | Recipient acknowledged | Per-buzz | Buzz ID | At-least-once |
| `BoardMuted`/`Unmuted` | Notification Engine | Buzz Dispatcher (delivery-time check) | Personal delivery override | Per-(board, person) | Board ID + Person ID | At-least-once |

### Trust & Safety
| Event | Producer | Key Consumers | Payload (meaning) | Ordering | Idempotency Key | Retry |
|---|---|---|---|---|---|---|
| `UserBlocked`/`Unblocked` | Trust & Safety | Invitations, Search, Mentions | Directional safety control | Per-(blocker, blocked) | Blocker ID + Blocked ID | At-least-once, must not be lost |
| `ReportSubmitted` | Trust & Safety | Moderation Queue | Concern flagged | Per-report | Report ID | At-least-once |
| `ReportResolved`/`ModerationActionTaken` | Trust & Safety | Identity, Shared Spaces | Resolution/enforcement | Per-report, ordered | Report ID | At-least-once, must not be lost |

### AI / NLU
| Event | Producer | Key Consumers | Payload (meaning) | Ordering | Idempotency Key | Retry |
|---|---|---|---|---|---|---|
| `TextParsed`/`VoiceTranscribed`/`PhotoParsed`/`EmailParsed` | AI/NLU | Reminder Management (Draft proposal) | Raw input converted to structured text | Per-submission | Submission ID | At-least-once |
| `DuplicateReminderSuspected` | AI/NLU | Reminder Management (Draft) | Similarity warning attached to a Draft | Per-draft | Draft ID | Best-effort, non-critical |

*Search and History raise no primary events of their own — see §I.*

---

## O. Architectural Risks

1. **Eventual-consistency perception risk on Home.** If the acting user's own Complete/Dismiss doesn't reflect instantly in their own view, the product will feel broken even though the backend is behaving correctly. Requires an explicit optimistic-update contract between client and read model (§K).
2. **Saga stuck-states.** Account Provisioning, Account Deletion, and Ownership Transfer are all multi-step processes with a window where a person is in an intermediate, not-fully-resolved state. Each needs monitoring for "stuck" instances (a saga that started but never reached a terminal event) — silent partial completion is the single most dangerous failure mode in an event-driven system and the one hardest to notice without deliberate tracking.
3. **Notification delivery is the highest-volume, highest-external-dependency, highest-emotional-stakes flow in the system.** A medication or vaccination reminder that silently fails to buzz because of a provider outage is a real-world harm, not just a UX blemish — this flow deserves disproportionate reliability investment relative to its architectural complexity.
4. **Recurring generation at scale.** Millions of active Recurrence Rules being swept on a rolling horizon is a genuine capacity-planning concern; generation lag (the job falling behind) would silently erode the product's core promise before anyone notices via user complaints.
5. **Timezone/DST resolution is a hard blocker, not a nice-to-have.** `GenerateNextOccurrence` cannot function correctly without the reference-timezone concept flagged repeatedly across all three prior documents and reconfirmed here from the event-flow angle.
6. **Clock/scheduling authority.** Multiple background workers computing "what's due now" must agree on a single authoritative time source — clock drift between workers is a subtle, hard-to-debug source of Occurrences buzzing at visibly inconsistent moments, which directly damages the "trustworthy" design goal.
7. **LLM provider latency/availability risk sits directly on the product's signature promise** (<10 second creation) — this dependency deserves a hard timeout and an always-available manual fallback, not just "best effort."
8. **Cross-context event ordering at Board membership boundaries.** The delivery-time re-check hotspot (§E1) is the clearest example: state captured at one point in time (Buzz generation) can be stale by the time a later action (delivery) occurs, and every flow that spans a real time gap between generation and action needs the same scrutiny applied.
9. **Background sweepers (Invitation/Draft expiry, Missed-Occurrence transition) need operational visibility.** These are easy to build once and forget; at scale, a sweeper falling silently behind produces a slow-growing backlog of stale pending states with no user-facing symptom until it's large.

---

## P. Recommendations

### P.1 — Domain Model amendments
- Formalize a **reference timezone** field on Reminder/Recurrence Rule (already recommended in the Business Behavior Model; the event-storming exercise confirms it is a hard functional blocker for `GenerateNextOccurrence`, not an edge-case nicety).
- Introduce an explicit **soft-deleted** intermediate state for Board and Account deletion (distinct from `Archived`), decoupling the user-facing "it's gone" moment from the heavy asynchronous cascade purge — this is a direct consequence of the `BoardDeleted` cascade hotspot (§B2) and did not exist in the original model.
- Formalize the **Reminder Draft** concept fully (already flagged in the Business Behavior Model) — the event storm confirms it needs its own TTL-driven expiry policy as a first-class background process, not an afterthought.

### P.2 — Business Behavior Model amendments
- Add an explicit rule: **Buzz delivery must re-verify current Membership, Block status, and Mute state at the moment of delivery**, not rely solely on state captured at scheduling/generation time. This is a genuinely new finding — the original workflow document described generation and delivery as adjacent steps without naming the real time gap between them.
- Name concrete **timeout/retry parameters as business policy, not implementation detail** — e.g., "an Invitation is valid for N days," "a Buzz delivery is retried M times over T minutes before falling back" — so engineering isn't left inferring numbers that are actually product decisions.
- Add `BuzzDeliveryExhausted` as a named terminal event in the Buzz lifecycle — the original document implied bounded retries without naming what happens when they're exhausted.

### P.3 — Information Architecture amendments
- Add an explicit principle: **a person's own actions must feel instant; other people's actions sync quickly but are not guaranteed instant** — this is the client-side contract implied by the read-your-own-writes hotspot (§K) and belongs in the UX principles list, not just an engineering footnote.
- Define the **AI "thinking" moment** explicitly as its own micro-state in the creation flow (a brief, friendly loading treatment, with a defined maximum wait before offering the manual fallback) — the current Information Architecture describes only the before/after of AI parsing, not the real latency window in between.

### P.4 — Overall Architecture Health Assessment

The prior three documents produced a genuinely event-friendly foundation: Occurrence was already split from Reminder as its own aggregate specifically for independent-scale writes, Buzz was already modeled per-recipient rather than per-reminder, and the append-only History design maps directly onto event sourcing without any rework. Nothing in this exercise required reopening a foundational decision — every finding here is an **addition or clarification**, not a reversal, which is a strong signal the domain boundaries were drawn correctly the first time.

The real risk in this system was never the shape of the domain — it's the handful of places where **time itself is a first-class actor**: recurrence generation, scheduled Buzz delivery, and the gap between when a fact is captured and when it's acted on. Those are exactly the places flagged above (timezone anchoring, delivery-time re-checks, clock authority, saga stuck-states). Resolve those five or six specific points before backend implementation begins, and this is a production-viable blueprint — not a draft that needs another full pass.

---

*This document, together with [PRODUCT_VISION.md](./PRODUCT_VISION.md), [DOMAIN_MODEL.md](./DOMAIN_MODEL.md), [BUSINESS_BEHAVIOR_MODEL.md](./BUSINESS_BEHAVIOR_MODEL.md), and [INFORMATION_ARCHITECTURE.md](./INFORMATION_ARCHITECTURE.md), completes BuzzMe's pre-implementation foundation. Every future service boundary, message schema, and infrastructure decision should trace back to a Command, Event, Policy, or Hotspot named here.*
