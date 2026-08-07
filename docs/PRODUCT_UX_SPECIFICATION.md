# BuzzMe — Product UX Specification

*Translates the finalized [PRODUCT_VISION.md](./PRODUCT_VISION.md), [DOMAIN_MODEL.md](./DOMAIN_MODEL.md), [BUSINESS_BEHAVIOR_MODEL.md](./BUSINESS_BEHAVIOR_MODEL.md), [INFORMATION_ARCHITECTURE.md](./INFORMATION_ARCHITECTURE.md), and [EVENT_STORMING.md](./EVENT_STORMING.md) into a behavioral specification of every screen. This is the contract between Product, UX, and Frontend Engineering — not a visual design. No colours, no layouts, no components — behavior only.*

---

## 0. How This Document Is Organized

Screens repeat the same handful of behaviors constantly — how offline works, what animation feels like, how confirmations are phrased. Section 1 defines those conventions once. Every screen's table below states only what's specific to it, and references §1 for anything that just follows the baseline. This mirrors the Information Architecture's own instinct to never repeat what a default already handles well.

Sections 2–8 cover every screen grouped by product surface. Section 9 covers interaction patterns that cut across screens (swipe, long-press, undo, etc.) rather than repeating them per screen. Section 10 covers offline behavior in full. Section 11 is the microcopy library. Section 12 is a consolidated accessibility review. Section 13 — friction, simplification, and merge/removal recommendations — closes the document.

---

## 1. Global Conventions

**Accessibility baseline** (applies to every screen unless a screen's table says otherwise): text reflows at any Dynamic Type size, never clips or truncates meaningfully; every icon-only control has a real accessible label; minimum touch target 44×44pt (iOS) / 48×48dp (Android); status is never colour-only (paired with an icon or word); reading/focus order is top-to-bottom, left-to-right, with the screen's single most important action reachable first.

**Offline baseline:** anything already synced is fully readable from local cache. Any write (create, edit, complete, dismiss, archive) is applied optimistically to the local view immediately and queued for sync — the person never watches a spinner for their own action. Anything that inherently requires a live connection to a person or an external provider (inviting someone, transferring ownership, deleting a Board/Account, AI parsing, push delivery, live search) is disabled or degrades gracefully offline — see §10 for the full breakdown.

**Animation baseline:** transitions are quick (150–250ms) and calm — no bounce, no confetti, nothing that reads as "gamified." A new item entering a list gently fades/slides into its correct chronological position. Completing an Occurrence gets one small, satisfying checkmark tick — a single quiet acknowledgment, never a celebration animation.

**Loading baseline:** lists use skeleton placeholders that match the eventual layout, never a centered spinner that causes a layout jump. Anything depending on the AI/NLU provider gets its own distinct "thinking" treatment (§11) rather than a generic loading state, because that specific wait sits on the product's core speed promise.

**Permission requests baseline:** every permission (notifications, microphone, camera, contacts) is requested at the exact moment it's needed, with a one-line reason in plain language — never bundled into an upfront onboarding permissions dump.

**Confirmation dialog baseline:** friction is proportional to consequence. Reversible, low-stakes actions (Archive, Mute, Complete, Dismiss, Leave-when-not-sole-Owner) get **no** confirmation dialog. Irreversible or other-people-affecting actions (Delete Reminder, Delete Board, Remove Member, Delete Account) get a confirmation that names the specific thing being affected, never a generic "Are you sure?"

---

## 2. Pre-Authentication Surfaces

### 2.1 Splash

| Field | Detail |
|---|---|
| Purpose | Bridge cold start to the correct destination while session state resolves. |
| Primary user goal | Get out of the way — ideally invisible. |
| Appears when | App cold launch only. |
| Entry points | OS launch (icon tap, deep link, notification tap while app is fully closed). |
| Exit points | Auto-routes to Home (valid session) or Welcome/Login (no session) — never a manual action. |
| Primary action | None — no interactive element. |
| Displayed information | Logo/wordmark only. |
| Empty / Loading state | The screen itself *is* the loading state; target under 1 second. |
| Error state | Session check fails → route to Welcome/Login as the safe default, no error shown here. |
| Offline state | Session resolves from local cache if a prior valid session exists; otherwise routes to Login, which itself explains the connectivity need. |
| Accessibility | Announces app name once; no focus trap since there's nothing to interact with. |
| Permissions | None requested here. |
| Events raised | None. |

### 2.2 Welcome / "Onboarding"

| Field | Detail |
|---|---|
| Purpose | The only pre-account screen — a single line of positioning plus a choice, not a tutorial. |
| Primary user goal | Decide: new here, or returning. |
| Appears when | First launch with no session, and no deep-linked Invitation in progress. |
| Entry points | Splash (no session). |
| Exit points | → Registration, or → Login. |
| Primary action | "Get Started" (→ Registration). |
| Secondary actions | "I already have an account" (→ Login). |
| Displayed information | Wordmark, one short line ("Help people remember together"), the two actions. No carousel, no swipeable slides, no feature list. |
| Empty / Loading / Error / Offline | Static content — none of these states meaningfully apply. |
| Accessibility | Both actions reachable and equally weighted for screen readers. |
| Animation | A single calm fade-in on load; nothing else. |
| Business rules enforced | None. |
| Events raised | None. |

*See §13.1 for a recommendation to consider removing this screen entirely.*

### 2.3 Registration

| Field | Detail |
|---|---|
| Purpose | Capture the minimum needed to create an account. |
| Primary user goal | Get in, fast. |
| Appears when | From Welcome's "Get Started," or from an unresolved Invitation deep link for a person with no account. |
| Entry points | Welcome, Invitation link. |
| Exit points | → Verification. |
| Primary action | "Create account." |
| Secondary actions | Link to Login ("Already have an account?"), required legal links (Terms/Privacy — minimal, not a blocking read-through). |
| Displayed information | Name, email or phone, password (or platform passkey/biometric where available). Nothing else — no profile photo, no username choice yet (deferred to Profile, first-run optional). |
| Empty state | N/A — this is itself the input state. |
| Loading state | Inline spinner on the submit button only; fields remain editable until submission completes. |
| Error state | Inline, per-field validation (invalid email format, weak password) — never a full-page error; a duplicate-account attempt routes to a friendly "looks like you already have an account" prompt toward Login, not a blocking error. |
| Offline state | Submit disabled with a plain inline message ("Connect to the internet to create your account") — registration cannot be queued, since it requires server-side uniqueness checks. |
| Accessibility | Standard form field labeling; password field has a visibility toggle. |
| Permissions | None yet. |
| Business rules enforced | Domain Model rule: default Privacy Settings applied on success are the most private reasonable default — invisible to the user at this step, not a choice presented here. |
| Events raised | `AccountRegistered`. |

### 2.4 Verification

| Field | Detail |
|---|---|
| Purpose | Confirm ownership of the email/phone just registered. |
| Primary user goal | Prove it's really them, quickly. |
| Appears when | Immediately after Registration. |
| Entry points | Registration only. |
| Exit points | → Home (success), stays on screen (wrong/expired code). |
| Primary action | Code entry (auto-submits on the final digit where platform conventions support it). |
| Secondary actions | "Resend code" (rate-limited, with a visible short cooldown), "Use a different email/phone." |
| Displayed information | Which channel the code was sent to, the code input, resend affordance. |
| Loading state | Brief inline check on submit. |
| Error state | Wrong code: inline shake + "That code isn't right — try again," field stays focused. Expired code: "This code's expired — send a new one" with resend already focused as the next action. |
| Offline state | Cannot verify without connectivity; same disabled-submit treatment as Registration. |
| Accessibility | Code input announces each digit entered; auto-submit is also reachable via an explicit submit action for anyone whose input method doesn't trigger it automatically. |
| Success feedback | Immediate transition to Home — no intermediate "Welcome!" splash screen; arriving at Home already populated with its warm empty state *is* the welcome. |
| Business rules enforced | On success, the Account Provisioning saga (Personal Board, Privacy Settings, Notification Preferences) fires — if this takes longer than an instant, a brief "Getting things ready…" beat is shown rather than a blank pause. |
| Events raised | `AccountVerified` → (via policy) `BoardCreated` (Personal), `MembershipGranted`, `PrivacySettingsInitialized`, `NotificationPreferencesInitialized`. |

### 2.5 Login

| Field | Detail |
|---|---|
| Purpose | Resume access for a verified person. |
| Primary user goal | Get back in without friction. |
| Entry points | Welcome, Splash (expired/no session), sign-out flow. |
| Exit points | → Home. |
| Primary action | "Log in." |
| Secondary actions | "Forgot password?", "Create account," biometric/passkey shortcut if previously enrolled. |
| Error state | Generic failure message regardless of which field was wrong (security rule — never confirm whether an email exists). A `Suspended` account gets a distinct, non-generic message pointing to an appeal path, not the generic failure copy. |
| Offline state | A previously valid session resumes without requiring fresh login (biometric unlock at most); a fresh login attempt is disabled offline with a plain message. |
| Business rules enforced | Only `Active` accounts may establish a session (`Suspended`/`Deleted` blocked, `Deactivated` offered an inline "Reactivate" instead of a dead end). |
| Events raised | `LoginSucceeded` / `LoginFailed`. |

### 2.6 Forgot Password / Account Recovery

| Field | Detail |
|---|---|
| Purpose | Restore access without ever confirming or denying account existence. |
| Primary user goal | Get back in when the credential itself is lost. |
| Entry points | Login. |
| Exit points | → Login (after success), or stays with a "check your email/phone" state. |
| Primary action | Submit email/phone → identical "If that account exists, you'll get a link" message every time. |
| Secondary actions | Resend link (rate-limited). |
| Error state | Expired/used token: "This link's expired — request a new one," one tap to resend. |
| Business rules enforced | Recovery never bypasses proof of channel ownership; a second token issued invalidates the first. |
| Events raised | `AccountRecoveryRequested`, `AccountRecovered`. |

---

## 3. Home & Reminders

### 3.1 Home (Today · Coming Up · A Few Things From Before)

| Field | Detail |
|---|---|
| Purpose | The single daily hub — everything due or coming up, across every Board, in one calm list. |
| Primary user goal | Glance, understand, and act within seconds. |
| Appears when | Default landing screen after auth; the Home tab. |
| Entry points | Bottom tab, app relaunch, "back" from any drill-in screen. |
| Exit points | Reminder Detail (tap a row), Create sheet (tap capture box or center tab), Board Detail (tap an optional board-avatar chip), Search, Profile. |
| Primary action | Type or speak into the always-visible, already-focused capture box. |
| Secondary actions | Tap a row → Reminder Detail; tap Complete/Dismiss directly from the row (see §9 for exact affordance); tap a board chip → jump to that Board. |
| Displayed information | Capture box; **Today** (due today, time-ordered); **Coming Up** (next ~7 days, lighter visual weight); **A few things from before** (collapsed by default — missed items, low-key styling, never red). Each row: icon, title, human "when," small participant/entity indicators, and a quiet "✓ Done — Mom" style credit if already resolved by someone. |
| Empty state | First-run: capture box front and center, one example prompt beneath it ("Try: 'Mum's birthday every year on 12 March'"), a soft one-line nudge to create a first Board — no tutorial carousel, no dense onboarding content. |
| Loading state | Skeleton rows for Today/Coming Up; the capture box is interactive immediately, never blocked by feed loading. |
| Error state | If the feed genuinely fails to load, an inline retry row — never a blank screen. |
| Offline state | Fully readable from cache; creating/completing/dismissing all work optimistically and queue for sync (§10). |
| Accessibility | Capture box is the first focus target and first thing announced; each row announces title, when, participants, and status as one coherent phrase. |
| Permissions | Notification permission requested contextually the first time a reminder with future delivery is created, not at first launch. |
| Navigation behavior | Home tab highlighted; drilling into anything pushes forward, back always returns exactly here. |
| Animation | New/updated rows fade-slide into correct chronological position; completing shows the one quiet checkmark tick (§1), then the row stays visible in its "done" state for the rest of the day before quietly rolling off on the next visit. |
| Success feedback | Instant, optimistic — the row updates the moment the person acts, not when the server confirms. |
| Failure feedback | If a queued action ultimately fails to sync, a small inline "Couldn't save — tap to retry" on that specific row only — never a blocking modal for someone else's screen. |
| Business rules enforced | Only Occurrences from Boards the person currently holds Active Membership on; a Board mute affects Buzz delivery only, never removes items from this list (Business Behavior Model rule 8). |
| Events raised | Per interaction: `CompleteOccurrence`/`DismissOccurrence`/`UndoOccurrenceResolution` commands; navigating to Create raises the AI Lifecycle chain (§3.3–3.4). |

*"Today," "Coming Up," and "A few things from before" are sections of this one screen, not separate screens — consistent with the Information Architecture. See §13.10 for a recommendation to reconsider even this section split.*

### 3.2 Reminder Detail (incl. History)

| Field | Detail |
|---|---|
| Purpose | The full picture of one Reminder, and the place to act on or edit it. |
| Primary user goal | Confirm details, resolve it, or change it. |
| Entry points | Any row tap — Home, Board Detail, Search results, a tapped notification. |
| Exit points | Back to wherever it was entered from; Edit → Manual Reminder Editor. |
| Primary action | Complete / Dismiss the current due Occurrence — large, clearly labeled buttons (not icon-only), positioned in the thumb zone. |
| Secondary actions | Edit, Archive, Delete, Share, (future) Snooze. |
| Displayed information | Title, full recurrence in plain language ("Every year on 9 July," not a cron-style string), participants, linked Entity if any, notes/attachments, and a **History** section below the fold — every past Occurrence with who resolved it and how. |
| Empty state | History empty state for a brand-new Reminder: "No history yet — this is its first time." |
| Loading state | Title/when render instantly from the data already known by the list that navigated here; only History/attachments lazy-load with a skeleton. |
| Error state | A failed action shows inline retry on the specific button pressed, never a separate error dialog. |
| Offline state | Fully viewable from cache; actions queue exactly as on Home. |
| Accessibility | Complete/Dismiss are labeled buttons, never icon-only; destructive actions require an explicit confirmation naming the Reminder's title. |
| Animation | Complete triggers the same tick as Home; History entries append with a gentle fade, never a jarring reflow. |
| Success feedback | Immediate in-place state change plus the tick animation. |
| Failure feedback | "Already done by [Name]" toast if a race is lost (§9, Idempotent Complete) rather than an error. |
| Business rules enforced | Idempotent completion (Business Behavior Model rule 11); edit permission per the Board's edit policy; deleting never deletes History (rule 9) — see §11 for how this is worded so "Delete" doesn't feel dishonest. |
| Events raised | `OccurrenceCompleted`/`Dismissed`/`Undone`, `ReminderUpdated`/`Archived`/`Deleted`. |

### 3.3 Create Reminder (Quick Capture)

| Field | Detail |
|---|---|
| Purpose | The fastest possible path from a thought to a remembered thing. |
| Primary user goal | Say what to remember and be done in under 10 seconds. |
| Appears when | It's either already visible (Home's embedded capture box) or opened as a sheet from anywhere via the center Create tab. |
| Entry points | Home (embedded), center tab (from any screen), Board Detail (pre-scoped to that Board). |
| Exit points | AI Confirmation Card (success), Manual Reminder Editor (explicit fallback or parse failure). |
| Primary action | Type. |
| Secondary actions | Tap the mic icon to speak instead (same field, second input method — never a separate feature); paste text (works identically to typing, see §9); "Enter details manually" link, always visible, never hidden. |
| Displayed information | The input field itself; once text exists, a subtle "thinking" indicator appears within roughly a second. |
| Loading state | The AI "thinking" micro-state (§11 for copy) — capped at a short maximum wait, after which the flow auto-offers the manual editor rather than leaving the person staring at a spinner. |
| Error state | Parsing fails entirely → auto-falls forward into the Manual Reminder Editor, pre-filled with the raw typed text as the title — never a dead-end error screen. |
| Offline state | Typing/voice input still works; since NLU parsing needs connectivity, the raw text is queued and parsed automatically once reconnected (preferred over a degraded blind guess — see §10). |
| Accessibility | Mic icon triggers native platform speech-to-text and is fully usable with system accessibility voice tools; field itself supports full screen-reader dictation. |
| Permissions | Microphone permission requested only on first mic-icon tap, with a one-line reason. |
| Business rules enforced | Nothing commits from this screen alone — see AI Confirmation Card. |
| Events raised | `SubmitTextForParsing`/`SubmitVoiceForParsing` → `TextParsed`/`VoiceTranscribed` → `ReminderDraftProposed`. |

### 3.4 AI Confirmation Card

| Field | Detail |
|---|---|
| Purpose | The single most important micro-screen in the product — a human confirms before anything is created or shared. |
| Primary user goal | Glance, confirm it's right, tap once. |
| Appears when | Immediately after successful parsing, as an inline card growing out of the capture box — never a full-screen navigation away from where the person just was. |
| Entry points | Create Reminder only. |
| Exit points | Add (→ Reminder created, same happy path as manual creation), Discard (→ back to an empty capture box), tap a field (→ inline correction, stays on this card). |
| Primary action | **Add** — one tap commits exactly as shown. |
| Secondary actions | Tap any individual field (date, board, entity, category) to correct just that field inline, without leaving the card; **Discard**, styled as low-emphasis since this is a low-stakes cancel, not a destructive action. |
| Displayed information | The Reminder rendered exactly as it will actually look in a list — title, when, board, entity/category icons — because it already *is* the finished thing, not a form describing one. |
| Empty / Loading state | Covered by Create Reminder's thinking state (§3.3). |
| Error / low-confidence state | An uncertain field (e.g., ambiguous recurrence) is visually flagged and invites a tap to fix, but never blocks the Add action — the person can always accept as-is. |
| Duplicate warning | If duplicate-detection fires, a small, non-blocking inline note appears ("Looks similar to 'Vet checkup' — add anyway, or view the existing one?") without gating Add. |
| Offline state | Only reachable after a successful parse, which required connectivity — see §3.3 for the offline-queued-parse path instead. |
| Accessibility | Every editable field is individually reachable and labeled for screen readers ("Date: 9 July, double-tap to edit"). |
| Animation | The card visibly grows out of the capture box — the input becoming the reminder, not replacing it. |
| Success feedback | Card collapses, Home's list updates in place with the new item already visible. |
| Business rules enforced | Rule 20 — nothing AI-parsed ever commits without this explicit confirmation. |
| Events raised | `ReminderDraftConfirmed` → `ReminderCreated`, or `ReminderDraftDiscarded`. |

### 3.5 Manual Reminder Editor

| Field | Detail |
|---|---|
| Purpose | Precise, explicit control — reached only by deliberate choice or AI failure, never the default path. |
| Primary user goal | Get exactly what they mean when natural language wasn't the right tool. |
| Entry points | "Enter details manually" from Create Reminder or the Confirmation Card; a failed/opted-out parse. |
| Exit points | Save (→ Reminder created/updated), Cancel. |
| Primary action | Save. |
| Secondary actions | Cancel. |
| Displayed information | Title and "when" up front (the only two fields shown expanded by default); Board, participants, entity, category, and notes are present but collapsed/optional, pre-filled with the smartest available default (Board from current context, category inferred from the title where possible) rather than presented as a flat, equally-weighted form. |
| Error state | Inline per-field validation. |
| Offline state | Fully creatable offline — this path has no AI dependency — and queues for sync exactly like any other write. |
| Accessibility | Standard form labeling, logical tab/focus order matching the visual field order. |
| Business rules enforced | Same as manual `CreateReminder`/`UpdateReminder` (Business Behavior Model §C1/C3). |
| Events raised | `ReminderCreated` or `ReminderUpdated`. |

---

## 4. Boards

### 4.1 Boards (tab)

| Field | Detail |
|---|---|
| Purpose | The stable home for every shared space a person belongs to. |
| Primary user goal | Find and enter a specific shared group. |
| Entry points | Bottom tab. |
| Exit points | Board Detail (tap a Board), Create Board. |
| Primary action | Tap a Board. |
| Secondary actions | "+" Create Board. |
| Displayed information | Each Board: name, small member-avatar cluster, a light next-due hint. **The Personal Board never appears here** — see §13's Domain Model cross-reference; it is a modeling convenience, not a user-facing concept. |
| Empty state | Brand-new user with no shared Boards: "You don't have any shared boards yet — create one to start remembering together," single CTA, not a scary blank list. |
| Loading state | Skeleton cards. |
| Offline state | Cached list, read-only for metadata; entering a Board still works fully from cache. |
| Events raised | None directly — pure navigation. |

### 4.2 Create Board

| Field | Detail |
|---|---|
| Purpose | Establish a new shared space. |
| Primary user goal | Name it and get in. |
| Entry points | Boards tab "+". |
| Exit points | Lands directly inside the new Board. |
| Primary action | "Create." |
| Secondary actions | Optional immediate invite step, clearly skippable ("Invite people now, or later — up to you"). |
| Displayed information | Board name field only, by default. |
| Error state | Inline validation (name required). |
| Business rules enforced | Creator becomes Owner atomically (Domain Model invariant). |
| Events raised | `BoardCreated`, `MembershipGranted`. |

### 4.3 Board Detail

| Field | Detail |
|---|---|
| Purpose | This Board's shared memory — its Reminders, scoped and focused. |
| Primary user goal | See what's coming up for this specific group. |
| Entry points | Boards tab. |
| Exit points | Reminder Detail, Board Options. |
| Primary action | Capture box, pre-scoped to this Board. |
| Secondary actions | Tap Board name/avatar strip or "···" → Board Options sheet. |
| Displayed information | Header (name, member-avatar strip, mute indicator if muted), Reminders list identical in shape to Home's Today/Coming Up, scoped to this Board only. |
| Empty state | "Nothing here yet — add your first reminder for [Board name]." |
| Offline state | Same as Home. |
| Business rules enforced | Visibility strictly by Active Membership; no content from other Boards ever appears here. |
| Events raised | Same as Home's per-row interactions, scoped to this Board. |

### 4.4 Board Options (sheet)

| Field | Detail |
|---|---|
| Purpose | The governance layer, deliberately one tap removed from the primary Reminders view. |
| Primary user goal | Manage the Board itself rather than its content. |
| Entry points | Board Detail header tap. |
| Exit points | Members, Entities, History, Board Settings, or dismiss without action. |
| Displayed information | Members, Entities, History, Board Settings, Mute this Board — plus, contextually: Transfer Ownership / Delete Board (Owner only), Leave Board (any Member). |
| Navigation behavior | Sheet dismisses on outside tap or swipe-down with zero confirmation — opening this menu is never itself a consequential action. |
| Events raised | None directly — a navigation surface. |

*See §13.3 for a recommendation to reduce this sheet's item count.*

### 4.5 Board Members

| Field | Detail |
|---|---|
| Purpose | See who belongs here and manage their standing. |
| Primary user goal | (Owner) Manage membership; (everyone) see who's in the group. |
| Entry points | Board Options. |
| Exit points | Invite Member, per-member actions. |
| Primary action | (Owner) Tap a member → Remove / Change Role. |
| Secondary actions | Invite button. |
| Displayed information | Avatar, name, role badge (Owner/Member/Guest) per person. |
| Destructive action | Remove Member requires a specific confirmation: "Remove [Name] from [Board]? They'll lose access, but their past reminders stay." |
| Business rules enforced | Only the Owner may remove or change roles; a Member cannot self-promote (Business Behavior Model §B5). |
| Events raised | `MemberRemoved`, `MemberRoleChanged`. |

### 4.6 Invite Member

| Field | Detail |
|---|---|
| Purpose | Bring a new person into this Board's shared memory. |
| Primary user goal | Get an invite out in one or two taps. |
| Entry points | Board Members, Board Options. |
| Exit points | Native share sheet (link), or back to Board Members on success. |
| Primary action | **Share Link** — the default, fastest, most prominent option. |
| Secondary actions | QR code and direct email/phone entry, demoted to a small "more ways to invite" disclosure rather than three equally-weighted choices (see §13.4). |
| Success feedback | "Invite sent" toast, or the native share sheet's own completion state. |
| Error state | A blocked relationship is rejected plainly but without exposing block details: "This invite can't be sent right now." |
| Offline state | Requires connectivity to issue a valid, trackable Invitation token. |
| Business rules enforced | Target's "who can invite me" Privacy Setting is checked before the Invitation is even created (Domain Model rule); expiring, single-use token always applied. |
| Events raised | `InvitationSent`. |

---

## 5. Entities

### 5.1 Entities (list)

| Field | Detail |
|---|---|
| Purpose | The short list of real-world subjects (pets, vehicles, family members) this Board's reminders cluster around. |
| Primary user goal | Find "everything about Rex" quickly, when needed. |
| Entry points | Board Options. |
| Exit points | Entity Detail, Create Entity. |
| Displayed information | Name, type icon, per entity. |
| Empty state | "No entities yet — track pets, vehicles, or family members here to link reminders to them." Framed as optional, low-pressure — not every Board needs one. |
| Events raised | None directly. |

### 5.2 Entity Detail

| Field | Detail |
|---|---|
| Purpose | Everything linked to one real-world subject. |
| Primary user goal | See its history and upcoming reminders in one place. |
| Entry points | Entities list, an Entity chip on a Reminder row. |
| Exit points | Reminder Detail (for any linked reminder). |
| Primary action | View linked reminders. |
| Secondary actions | Archive Entity. |
| Displayed information | Name, type, a small list of linked Reminders (past and upcoming). |
| Destructive action | None truly destructive here — Archive is reversible and never orphans linked Reminders (Domain Model rule 18). |
| Events raised | `EntityCreated`, `EntityArchived`, `ReminderLinked`/`Removed`. |

---

## 6. Search

### 6.1 Search

| Field | Detail |
|---|---|
| Purpose | Find anything the person has access to, without making them pick a category first. |
| Primary user goal | Type a word, find the thing. |
| Entry points | Search icon (Home, Boards top bar). |
| Exit points | Direct navigation into whatever was tapped — a Reminder result opens Reminder Detail already showing its Board context, a Board result opens Board Detail, a Person result opens their profile card. |
| Primary action | Type a query. |
| Displayed information | A single field; results stream in beneath it, grouped by type (Reminders, Boards, People, Entities, History), ranked by relevance — never gated behind a category picker. |
| Empty state (no query yet) | A calm, empty field — no forced suggestions, optionally recent searches if that proves useful, nothing mandatory. |
| No-results state | "Nothing found for '…'" with a gentle spelling-check nudge, never a dead end. |
| Offline state | Searches local cache only; if results may be incomplete, a small "showing offline results" note appears rather than silently under-delivering. |
| Accessibility | Results announce their type and context as part of the row's label ("Reminder, Vet visit, on Family board"). |
| Business rules enforced | Privacy filtering (Domain Model rule) applies invisibly and always — a Hidden person never appears, with no advanced-search bypass. |
| Events raised | None — Search is a pure read, per the Event Storming model (§I). |

---

## 7. Notifications (Buzz)

### 7.1 System Push (Buzz)

| Field | Detail |
|---|---|
| Purpose | Deliver a timely, individually-relevant nudge outside the app. |
| Primary user goal | Know what's due, and optionally resolve it, without opening the app. |
| Appears when | An Occurrence approaches or reaches its due moment for a given recipient. |
| Displayed information | One friendly line ("🐝 Rex's vet visit is tomorrow at 4pm"), never red, never an exclamation mark implying urgency that isn't real. Multiple same-day Buzzes are grouped into one notification rather than stacking individually. |
| Primary action | Tap → opens directly to that Occurrence's Reminder Detail, not a generic Home screen. |
| Secondary actions | Native action buttons where the OS supports them: **Done**, **Dismiss**, (future) **Snooze** — resolvable without opening the app at all. |
| Failure/offline state | If push delivery fails after retries, the Buzz still appears in-app on next open (§7.2) — never silently dropped. |
| Accessibility | Read aloud by the OS exactly as displayed; action buttons are labeled, not icon-only, where the platform allows. |
| Permissions | Notification permission, requested contextually (§1) rather than at first launch. |
| Business rules enforced | Delivery re-checks current Membership/Block/Mute state at the moment of send, not just at scheduling (Event Storming §E1 hotspot). |
| Events raised | `BuzzGenerated`, `BuzzDelivered`/`DeliveryFailed`, `BuzzSeen`/`Dismissed`, and — if acted on via a native button — the same `OccurrenceCompleted`/`Dismissed` events as in-app. |

### 7.2 In-App Buzz Fallback

| Field | Detail |
|---|---|
| Purpose | A quiet safety net for Buzzes that couldn't be delivered externally — not a browsable "inbox." |
| Primary user goal | Never actually notice this screen exists, unless something genuinely failed to push. |
| Appears when | Only when at least one undelivered Buzz exists — a small indicator appears (e.g., on Home); otherwise this surface is entirely invisible, deliberately not a persistent tab or icon (see §13.6). |
| Displayed information | The failed-to-deliver Buzz(es), each resolvable exactly like a Home row. |
| Events raised | `BuzzSeen`/`Dismissed`, plus whatever Occurrence action is taken. |

---

## 8. Profile & Settings

### 8.1 Profile

| Field | Detail |
|---|---|
| Purpose | The person's own identity and account surface — never their content. |
| Displayed information | Photo, display name, username; "My Boards" shortcut list; links to Privacy, Notifications, Account, Blocked Users, Help, Sign Out. |
| Never displayed | Reminders (that's Home/Boards), any activity feed or streak/stat module, any public wall — see Information Architecture §7. |
| Entry points | Profile icon (top corner, every screen). |
| Events raised | `ProfileUpdated` on edits. |

### 8.2 Privacy Settings

| Field | Detail |
|---|---|
| Purpose | Control discoverability and reachability with the fewest possible decisions. |
| Primary action | Choose one preset: **Everyone / People I know / Nobody unless I invite them.** |
| Secondary actions | An "Advanced" disclosure exposing the three granular toggles ("who can invite me / mention me / send me board invitations") individually — see §13.5 for a recommendation to hide this disclosure until it's actually needed. |
| Business rules enforced | Changes take effect immediately for future checks, never retroactively revoking something already in flight (Business Behavior Model §PRV-01). |
| Events raised | `PrivacySettingsChanged`. |

### 8.3 Notification Settings

| Field | Detail |
|---|---|
| Purpose | Channel and quiet-hours control — deliberately not where Board muting lives. |
| Displayed information | Preferred channel (defaulted to push), quiet hours (defaulted to a sensible window). |
| Explicitly absent | Per-Board mute — that's a contextual action inside each Board's options, not a global settings list item (Information Architecture §8). |
| Events raised | `NotificationPreferencesChanged`. |

### 8.4 Blocked Users

| Field | Detail |
|---|---|
| Purpose | Manage the (hopefully short, hopefully rarely visited) list of people a person has blocked. |
| Primary action | Unblock (no confirmation required — reversible, low-stakes, and re-establishing contact is the person's own choice to make freely). |
| Events raised | `UserUnblocked`. |

### 8.5 Account

| Field | Detail |
|---|---|
| Purpose | Email/phone/password/recovery management. |
| Displayed information | Current email/phone (editable, re-verification required on change), password change, linked recovery methods. |
| Exit points | Link to Delete Account, kept visually and navigationally separate — never a checkbox on this same screen, given how much heavier that decision is. |
| Events raised | `ProfileUpdated`-adjacent identity events per field. |

### 8.6 Help & About

| Field | Detail |
|---|---|
| Purpose | Static support content — FAQ, contact, app version. |
| Explicitly not | A live chat/support inbox — BuzzMe is not a messaging app, and Help shouldn't accidentally become one. |

### 8.7 Delete Account

| Field | Detail |
|---|---|
| Purpose | The single heaviest, most protected flow in the product. |
| Primary user goal | Leave permanently, fully understanding the consequence. |
| Entry points | Account settings only — never one tap from anywhere else. |
| Steps | (1) A plain-language warning screen explaining what's permanent and what survives (shared History stays, attributed anonymously); (2) if the person is the sole Owner of a shared Board with other Members, the flow blocks here with a direct link into the exact same Transfer Ownership flow used elsewhere (§4.4) — no bespoke variant; (3) an explicit typed or re-authenticated confirmation; (4) a calm farewell screen ("Your account has been deleted") before returning to Welcome — never dumping the person silently at a login screen. |
| Business rules enforced | A Board is never left ownerless by account deletion (Domain Model invariant); shared History always survives. |
| Events raised | `AccountDeletionRequested` → `AccountDeleted`. |

---

## 9. Cross-Cutting Interaction Patterns

- **Swipe:** native swipe-to-dismiss on system notifications; optional swipe-to-complete/archive on list rows, always with an equivalent tap-based path available (accessibility — see §1).
- **Long-press:** on a Reminder row, opens a quick action menu (Complete / Dismiss / Edit / Archive) as a power-user accelerator — never the *only* way to reach those actions; tapping through to Reminder Detail always offers the identical set.
- **Context menu:** long-press on a Board row surfaces Mute / Leave shortcuts, same accelerator principle.
- **Drag:** intentionally absent everywhere — no drag-to-reorder for Boards or Reminders. Reordering implies organizing/prioritizing, which belongs to task-manager territory, not remembering (see §13.8's related recommendation on Snooze).
- **Voice input:** the mic icon in the capture box triggers native speech-to-text; it is a second input method into the same field, never a separate feature or screen.
- **Typing:** standard, the baseline.
- **Paste:** pasting text into the capture box behaves identically to typing — the same NLU pipeline processes it, which also naturally supports a future "paste a forwarded email" use case with no new mechanism.
- **Share (into BuzzMe):** BuzzMe registers as a native OS share target, so a person can share text/a link/a photo from another app directly into the capture flow — a small, high-leverage addition to "reduce forms" that wasn't explicit in the prior documents but follows directly from the same principle.
- **Notification actions:** Done / Dismiss / (future) Snooze, directly on the push notification (§7.1).
- **Undo:** after Complete or Dismiss, a brief snackbar ("Done — Undo") stays available for a few seconds; the same reversal remains reachable afterward from Reminder Detail/History, just without the snackbar's immediacy.
- **Confirmation dialogs:** governed by §1's proportionality rule — reversible actions never interrupt; irreversible or other-people-affecting actions always name the specific thing being affected.
- **Destructive actions:** Delete Reminder, Delete Board, Remove Member, and Delete Account are the only four true destructive-confirmation moments in the entire product — everything else (Archive, Mute, Dismiss, Leave when not sole Owner) is reversible and interrupts nobody.

---

## 10. Offline Behavior

**Works fully offline:** viewing any already-synced Home, Board, Reminder, Entity, or Profile content; creating a Reminder manually (no AI dependency); completing, dismissing, and undoing Occurrences; navigating anywhere already cached.

**Requires connectivity:** AI/NLU parsing (queued, not blind-guessed — see below), sending or accepting Invitations, Transfer Ownership, Delete Board, Delete Account, live Search beyond the local cache, push delivery itself, and voice/photo import (external providers).

**Queued and synced:** every offline write is applied optimistically to the local view the instant it's made, then queued and synced silently on reconnect. A small, non-blocking "syncing…" indicator may appear; it never blocks further interaction.

**AI parsing while offline:** rather than degrading to a blind, unparsed guess, the raw typed/spoken text is queued and parsed automatically the moment connectivity returns, with the resulting Confirmation Card then presented as normal — preserving the "AI proposes, human confirms" rule even across a connectivity gap.

**Conflict resolution:** if two devices (or two people on a shared Occurrence) both acted while offline and reconnect with conflicting state, resolution is silent and automatic — last-write-wins on the *current* state, with every attempt preserved in History regardless (mirroring the Event Storming model's concurrency handling, §D3). The person is never shown a manual "merge conflict" dialog; if they're curious what happened, History has the full, honest record.

---

## 11. Microcopy Library

**Buttons:** *Add · Save · Create · Send Invite · Complete · Dismiss · Archive · Delete · Cancel · Discard · Undo · Resend Code · Get Started*

**AI thinking states:** *"Got it, one sec…" → "Just double-checking the details…"* (shown only if the wait exceeds roughly a second) → on timeout, auto-offer: *"Taking longer than usual — want to enter it yourself instead?"*

**Confirmation dialogs (destructive):**
- Delete Reminder: *"Delete '[Title]'? It'll stop reminding anyone, but its history stays."*
- Delete Board: *"Delete '[Board name]'? This removes it and everything in it for everyone — permanently."*
- Remove Member: *"Remove [Name] from [Board]? They'll lose access, but their past reminders stay."*
- Delete Account: *"This permanently deletes your account. Shared reminders you've added will stay for others, but won't be linked to you anymore."*

**Confirmations (non-destructive, quiet toasts, no dialog):** *"Archived." · "Muted [Board name]." · "Left [Board name]." · "Invite sent."*

**Errors:**
- Login: *"That didn't work — check your details and try again."*
- Verification: *"That code isn't right — try again."* / *"That code's expired — send a new one."*
- Network/offline: *"You're offline — this will save once you're back online."*
- Blocked invite: *"This invite can't be sent right now."*

**Warnings:**
- Sole Owner leaving: *"You're the only owner of [Board name]. Choose someone to take over before you go."*
- Suspicious duplicate: *"Looks similar to '[existing title]' — add anyway, or view the existing one?"*

**Empty states:**
- Home (new user): *"Nothing here yet — try: 'Mum's birthday every year on 12 March.'"*
- Boards (new user): *"You don't have any shared boards yet — create one to start remembering together."*
- Board Detail: *"Nothing here yet — add your first reminder for [Board name]."*
- Entities: *"No entities yet — track pets, vehicles, or family members here to link reminders to them."*
- Search, no results: *"Nothing found for '[query]' — check the spelling, or try a different word."*

**Loading messages:** *"Getting things ready…"* (post-verification provisioning, only if it's not instant) — otherwise, skeleton screens with no copy at all, per §1.

**Permission requests:**
- Notifications: *"Turn on notifications so BuzzMe can buzz you when this is due."*
- Microphone: *"Allow microphone access to add reminders by speaking."*
- Contacts (future): *"Allow contacts access to find people you already know."*

**Invitation messages (pre-filled, editable share text):** *"[Name] invited you to remember things together on BuzzMe — join their '[Board name]' board: [link]"*

**Deletion confirmations:** see Confirmation dialogs above — always name the specific object, never a generic "Are you sure?"

---

## 12. Accessibility — Consolidated Review

- **Screen readers (VoiceOver/TalkBack):** every icon-only control has a real label; reading order matches visual priority (capture box first on Home); destructive confirmations read the full specific sentence, not just "Confirm"; status changes (e.g., a completed Occurrence) are announced, not just visually updated.
- **Dynamic text:** every screen reflows rather than clips at the largest supported text size — verified explicitly for Home's list rows and the AI Confirmation Card, the two densest information displays in the app.
- **Motor impairment:** every touch target meets platform minimums; every swipe/long-press interaction has a full tap-based equivalent (§9); nothing times out before a person using assistive tech can respond (the AI-thinking timeout offers a fallback rather than failing silently).
- **Colour blindness:** status is never colour-only — a missed item is marked with an icon and a word, never a coloured dot alone; the deliberate absence of red anywhere in the missed/overdue treatment (Information Architecture §11) also happens to sidestep the most common colour-contrast confusion entirely.
- **Voice input:** the mic icon works with native platform speech-to-text and remains fully usable when driven by system-level voice control tools, not just BuzzMe's own mic button.
- **One-handed use:** primary actions (capture box, Complete/Dismiss, bottom tabs) live in the lower two-thirds of the screen by construction, not as a retrofit.
- **Keyboard navigation (Web):** every interactive element reachable via Tab in a logical order matching the visual layout; the capture box is keyboard-focusable and submittable via Enter without ever requiring a mouse; modal sheets (Board Options, Confirmation Card) trap focus appropriately and return it to the triggering element on close.

---

## 13. Friction, Simplification & Merge Recommendations

1. **Consider removing the Welcome/Onboarding screen (§2.2) entirely.** It's already been reduced to a single line and two buttons — the natural next question is whether even that earns its own screen versus folding directly into a combined Register/Login choice with the tagline as a subtitle, saving one tap for every single new user before they've done anything yet.
2. **The Manual Reminder Editor should never present all fields at once**, even as the deliberate fallback path — Title and "when" expanded, everything else collapsed with smart defaults pre-filled, so the fallback path still respects the product's core speed promise rather than reading as "now here's the real form."
3. **Board Options (§4.4) currently lists up to eight items** (Members, Entities, History, Settings, Mute, Transfer, Delete, Leave). Recommend testing whether Entities and History collapse into a single "More about this board" secondary screen, bringing the primary sheet down to five items and keeping even this secondary surface calm.
4. **Invite Member's three channel choices should not be presented as equally weighted.** Share Link as the one obvious primary action, with QR and direct entry demoted to a small "more ways to invite" disclosure — three co-equal buttons for one decision is exactly the kind of small choice-paralysis moment the product should refuse to create.
5. **Privacy Settings' "Advanced" disclosure should stay genuinely hidden** until a person has interacted with the preset picker at least once — don't even show the disclosure link to a brand-new user who has never had reason to want it.
6. **The in-app Buzz fallback list (§7.2) must never become a discoverable, persistently-visible "inbox."** The moment it's a standing icon people feel some obligation to check, it starts drifting toward the notification-feed anti-pattern this product deliberately avoids — it should be genuinely invisible except at the rare moment it's needed.
7. **Delete Account's ownership-transfer precondition should reuse the existing Transfer Ownership flow exactly**, not a bespoke in-context variant — fewer unique interaction patterns to design, build, test, and for a person to ever have to learn.
8. **Hold back Snooze/Skip (currently marked future) until real usage data asks for it.** A snooze button is the single feature most likely to quietly tip BuzzMe toward feeling like a task manager — it implies "this is a task I'm avoiding," which cuts directly against the shared-memory framing the whole product is built on. This is worth stating plainly as a deliberate, ongoing restraint, not just a sequencing decision.
9. **Long-press and swipe accelerators must never be the only path to an action.** Every gesture-based shortcut needs a tap-through equivalent reachable the "slow way," both for accessibility and because a gesture someone has to discover on their own contradicts the 30-second-understand goal.
10. **Reconsider whether "Today" and "Coming Up" need to be two separately-labeled sections at all**, versus one continuous chronologically-sorted list with lightweight date-group headers ("Today," "Tomorrow," "This week"). The latter may be a genuinely simpler mental model — a person scanning a list naturally reads it by date without first needing to understand what qualifies as "Coming Up" as a named concept. Worth testing before committing to the two-section structure as final.

---

*This document, together with the four documents it translates, completes BuzzMe's foundation from business philosophy through to screen-level behavior. Every future visual design, component library, and frontend implementation decision should be traceable to a row in one of the tables above — and any new screen, menu, or setting proposed later should have to justify itself against §13's standard before it's added.*
