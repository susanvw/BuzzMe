# BuzzMe — MVP Scope: The Ruthless Cut

*This document doesn't redesign anything in the architecture that precedes it — it decides what of it ships first. The test for every line below is the one the brief set: could you explain it to your mom in 30 seconds? If not, it's cut, simplified, or deferred.*

*Consolidated from an earlier two-document split (a scope-cut pass, then a philosophy-tightening pass) into one file — maintaining two overlapping "MVP" documents was itself a small piece of the complexity this document exists to remove. Nothing below contradicts the six architecture documents that precede it; it exercises a deliberately small slice of what they describe.*

**The core idea, restated, because everything below answers to it:** People create Boards. People invite others to a Board. Everyone on a Board sees the same reminders and gets buzzed at the same time — the creator picks when, from a short fixed list, once. No per-person schedules. No configuration. No AI dependency. If you want something nobody else needs to know, that's what your Personal Board is for — which isn't a different kind of thing, just a Board with one Member. The product should feel closer to a WhatsApp group for remembering things than a productivity application.

---

## 1. Feature-by-Feature Review

| Feature | Decision | Why |
|---|---|---|
| **Entities** (Rex, the car) | **MOVE TO FUTURE** | Real value, zero necessity — you can create and share a reminder without ever needing to know what an Entity is. Adds a screen, an empty state, and a linking step for a benefit that only shows up after weeks of use. |
| **Categories** | **MOVE TO FUTURE** | V1 ships with one generic reminder icon. Category-based icons are a polish layer with nothing to learn from yet — build it once real usage shows which categories actually matter. |
| **History (dedicated screen)** | **SIMPLIFY** | The one-line "✓ Done — Mom" credit directly on the reminder is the actual value (shared accountability) and it's nearly free. The deeper look-back screen is a nice-to-have — defer it. |
| **Reminder Notes** | **MOVE TO FUTURE** | A reminder is a title and a time. The moment it also has notes, it's inviting itself to become a task with a description field. |
| **Attachments** | **MOVE TO FUTURE** | Has no real purpose until photo-import exists, which is itself future and explicitly non-AI (see [V2_IMPORTS.md](./V2_IMPORTS.md)). Building the storage/UI for it now is building ahead of the feature that needs it. |
| **Search** | **SIMPLIFY** | Reminders and Boards only. No People, Entities, or History facets — those categories barely exist in V1 anyway (see below). |
| **AI creation as the primary path** | **REMOVE as a pillar; MOVE the feature itself to future, optional** | See §5 — this is the single biggest change from the original architecture. The product must be, and is, completely successful with zero AI involved. |
| **Recurring reminders** | **SIMPLIFY** | Once, Daily, Weekly, Monthly, Yearly. Five options, not an open-ended rule builder. Nobody explaining this to their mom says "every second Tuesday except school holidays." |
| **Notification presets** | **SIMPLIFY, FIXED** | At time, 15 minutes before, 1 hour before, 8 hours before, 1 day before, 1 week before — a short fixed list, defaulted to **At time** so most people never touch it, changeable with one tap. No per-person schedules, ever — this is the whole point of the simplification. |
| **Owner/Member only** | **SIMPLIFY, with one addition** | Two roles. See §4 — Remove Member is added as a third Owner power; everything else stays exactly this simple. |
| **Guests** | **MOVE TO FUTURE** | A role for a use case (limited, temporary access) V1 doesn't have a scenario for yet. |
| **Snooze** | **REMOVE, not deferred** | A snooze button says "this is a task I'm avoiding." That's the one feature most likely to turn BuzzMe into a to-do app by accident. |
| **Archive** | **REMOVE** | Deleting a Reminder already keeps its history (that's a hard rule, not a UI choice) — so Archive and Delete do almost the same thing from where the user's standing. One honest "Delete" beats two confusing near-synonyms. Board Archive goes too: an unused Board just sits there quietly; nobody needs a button to formally pause it. |
| **Delete enough?** | **YES** | With honest copy ("stops reminding, but stays in history"), Delete alone covers it. |
| **Reminder editing** | **SIMPLIFY** | Editing reuses the exact same short form as creating one — open it, change what's wrong, save. Not a separate "editor" screen with its own layout to learn. |
| **Board settings** | **SIMPLIFY** | Name, Delete, and that's essentially it. No visibility tiers (every V1 Board is simply private to its members), no invitation-policy toggle — see §4 for why "any Member can invite" is the one fixed default. |
| **Profile settings** | **SIMPLIFY** | Name, photo, account (email/phone), Delete Account. Nothing else. |

**A few more the same lens exposes**, beyond the explicit review list:

| Feature | Decision | Why |
|---|---|---|
| Notification Settings screen | **REMOVE** | With no per-person schedules and no channel choice to make, there's nothing left to configure. Rely on the OS's own notification settings. Per-Board Mute survives as the one exception — it's contextual, one tap, and genuinely useful. |
| Privacy visibility settings (Public/Hidden/Visible-by-username, etc.) | **MOVE TO FUTURE** | These govern discoverability — and there's no discovery in V1 (see People Search, below). A setting with nothing to control isn't a setting, it's a screen nobody needed. |
| People / username Search & Discovery | **REMOVE for V1** | Invite by link or QR code only. You invite people you already know; you don't need to find strangers. This single cut quietly removes the need for most of the Privacy visibility model too. |
| Per-reminder Participant subsetting ("just Mom and Dad") | **REMOVE, permanently** | Not deferred — ruled out. Every Reminder belongs to and notifies the whole Board, no exceptions. If two people need different reminders, the answer is another Board, not a picker — see the Domain Model amendment in §7. |
| Mentions (@name) | **MOVE TO FUTURE** | Nothing left for a mention to attach to once Notes are cut. |
| Explicit Transfer Ownership flow | **MOVE TO FUTURE** | If the Owner leaves and others remain, BuzzMe silently hands ownership to someone else. No offer/accept screen needed for V1 — just make leaving always work. |
| Member Role Change UI | **REMOVE for V1** | With only Owner and Member, and ownership handoff automatic, there's nothing left to change between. |
| Report + Moderation Queue | **MOVE TO FUTURE** | Block alone (one screen, one action) covers "get away from someone" for V1. Abuse reporting routes to a plain support email address instead of in-app moderation infrastructure. |
| **Remove Member** | **KEEP — non-negotiable** | Not in the original permission list; added deliberately. See §4. Exempt from every future simplification pass. |
| **Block** | **KEEP — non-negotiable** | Minimal, essential, one screen, one action — the safety net every V1 still needs regardless of how small the app is. Exempt from every future simplification pass. |
| AI Duplicate Detection | **MOVE TO FUTURE** | A nice catch, not a launch requirement — and depends on AI, which is no longer a pillar at all. |
| Structured Create/Edit form | **KEEP — this is the core loop** | Four fields (Title, When, Board, Notify), commits directly, no AI required. This — not an AI confirmation card — is what makes the product feel fast. |

---

## 2. Notifications, Permissions, and AI — The Three Things That Actually Changed

**Notifications:** one fixed flow — `Reminder → Board → Members → Notify everyone` — from a fixed preset list, defaulted to **At time**. No per-person timing, ever.

**Permissions:** Owner (Rename, Delete, **Remove Member**), Member (View, Add, Edit, Complete, Leave). **Any Member can invite, by default, with no toggle** — the same balance a WhatsApp group strikes (anyone can add, only an admin/Owner can remove), which is what makes open inviting safe rather than reckless. No Admin, no Moderator, no Guest, no permission matrix.

**AI:** demoted from product pillar to optional future input method. The default creation flow is a short structured form that commits directly — no confirmation step, because there's nothing to review that the person didn't just enter themselves. If natural-language creation ships later, it fills the identical form and shows a confirmation card *only* for that path, since that's the one case a machine's guess genuinely needs a human check. It changes no domain object, no workflow, no mental model. The product is, and must remain, completely successful if AI never ships at all — this also quietly removes the single largest architectural risk the Event Storming document flagged (an LLM provider's latency sitting on the core creation promise); it no longer sits there.

---

## 3. The MVP, Answered Directly

**How do I create a Board?**
Boards tab → "+" → type a name → done. You're the Owner.

**How do I invite friends?**
Inside the Board → Invite → share a link however you already share things, or show a QR code. They tap it; if they don't have BuzzMe yet, they get it and land straight inside the Board. Any Member can do this, not just the Owner.

**How do I create a reminder?**
Tap Create. Title, When, Board, Notify — four short fields, sensible defaults already filled in. Save. Under ten seconds, no AI required.

**How do notifications work?**
Whoever creates the reminder picks when everyone gets buzzed, from a short fixed list, defaulted to "At time" so most people never touch it. Every Board Member gets the exact same buzz at the exact same time. Nobody sets up their own schedule.

**How do people complete reminders?**
Tap "Done" — right on the notification, or in the app. Everyone else on the Board sees who did it.

**How do I edit a reminder?**
Open it, change the part that's wrong, save. Same short form as creating one.

**How do I leave a Board?**
Tap Leave. If you're the only Owner and other people remain, BuzzMe quietly hands ownership to someone else so you're never stuck.

**How do I delete a Board?**
Owner only. One confirmation, naming the Board. It's gone for everyone, permanently.

**How do I delete my account?**
One confirmation. If you're the last owner of a shared Board, BuzzMe hands it off automatically first. Your account and personal reminders are gone; anything you contributed to a shared Board's history stays for the people who relied on it, just no longer tied to your name.

**"What does BuzzMe do?"** — in one paragraph:
*BuzzMe is a shared reminder app for the people you actually care about. You create a Board for your family, your team, your trip — whatever the group is — invite them with a link, and from then on, whenever anyone adds something worth remembering — a birthday, a vet visit, bringing the tent — everyone on that Board gets buzzed at the same time, so nobody's left being the only one who remembers. No calendar to sync, no tasks to manage, no AI to trust, nothing to configure. You just say what to remember, and BuzzMe makes sure your people know too.*

---

## 4. What Actually Ships

Fifteen screens, not thirty:

Splash · Welcome · Register · Verify · Login · Forgot Password · **Home** · **Reminder Detail** · **Create/Edit** (one short structured form, reused for both — no separate "editor" screen, no AI confirmation step in the default path) · **Boards list** · **Board Detail** · **Board Options** (Members/Invite, Mute, Rename, Delete, Leave — nothing else) · **Board Members, Invite & Remove** · **Profile** · **Delete Account**

Everything else named in the prior documents still exists as a real, considered idea — it's simply not in the way of the first thing a new user does. That's the actual measure of success here: not how much BuzzMe can eventually do, but how little a person has to learn before they've already remembered something together with someone they love.

---

## 5. What This Means for the Existing Documents

Nothing here required reopening the Domain Model's core shape — a Board with Members and Reminders was already exactly right (Domain Model §1.2's Personal Board decision is the proof: it was never a separate concept, just a Board with one Member). What changed is narrower and more surgical:

- **Participant subsetting is now permanently ruled out**, not deferred — Reminder always notifies its whole Board.
- **AI drops out of the default path entirely** — the structured form is what "10 seconds" is measured against now, not AI parsing speed.
- **Remove Member was missing from the given permission list and has been added** — a genuine gap, not scope creep, and non-negotiable going forward.
- Every other cut in this document is exactly that — a cut, reversible by nothing more than moving an item from "future" back onto the roadmap once real usage justifies it.

---

*This document is a scope decision layered on architecture that remains correct as written. "MOVE TO FUTURE" means "not yet" — every one of those items already has a home to return to when it's earned its place. "REMOVE" means the opposite: a considered decision that the feature doesn't belong in this product's identity at all, not just its first release.*
