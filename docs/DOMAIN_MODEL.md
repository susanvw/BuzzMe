# BuzzMe — Conceptual Domain Model

*Companion document to [PRODUCT_VISION.md](./PRODUCT_VISION.md). This document defines the language of the BuzzMe business — the concepts, rules, and relationships that exist regardless of database, API, or screen. It is the foundation every future architecture, schema, API contract, and UI decision should trace back to.*

*No tables. No endpoints. No UI. Just the domain.*

---

## 0. How to Read This Document

Section 1 makes three foundational modeling decisions explicit and defends them, because they shape everything downstream. Section 2 defines every domain concept in depth. Section 3 answers the specific relationship questions the business needs answered. Section 4 is the consolidated rulebook. Section 5 is the glossary — the one true dictionary for Product, UX, Engineering, and QA. Sections 6–7 give the DDD structure (aggregates, bounded contexts). Section 8 proves the model holds up under future scale and features.

---

## 1. Foundational Modeling Decisions

Before listing concepts, three assumptions are worth challenging directly, because getting them wrong would quietly corrupt the whole model.

### 1.1 "Board" is the right name — kept deliberately, not by default

The brief invited a better name than **Board**. Candidates considered:

- **Circle** — warmer, relationship-first, but already strongly associated with location-sharing apps (Life360, Google Circles) and shifts emphasis entirely to *people*, losing the "place where things live" quality.
- **Hive** — thematically fun given the bee/Buzz branding, but cute-first naming risks undermining the "calm, trustworthy" register the product needs; too easily reads as a gimmick rather than a durable business term.
- **Space** — dangerously overloaded (workspace, Slack Spaces, Google Spaces) and carries corporate-tool connotations the product explicitly rejects.

**Decision: keep Board.** The Product Vision already anchors the metaphor — *"a shared memory board where people can remember life's important moments together"* — so the domain term and the product's own self-description are the same word. It satisfies zero-learning-curve (everyone understands "a board with things on it") without importing scheduling, task, or chat baggage the way "calendar," "project," or "group chat" would.

What *is* worth correcting is treating Board as a single-purpose container. A Board is really two things fused into one concept: **a group of people** (who) and **a collection of reminders** (what). This document keeps them fused deliberately — splitting "the people" and "the things they remember" into two separate top-level concepts would add a layer of indirection the product doesn't need yet. If BuzzMe later needs a group of people who exist independently of any shared reminders (e.g., a pure contact circle), that's the moment to split it. Not before.

### 1.2 Personal reminders are not a special case — they live on an implicit Personal Board

The product vision states users can create reminders nobody else sees. The tempting model is a `Reminder.isPersonal` flag or a nullable board reference. Both are rejected here.

**Decision:** every User implicitly owns exactly one **Personal Board** — a Board with a single, permanent Member (themselves), created automatically at registration, never invitable, never leavable, never deletable independent of the account itself. A "personal reminder" is simply a Reminder that lives on this board.

This gives the entire domain one invariant instead of two: *a Reminder always belongs to exactly one Board.* No branching logic anywhere in the model for "personal vs shared." Visibility, history, and notification rules all fall out of the same Membership mechanism that already governs shared boards. The UI is free to never show this board as a labeled "Board" at all — that's a presentation decision, not a domain one.

### 1.3 Occurrence is its own Aggregate Root, not a child of Reminder

The obvious model nests Occurrences inside their parent Reminder as child entities. It's rejected here for a scale reason: a recurring Reminder (e.g., a birthday going for decades, a weekly bin reminder) accumulates an unbounded stream of Occurrences. If Occurrence is a child entity, then *marking one Tuesday's bin reminder complete* would conceptually require loading and re-saving the entire Reminder aggregate — including years of unrelated history — every single time. At "millions of reminders" scale, that's a design that fights itself.

**Decision:** **Reminder** (the definition — what, who, and how it repeats) and **Reminder Occurrence** (a single dated, actionable instance) are two separate Aggregate Roots, linked by reference. This is elaborated in Sections 2 and 6.

---

## 2. Core Domain Concepts

### User

- **Purpose:** Represents a real person who has an identity within BuzzMe. The anchor for authentication, ownership, and accountability across the whole domain.
- **Owner:** The person themselves.
- **Who may change it:** Only the User (their own identity attributes); platform administrators only for Trust & Safety enforcement (suspension), never for content edits.
- **Belongs to:** Nothing — User is a root concept.
- **Lifecycle:** `Registered → Active → Deactivated → Deleted`. Deactivation is reversible (a pause); Deletion is not.
- **Invariants:** A User has exactly one Personal Board, created atomically with the account and never independently destroyed. A User always has exactly one set of Privacy Settings and one set of Notification Preferences (both created with sensible defaults at registration — never in an undefined state).
- **Relationships:** Owns/holds Memberships in many Boards. Owns a Profile. Can issue and receive Invitations. Can Block other Users. Can author Reminders and Reminder Occurrence activity.
- **Future extensibility:** Multiple linked identities (e.g., a household admin managing a young child's or elderly parent's simplified account) is a natural extension — modeled as a distinct "Managed Account" relationship between two Users, not a change to User itself.

### Profile

- **Purpose:** How a User is represented to others — the human-readable face of the account.
- **Owner:** The User.
- **Who may change it:** Only the User.
- **Belongs to:** Exactly one User (1:1).
- **Lifecycle:** Created with the User; edited freely; never independently deleted (deleted only when the User is deleted).
- **Invariants:** Every Profile has a Display Name; Username is unique platform-wide; Profile Photo, Username, and optional Email/Phone are all independently updatable.
- **Relationships:** Governed for visibility by Privacy Settings — Profile itself is one concept, not two. "Public profile" and "private profile," as named in the Product Vision, are not two stored objects; they are two *views* of the same Profile, filtered by the User's Visibility Settings and by the viewer's relationship to that User (stranger, board co-member, invited contact). Modeling it this way avoids the data-integrity risk of two profiles drifting out of sync.
- **Future extensibility:** Additional profile facts (pronouns, timezone, preferred language) extend the concept without changing its shape.

### Board

- **Purpose:** The shared space in which a group of people hold reminders in common — the materialization of a relationship (family, team, household, couple, club).
- **Owner:** Exactly one current Owner Member at all times (see Membership).
- **Who may change it:** Owner always; other Members only to the extent the Board's settings permit (e.g., "any member can add reminders" vs. "only Owner can").
- **Belongs to:** Nothing above it — Board is a root concept, though every Board exists *because of* the People who share it.
- **Lifecycle:** `Created → Active → Archived → Deleted`. Archiving preserves history read-only; Deletion is a distinct, harder-to-reach action requiring Owner confirmation, and does not cascade-delete Reminder History (see §4, History rules).
- **Invariants:**
  - A Board always has exactly one Owner. Never zero, never more than one at a time.
  - A Board always has at least one Member (the Owner, at minimum).
  - A Personal Board always has exactly one Member, forever.
  - Board Visibility is one of: Private, Invite-Only, Public Read-Only (future), Public Community (future).
- **Relationships:** Has many Memberships. Has many Reminders. Has many Entities. Has an Invitation Policy (who may send invitations on this Board's behalf).
- **Future extensibility:** Visibility enumeration already anticipates Public Read-Only and Public Community boards without any structural change — see §8.

### Membership

- **Purpose:** Represents a specific User's relationship to a specific Board — the answer to "does this person belong here, and in what capacity."
- **Owner:** Co-owned conceptually by the Board (which grants it) and the User (who holds it).
- **Who may change it:** The Board Owner (grant, remove, change role, transfer ownership); the User themselves (accept an invitation, leave).
- **Belongs to:** Exactly one Board and exactly one User — the join point between them.
- **Lifecycle:** `Invited (pending) → Active → (Left | Removed)`. A Membership that ends is not deleted outright — it is marked ended, preserving the historical fact that this person was once part of this shared memory (see history rules).
- **Invariants:**
  - Role is one of: **Owner**, **Member**, **Guest** (see Glossary).
  - Exactly one Active Membership per Board may hold the Owner role at any time.
  - A Membership cannot be granted to a blocked relationship (see Trust & Safety rules).
- **Relationships:** Determines what Reminders a User can see (visibility = Active Membership on that Board). Referenced by Notification Preferences (a user can mute a specific Board without leaving it).
- **Future extensibility:** Additional roles (e.g., a time-limited "Guest" with read-only, single-Occurrence access — for a one-off event like a wedding) fit into the existing Role enumeration without new concepts.

### Reminder

- **Purpose:** The definition of something a Board's members care about remembering — the *what*, *who it concerns*, and *how often it recurs*. This is the core domain concept the entire product exists to serve.
- **Owner:** The Board it belongs to; authored by one Member.
- **Who may change it:** The author, plus any Member the Board's settings permit (default: any active Member may edit a shared Reminder — this matches the "shared responsibility" philosophy; Personal Board reminders are editable only by their sole Member).
- **Belongs to:** Exactly one Board (never zero, never many — see §3).
- **Lifecycle:** `Created → Active → (Edited)* → Archived/Deleted`. Editing changes future behavior only; it never rewrites what already happened (see history rules).
- **Invariants:**
  - Always belongs to exactly one Board.
  - Always has a Recurrence Rule — even a "one-time" Reminder is modeled as a Recurrence Rule with a single occurrence, so there is one consistent mechanism rather than two.
  - Notifies its Board's full Active Membership, unconditionally — there is no Participant-subsetting concept; a Reminder cannot target a narrower audience than "everyone on the Board" (MVP_SCOPE.md's permanent simplification, resolving what this section originally described as a subset — see REFACTOR_SHARED_BOARDS.md §3).
  - May optionally reference one Entity (e.g., "Rex" for a vet reminder) and one Category (e.g., "Health").
- **Relationships:** Generates Reminder Occurrences via its Recurrence Rule. May be instantiated from a Reminder Template. Optionally references an Entity.
- **Future extensibility:** AI-suggested edits, duplicate-detection linking, and smart re-scheduling all operate *on* this concept without changing its shape — they are behaviors in the AI/NLU supporting context that ultimately produce ordinary Reminder edits.

### Recurrence Rule

- **Purpose:** Expresses how often (or whether) a Reminder repeats — the pattern that produces Occurrences. Called out separately from Reminder because it's a distinct piece of meaning (e.g., "every year on 9 July," "every Tuesday," "once, tomorrow at 4pm").
- **Owner:** The Reminder it belongs to.
- **Who may change it:** Whoever may edit the parent Reminder.
- **Belongs to:** Exactly one Reminder (1:1) — it is not independently addressable.
- **Lifecycle:** Changes in place as the Reminder is edited; a change to the rule affects only future, not-yet-materialized Occurrences.
- **Invariants:** Always resolves to a deterministic next-occurrence date given a point in time. A one-time Reminder is a Recurrence Rule with exactly one resolution.
- **Relationships:** Produces Reminder Occurrences.
- **Future extensibility:** Naturally extends to complex patterns (every second Tuesday, school-term-only, lunar/relative dates like "the day before Mother's Day") without touching any other concept.

### Reminder Occurrence

- **Purpose:** A single, concrete, dated instance of a Reminder becoming due — the actionable unit that notifications, completion, and history all attach to.
- **Owner:** Generated by, and referencing, its parent Reminder — but see §1.3, it is its own Aggregate Root.
- **Who may change it:** Any Member entitled to act on the parent Reminder may dismiss/complete/reschedule *this instance*; the system generates it automatically from the Recurrence Rule.
- **Belongs to:** Exactly one Reminder (by reference), and transitively, exactly one Board.
- **Lifecycle:** `Scheduled → Due → (Acknowledged | Completed | Dismissed | Missed) → Archived (historical)`. Terminal states are reversible for a short grace window (an "undo"), but the reversal is itself recorded as a new History entry rather than silently erasing the prior state (see §4).
- **Invariants:**
  - Always references exactly one parent Reminder.
  - Once resolved, is retained forever as History — it is never hard-deleted, even if the parent Reminder is later deleted or archived (see §4).
  - Completion is idempotent — if two Members act on the same shared Occurrence near-simultaneously, the second action is a no-op that surfaces "already handled by [name]," not a duplicate or conflicting state.
- **Relationships:** The target of Notifications/Buzzes (one Buzz per relevant recipient, per Occurrence). The subject of Reminder History entries.
- **Future extensibility:** This is the natural point for future features like snoozing, per-occurrence exceptions ("skip this one"), and wearable check-off actions — all operate on Occurrence without touching Reminder.

### Notification (Buzz)

- **Purpose:** The friendly, individually-delivered nudge that tells one person about one Occurrence. In BuzzMe's language this concept is always called a **Buzz** — see Glossary.
- **Owner:** Generated by the Notification Engine on behalf of an Occurrence, for a specific recipient.
- **Who may change it:** The recipient may dismiss/mark-seen their own Buzz; no one else can act on another person's Buzz.
- **Belongs to:** Exactly one Occurrence and exactly one recipient User. A shared Reminder with five participating Members produces up to five distinct Buzzes for one Occurrence — not one shared Buzz object — because each person's delivery timing, channel, and mute state are independent.
- **Lifecycle:** `Generated → Delivered → Seen/Dismissed`. Never resurrected once dismissed; a new due moment generates a new Buzz.
- **Invariants:**
  - Always tied to exactly one Occurrence and exactly one recipient.
  - Delivery always respects that recipient's Notification Preferences (mute, quiet hours, channel) at the moment of delivery.
  - Suppressing a Buzz (mute) never alters the underlying Occurrence's due status or history — muting is a personal filter on communication, not a shared action on the fact itself.
- **Relationships:** References an Occurrence; references a recipient User; governed by that User's Notification Preferences.
- **Future extensibility:** New delivery channels (push, SMS, wearable haptic, email digest) are additive — they change *how* a Buzz is delivered, never what a Buzz conceptually is.

### Invitation

- **Purpose:** An offer of Membership on a specific Board, extended to a specific person or to anyone holding a link/QR code.
- **Owner:** The inviter (a Member entitled by the Board's invitation policy to invite).
- **Who may change it:** The inviter may revoke it; the invitee may accept or decline it; the system expires it automatically.
- **Belongs to:** Exactly one Board; issued by exactly one inviting User.
- **Lifecycle:** `Created → Pending → (Accepted → becomes a Membership) | Declined | Expired | Revoked`.
- **Invariants:**
  - Always carries an expiring, single-purpose Invitation Token — no invitation is valid indefinitely.
  - Cannot be created by, or accepted from, a blocked relationship (see §4).
  - Acceptance always converts to exactly one new Membership; it never creates duplicate Memberships if the invitee is already an Active Member (accepting again is a no-op).
  - If the target Board is deleted while an Invitation is pending, the Invitation is automatically expired.
- **Relationships:** References a Board, an inviter User, and (for targeted invitations) an invitee identity (email/phone) or, for link/QR invitations, resolves the invitee identity only at acceptance time.
- **Future extensibility:** The channel (email, SMS, link, QR) is a delivery detail on the same concept, not four different concepts — new channels (e.g., NFC tap, WhatsApp) extend the channel list only.

### Reminder History / Activity

- **Purpose:** The immutable, append-only record of what happened and *who did it*, for a given Reminder and its Occurrences. This matters more in BuzzMe than in a personal to-do app precisely because responsibility is shared — "did someone already handle this?" is a constant, real question in a family or team.
- **Owner:** The system, on behalf of the Board — no single Member owns or can edit History.
- **Who may change it:** No one edits or deletes History directly; it only grows, through the natural actions Members take (create, edit, complete, dismiss, undo).
- **Belongs to:** Referenced from a Reminder Occurrence (and transitively, a Reminder and Board).
- **Lifecycle:** Append-only. Entries are never removed, even when their subject Reminder or Occurrence is later archived or deleted.
- **Invariants:**
  - A deleted Reminder never deletes its History — this is a hard rule, not a default (see §4).
  - Every History entry records an actor, an action, and a timestamp.
  - Correcting a past action (e.g., un-completing an Occurrence) creates a *new* entry; it never overwrites the old one.
- **Relationships:** Powers the per-Board Activity Feed (a bounded, non-social projection of recent History — see below) and any future "memory book" look-back features.
- **Future extensibility:** This is the concept that eventually powers a genuinely delightful future feature — a shared family's or team's remembered history over years — without any new modeling, because it was captured as it happened.

### Reminder Template

- **Purpose:** A reusable pattern (e.g., "Birthday," "Vaccination," "Car Service," "Bin Day") that pre-fills structure and recurrence to speed up creation, and that gives the AI/NLU context strong hints during natural-language extraction.
- **Owner:** The platform, for system-provided templates; potentially a User or Board in future for custom templates.
- **Who may change it:** Platform content owners for system templates; the future owner for custom ones.
- **Belongs to:** Nothing — Templates are reference concepts, instantiated *by* Reminders, not owned by them.
- **Lifecycle:** `Published → Versioned → Deprecated`. Stable, low-churn.
- **Invariants:** Editing a Template never retroactively changes Reminders already created from it — a Template is a starting point, not a live link.
- **Relationships:** Optionally referenced by a Reminder at creation time.
- **Future extensibility:** Natural home for "smart suggestions" — e.g., recognizing a vet-visit pattern and suggesting the Vaccination template.

### Reminder Category

- **Purpose:** Lightweight classification (Birthday, Health, Household, Travel, Pet, Vehicle, School, Finance…) used for iconography, filtering, and AI extraction confidence — not a business-critical concept, but a real one users perceive.
- **Owner:** The platform (a fixed, curated list, at least initially).
- **Who may change it:** Platform content owners.
- **Belongs to:** Nothing — referenced by Reminders.
- **Lifecycle:** Stable reference data.
- **Invariants:** A Reminder has exactly one primary Category (for its visual identity); secondary tags are a future, non-core extension.
- **Relationships:** Referenced by Reminder.
- **Future extensibility:** User-defined custom categories are a plausible, low-risk future addition.

### Entity (Dog, Car, Child, House…)

- **Purpose:** Represents a persistent real-world subject that recurring reminders cluster around — "Rex" the dog, the family Corolla, "Emma," the house. Modeling this explicitly (rather than leaving it as free text on each Reminder) is what lets BuzzMe answer "show me everything about Rex" and lets AI extraction recognize "the dog" as a known, existing thing rather than a new one each time.
- **Owner:** The Board it belongs to.
- **Who may change it:** Any Member entitled to edit shared content on that Board.
- **Belongs to:** Exactly one "home" Board in v1 (see §3 for the cross-board question).
- **Lifecycle:** `Created → Active → Archived`. Archiving (not deleting) is the expected end state — a pet passing away or a car being sold doesn't erase the history of reminders that referenced it.
- **Invariants:** Archiving an Entity never deletes or orphans Reminders that reference it; they simply keep referencing an archived Entity.
- **Relationships:** Optionally referenced by many Reminders.
- **Future extensibility:** Cross-board sharing of a single Entity (e.g., a co-parented child relevant to two households) is a real future need — see §3 for why it's deliberately deferred rather than built now.

### Privacy Settings

- **Purpose:** Governs how discoverable and reachable a User is — who can find them, invite them, or mention them.
- **Owner:** The User, exclusively.
- **Who may change it:** Only the User.
- **Belongs to:** Exactly one User (1:1), always present from registration with sensible defaults.
- **Lifecycle:** Mutable at any time; always exists, never in a null/undefined state.
- **Invariants:** Visibility is one of Private Account, Public Account, Hidden from Search, Visible by Username Only, Visible by Invite Only. Independently, the User controls: who may invite them, who may mention them, who may send them Board invitations.
- **Relationships:** Consulted by Invitation creation, Search, and Mentions — never bypassed by any of them.
- **Future extensibility:** Additional granular controls (e.g., "who can see my activity on shared boards") extend the same concept without restructuring it.

### Notification Preference

- **Purpose:** Governs how and when a User is willing to be interrupted — channel, quiet hours, mute state, and per-Board overrides.
- **Owner:** The User, exclusively.
- **Who may change it:** Only the User.
- **Belongs to:** Exactly one User; may hold per-Board overrides (e.g., mute one noisy Board without going silent everywhere).
- **Lifecycle:** Mutable at any time; always exists with sensible defaults.
- **Invariants:** Preferences affect delivery only — see the Buzz invariant above; they can never delete or alter an Occurrence.
- **Relationships:** Consulted by the Notification Engine at the moment a Buzz would be delivered.
- **Future extensibility:** New channels (wearables, digest emails) are new preference dimensions, not new concepts.

### Block

- **Purpose:** A directional safety control — one User declaring they do not want another User able to interact with them.
- **Owner:** The blocking User.
- **Who may change it:** Only the blocking User (create or remove the block).
- **Belongs to:** The blocking User; references the blocked User.
- **Lifecycle:** `Created → Active → Removed`.
- **Invariants:** See §4 for the full cascade of effects (invitations, mentions, discoverability).
- **Relationships:** Consulted by Invitation, Search, and Mention logic.
- **Future extensibility:** Could extend to Board-level blocks (banning someone from ever being re-invited to a specific Board) as a distinct, Owner-held concept later.

### Report

- **Purpose:** A User flagging another User or a piece of content (a Reminder, a Profile) for review — the mechanism that feeds Trust & Safety moderation.
- **Owner:** The reporting User initiates it; the platform owns its resolution.
- **Who may change it:** The platform (Trust & Safety) resolves it; the reporter cannot retract facts already submitted, though they can withdraw the report itself.
- **Belongs to:** References the reporter, the reported User/content, and a reason.
- **Lifecycle:** `Submitted → Under Review → Resolved (actioned | dismissed)`.
- **Invariants:** A Report never itself removes content or restricts a User — it only triggers review; only a platform moderation action (a separate, privileged capability) can restrict.
- **Relationships:** May reference a User, a Board, a Reminder, or a Profile as its subject.
- **Future extensibility:** Automated triage (pattern detection across multiple reports) is additive, not structural.

### Mention

- **Purpose:** A reference to a specific User within a Reminder's text or notes, intended to draw their attention directly — e.g., "@Dad, can you handle this one."
- **Owner:** The author of the content containing the mention.
- **Who may change it:** Governed at creation by the mentioned User's Privacy Settings (who can mention them).
- **Belongs to:** The Reminder (or Occurrence note) it appears within.
- **Lifecycle:** Created with the content; not independently editable.
- **Invariants:** A Mention can only target an active co-Member of the same Board, and only if that person's Privacy Settings permit being mentioned by this author.
- **Relationships:** May trigger a targeted Buzz to the mentioned person.
- **Future extensibility:** Could extend to mentioning an Entity ("@Rex's vet visit") as a lightweight cross-reference.

### Board Activity Feed

- **Purpose:** A calm, bounded, chronological view of recent Reminder History *within one Board* — "Dad added a reminder," "Mom marked the vet visit done." Deliberately not a cross-board or algorithmic feed.
- **Owner:** The Board (a projection, not independently owned content).
- **Who may change it:** No one directly — it is a read view generated from History.
- **Belongs to:** Exactly one Board.
- **Lifecycle:** Continuously derived; nothing to manage.
- **Invariants:** Never surfaces content from a Board the viewer isn't an Active Member of. Never becomes a place to post free-standing content — it only reflects Reminder-related History, guarding explicitly against the product drifting toward a social feed (see Vision §13).
- **Relationships:** Derived entirely from Reminder History.
- **Future extensibility:** None encouraged — this concept should stay small on purpose.

### Attachment

- **Purpose:** An optional supporting artifact for a Reminder or Occurrence — a photo of an invitation card, a vet document, a scanned appointment letter.
- **Owner:** The Member who added it.
- **Who may change it:** The adding Member, or any Member entitled to edit the parent Reminder.
- **Belongs to:** Exactly one Reminder or Occurrence.
- **Lifecycle:** `Added → (Removed)`. Follows the retention rules of its parent (kept as part of History once the Occurrence is resolved).
- **Invariants:** An Attachment never exists without a parent Reminder or Occurrence — it has no independent meaning.
- **Relationships:** Referenced by Reminder or Occurrence; a natural input to future AI photo-parsing (an Attachment can be the *source* a Draft Reminder was extracted from).
- **Future extensibility:** This is the anchor point for "importing reminders from photos" — the photo becomes an Attachment on the Reminder it produced, preserving provenance.

### Concepts deliberately excluded or deferred

- **"Relationship" as a standalone concept** — rejected. Everything it would represent (who is connected to whom, in what capacity) is already fully captured by **Board + Membership**. Adding a separate Relationship concept would duplicate meaning without adding expressive power, violating the simplicity principle.
- **Comment / open-ended threads** — deliberately *not* modeled as a general commenting/chat feature. If notes-on-a-Reminder are needed, they should stay scoped to a single short note per Occurrence (closer to an Attachment's text sibling than a conversation), never a threaded reply system — the Product Vision is explicit that BuzzMe is not a messaging app.
- **Audit History (security/administrative log)** — distinct in *purpose* from Reminder History (which is domain/product-facing, about remembering things together) even though both are append-only logs. Audit History belongs conceptually to the Trust & Safety / Identity bounded context (who changed account security settings, who removed a Board Owner) rather than Reminder Management. Called out separately here so it isn't accidentally conflated with Reminder History during implementation.

---

## 3. Key Relationship Questions, Answered

**Can reminders exist without a board?**
No. Every Reminder belongs to exactly one Board, including personal ones — see §1.2's Personal Board decision. This is a single, unbroken invariant across the whole domain.

**Can boards exist without owners?**
No. A Board always has exactly one current Owner. The system never allows the last Owner to leave or be removed without either transferring ownership to another Member first, or dissolving the Board (which only the Owner may initiate).

**Can a reminder belong to multiple boards?**
No, by design, in v1. A Reminder belongs to exactly one Board because a Reminder's meaning — who it's for, who's accountable for it — comes *from* that Board's relationship. Cross-posting the same Reminder instance to two Boards would blur that accountability and contradict the "reminder belongs to the shared relationship" premise the entire product is built on. If the same real-world event genuinely matters to two Boards (a wedding relevant to both "Family" and "Wedding Planning"), the correct pattern is to create it once on the most relevant Board and separately decide whether it needs its own presence elsewhere — not to make one Reminder object multi-homed. A future "Linked Reminder" concept could formalize deliberate cross-referencing later; it is not needed now.

**Can a user own multiple boards?**
Yes, without limit. A User can hold the Owner role on many Boards simultaneously (their Family board, their Rugby Team board, etc.), and can additionally hold Member or Guest roles on many others.

**Can entities belong to multiple boards?**
Not in v1 — an Entity has exactly one home Board, mirroring the Reminder rule for the same reason: clean ownership and accountability. This is the deliberately deferred case flagged in §2 (Entity) — a co-parented child or a jointly-owned pet relevant to two households is a real scenario, but solving it prematurely (with cross-board Entity sharing) adds real complexity for a case that's a minority of usage. When it's tackled, it should be an explicit "shared reference" relationship between an Entity and additional Boards — not a redefinition of "home Board."

**How are recurring reminders represented?**
A single **Reminder** holds one **Recurrence Rule**, which deterministically produces a stream of **Reminder Occurrences** over time. The Reminder is the eternal definition; Occurrences are the dated, actionable instances. Editing the Reminder's rule affects only future, not-yet-materialized Occurrences — past Occurrences are untouched, preserving history integrity. Individual Occurrences can be adjusted or skipped independently of the overall pattern (the familiar "just this once" exception).

**How should reminder history work?**
As an append-only ledger (§2, Reminder History / Activity) that grows from real actions Members take, is never edited or deleted, and survives the deletion or archiving of its parent Reminder or Occurrence. Corrections create new entries rather than overwriting old ones. This is treated as a hard business rule, not an implementation nicety — it's the literal difference between a "reminder tool" and a "shared memory."

**How do invitations work?**
An Invitation is issued by a Member (per that Board's invitation policy) against a specific Board, carries an expiring single-use token, targets either a specific contact (email/phone) or is channel-agnostic (link/QR, resolved at acceptance), and converts into a Membership only upon acceptance. It always expires; it is revocable by the inviter before acceptance.

**How do blocked users affect invitations?**
A block is bidirectional in its practical effect even though it's directionally created: if A blocks B, neither A can invite B, nor can B invite A, nor can either accept an Invitation the other previously sent. Any pending Invitation between the two parties is automatically revoked the moment the block is created. Blocking does **not** automatically remove the blocked party from Boards they already share — that is a separate, Owner-held decision (Remove Member), because personal safety control (blocking) and board governance (membership) are related but distinct powers, and conflating them would let one Member's block silently override an Owner's authority over their own Board.

---

## 4. Business Rules (Consolidated)

1. A Board always has exactly one current Owner — never zero, never more than one.
2. A Board's sole Owner cannot leave or be removed without first transferring ownership, or dissolving the Board entirely.
3. Every Reminder belongs to exactly one Board, with no exceptions — personal reminders live on an implicit, single-member Personal Board.
4. A Reminder always notifies its Board's full Active Membership; there is no narrower, per-Reminder audience (MVP_SCOPE.md's permanent removal of Participant subsetting).
5. Recurring Reminders are represented by one Reminder plus one Recurrence Rule; even one-time Reminders use the same mechanism, resolving to a single Occurrence.
6. Individual Occurrences can be adjusted or skipped without altering the parent Recurrence Rule.
7. A Notification (Buzz) always relates to exactly one Occurrence and exactly one recipient — a shared Reminder produces one independent Buzz per relevant, non-muted Member.
8. Muting or otherwise adjusting Notification Preferences changes delivery only; it never alters or deletes the underlying Occurrence.
9. A deleted or archived Reminder never deletes its History or past Occurrences — history is permanent and append-only.
10. Corrections to past actions (e.g., un-completing an Occurrence) create new History entries; they never overwrite prior ones.
11. Completing or dismissing a shared Occurrence is idempotent — simultaneous actions by two Members resolve to one recorded outcome, surfaced clearly to the second actor.
12. An Invitation always expires and is scoped to exactly one Board and one inviter.
13. A blocked user cannot send or accept Invitations to/from the blocker; existing pending Invitations between blocked parties are auto-revoked.
14. Blocking does not automatically remove existing shared Board membership — that remains an explicit Owner action.
15. Personal reminders (on a Personal Board) are visible to exactly one person, are never discoverable by others, and are never surfaced in cross-board AI suggestions without explicit user action.
16. Visibility (who can see a Reminder) is governed solely by Active Membership on its Board — no other mechanism grants Reminder visibility.
17. Removing a Member from a Board revokes their future access but never deletes content or History they authored — authorship of shared memory is preserved even after someone leaves.
18. Archiving an Entity never orphans or deletes Reminders referencing it — it simply marks the Entity inactive going forward.
19. Editing a Reminder Template never retroactively changes Reminders already instantiated from it.
20. AI-extracted Reminders (from text, photo, email, or voice) are always presented as an unconfirmed draft; nothing becomes a real, notifying Reminder without explicit human confirmation.
21. A Mention can only target an active co-Member of the same Board, and only where that person's Privacy Settings permit being mentioned by that author.
22. The Board Activity Feed only ever reflects Reminder History for Boards the viewer actively belongs to, and never accepts free-standing posts.

---

## 5. Ubiquitous Language — The BuzzMe Glossary

*One word, one meaning. This is the shared vocabulary for Product, UX, Engineering, and QA — use these words exactly, everywhere, including in code, tickets, and copy.*

| Term | Definition |
|---|---|
| **Reminder** | The definition of something worth remembering together — the what, who, and how often. Not yet a specific moment in time. |
| **Occurrence** | One specific, dated instance of a Reminder becoming due. What a Buzz is actually about. |
| **Buzz** | The friendly notification a person receives about an Occurrence. Never call it an "alert" or "notification" in product-facing language — always Buzz. |
| **Board** | A shared space where a group of people hold Reminders in common. The unit of shared memory. |
| **Personal Board** | The implicit, single-member Board every account has automatically, holding that person's private Reminders. Not shown to users as a labeled "Board." |
| **Member** | A person with active, standing access to a Board. |
| **Owner** | The single Member of a Board with ultimate authority over it — settings, removing Members, dissolving the Board. Every Board has exactly one. |
| **Guest** | A lightweight, limited-access participant on a Board (e.g., read-only, or scoped to a single Occurrence) — not a full Member. |
| **Invite / Invitation** | An offer of Membership on a Board, sent via email, SMS, link, or QR code, that expires and must be accepted. |
| **Entity** | A persistent real-world subject Reminders can be about — a pet, vehicle, child, or property. |
| **Recurrence Rule** | The pattern that determines how a Reminder repeats (or that it happens only once). |
| **History / Activity** | The permanent, append-only record of what happened to a Reminder and its Occurrences, and who did it. |
| **Archive** | To retire a Board, Reminder, or Entity from active use while preserving its History permanently — distinct from Delete. |
| **Dismiss** | To acknowledge an Occurrence without marking the underlying thing as done (e.g., "I've seen this Buzz"). |
| **Complete** | To mark an Occurrence as done/handled. |
| **Muted** | A Notification Preference state where Buzzes are suppressed for a User, on one Board or overall — never affects the Occurrence itself, only its delivery. |
| **Blocked** | A directional safety state where one User has restricted another from inviting, mentioning, or interacting with them. |
| **Mention** | A direct reference to a specific co-Member within a Reminder, intended to draw their attention. |
| **Template** | A reusable, pre-filled pattern for common Reminders (e.g., "Birthday," "Vaccination") that speeds up creation. |
| **Category** | A lightweight classification on a Reminder (Health, Household, Travel, etc.) used for icons and filtering. |
| **Draft** | An AI-proposed Reminder awaiting explicit human confirmation before it becomes real. Never auto-committed. |

---

## 6. Aggregate Roots

An Aggregate Root is the entry point for a consistency boundary — the object whose invariants must always hold true together, in one transaction, and the only thing other parts of the system are allowed to reference directly.

| Aggregate Root | Why |
|---|---|
| **User** | Owns its own identity, Profile, Privacy Settings, and Notification Preferences as one consistent whole — none of these have independent lifecycles worth splitting out. |
| **Board** | Must enforce "always exactly one Owner" and "always at least one Member" as hard, transactional invariants — this requires Membership changes to be consistent with the Board they belong to. Membership is modeled as an entity *within* the Board aggregate for this reason. |
| **Reminder** | Owns its Recurrence Rule and its own definitional invariants (belongs to one Board, notifies that Board's full Membership). Deliberately does **not** own its Occurrences — see next row. |
| **Reminder Occurrence** | Split out as its *own* Aggregate Root specifically to support scale: individual due-date actions (complete, dismiss, undo) happen at far higher frequency and independence than edits to the parent Reminder, and shouldn't require loading years of accumulated history to perform. References its parent Reminder by identity only. |
| **Invitation** | Has its own lifecycle, expiry, and token integrity independent of both the inviting Board and the invited User — neither of those aggregates should need to be locked to expire or revoke an Invitation. |
| **Notification (Buzz)** | High-volume, independently-delivered, per-recipient — needs to be generated and updated (delivered/seen) without touching the Occurrence it references. |
| **Entity** | Has an independent lifecycle (created, archived) and is referenced by many Reminders over time — needs stable identity outside any single Reminder. |
| **Block** | Needs to be checked quickly and independently by Invitation, Search, and Mention logic without loading either User's full aggregate. |

**Deliberately not Aggregate Roots:** Reminder History (an append-only stream referencing Occurrence, not a consistency boundary of its own), Board Activity Feed (a pure projection), Reminder Template and Reminder Category (simple reference/lookup data), Attachment and Mention (meaningless outside their parent Reminder/Occurrence).

---

## 7. Bounded Contexts

| Bounded Context | Classification | Why it exists as its own context |
|---|---|---|
| **Reminder Management** | **Core Domain** | The heart of the product: Reminder, Recurrence Rule, Occurrence, Entity, Category, Template, Attachment. This is where BuzzMe's actual differentiation lives. |
| **Shared Spaces** | **Core Domain** | Board and Membership — the concept of a relationship holding memory in common. Treated as core alongside Reminder Management because the two are inseparable in the product's philosophy: a Reminder only has meaning *because* of the Board it belongs to. |
| **Notification Engine** | Supporting Domain | Buzz generation and delivery, Notification Preferences. Important and product-differentiating ("friendly, not spammy"), but it serves Reminder Management rather than defining the business itself. |
| **Invitations & Onboarding** | Supporting Domain | Spans Identity, Shared Spaces, and Trust & Safety (must check blocks) and has its own expiry/token lifecycle — kept separate so no single context has to own cross-cutting join logic. |
| **AI / Natural Language Understanding** | Supporting Domain | Converts free text, photos, email, and voice into Draft Reminders. Deliberately isolated so that as input modalities multiply (voice, photo, email, future WhatsApp detection), none of it touches Reminder Management's core rules — it only ever produces a draft that a human confirms. |
| **Identity & Access** | Generic Subdomain | User, Profile, Privacy Settings. Necessary and important, but not differentiating — every shared-access product needs this. |
| **Trust & Safety** | Generic Subdomain | Block, Report, moderation. Necessary for a healthy product, not a differentiator. |
| **History & Activity** | Supporting Domain | Reminder History, Board Activity Feed, and (separately) Audit History. A cross-cutting concern many contexts publish into, but with its own append-only, non-negotiable retention rules that deserve isolation. |
| **Search & Discovery** | Generic Subdomain (future) | Finding users by username, and later, finding Public Community boards. Not needed for MVP but the Visibility model already anticipates it. |
| **Analytics** | Generic Subdomain (future) | Aggregate usage insight. Purely internal-facing; never should influence core domain rules. |

---

## 8. Future-Proofing

The core domain — **Board, Membership, Reminder, Recurrence Rule, Occurrence** — is intentionally small and stable. Every future capability in the brief is designed to attach at the edges rather than reshape the center:

- **Millions of users and reminders:** Occurrence's status as an independent Aggregate Root (§6) means high-frequency actions (complete, dismiss, undo) never contend with or require loading a Reminder's full accumulated history. History's append-only nature scales horizontally with no update contention.
- **Public communities / Public Read-Only boards:** Already accounted for in Board's Visibility invariant (§2) — Private, Invite-Only, Public Read-Only, Public Community are enumeration values on the same concept, not new aggregates. A "Follower"-style lightweight role extends the existing Role set on Membership (already anticipated via Guest).
- **Private families, shared households, teams, and future organisations:** Board is deliberately generic — "a group of people sharing memory" — so it scales from a two-person couple to a 500-person community without new concepts. If true organisation-scale needs emerge later (permission hierarchies, sub-groups, delegated admin), that should be a new bounded context layered *on top of* Board, not a retrofit into Board itself — protecting the simplicity that serves the 99% family/friends case.
- **AI reminder generation, voice input, photo parsing, email parsing:** All are simply different input channels feeding the same "Draft Reminder" concept in the AI/NLU supporting context (§7), which always terminates in explicit human confirmation before touching Reminder Management. Adding a new input modality never requires changing a single core-domain rule.
- **Wearables:** A new delivery channel within Notification Preferences (§2) — changes how a Buzz is delivered, not what a Buzz, Reminder, or Occurrence conceptually is.
- **Future integrations** (calendar export, WhatsApp detection, third-party imports): Modeled as external adapters that either produce Draft Reminders (inbound) or subscribe to Occurrence/Buzz events (outbound). None require new core concepts.

The test for every future feature request should be: *does it fit as a new value in an existing enumeration, a new bounded context, or a new adapter — or does it require touching the invariants in Sections 1 and 4?* The first three are healthy growth. The last is a signal to slow down and revisit this document before building.

---

*This document, together with [PRODUCT_VISION.md](./PRODUCT_VISION.md), is the reference for "is this the right shape." Database schemas, API contracts, and screen designs should all be traceable back to a concept named here — and if a future need can't be expressed in this language, that's a sign the language needs to evolve deliberately, not that the product should quietly grow a workaround.*
