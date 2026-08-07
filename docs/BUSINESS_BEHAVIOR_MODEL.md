# BuzzMe — Business Behavior Model

*Companion to [PRODUCT_VISION.md](./PRODUCT_VISION.md) and [DOMAIN_MODEL.md](./DOMAIN_MODEL.md). This document describes BuzzMe as a living business: every major workflow, beginning to end, in the language already established by the Domain Model. No UI, no APIs, no database, no classes — behavior only.*

*Each workflow is written as one continuous story including its branches (accept/reject, complete/undo, etc.) rather than as one template per state transition — a workflow is a beginning-to-end story, not a single step. Workflow IDs (e.g. `IDN-01`) exist so engineering can trace future commands/events/endpoints back to a single source of truth.*

---

## 0. Conventions Used Throughout

- **Actor** — who initiates the workflow (a User, the System/scheduler, or a platform Moderator).
- **Idempotency** is treated as a first-class concern everywhere a network retry, multi-device action, or scheduled job could plausibly fire twice.
- **Compensation** describes what undoes a partially-completed workflow when a later step fails — BuzzMe has no financial transactions, so compensation is almost always "leave the safe partial state and let the user retry," never a complex rollback.
- **History Recorded** always refers to the append-only Reminder History / Activity ledger defined in the Domain Model (§2) — distinct from platform Audit Requirements, which cover security/administrative logging.
- Every workflow that touches another bounded context names it explicitly under **Cross-Context Interactions**.

---

## A. IDENTITY & ACCESS

### IDN-01 · Register & Verify Account

- **Goal:** Turn a new person into a trusted, addressable User.
- **Trigger:** Person submits registration details (or completes an Invitation-driven sign-up — see `MEM-08`).
- **Preconditions:** Email/phone not already registered and verified; not on a platform ban list.
- **Happy Path:** Details submitted → account created in `PendingVerification` state → verification code/link sent → person confirms → account becomes `Active` → default Personal Board, Privacy Settings, and Notification Preferences are provisioned atomically.
- **Alternative Paths:** Registration arrives via an Invitation acceptance (identity is pre-seeded with the inviter's context); person re-requests a verification code if the first expires.
- **Failure Scenarios:** Verification code expires unused → account remains `PendingVerification` indefinitely, gently reminded, never auto-promoted to `Active`; duplicate registration attempt on an already-verified email/phone → rejected with a path to Login/Recover instead; verification code guessed/brute-forced → rate-limited and locked after threshold.
- **Business Rules Applied:** A User always has exactly one Personal Board, Privacy Settings, and Notification Preferences from the moment of creation (Domain Model invariant) — these must not exist in a null state even transiently.
- **Domain Events Raised:** `AccountRegistered`, `AccountVerificationRequested`, `AccountVerified`.
- **Objects Created:** User, Profile, Privacy Settings, Notification Preferences, Personal Board (with a single Membership: Owner = self).
- **Objects Updated:** —
- **Objects Archived:** —
- **Notifications Generated:** Verification code/link (not a Buzz — this is system-critical, not a "shared memory" nudge, and must bypass mute/quiet-hours entirely).
- **History Recorded:** None yet (no Reminder exists) — this is an Audit event, not a Reminder History event.
- **Security Checks:** Rate limiting on verification attempts; password/credential strength if applicable.
- **Privacy Checks:** Default Privacy Settings must default to the *most* private reasonable option, never the most open (matches Vision's privacy-first principle).
- **Cross-Context Interactions:** Invitations & Onboarding (if arriving via invite), Shared Spaces (Personal Board provisioning).
- **Race Conditions / Concurrency:** Two simultaneous registration attempts with the same email — must be resolved by a uniqueness constraint conceptually owned by Identity, not left to chance.
- **Idempotency:** Re-submitting the same registration request before verification completes must not create a second account.
- **Compensation:** If Personal Board provisioning fails after the User record is created, the whole registration is treated as not-yet-complete (retry provisioning) rather than leaving a User without a Personal Board.
- **Audit Requirements:** Registration timestamp, IP/device fingerprint, verification method — retained for Trust & Safety.
- **Future Considerations:** Social/SSO registration is an additive alternate path into the same happy path from "account created" onward; nothing else changes.

### IDN-02 · Login & Session Establishment

- **Goal:** Let a verified User resume access.
- **Trigger:** Person submits credentials.
- **Preconditions:** Account is `Active` (not Suspended, not Deleted).
- **Happy Path:** Credentials verified → session established → Notification channel (push token, etc.) refreshed.
- **Alternative Paths:** Login from a new device triggers a friendly "new device" notice (Trust & Safety awareness, not a blocker).
- **Failure Scenarios:** Wrong credentials → generic failure (never reveal whether the email exists — privacy/security rule); account Suspended → distinct message directing to appeal, not a generic error; account Deactivated → offer to Reactivate (`IDN-04`) inline.
- **Business Rules Applied:** Only `Active` accounts may establish a session; a `Suspended` account is explicitly blocked even with correct credentials.
- **Domain Events Raised:** `LoginSucceeded`, `LoginFailed`.
- **Objects Created:** A Session (outside the core domain model — an Identity-context implementation detail, not a business concept).
- **Objects Updated:** User's last-seen timestamp.
- **Security Checks:** Brute-force/rate limiting; anomaly detection on device/location (future).
- **Privacy Checks:** None beyond not leaking account existence on failure.
- **Cross-Context Interactions:** None beyond Identity itself.
- **Race Conditions:** Concurrent logins from multiple devices are expected and valid — BuzzMe is explicitly multi-device; no single-session assumption should ever be designed in.
- **Idempotency:** N/A — each login is a distinct action by nature.
- **Audit Requirements:** Failed-login counters for Trust & Safety anomaly detection.
- **Future Considerations:** Biometric/passkey login is an additive credential type, not a new workflow shape.

### IDN-03 · Recover Account

- **Goal:** Restore access to a person who has lost their credential, without ever helping an attacker do the same.
- **Trigger:** Person requests recovery (forgotten password/passkey).
- **Preconditions:** Account exists and is `Active` or `Deactivated` (not `Deleted` — a deleted account is not recoverable, see `IDN-06`).
- **Happy Path:** Recovery request → single-use, expiring token sent to a verified channel (email/phone on file) → person sets new credential → all other active sessions are optionally invalidated.
- **Failure Scenarios:** Recovery requested for an email/phone with no matching account → generic "if this exists, you'll receive a link" response (never confirms non-existence); token expires unused → must request again; token reused after consumption → rejected.
- **Business Rules Applied:** Recovery may never bypass verification of channel ownership — a person cannot recover into an account by any path that skips proving they control the registered email/phone.
- **Domain Events Raised:** `AccountRecoveryRequested`, `AccountRecovered`.
- **Security Checks:** Token single-use and expiring; rate-limited requests per account per hour.
- **Privacy Checks:** No account-existence leakage (as above).
- **Cross-Context Interactions:** None.
- **Race Conditions:** Two recovery tokens issued in quick succession — only the most recently issued token should remain valid; earlier ones are implicitly invalidated to avoid a stale-token being used after the account state has moved on.
- **Compensation:** If credential update fails after token consumption, the token must not be treated as "spent" in a way that permanently locks the person out — a fresh token issuance must remain possible.
- **Audit Requirements:** Every recovery attempt (success and failure) logged for Trust & Safety review.
- **Future Considerations:** Account recovery via a trusted co-Member vouching (social recovery) is a plausible future path for family-oriented users who lose all channels — not built now, but the workflow shape (issue → verify → consume) already accommodates a new verification method later.

### IDN-04 · Deactivate & Reactivate Account

- **Goal:** Let a person pause their presence without the finality of deletion.
- **Trigger:** User chooses to deactivate; or logs back in later to reactivate.
- **Preconditions (Deactivate):** Account is `Active`. **(Reactivate):** Account is `Deactivated` and not superseded by a `Suspended` or `Deleted` state.
- **Happy Path:** Deactivate → account hidden from Search and new invitations, existing Board memberships and Reminders untouched but the person stops receiving Buzzes → Reactivate at any time by logging back in, fully restoring prior state with zero data loss.
- **Business Rules Applied:** Deactivation never removes Board Memberships, Reminders, or History — it only suppresses discoverability and delivery. A Deactivated Owner still counts as "the Owner" for invariant purposes (a Board is not ownerless just because its Owner is deactivated) — but see the Domain Model challenge in §I for the edge case where this becomes a real operational problem.
- **Domain Events Raised:** `AccountDeactivated`, `AccountReactivated`.
- **Objects Updated:** User status.
- **Notifications Generated:** None generated *to* a deactivated account; other Board members are not notified of another Member's deactivation (this is a personal, not shared, state).
- **Privacy Checks:** Deactivated accounts must disappear from Search regardless of prior Visibility setting.
- **Future Considerations:** A "scheduled deactivation" (e.g., "pause for my 3-week holiday") is a natural, low-risk future extension.

### IDN-05 · Suspend Account (Trust & Safety Enforcement)

- **Goal:** Platform-enforced restriction of a User found to violate policy, distinct from the User's own choice to deactivate.
- **Trigger:** Resolution of a Report (`TS-04`) or automated policy detection.
- **Preconditions:** A Report or automated signal has been reviewed and actioned by a Moderator.
- **Happy Path:** Moderator actions a Report → account moves to `Suspended` → all sessions invalidated → the person is notified of the suspension and, where policy allows, an appeal path.
- **Failure/Edge Scenarios:** The suspended User is the sole Owner of one or more shared Boards — see §I for the identified gap and recommended resolution (forced ownership transfer or Board freeze).
- **Business Rules Applied:** A `Suspended` account cannot Login (`IDN-02`), cannot send Invitations, cannot be granted new Memberships. Existing content (Reminders, History) they authored is **not** deleted — shared memory integrity survives even Trust & Safety action against its author (consistent with the "authorship preserved" rule in the Domain Model).
- **Domain Events Raised:** `AccountSuspended`.
- **Security Checks:** Only privileged Moderator actors may trigger this — never a peer User action (that's `TS-02`, Block, which is personal-scope only).
- **Audit Requirements:** Full moderation trail: which Report(s) led to the action, which Moderator, what policy was cited.
- **Cross-Context Interactions:** Trust & Safety → Identity → Shared Spaces (ownership implications) → Notification Engine (stop all delivery immediately).
- **Future Considerations:** Graduated enforcement (warning → temporary restriction → suspension) is a natural evolution of this single binary workflow.

### IDN-06 · Delete Account

- **Goal:** Permanently and irreversibly remove a person's identity from BuzzMe, while honoring the "shared memory" promise to the people who remain.
- **Trigger:** User explicitly requests deletion (this is a hard-to-reverse action per the platform's action-risk classification — always requires explicit confirmation, never a side effect of another action).
- **Preconditions:** Account is `Active` or `Deactivated`.
- **Happy Path:** User confirms deletion → if the User is the sole Owner of any shared Board with other active Members, the system requires ownership transfer *first* (cannot proceed silently) → Personal Board and all personal-only Reminders are permanently deleted → Profile and Privacy Settings are permanently deleted → on shared Boards, the person's past authored content and History **remain**, attributed to a de-identified placeholder (e.g., "a former member") rather than being stripped or reassigned to nobody — preserving the shared memory's integrity without keeping the deleted person's identifying data.
- **Alternative Paths:** User is the sole Owner of a Board with no other Members — that Board is deleted along with the account, since it has no remaining reason to exist.
- **Failure Scenarios:** Deletion requested while sole-Owner-with-other-members condition is unresolved → blocked with a clear required action (transfer ownership or remove other members first).
- **Business Rules Applied:** A Board is never left ownerless, even by account deletion (Domain Model invariant, enforced here as a precondition rather than a post-hoc cleanup). Deleting an account never deletes shared History other Members depend on.
- **Domain Events Raised:** `AccountDeletionRequested`, `AccountDeleted`.
- **Objects Archived/Deleted:** User, Profile, Privacy Settings, Notification Preferences, Personal Board and its Reminders — all hard-deleted. Shared-Board content is retained with authorship anonymized.
- **Compensation:** None — deletion is designed to be a two-step (transfer-then-delete) flow specifically so there is no partial, hard-to-reverse failure state to compensate for.
- **Privacy Checks:** This is the authoritative "right to be forgotten" workflow — must remove all personally identifying data not otherwise required for shared memory integrity or legal retention.
- **Audit Requirements:** Deletion event itself is retained (fact that an account was deleted, when) even though the account's personal data is gone — needed for Trust & Safety and legal compliance.
- **Cross-Context Interactions:** Shared Spaces (ownership precondition), History (anonymization), Notification Engine (immediate unsubscribe from all delivery).
- **Future Considerations:** A cooling-off/undo window (e.g., 14 days before permanent purge) is a strong future candidate to prevent accidental, emotional-moment deletions — should be added before this ships broadly.

---

## B. SHARED SPACES (Boards & Membership)

### BRD-01 · Create Board

- **Goal:** Establish a new shared space for a group of people to remember things together.
- **Trigger:** User initiates board creation.
- **Preconditions:** Actor is an `Active` User.
- **Happy Path:** User names the Board (and optionally sets Visibility, Invitation Policy) → Board created → creator becomes its Owner and sole initial Member.
- **Business Rules Applied:** A Board always has exactly one Owner from the instant of creation (Domain Model invariant) — there is no window where a Board exists ownerless.
- **Domain Events Raised:** `BoardCreated`, `MembershipGranted` (Owner, self).
- **Objects Created:** Board, Membership (Owner).
- **History Recorded:** First Activity entry: "Board created by [name]."
- **Security/Privacy Checks:** None beyond standard authentication — creating a Board carries no special privilege requirement.
- **Cross-Context Interactions:** None beyond Identity (who's creating it).
- **Idempotency:** A double-submitted create request (e.g., a UI double-tap) must not silently produce two Boards — the action needs a client-supplied idempotency reference.
- **Future Considerations:** Creating a Board from a Template (e.g., "Start a Rugby Team board" pre-populated with common reminder types) is a natural, low-risk future extension.

### BRD-02 · Update Board (Rename, Settings, Visibility)

- **Goal:** Let a Board evolve as the group's needs change.
- **Trigger:** Owner (or a permitted Member, per Invitation Policy) edits Board name, Invitation Policy, or Visibility.
- **Preconditions:** Actor holds a role authorized for the specific change — renaming may be broader than changing Visibility, which is Owner-only (Visibility changes affect discoverability/risk and should not be delegable by default).
- **Happy Path:** Change submitted → validated → applied → Members notified only for changes that materially affect them (e.g., Visibility going from Private to Public Community — see §I on Board Join Policy).
- **Failure Scenarios:** A Member without permission attempts to change Visibility → rejected with a clear authorization message.
- **Business Rules Applied:** Visibility values are constrained to the defined enumeration (Private, Invite-Only, Public Read-Only [future], Public Community [future]).
- **Domain Events Raised:** `BoardRenamed`, `BoardSettingsUpdated`, `BoardVisibilityChanged`.
- **Objects Updated:** Board.
- **History Recorded:** Each change recorded with actor and old/new value.
- **Security Checks:** Role-based authorization per field being changed.
- **Cross-Context Interactions:** Search & Discovery (a Visibility change to Public affects future findability).
- **Race Conditions:** Two Owners... wait, only one Owner exists at a time, so this specific race collapses to "Owner and a delegated Member edit settings simultaneously" — last-write-wins with full History of both attempts is sufficient; no merge logic is needed for simple scalar settings.
- **Future Considerations:** Board-level custom icon/theme is a purely cosmetic, additive future field.

### BRD-03 · Archive Board

- **Goal:** Retire a Board from active use while preserving everything it holds, reversibly.
- **Trigger:** Owner archives the Board.
- **Preconditions:** Actor is the Owner.
- **Happy Path:** Board marked `Archived` → becomes read-only for all Members → Reminders stop generating new Occurrences (no more Buzzes) → History remains fully visible.
- **Alternative Paths:** Owner un-archives (`Active` restored) at any time.
- **Business Rules Applied:** Archiving is reversible; it must never be conflated with Deletion.
- **Domain Events Raised:** `BoardArchived`, `BoardUnarchived`.
- **Objects Updated:** Board status; implicitly, all its Reminders stop producing Occurrences (a derived effect, not a direct edit to each Reminder).
- **Notifications Generated:** All Members informed the Board is now archived (a meaningful, non-spammy, one-time notice).
- **Cross-Context Interactions:** Reminder Management (occurrence generation halts), Notification Engine (no further Buzzes for this Board's Reminders).
- **Future Considerations:** Auto-archiving a Board with no activity for a long period, offered as a gentle suggestion rather than an automatic action (respecting user agency).

### BRD-04 · Delete Board

- **Goal:** Permanently remove a Board that is no longer wanted, with full awareness this is destroying shared memory.
- **Trigger:** Owner explicitly deletes the Board (a hard-to-reverse action requiring strong confirmation).
- **Preconditions:** Actor is the Owner.
- **Happy Path:** Owner confirms (ideally with a "this cannot be undone and will remove this shared memory for everyone" explicit warning) → all Memberships end → all Reminders, Occurrences, Entities, and History for this Board are permanently removed.
- **Business Rules Applied:** This is the one workflow where Reminder History for a Board *does* get destroyed — but only through this single, explicit, Owner-confirmed action, never as a side effect of any other workflow (Reminder deletion, Member removal, etc. all preserve History; only whole-Board deletion destroys it).
- **Domain Events Raised:** `BoardDeleted` (a single event; downstream systems cascade-clean their own projections rather than BuzzMe emitting one event per contained Reminder).
- **Objects Deleted:** Board, all Memberships, all Reminders, all Occurrences, all Entities, all History for this Board.
- **Notifications Generated:** All former Members informed the Board and its contents no longer exist.
- **Security Checks:** Owner-only; recommend a secondary confirmation step (re-authentication or explicit typed confirmation) given the irreversible, other-people-affecting nature of this action.
- **Cross-Context Interactions:** Every context that referenced this Board (Notification Engine, History, Search) must treat the Board as gone.
- **Compensation:** None by design — see §I for a recommended short undo/grace window, mirroring Account Deletion.
- **Future Considerations:** A "soft delete with grace period" is strongly recommended before this ships (see §I).

### BRD-05 · Transfer Ownership

- **Goal:** Move the Owner role from one Member to another, without ever leaving the Board ownerless.
- **Trigger:** Current Owner initiates a transfer to a chosen Member.
- **Preconditions:** Target is an Active Member of the Board (not a Guest, not an outsider).
- **Happy Path (as challenged in §I):** Owner offers ownership to a Member → **the receiving Member must explicitly accept** before the transfer takes effect → on acceptance, the old Owner becomes a regular Member (or leaves, if that was their intent) and the new Owner is recorded.
- **Alternative Paths:** Receiving Member declines → nothing changes, Owner may offer to someone else.
- **Failure Scenarios:** Owner offers to someone who then leaves the Board before accepting → offer implicitly lapses.
- **Business Rules Applied:** A Board never has zero or multiple Owners at any observable instant — the accept step is atomic with the role swap.
- **Domain Events Raised:** `BoardOwnershipTransferOffered`, `BoardOwnershipTransferred`, `BoardOwnershipTransferDeclined`.
- **Objects Updated:** Two Memberships (old Owner → Member, new Owner ← Member).
- **History Recorded:** "Ownership transferred from [A] to [B]."
- **Security Checks:** Only the current Owner may initiate; only the named target may accept.
- **Race Conditions:** Owner issues two concurrent transfer offers to different people — only one may be outstanding at a time; issuing a second implicitly supersedes the first.
- **Cross-Context Interactions:** None beyond Shared Spaces itself.
- **Future Considerations:** Co-ownership (multiple Owners) is a real future ask for e.g. a married couple jointly running a Family board — deliberately not modeled now to keep the "exactly one Owner" invariant simple; flagged in §I.

### BRD-06 · Leave Board

- **Goal:** Let a Member voluntarily exit a shared space.
- **Trigger:** Member chooses to leave.
- **Preconditions:** If the Member is the Owner, they must transfer ownership first (`BRD-05`) — leaving is blocked otherwise, per the Domain Model invariant.
- **Happy Path:** Non-Owner Member leaves → their Membership ends → their access to future Reminders on that Board stops → their past-authored content and History remain, attributed to them by name (unlike account deletion, leaving a Board does not anonymize anything — they still exist as a User elsewhere on the platform).
- **Failure Scenarios:** Sole Owner attempts to leave without transferring → blocked with a clear required action.
- **Business Rules Applied:** Leaving never deletes content or History (Domain Model rule 17).
- **Domain Events Raised:** `MemberLeft`.
- **Objects Updated:** Membership status → `Left`.
- **Notifications Generated:** A quiet notice to the Owner that a Member left (not to every Member — avoids feeling like a social-network departure announcement).
- **History Recorded:** "‏[Name] left the board."
- **Future Considerations:** A "snooze my membership" (temporary leave, e.g., during a separation) is a plausible sensitive future feature — not built now.

### BRD-07 · Remove Member / Change Member Role

- **Goal:** Let an Owner manage who belongs to their Board and in what capacity.
- **Trigger:** Owner removes a Member, or changes a Member's Role (e.g., Member ↔ Guest).
- **Preconditions:** Actor is the Owner; target is not the Owner themselves (use `BRD-05` to change that).
- **Happy Path:** Owner removes a Member → Membership ends immediately, same effects as Leave but system-initiated → or Owner changes a Member's Role → new Role takes effect immediately for future actions (does not retroactively alter what they already did).
- **Business Rules Applied:** Only the Owner may remove Members or change roles — a Member cannot promote or demote another Member or themselves (identified gap, formalized here — see §I).
- **Domain Events Raised:** `MemberRemoved`, `MemberRoleChanged`.
- **Objects Updated:** Membership status/role.
- **Notifications Generated:** The removed/role-changed person is informed directly; other Members are not broadcast this (privacy-respecting, not a public event).
- **History Recorded:** "‏[Name] removed [Name] from the board" / "changed [Name]'s role to Guest."
- **Cross-Context Interactions:** Trust & Safety — a removal is sometimes the direct consequence of a Report resolution, but the removal action itself always belongs to the Owner (or a Moderator in an extreme platform-level case), never automatically triggered by a Report alone.
- **Future Considerations:** Self-service role requests ("ask to become a full Member") is a plausible low-risk future addition for Guest-role users.

### MEM-08 · Invitation Lifecycle (Send → Accept / Decline / Revoke / Expire)

- **Goal:** Bring a new person into a Board's shared memory, safely and with clear consent on both sides.
- **Trigger:** A Member entitled by the Board's Invitation Policy sends an Invitation via email, SMS, link, or QR code.
- **Preconditions:** Inviter is entitled to invite on this Board; target (if a known identity, not a channel-agnostic link) is not blocked by, and has not blocked, the inviter; target's Privacy Settings permit being invited by this inviter.
- **Happy Path:** Invitation created with an expiring, single-use token → delivered via chosen channel → recipient opens it → if unregistered, they Register first (`IDN-01`) with the invitation context carried through → recipient accepts → a new Membership is granted on the target Board → Invitation marked `Accepted`.
- **Alternative Paths:**
  - Recipient **declines** → Invitation marked `Declined`, inviter may be informed.
  - Inviter **revokes** before acceptance → Invitation marked `Revoked`, link/QR becomes inert.
  - Token **expires** unused → Invitation marked `Expired` automatically by the system, no notification required (avoids nagging).
  - Recipient **already an Active Member** → acceptance is a no-op that simply confirms existing membership (idempotent, not an error).
- **Failure Scenarios:** A block exists between inviter and recipient at send time → creation is rejected outright; a block is created by *either* party *after* the Invitation was sent but before acceptance → the pending Invitation is automatically revoked at the moment the block is created (Domain Model rule 13).
- **Business Rules Applied:** Rules 12–14 from the Domain Model in full: expiry is mandatory, blocking cascades to auto-revoke, blocking never auto-removes existing Membership (that's a separate, Owner-held action).
- **Domain Events Raised:** `InvitationSent`, `InvitationAccepted`, `InvitationDeclined`, `InvitationRevoked`, `InvitationExpired`, `MembershipGranted`.
- **Objects Created:** Invitation; on acceptance, a Membership.
- **Objects Updated:** Invitation status through its lifecycle.
- **Notifications Generated:** Delivery of the invite itself (via the chosen channel — not necessarily a Buzz, since the recipient may not be a User yet); a welcome Buzz to the new Member once Membership is granted.
- **History Recorded:** "‏[Name] joined the board" once accepted — declines/expiries are not board-visible History (they're between the inviter and the system, not shared memory yet).
- **Security Checks:** Token must be single-use, expiring, and unguessable.
- **Privacy Checks:** Target's "who can invite me" setting is checked *before* the Invitation is even created, not just at acceptance — an unwanted invitation should never be generated at all, not just silently blocked at the last step.
- **Cross-Context Interactions:** Identity (registration fork for unregistered invitees), Trust & Safety (block checks both at send and continuously until acceptance), Shared Spaces (Membership creation), Notification Engine (delivery).
- **Race Conditions:** Recipient accepts on two devices simultaneously → idempotent, results in exactly one Membership, not two; inviter revokes at the same instant the recipient accepts → whichever the system's transactional boundary resolves first wins, and the loser sees a clear, honest message ("this invitation was just revoked" or "you're already a member") rather than a silent failure.
- **Idempotency:** Accepting an already-accepted Invitation, or revoking an already-expired one, must be safe no-ops.
- **Audit Requirements:** Full send/accept/decline/revoke/expire trail retained for Trust & Safety and abuse investigation (e.g., invitation spam).
- **Future Considerations:** Bulk invitations (invite a whole existing contact list at once) is additive — same workflow, N instances.

---

## C. REMINDER MANAGEMENT

### RMD-01 · Create Reminder (Manual)

- **Goal:** Capture something worth remembering, in under ten seconds.
- **Trigger:** A Member (or the sole Member of a Personal Board) creates a Reminder.
- **Preconditions:** Actor holds Active Membership on the target Board and that Board's settings permit them to add Reminders.
- **Happy Path:** Title, Recurrence Rule (even a single-occurrence one), optional Entity/Category submitted → Reminder created → Recurrence Rule immediately resolves at least its next Occurrence.
- **Business Rules Applied:** The Reminder notifies its Board's full Active Membership, with no separate audience to select or validate (rule 4); a Reminder always belongs to exactly one Board (rule 3); it always has a Recurrence Rule, even for one-time reminders (rule 5).
- **Domain Events Raised:** `ReminderCreated`, `OccurrenceGenerated` (at least the first).
- **Objects Created:** Reminder, Recurrence Rule, at least one Reminder Occurrence.
- **History Recorded:** "‏[Name] created '[Reminder title]'."
- **Security Checks:** Board-permission check as above.
- **Privacy Checks:** If the target Board is the actor's Personal Board, no other party is ever notified or made aware — full isolation.
- **Cross-Context Interactions:** Notification Engine (schedules the future Buzz for the generated Occurrence).
- **Idempotency:** A double-submitted create request must not produce duplicate Reminders.
- **Future Considerations:** This is the workflow AI-assisted creation (`RMD-02`) ultimately feeds into — they converge on the same "Reminder created" happy path from different starting points.

### RMD-02 · AI-Assisted Reminder Creation

- **Goal:** Let a person describe something naturally ("Emma's birthday every year on 9 July") and have BuzzMe do the structuring — AI reduces the form, it never replaces the person's judgment.
- **Trigger:** User provides natural-language input (typed or, in future, voice/photo/email — see Section H).
- **Preconditions:** Actor holds Active Membership on some Board they intend the reminder to land on (default: their Personal Board, unless they specify or are already inside a shared Board's context).
- **Happy Path:** Natural language parsed → a **Reminder Draft** is proposed (title, date/recurrence, suggested Category/Entity — all extracted, none committed) → shown to the User for confirmation → User confirms (optionally editing first) → Draft converts into a real Reminder via the exact same happy path as `RMD-01` from that point on.
- **Alternative Paths:** User edits the Draft before confirming (e.g., corrects a misread date); User discards the Draft entirely (nothing is created, nothing is shared).
- **Failure Scenarios:** Parsing produces low-confidence or ambiguous results (e.g., unclear recurrence) → the Draft is still shown, but incomplete fields are highlighted for the User rather than guessed silently; parsing fails entirely → User is offered the manual path (`RMD-01`) as a graceful fallback, never a dead end.
- **Business Rules Applied:** Rule 20 — AI-extracted Reminders are always a Draft requiring explicit human confirmation; nothing is ever silently committed or shared with other Board Members before that confirmation.
- **Domain Events Raised:** `ReminderDraftProposed`, `ReminderDraftConfirmed` (→ triggers the same events as `RMD-01`), `ReminderDraftDiscarded`.
- **Objects Created:** A Reminder Draft (see §I — this needs to be formalized as its own light concept); on confirmation, a real Reminder as in `RMD-01`.
- **Privacy Checks:** A Draft is visible **only** to the person who created it until confirmed — never to other Board Members, even if the target Board is shared. This matters concretely: an AI-parsed email or photo could contain information the User doesn't want to share verbatim, so the Draft is a private staging area by design.
- **Cross-Context Interactions:** AI/NLU (parsing) → Reminder Management (draft → confirmed reminder).
- **Idempotency:** Re-submitting the same natural-language input should not create duplicate Drafts if one is already pending confirmation from the same input.
- **Compensation:** A Draft that is never confirmed simply expires after a reasonable window and is discarded — no cleanup burden on the user.
- **Future Considerations:** Duplicate-detection (`AI-04`) runs at Draft time, warning the User if a very similar Reminder already exists, before they confirm.

### RMD-03 · Edit Reminder

- **Goal:** Keep a Reminder accurate as circumstances change.
- **Trigger:** An entitled Member edits an existing Reminder (title, Recurrence Rule, Category, Entity link).
- **Preconditions:** Actor is entitled per the Board's edit policy (default: any Active Member on a shared Board; only the sole Member on a Personal Board).
- **Happy Path:** Change submitted → applied → future, not-yet-materialized Occurrences reflect the new rule; already-generated future Occurrences are re-evaluated against the new rule (see race condition note below); past/resolved Occurrences and History are untouched.
- **Business Rules Applied:** Rule 6 — editing the Recurrence Rule never rewrites the past.
- **Domain Events Raised:** `ReminderUpdated`, `RecurrenceRuleUpdated` (if the schedule itself changed).
- **Objects Updated:** Reminder, Recurrence Rule; possibly regenerates not-yet-due Occurrences.
- **History Recorded:** "‏[Name] updated '[Reminder title]'" with a summary of what changed.
- **Race Conditions:** Editing a Recurrence Rule while an Occurrence generated under the *old* rule is imminently due — the in-flight Occurrence should complete its lifecycle under the rule that produced it; only occurrences not yet generated adopt the new rule, avoiding a Buzz that contradicts what the user just saw on screen.
- **Cross-Context Interactions:** Notification Engine (reschedules pending Buzzes if timing changed).
- **Future Considerations:** "Edit this occurrence only" vs. "edit all future occurrences" (the familiar recurring-calendar-event choice) is an important near-term addition, not deferred as far as some other future items — flagged in §I.

### RMD-04 · Archive / Delete Reminder

- **Goal:** Retire a Reminder that's no longer needed, while protecting the shared memory it already produced.
- **Trigger:** An entitled Member archives or deletes a Reminder.
- **Preconditions:** Actor is entitled per Board edit policy.
- **Happy Path (Archive):** Reminder marked `Archived` → stops generating new Occurrences → all past Occurrences and History remain fully visible and intact, reversible by un-archiving.
- **Happy Path (Delete):** Reminder marked `Deleted` → same immediate effect (no new Occurrences) → but framed to the user as more final; **History and past Occurrences still survive** regardless (rule 9 is unconditional — this is the one place "Delete" does *not* mean "erase," precisely because erasing shared memory contradicts the product's purpose).
- **Business Rules Applied:** Rule 9, without exception, for Reminder-level deletion (contrast with `BRD-04`, Board deletion, which is the sole workflow that does destroy History).
- **Domain Events Raised:** `ReminderArchived`, `ReminderDeleted`.
- **Objects Updated:** Reminder status. Objects Archived: the Reminder itself; its History is retained, not archived-as-in-hidden.
- **Notifications Generated:** None broad-cast — a quiet History entry suffices; deleting a shared Reminder doesn't warrant interrupting everyone.
- **Future Considerations:** This is a place where "Delete" as a word may need product-copy disambiguation from its true behavior (see §I) — users should never be surprised that "deleted" reminders still show up in history/look-back features.

### RMD-05 · Recurring Occurrence Generation (System Process)

- **Goal:** Keep a rolling window of upcoming, concrete Occurrences materialized ahead of time for every active Recurrence Rule, without generating an unbounded, infinite backlog.
- **Trigger:** Scheduled system process (e.g., runs continuously/rolling, not user-initiated).
- **Preconditions:** Parent Reminder is `Active` (not Archived/Deleted); parent Board is `Active` (not Archived).
- **Happy Path:** For every Active Reminder, the system ensures the next N Occurrences (or the next occurrence within a rolling time horizon) exist, generating new ones as time passes and past ones resolve.
- **Business Rules Applied:** Rule 5 — one Recurrence Rule, deterministic resolution; a one-time Reminder generates exactly one Occurrence, ever.
- **Domain Events Raised:** `OccurrenceGenerated`.
- **Objects Created:** Reminder Occurrence(s).
- **Race Conditions / Concurrency:** The scheduler must never generate the *same* Occurrence twice (e.g., if the job runs on overlapping workers) — generation must be idempotent per (Reminder, due-date) pair.
- **Idempotency:** A given (Reminder, calculated due-date) combination must always resolve to exactly one Occurrence, regardless of how many times the generation process runs over it.
- **Future Considerations / Scalability:** At "millions of reminders," a rolling-horizon generation strategy (materialize the next occurrence or two, not the next fifty years) is essential — this is exactly why Occurrence was split into its own Aggregate Root in the Domain Model (§1.3). **Timezone/DST ambiguity is a genuine open gap** — see §I.

### RMD-06 · Resolve Occurrence (Complete / Dismiss / Undo / Snooze)

- **Goal:** Let people mark what actually happened with a due moment — and let a shared responsibility show clearly who handled it.
- **Trigger:** A Member acts on a due or upcoming Occurrence.
- **Preconditions:** Occurrence is in a resolvable state (`Due` or recently `Scheduled`, not already archived-as-history beyond the undo grace window).
- **Happy Path (Complete):** Member marks it done → Occurrence → `Completed`, actor recorded.
- **Happy Path (Dismiss):** Member acknowledges without claiming it's done → Occurrence → `Dismissed`, actor recorded. (Distinct from Complete — "I saw this" vs. "this is handled," an important, real distinction for shared responsibility.)
- **Alternative Path (Undo):** Within a short grace window, the same or another Member reverses a Complete/Dismiss → a **new** History entry records the reversal; the prior entry is never erased.
- **Alternative Path (Snooze — future):** Member defers the Buzz to a later moment without changing the underlying due date — purely a delivery-timing adjustment, not a change to the Occurrence's actual due status.
- **Failure/Edge Scenarios:** Two Members act on the same shared Occurrence near-simultaneously (e.g., both tap "Complete" within seconds) → idempotent: the second actor sees "already completed by [name]," not an error or a duplicate state (Domain Model rule 11).
- **Business Rules Applied:** Rules 9–11 in full.
- **Domain Events Raised:** `OccurrenceCompleted`, `OccurrenceDismissed`, `OccurrenceUndone`, `OccurrenceSnoozed` (future), `OccurrenceMissed` (system-raised when a Due occurrence passes with no action — see below).
- **Objects Updated:** Reminder Occurrence status.
- **History Recorded:** Every transition, including reversals, permanently.
- **Notifications Generated:** Optionally, a light confirmation to other Members that someone handled it ("Mom marked the vet visit done") — this is precisely the shared-accountability value BuzzMe is built to deliver, and should be a Buzz, not silence.
- **Race Conditions:** Simultaneous Complete/Undo from two devices of the same person (e.g., action queued offline on both) → last-write-wins on state, but both actions are preserved in History with timestamps, so nothing is silently lost even if the *current* state reflects only the latest.
- **Idempotency:** Repeated identical action (e.g., a retried network call re-sending "Complete") must not create duplicate History entries — it must be recognized as the same logical action.
- **Cross-Context Interactions:** Notification Engine (a resolved Occurrence should cancel any not-yet-delivered pending Buzzes for it).
- **Future Considerations:** `OccurrenceMissed` — what happens when a Due occurrence passes with zero action from anyone? The Domain Model doesn't currently define this as an explicit terminal state; recommend adding it explicitly (see §I) rather than leaving "due and ignored" as an undefined limbo state.

### ENT-01 · Entity Lifecycle (Create / Archive / Attach / Detach)

- **Goal:** Let recurring reminders cluster meaningfully around a real-world subject (a pet, car, child, house).
- **Trigger:** A Member creates an Entity, archives one, or attaches/detaches a Reminder to/from one.
- **Preconditions:** Actor holds Active Membership on the target Board (Entities belong to exactly one home Board, per the Domain Model's v1 decision).
- **Happy Path (Create):** Member names the Entity, optionally with a type/category (Pet, Vehicle, Person, Property) → Entity created, Active.
- **Happy Path (Attach/Detach):** When creating or editing a Reminder, a Member links it to an existing Entity on the same Board, or unlinks it.
- **Happy Path (Archive):** Member archives an Entity (e.g., the pet passed away, the car was sold) → Entity → `Archived`; all Reminders that reference it keep referencing it, unaffected.
- **Business Rules Applied:** Rule 18 — archiving an Entity never orphans or deletes referencing Reminders.
- **Domain Events Raised:** `EntityCreated`, `EntityArchived`, `ReminderAttachedToEntity`, `ReminderDetachedFromEntity`.
- **Objects Created/Updated/Archived:** Entity created/archived; Reminder's Entity reference updated on attach/detach.
- **History Recorded:** "‏[Name] added 'Rex' as a pet" / "archived 'Rex'."
- **Cross-Context Interactions:** AI/NLU — a well-known Entity dramatically improves natural-language extraction confidence (recognizing "the dog" as a reference to an existing Entity rather than a new one).
- **Future Considerations:** Cross-board Entity sharing (§I) is the main open extensibility question here.

---

## D. NOTIFICATION ENGINE

### NOT-01 · Buzz Lifecycle (Generate → Deliver → Retry → Seen → Dismissed)

- **Goal:** Deliver a timely, friendly, individually-relevant nudge — and never let a failed delivery silently vanish or a retry silently duplicate.
- **Trigger:** An Occurrence approaches or reaches its due moment (system-scheduled), per each current Board Member.
- **Preconditions:** Recipient holds Active Membership on the Occurrence's Board; recipient's Notification Preferences do not fully mute this Board/Reminder at generation time.
- **Happy Path:** Buzz generated for one (Occurrence, Recipient) pair → delivery attempted via the recipient's preferred channel(s) → delivered → recipient sees it (`Seen`) → recipient dismisses it, or it's implicitly resolved when they resolve the underlying Occurrence (`RMD-06`).
- **Alternative Paths:** Recipient has multiple devices → delivered to all, but "Seen" on any one device marks the Buzz `Seen` everywhere (it's about the person, not the device).
- **Failure Scenarios:** Delivery fails (push token invalid, network issue) → `BuzzDeliveryFailed` → **retried** on the same Buzz identity (never by generating a second Buzz for the same Occurrence/Recipient pair) → after a bounded number of retries/time, the Buzz is marked undeliverable via that channel and, if configured, falls back to an alternate channel.
- **Business Rules Applied:** Rules 7–8 — one Buzz per (Occurrence, Recipient); preferences affect delivery only, never the underlying Occurrence.
- **Domain Events Raised:** `BuzzGenerated`, `BuzzDeliveryAttempted`, `BuzzDelivered`, `BuzzDeliveryFailed`, `BuzzSeen`, `BuzzDismissed`.
- **Objects Created:** Notification (Buzz), one per (Occurrence, Recipient).
- **Objects Updated:** Buzz status through its lifecycle.
- **Security Checks:** Only the addressed recipient's own client may mark their Buzz Seen/Dismissed.
- **Privacy Checks:** A Buzz for a Personal Board Reminder is generated only for that one person — structurally impossible to leak to anyone else since it's never generated for any other recipient.
- **Cross-Context Interactions:** Reminder Management (source Occurrence); Identity (channel/token lookup); Trust & Safety indirectly (a Suspended account receives no Buzzes at all).
- **Race Conditions:** A retry firing at the same moment a fresh delivery attempt is scheduled (e.g., a scheduler double-fire) → must resolve to the same Buzz identity, not a duplicate.
- **Idempotency:** This is the single most idempotency-sensitive workflow in the whole system — retries, multi-device delivery, and Reminders shared across multiple Members all multiply the opportunities for accidental duplication; the (Occurrence, Recipient) pair is the canonical de-duplication key.
- **Compensation:** A permanently undeliverable Buzz (all channels exhausted) is not silently dropped — it should still be visible in-app on next open, since "in-app, next time they look" is itself a valid delivery channel of last resort.
- **Future Considerations / Scalability:** At scale, Buzz generation/delivery is the highest-volume workflow in the system by an order of magnitude — this is precisely why it's modeled as its own Aggregate Root, independently scalable from Reminder/Occurrence writes.

### NOT-02 · Notification Preference Changes (Mute Board, Quiet Hours, Channel)

- **Goal:** Let a person control how and when they're interrupted, without affecting anyone else's experience or the underlying facts.
- **Trigger:** User changes a Notification Preference (mutes a specific Board, sets quiet hours, changes preferred channel).
- **Preconditions:** None beyond being the account owner — this is always fully self-service.
- **Happy Path:** Preference changed → takes effect on the next Buzz generation/delivery cycle; no retroactive effect on already-delivered Buzzes.
- **Business Rules Applied:** Rule 8 — muting never touches the Occurrence itself, and never affects other people's delivery for the same shared Reminder.
- **Domain Events Raised:** `NotificationPreferencesChanged`, `BoardMuted`, `BoardUnmuted`.
- **Objects Updated:** Notification Preferences (and, for a Board mute, the Board-scoped override within it).
- **Notifications Generated:** None — this is a purely personal, silent change; other Board Members are never informed that someone muted the Board (would feel surveillance-like and violates the calm, private-by-choice principle).
- **Future Considerations:** Digest mode (batch several Buzzes into one daily summary) is an additive delivery strategy, not a new concept.

---

## E. TRUST & SAFETY / PRIVACY

### PRV-01 · Change Profile Visibility & Privacy Controls

- **Goal:** Let a person control their own discoverability and reachability at all times.
- **Trigger:** User changes Visibility (Private/Public/Hidden/Visible-by-username/Visible-by-invite) or any of "who can invite me / mention me / send me board invitations."
- **Preconditions:** None — always fully self-service, effective immediately.
- **Happy Path:** Setting changed → takes effect immediately for all future Search, Invitation, and Mention checks; never retroactive (an Invitation already pending under looser settings is not auto-revoked by tightening settings afterward — that would be surprising and punitive; the person can instead decline it).
- **Business Rules Applied:** Rule 17 — Visibility governs discoverability and reachability only, never Board access, which is governed solely by Membership.
- **Domain Events Raised:** `PrivacySettingsChanged`.
- **Cross-Context Interactions:** Search & Discovery (immediate effect on findability), Invitations (immediate effect on who may newly invite this person), Mentions (immediate effect on who may newly mention this person).
- **Future Considerations:** Per-Board visibility overrides (e.g., visible-by-username generally, but fully hidden from one specific Board's search) is a plausible fine-grained future control.

### TS-02 · Block User / Unblock User

*(Full lifecycle and cascading effects already specified in detail in Domain Model §3 and §4, rules 13–14; summarized here in workflow form.)*

- **Goal:** Give a person unilateral, immediate protection from another person, without needing anyone's permission or triggering board-governance consequences they didn't ask for.
- **Trigger:** User blocks (or later unblocks) another User.
- **Preconditions:** None — always fully self-service and immediate.
- **Happy Path (Block):** Block created → any pending Invitations between the two are auto-revoked → future Invitations, Mentions, and (per default) Search visibility between the two are cut off in both directions → existing shared Board Memberships are **not** touched.
- **Happy Path (Unblock):** Block removed → normal interaction eligibility resumes; does **not** retroactively resurrect any Invitation that was auto-revoked (the person would need to be re-invited).
- **Domain Events Raised:** `UserBlocked`, `UserUnblocked`, and consequentially `InvitationRevoked` for any caught in the cascade.
- **Objects Created/Updated:** Block relationship created/removed; affected Invitations updated.
- **Notifications Generated:** The blocked party is never explicitly told "you have been blocked" (this is a deliberate, near-universal social-product norm to avoid confrontation) — they simply experience the practical effects.
- **Privacy Checks:** The identity of who blocked whom is never exposed to the blocked party or to any third party.
- **Cross-Context Interactions:** Invitations (cascade revoke), Search (mutual hiding), Mentions (mutual restriction).
- **Race Conditions:** A block created at the exact moment an Invitation between the two is being accepted → the block should win (reject the acceptance), since safety takes precedence over a race with a not-yet-finalized action.
- **Future Considerations:** Board-scoped blocks ("never let this specific person back into this specific Board") noted as a plausible future Owner-held power distinct from personal Block.

### TS-03 · Report User or Content

- **Goal:** Let anyone flag a concern for platform review, without that flag itself being able to punish anyone unilaterally.
- **Trigger:** User submits a Report against a User, Board, Reminder, or Profile.
- **Preconditions:** None — always available.
- **Happy Path:** Report submitted with a reason and reference to the subject → queued `Under Review` → a Moderator reviews and resolves it (`TS-04`).
- **Business Rules Applied:** A Report never itself restricts anything — only a subsequent, distinct Moderation Action can (Domain Model, Report §2).
- **Domain Events Raised:** `ReportSubmitted`.
- **Objects Created:** Report.
- **Privacy Checks:** The reported party is never told who reported them.
- **Future Considerations:** Automated triage (clustering multiple reports about the same subject) is additive analysis, not a new concept.

### TS-04 · Moderation Review & Action

- **Goal:** Resolve a Report fairly and consistently, taking real action only when warranted.
- **Trigger:** A Moderator picks up a submitted Report.
- **Preconditions:** Actor holds a privileged Moderator capability — never a peer User action.
- **Happy Path:** Moderator reviews the Report and its subject → resolves as Actioned (may trigger `IDN-05` Suspend, `BRD-07` Remove Member, or content removal) or Dismissed (no violation found) → Report closed.
- **Domain Events Raised:** `ReportResolved`, `ModerationActionTaken` (if any action is taken).
- **Audit Requirements:** Full trail of every Report's resolution, the Moderator, and the policy basis — this is the platform's core accountability record for Trust & Safety and must be tamper-evident.
- **Cross-Context Interactions:** Identity (Suspend), Shared Spaces (Remove Member), Reminder Management (content removal, if the report concerned a specific Reminder).
- **Future Considerations:** An appeals workflow is a near-term necessity once Suspend/moderation actions are live in production, not a distant future item.

---

## F. HISTORY & ACTIVITY

### HST-01 · Record Activity (Cross-Cutting System Process)

- **Goal:** Ensure every meaningful action across the domain leaves a permanent, attributable trace — the literal mechanism that makes BuzzMe a *shared memory* rather than just a shared to-do list.
- **Trigger:** Any domain event that represents a meaningful action on a Reminder, Occurrence, Board, or Membership (creation, edit, completion, join, leave, etc.).
- **Preconditions:** None — this is not a user-invoked workflow; it is a standing subscription to the rest of the domain's events.
- **Happy Path:** A qualifying domain event fires → a corresponding History/Activity entry is appended, referencing the acting User, the subject, the action, and the timestamp.
- **Business Rules Applied:** Rules 9–10 in full — append-only, never overwritten, survives deletion/archiving of its subject.
- **Domain Events Raised:** History does not raise its own primary business events — it *consumes* the events raised by other workflows. Treat any distinct "history was recorded" signal as an internal implementation detail, not a business event, to avoid double-counting in the Event Catalogue (§J notes this explicitly).
- **Objects Created:** History/Activity entries (append-only).
- **Cross-Context Interactions:** Subscribes to Shared Spaces, Reminder Management, and (for Audit specifically) Identity and Trust & Safety events.
- **Race Conditions:** Two events about the same subject arriving out of order (e.g., due to distributed processing) → entries must be ordered by a reliable logical/business timestamp, not by arrival order, so the ledger reads coherently even under eventual-consistency delivery.
- **Idempotency:** The same underlying event redelivered (e.g., an at-least-once event bus) must not produce a duplicate History entry — each event needs a stable identity for de-duplication.
- **Future Considerations:** This is the eventual backbone of a "look-back"/memory-book feature (Product Vision §11) — worth keeping deliberately rich and complete now, even before that feature exists, since History can't be retroactively reconstructed for past events once they're gone.

### HST-02 · View / Restore History

- **Goal:** Let people look back at what happened, and — where a reversal is still within its grace window — undo a recent action.
- **Trigger:** Member views a Board's or Reminder's History; optionally invokes an Undo (see `RMD-06`).
- **Preconditions:** Actor holds Active Membership on the relevant Board (History visibility is scoped exactly like Reminder visibility — Membership-gated, never broader).
- **Happy Path:** History is read-only and chronological; "Restore" in the sense of reversing a recent action is really `RMD-06`'s Undo path, not a distinct History-context action — History itself is never edited, only appended to.
- **Business Rules Applied:** Same Membership-based visibility rule as Reminders (rule 16) — a former Member who has left no longer sees the Board's ongoing History, even though their past contributions remain attributed within it for current Members.
- **Future Considerations:** Exporting a Board's History (e.g., "our family's year in review") is a strong, low-risk, high-delight future feature building directly on this ledger.

---

## G. SEARCH & DISCOVERY

### SRCH-01 · Find User (with Privacy Filtering)

- **Goal:** Let people find each other to invite or connect, without ever exposing someone who doesn't want to be found.
- **Trigger:** User searches by name, username, or contact match.
- **Preconditions:** None to *search*; results are filtered per each candidate's own Privacy Settings.
- **Happy Path:** Query submitted → candidates matched → each candidate is included or excluded based on their individual Visibility setting evaluated against the searcher's relationship to them (stranger vs. already-connected) → results returned.
- **Business Rules Applied:** A `Private Account` or `Hidden from Search` setting removes a person from results entirely, regardless of query match quality; `Visible by Username Only` requires an exact username match, not fuzzy name search; a blocked relationship (either direction) always excludes, overriding any Visibility setting.
- **Privacy Checks:** This workflow *is* the privacy check, applied per-candidate, every time — there is no "search index" shortcut that could bypass current settings, since settings can change between searches.
- **Cross-Context Interactions:** Trust & Safety (block exclusion), Identity (Privacy Settings).
- **Future Considerations:** Contact-list matching ("find people you already know") must apply the exact same per-candidate filtering — it is not a privileged, unfiltered path just because the searcher already has the contact's phone number.

### SRCH-02 · Find Board / Public Community (Future)

- **Goal:** Let people discover and join open, public shared spaces once that Visibility tier exists.
- **Trigger:** User searches or browses Public Community boards.
- **Preconditions:** Board Visibility is `Public Read-Only` or `Public Community`.
- **Happy Path:** Board appears in results → for Public Read-Only, viewing requires no Membership; for Public Community, joining requires a distinct **Join** action (see §I — this is a genuine gap, since the current model only has Invitation-driven Membership, not self-service joining).
- **Future Considerations:** This entire workflow is gated on resolving the Board Join Policy gap identified in §I before it can be built responsibly.

---

## H. FUTURE AI CHANNELS

*(All three converge on the same Draft → Confirm pattern as `RMD-02`; described briefly here as input-channel variants, not separate core workflows.)*

### AI-01 · Import Reminder from Photo (Future)

- **Goal:** Extract a reminder from a photographed invitation, appointment card, or document.
- **Trigger:** User attaches a photo and requests extraction.
- **Happy Path:** Photo → OCR/vision parsing → Reminder Draft proposed, with the photo itself retained as an Attachment on the resulting Reminder (provenance preserved) → same confirmation step as `RMD-02`.
- **Privacy Checks:** The photo is processed only for the requesting User's own Draft; never shared or visible to others until/unless the resulting Reminder is confirmed onto a shared Board.
- **Future Considerations:** This is the concrete reason Attachment was modeled with explicit provenance semantics in the Domain Model (§2).

### AI-02 · Import Reminder from Email (Future)

- **Goal:** Extract a reminder from a forwarded or connected email (e.g., a vet appointment confirmation).
- **Happy Path:** Email content → NLU parsing → Reminder Draft proposed → same confirmation step.
- **Privacy/Security Checks:** Requires explicit User consent to access/parse email content — never passive/always-on scanning without clear opt-in; scope of access should be as narrow as the feature requires (e.g., only forwarded messages, not full inbox access, unless the user explicitly grants more).

### AI-03 · Voice Reminder Creation (Future)

- **Goal:** Let a person speak a reminder naturally.
- **Happy Path:** Voice → transcription → same NLU parsing pipeline as `RMD-02`'s text path → Reminder Draft proposed → same confirmation step.
- **Future Considerations:** Converges entirely into the existing text-based Draft pipeline once transcribed — no new domain concept needed, only a new input adapter.

### AI-04 · Duplicate Reminder Detection (Future)

- **Goal:** Gently warn when a new Reminder (manual or AI-drafted) looks like it duplicates an existing one on the same Board.
- **Trigger:** Runs automatically at Reminder/Draft creation time.
- **Happy Path:** New Reminder/Draft compared against existing active Reminders on the target Board → if a strong match is found, the User is shown a gentle prompt ("You already have a similar reminder — create anyway, or view the existing one?") → User decides; the system never blocks or auto-merges without consent.
- **Business Rules Applied:** Never silently merges or discards a User's input — mirrors the AI confirmation principle (rule 20) exactly: detection informs, humans decide.

---

## I. Domain Model Challenges & Gaps Identified

Working through every workflow end-to-end surfaced real gaps in [DOMAIN_MODEL.md](./DOMAIN_MODEL.md). These are recommended amendments, not just observations:

1. **Reminder Draft needs to be a first-class concept.** It was named in the glossary ("Draft") but never given ownership, a lifecycle, or invariants. Recommend formalizing it as its own lightweight concept: owned solely by its creator until confirmed or discarded; expires automatically if left unconfirmed; never visible to any other Board Member, even on a shared Board, before confirmation. This closes a real privacy gap — without it, an AI-parsed email or photo could theoretically leak partial/incorrect information to a shared Board before the human ever reviewed it.

2. **Ownership Transfer should be a two-step handshake, not a unilateral action.** The original model let the Owner unilaterally reassign ownership. Real-world implication: an Owner could dump the Owner role (with its responsibilities) onto someone who never agreed to take it on. Recommend the offer/accept pattern described in `BRD-05`.

3. **Membership Role Change was implicit, not explicit.** The Domain Model listed Role as a Membership attribute but never named the transition or its authorization rule as its own thing. Now formalized in `BRD-07`: only the Owner may change roles; a Member cannot self-promote or promote another Member.

4. **Recurrence timezone ambiguity is unresolved.** "Every year on 9 July" needs an anchor timezone to resolve deterministically for members in different regions — otherwise two family members in different time zones could see a Buzz land on visibly different calendar days for what's meant to be the same shared moment. Recommend the Domain Model be amended so every Reminder carries a reference timezone (default: the Board's, or the creator's at time of creation), resolved once and stored, not recomputed per viewer.

5. **Buzz needs an explicit `Failed`/`Retrying` transient state**, and the model must be explicit that retries operate on the *same* Buzz identity — never by generating a new Buzz for the same (Occurrence, Recipient) pair. Left unstated, a naive retry implementation could duplicate notifications, directly undermining the "friendly, not spammy" promise in the Product Vision.

6. **`OccurrenceMissed` is an undefined terminal state.** What happens when a Due Occurrence passes with zero action from anyone? The Domain Model's lifecycle (`Scheduled → Due → Acknowledged/Completed/Dismissed/Missed`) already lists Missed as a possible state but never defines when or how it's reached. Recommend: a Due Occurrence transitions to `Missed` automatically after a defined grace window with no action, generating its own quiet History entry — "due and not acted upon" should be a known, visible fact, not an undefined limbo.

7. **Board Invitation Policy's values were never enumerated.** Recommend formalizing as: `OwnerOnly`, `AnyMember`, with `OwnerAndDesignatedMembers` flagged as a plausible near-future addition.

8. **Public Community boards expose a missing "Join" concept.** The current model only has Invitation-driven Membership. A self-service Public Community (Vision §12, Domain Model §8 future roadmap) needs either an "Open Join" (instant Membership, no approval) or "Request to Join" (Owner/Moderator approval) path — a distinct Board Join Policy, separate from and complementary to the existing Invitation Policy. This must be resolved before `SRCH-02` can ship.

9. **Platform Suspension of a sole Board Owner is operationally undefined.** If Trust & Safety suspends a User who is the sole Owner of shared Boards with other active Members, those Boards are left in a state the current model doesn't address (the Owner still technically "exists" but can't act). Recommend: a platform-triggered forced ownership transfer to the next-longest-standing active Member, with the other Members notified — falling back to freezing the Board read-only, pending manual Trust & Safety resolution, only if no other Member exists.

10. **Co-ownership was deliberately excluded but is a near-term real request.** Couples running a shared Family board will naturally expect joint ownership. The current "exactly one Owner" invariant is the right default for simplicity, but should be revisited as a considered, explicit extension (not an accidental erosion) once real usage data confirms the need — noted here so it isn't "discovered" later as a surprise limitation.

11. **Board and Account deletion both lack a grace/undo window.** Both `BRD-04` and `IDN-06` are currently single-step, irreversible actions the moment they're confirmed. Given these destroy shared memory other people may still value, recommend a short (e.g., 14-day) soft-delete grace period before permanent purge, consistent with the platform's own "hard-to-reverse action" risk classification.

12. **"Delete Reminder" doesn't mean what it sounds like.** Per rule 9, deleting a Reminder never deletes its History or past Occurrences — but the word "Delete" naturally implies erasure to a typical user. This isn't a domain-model gap so much as a glossary/copy risk flagged here so Product/UX addresses it deliberately (e.g., through in-product language, not by weakening the underlying rule).

---

## J. Business Event Catalogue

*Grouped by bounded context. "Fired When" is the business moment, not a technical trigger. History (§F) is not listed as a producer of its own primary events — it is a consumer of everything below, appending an Activity entry for each.*

### Identity & Access
| Event | Fired When |
|---|---|
| `AccountRegistered` | A new person completes registration. |
| `AccountVerificationRequested` | A verification code/link is (re)issued. |
| `AccountVerified` | The person confirms ownership of their email/phone. |
| `LoginSucceeded` / `LoginFailed` | Each login attempt resolves. |
| `AccountRecoveryRequested` / `AccountRecovered` | Recovery flow issued / completed. |
| `AccountDeactivated` / `AccountReactivated` | The person pauses / resumes their own account. |
| `AccountSuspended` | Trust & Safety enforces a restriction. |
| `AccountDeletionRequested` / `AccountDeleted` | Deletion confirmed / permanently completed. |
| `ProfileUpdated` | Any Profile field changes. |
| `PrivacySettingsChanged` | Any visibility/reachability control changes. |
| `NotificationPreferencesChanged` | Any delivery preference changes. |

### Shared Spaces (Boards & Membership)
| Event | Fired When |
|---|---|
| `BoardCreated` | A new Board is established. |
| `BoardRenamed` / `BoardSettingsUpdated` | Name or policy fields change. |
| `BoardVisibilityChanged` | Visibility tier changes. |
| `BoardArchived` / `BoardUnarchived` | Board paused / resumed. |
| `BoardDeleted` | Board permanently removed. |
| `BoardOwnershipTransferOffered` | Owner proposes a new Owner. |
| `BoardOwnershipTransferred` / `BoardOwnershipTransferDeclined` | Target accepts / declines. |
| `MemberLeft` | A Member voluntarily exits. |
| `MemberRemoved` | Owner removes a Member. |
| `MemberRoleChanged` | Owner changes a Member's Role. |
| `MembershipGranted` | A new active Membership is established (via invite acceptance or board creation). |
| `InvitationSent` / `InvitationAccepted` / `InvitationDeclined` / `InvitationRevoked` / `InvitationExpired` | Each stage of the Invitation lifecycle. |

### Reminder Management
| Event | Fired When |
|---|---|
| `ReminderCreated` | A confirmed Reminder comes into existence. |
| `ReminderDraftProposed` / `ReminderDraftConfirmed` / `ReminderDraftDiscarded` | AI-assisted creation stages. |
| `ReminderUpdated` / `RecurrenceRuleUpdated` | A Reminder or its schedule changes. |
| `ReminderArchived` / `ReminderDeleted` | Reminder retired (History always survives). |
| `OccurrenceGenerated` | A concrete due instance is materialized. |
| `OccurrenceCompleted` / `OccurrenceDismissed` / `OccurrenceUndone` | An Occurrence is resolved or a resolution is reversed. |
| `OccurrenceSnoozed` (future) | Delivery deferred without changing due status. |
| `OccurrenceMissed` | A Due Occurrence passes unresolved past its grace window. |
| `EntityCreated` / `EntityArchived` | An Entity is added / retired. |
| `ReminderAttachedToEntity` / `ReminderDetachedFromEntity` | A Reminder is linked / unlinked to an Entity. |
| `AttachmentAdded` | A supporting file is added to a Reminder/Occurrence. |

### Notification Engine
| Event | Fired When |
|---|---|
| `BuzzGenerated` | A notification instance is created for one (Occurrence, Recipient) pair. |
| `BuzzDeliveryAttempted` / `BuzzDelivered` / `BuzzDeliveryFailed` | Each delivery attempt resolves. |
| `BuzzSeen` / `BuzzDismissed` | Recipient views / dismisses it. |
| `BoardMuted` / `BoardUnmuted` | A per-Board delivery override changes. |

### Trust & Safety
| Event | Fired When |
|---|---|
| `UserBlocked` / `UserUnblocked` | A directional safety control changes. |
| `ReportSubmitted` | Someone flags a User/Board/Reminder/Profile. |
| `ReportResolved` | A Moderator closes a Report. |
| `ModerationActionTaken` | A Report resolution results in a concrete action (suspend/remove/etc.). |

### Search & Discovery
No dedicated domain events — Search is a read-time query against current state (Profile, Privacy Settings, Board Visibility), not a state-changing workflow. It reacts to events from other contexts rather than producing its own.

### AI / NLU (Future)
| Event | Fired When |
|---|---|
| `PhotoImportRequested` / `PhotoParsed` | Photo submitted / extraction completes. |
| `EmailImportRequested` / `EmailParsed` | Email submitted / extraction completes. |
| `VoiceInputReceived` / `VoiceTranscribed` | Voice submitted / transcription completes. |
| `DuplicateReminderSuspected` | The duplicate-detection check flags a likely match. |

---

*This document, together with [PRODUCT_VISION.md](./PRODUCT_VISION.md) and [DOMAIN_MODEL.md](./DOMAIN_MODEL.md), forms the complete pre-implementation foundation for BuzzMe. Every future API, event schema, and screen should be traceable to a workflow, rule, or event named here — and every gap closed in Section I should be reflected back into the Domain Model before engineering treats it as settled.*
