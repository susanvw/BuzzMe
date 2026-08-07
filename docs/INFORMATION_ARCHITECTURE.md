# BuzzMe — Information Architecture & Product Experience

*Builds on the finalized [PRODUCT_VISION.md](./PRODUCT_VISION.md), [DOMAIN_MODEL.md](./DOMAIN_MODEL.md), and [BUSINESS_BEHAVIOR_MODEL.md](./BUSINESS_BEHAVIOR_MODEL.md). This document does not touch any of them — it defines how the business and domain already described should be experienced by a real person holding a phone. No visual design, no wireframes, no implementation.*

*One promise governs every decision below: **help people remember together.** Every navigation choice, every screen, every omission either serves that promise or is cut.*

---

## 1. Primary Navigation Model

**Decision: a 3-item bottom tab bar — Home, Create (center, elevated), Boards — plus two persistent corner affordances (Search, Profile) that are not tabs.**

Why not a drawer: a drawer hides destinations behind a gesture and a label most people never learn to associate with navigation depth — it is the single most reliable way to make an app feel like it has "more in it than you'd think," which is the opposite of *obvious*. Drawers belong to information-dense tools (email, enterprise dashboards); BuzzMe has almost nothing to hide.

Why not Home-only: once a person belongs to more than one Board, they need a stable, revisit-able "place" for board management (invite, settings, membership) that isn't constantly reshuffled by the passage of time the way a daily feed is. A single Home screen would force board management into a menu, which recreates the drawer problem one level down.

Why 3 tabs, not more: every additional tab is a standing decision the user has to learn once and re-make every time they open the app ("where do I find X?"). Search and Profile are *lookup* actions, not *destinations* someone browses idly — they belong as icons, not tabs, freeing the tab bar for the only two places a person actually returns to sit and look around: their day (Home) and their groups (Boards). Create sits between them, visually elevated, because it is the single most important action in the product and deserves to look different from a place you go — it's a thing you *do*.

This is a **hybrid** model in the sense that Web/Tablet reflow the same three destinations into a persistent side rail rather than a bottom bar (see §13) — but the destination count and their meaning never change across platforms.

---

## 2. The Home Experience

**Home shows: a single obvious "Add a reminder" prompt, then Today, then Coming Up. Nothing else, by default.**

*Revised: this prompt opens the four-field structured Create form (§5) directly — it is a shortcut to that form, not an inline free-text capture field. BuzzMe no longer assumes AI parsing is available, so the fastest path is the shortest form, not the fewest keystrokes into a sentence.*

At the very top, before any list, sits one unmistakable, thumb-reachable prompt — visible the instant the app opens, zero navigation between opening the app and starting to fill it in.

Below it: **Today** — every Occurrence due today across every Board the person belongs to (including their Personal Board), collapsed into one calm, chronological list, each row showing who else it concerns if it's shared. Below that: **Coming Up** — the next handful of upcoming Occurrences, lighter visual weight, clearly "not urgent yet."

**Explicitly not on Home by default:** a list of Boards (that's the Boards tab's job — see the IA challenge in §15 about why this isn't redundant), a generic "Activity" feed of what other people did (feels like a social feed the instant it's global rather than scoped to one Board), and any AI "suggestions" module — AI has no standing presence on Home at all in the current product.

Missed items (Occurrences that passed with no action) do not appear as an alarming banner at the top. They collapse into a quiet, low-emphasis "A few things from before" section beneath Coming Up — present, honest, never guilt-inducing (see §11).

---

## 3. Board Experience

**Entering a Board is one tap from the Boards tab. Reminders are the entire primary view — everything else is one tap further, in an options sheet, not a second row of tabs.**

The Board screen shows: the Board's name and a small strip of member avatars up top, then its Reminders (grouped Today / Coming Up / Later, exactly like Home but scoped to this one Board), full width, uncluttered. There is no segmented tab row for "Members / Entities / History" competing with Reminders for visual priority — that would imply four equally important things to look at, when in reality 95% of a Board visit is "what's coming up here."

Tapping the Board's name/avatar strip (or a single "···" affordance) opens **Board Options** — a sheet, not a new screen hierarchy — containing: Members (view/invite/remove/change role), Entities (the short list of pets/vehicles/people this Board tracks), History (the fuller activity look-back), Board Settings (rename, visibility, invitation policy), Mute this Board, and, contextually, Transfer Ownership / Leave / Delete for the Owner. This keeps the primary screen about the *content* (what to remember) and treats governance as something you reach for, not something you look at.

---

## 4. Reminder Experience

**A Reminder row shows only: an icon, its title, and a human-readable "when." Everything else is behind opening it.**

Visible on the row, always: a small category icon (auto-assigned, never chosen by the user unless they want to), the title, and the next due moment in plain language — "Tomorrow at 4pm," "Every year on 9 July," "In 3 days." If the Reminder involves an Entity (a pet, a car), a tiny name/icon appears inline ("🐾 Rex"). If it's shared with specific people rather than the whole Board, a couple of small avatars appear. If someone already resolved a due Occurrence, the row shows it quietly ("✓ Done — Mom") rather than disappearing, so the shared-accountability value is visible without a tap.

**Behind "More" (i.e., tapping into the Reminder):** the full recurrence description, the full participant list, any notes or attachments, the resolution history for past Occurrences, and Edit/Archive/Delete. A list row that tried to show all of this would stop being scannable — the whole point of a shared memory board is being able to glance at ten things in five seconds, not read paragraphs.

---

## 5. Reminder Creation

*Revised: BuzzMe is no longer AI-first. This section describes a short structured form as the one, fully-sufficient primary path — see [MVP_PHILOSOPHY.md](./MVP_PHILOSOPHY.md) §5.*

**The fastest path: tap Create, fill four short fields, tap Save. No AI required, none assumed.**

1. Person taps the center Create action from anywhere (or is already inside a Board, which pre-fills the fourth field below).
2. Four fields, nothing more: **Title** (free text), **When** (a date, plus one of five fixed recurrence options — Once/Daily/Weekly/Monthly/Yearly), **Board** (defaults to wherever they are; a simple picker if not), **Notify** (defaults to "At time," one tap to pick a different fixed preset).
3. **Save** commits it immediately — no intermediate review screen, because there's nothing to review that the person didn't just type themselves.

This is the only creation path required for the product to be complete. **If natural-language input ships in the future, it is a second, optional way to fill in the same four fields** — a person types or speaks a sentence, sees a confirmation card showing what was inferred (the one moment a confirmation step is genuinely needed, since a machine's guess deserves a check a person's own typing doesn't), corrects anything wrong, and saves through the identical path above. It never becomes a second mental model, a second screen shape, or a dependency the structured path relies on.

---

## 6. Search

**One search bar. No category picker before searching.**

A person searching doesn't think "I want to search Reminders" versus "I want to search People" — they think "where's that thing." Typing a query returns grouped results — Reminders, Boards, People, Entities, History — in one scroll, ranked by relevance, with a Board's/Reminder's shared context shown inline in the result itself (e.g., a Reminder result shows which Board it's on) so there's never a need to drill down just to get orientation.

Privacy filtering (per the Domain Model's Search rules) applies invisibly and always — a person who's set themselves to Hidden simply never appears, with no separate "advanced search" mode that could bypass it.

---

## 7. Profile

**Belongs there:** name, username, photo; a short list-shortcut to "My Boards"; Privacy; Notifications; Account (email/phone/recovery); Blocked Users; Help; Sign out / Delete account.

**Never belongs there:** the Reminders themselves (that's Home/Boards' job — Profile is about the person, not their content), any activity feed or "you completed 47 reminders this month!" stat/streak module (gamification directly contradicts the calm, non-productivity-app promise), a public-facing wall or list of posts (there is nothing to post — BuzzMe is not a place people present themselves to an audience).

---

## 8. Settings — Every Setting Challenged

The standard is not "does this setting make sense" but **"why does a person need to touch this at all."**

| Candidate setting | Verdict | Why |
|---|---|---|
| Notification channel (push/SMS/email) | **Keep**, defaulted to push | Real per-person variance in what device they check; but default so well most never open it. |
| Quiet hours | **Keep**, defaulted to a sensible window (e.g. 9pm–7am) | Real need, but the default should satisfy almost everyone. |
| Mute a specific Board | **Remove from Settings entirely** — make it a contextual action inside that Board's options | A person mutes a Board *at the moment* it's annoying them, not by browsing a global settings list; putting it in Settings makes it undiscoverable exactly when it's needed. |
| Per-reminder lead time ("remind me X before") | **Remove as a global setting** — AI/category supplies a sensible default (birthday → day before; medication → same day), overridable per-reminder only | A global default satisfies nobody (every category wants a different lead time) and a global setting most people never find satisfies nobody either. |
| Theme (light/dark) | **Remove** — follow system setting automatically | The OS already solved this; a duplicate in-app toggle is one more decision nobody asked to make. |
| Language | **Keep** | Genuine, unavoidable variance. |
| "Who can invite/mention/board-invite me" (3 separate toggles) | **Collapse into one Privacy preset** (e.g., "Everyone" / "People I know" / "Nobody unless I invite them"), with the three granular toggles demoted to an "Advanced" disclosure only the rare power user opens | Three switches for a decision almost everyone makes the same simple way is exactly the kind of settings-matrix BuzzMe should refuse to build by default. |
| Blocked users list | **Keep**, but buried one level deep | Necessary for management, genuinely low-frequency. |
| Account (email/phone/password/recovery) | **Keep** | Unavoidable. |
| Data export / delete account | **Keep** | Necessary, and likely a legal requirement. |

The measure of success for Settings is that the overwhelming majority of users never need to open it — every entry that survives this table had to prove it can't be a smart default or a contextual, in-the-moment action instead.

---

## 9. Notification (Buzz) Experience

Buzzes read like a thoughtful person texting you, not a system alerting you: *"🐝 Rex's vet visit is tomorrow at 4pm,"* never in red, never with an exclamation mark implying urgency that isn't real. Multiple Buzzes arriving close together are batched into a single grouped notification ("3 things coming up today") rather than stacking individually — the moment a person's lock screen looks like spam, the "friendly not spammy" promise is broken regardless of how nice the copy is.

Dismissal follows native platform conventions (swipe), and tapping a Buzz goes directly to that specific Occurrence, not a generic Home screen — one tap from notification to action. Where the OS supports it, native action buttons ("Done" / "Snooze") let a person resolve an Occurrence without even opening the app — this matters as much as the 10-second creation goal: the fastest possible interaction is the one that happens entirely on the lock screen.

---

## 10. Empty States

A brand-new user should never see a blank, uncertain screen. Home's empty state is the *same* screen as the populated one — capture box front and center, with one soft example beneath it ("Try: 'Mum's birthday every year on 12 March'") and a single gentle nudge to create a first Board with someone, rather than a wall of onboarding carousels explaining features nobody's used yet. The product teaches by being used, not by being explained — the first thing a new user should do is create a reminder, within the promised 30 seconds, not swipe through three slides about the product's philosophy.

---

## 11. Returning Users

**Daily:** Home just works — a quick glance, nothing has to catch up, nothing feels different from yesterday.

**Weekly:** Coming Up naturally has more in it; no structural change, the same screen simply reflects a fuller week.

**Monthly or longer absence:** the risk is a person returning to find a stack of red "overdue" badges that make them feel like they failed at something. BuzzMe must never produce that feeling — missed Occurrences fold quietly into the low-key "A few things from before" section (§2), phrased without blame, and there is no red anywhere in this experience. Returning after a long gap should feel like being welcomed back, not confronted with an inbox of guilt.

---

## 12. Accessibility

- **Large text:** every layout reflows, never truncates or clips — a Reminder row with a long title wraps to a second line rather than ellipsizing content someone needs.
- **VoiceOver / TalkBack:** every icon-only control carries a real label ("Mute Family board," not "button"); the capture box is the first element announced on Home, preserving the <10-second promise for screen-reader users too.
- **Colour blindness:** urgency or status is never colour-only — a missed item is marked with an icon and words, never a red dot alone.
- **Motor impairment:** touch targets are never below platform minimums; every swipe-to-dismiss action has an equivalent tap-based alternative; nothing times out before a person can respond to it.
- **One-handed use:** primary actions live in the lower two-thirds of the screen — the bottom tab bar and the bottom-anchored capture box already support this by construction, not as an afterthought.

---

## 13. Cross-Platform Consistency

**iOS and Android** share the identical IA and terminology (Home / Boards / Create, Buzz, Board, Occurrence) while respecting each platform's native notification, permission, and gesture conventions — the mental model never diverges, only the chrome.

**Web** reflows the same three destinations into a slim persistent left rail instead of a bottom bar (a wide viewport has room to keep navigation always visible rather than requiring a tap) — but it is still exactly three destinations plus the two corner affordances, never more just because there's screen space to fill.

**Tablet** gets the same rail as Web, with room for a master-detail layout (Boards list alongside the open Board's content) — a convenience of space, not a different product.

**Wearables** get no navigation hierarchy at all — a Buzz arrives, and the only actions are Complete / Dismiss / Snooze, directly on the watch face. A wearable that tries to replicate Home, Boards, and Search has already misunderstood what a wearable is for.

---

## 14. Product Anti-Patterns (UX-Specific)

Each of these is a small, individually-reasonable-sounding decision that would quietly turn BuzzMe into something else:

- **→ Calendar:** adding a month-grid as a primary view, hour-by-hour time slots, "find a time that works" scheduling. If a calendar-style view exists at all, it is secondary and optional — never Home's default shape.
- **→ Task manager:** priority flags, subtasks, kanban columns, progress bars, streak counters, nested checklists inside a Reminder. None of these belong; a Reminder is one thing to remember, not a project.
- **→ Chat app:** threaded replies, typing indicators, read receipts beyond a simple "seen," emoji reactions, direct messaging. A note on a Reminder stays a single short line, never a conversation.
- **→ Social network:** a public feed of others' activity, follower counts, likes, a "discover" tab, algorithmic ranking of what's shown. The Board Activity view stays private, scoped to one Board, and purely factual.
- **→ Enterprise tool:** permission matrices beyond Owner/Member/Guest, admin dashboards, bulk CSV import as a primary flow, org charts, SSO-first login screens. If any of this is ever needed for a future business tier, it must never leak into the primary IA that families and friends use.

---

## 15. Information Architecture Tree

```
Home  (tab)
├─ Quick Capture  (always-visible input — not a separate screen)
├─ Today
├─ Coming Up
├─ A few things from before  (collapsed, low-emphasis — missed items)
└─ Reminder Detail  (drill-in from any row, any tab)
   ├─ Edit
   ├─ History  (past occurrences for this reminder)
   ├─ Attachments / Notes
   └─ Archive / Delete

Create  (center action — opens Quick Capture as a sheet from anywhere)
├─ Natural language input (type or speak — one field, two input methods)
├─ Confirmation card  (inline, not a form)
└─ Enter details manually  (fallback only)

Boards  (tab)
├─ [List of shared Boards the person belongs to — Personal Board NEVER listed here, see §18.5]
├─ Create Board
└─ Board
   ├─ Reminders  (default view — identical shape to Home, scoped to this Board)
   │  └─ Reminder Detail  (same as above)
   └─ Board Options  (sheet, not a tab row)
      ├─ Members  (view / invite / remove / change role)
      ├─ Entities  (short list — e.g. Rex, the Corolla — each showing its linked reminders)
      ├─ History  (fuller activity look-back)
      ├─ Board Settings  (rename, visibility, invitation policy)
      ├─ Mute this Board
      └─ Transfer Ownership / Leave / Delete  (contextual to role)

Search  (icon, not a tab)
└─ Unified results: Reminders · Boards · People · Entities · History

Profile  (icon, not a tab)
├─ My Profile  (name, username, photo)
├─ My Boards  (shortcut)
├─ Privacy
├─ Notifications
├─ Account  (email / phone / recovery)
├─ Blocked Users
├─ Help & About
└─ Sign Out / Delete Account
```

**Challenges to this structure, resolved:**

- *Does Boards deserve a whole tab, or could it live inside Home?* Kept as a tab because Board management (invite, settings, membership) needs a stable, revisit-able home that doesn't get pushed around by the passage of time the way Home's daily feed does — collapsing it into Home would force governance actions into a menu, recreating the exact "hidden complexity" problem a drawer would cause.
- *Does Entities deserve a screen at all?* Nearly cut. Kept, deliberately minimal (a short list, not a hub), because the Domain Model's own justification for the concept — "show me everything about Rex" — is a real, distinct value a Reminder list alone can't give. This is the single most cuttable item in the tree if usage data ever shows nobody opens it.
- *Does History deserve a separate screen, or should it be inline?* Resolved as **both, at different depths** — 90% of the value ("who handled this") is shown directly on the Reminder row itself ("✓ Done — Mom"), so most people never need the dedicated History screen at all; it remains one level deeper in Board Options purely for the rare full look-back.
- *Should the Personal Board appear in the Boards tab list?* No — see §18.5. It is a domain modeling convenience, not a user-facing concept, and showing it would break the calm mental model by mixing "me alone" in with "Family," "Rugby," etc.

---

## 16. Key User Journeys

**New user:** Open app → Register/verify (quick, minimal fields) → land directly on Home with its warm empty state → type or speak a first reminder into the already-focused capture box → confirm → done. No tutorial carousel between steps 2 and 3.

**Invite a friend:** From Boards → open a Board → Board Options → Members → Invite → choose channel (link is fastest, no typing an email/number required) → share the link through whatever the person already uses (Messages, WhatsApp, etc.) → done.

**Create a Board:** Boards tab → Create Board → name it → (optionally invite people immediately, or skip and invite later) → land inside the new, empty Board, capture box ready.

**Create a reminder (general):** Home's capture box → type/speak → confirm card → Add. (Same shape whether on Home, targeting the inferred Board, or inside a specific Board, which simply pre-fills that Board as the target.)

**Birthday reminder:** Type "Emma's birthday every year on 9 July" → AI infers annual recurrence, links to the existing "Emma" Entity if one exists (or offers to create one), suggests the Family Board because Emma's already there → confirm → done.

**Recurring reminder:** Type "remind everyone every Tuesday to put the bins out" → AI infers weekly recurrence and "everyone" as all current Board Members → confirm → done. Editing later offers the familiar "just this once / all future" choice the moment more than one Occurrence exists.

**Entity-linked reminder:** Inside a Board with an existing Entity ("Rex"), type "vet visit for Rex next Thursday at 4" → AI recognizes "Rex" as the known Entity rather than proposing a new one → confirm → done; the reminder now shows Rex's icon inline and appears in Rex's own short history.

**Dismiss a Buzz:** Buzz arrives → swipe away (native gesture) or tap the native "Dismiss" action directly on the notification — no app open required.

**Complete an Occurrence:** From the Buzz's native action button, or from the Today list, one tap marks it done — the row updates in place to show who completed it; no confirmation dialog for this low-stakes, reversible action.

**Search:** Tap the search icon from anywhere → type a partial name/word → grouped results appear as you type → tap the right one, land directly in context (e.g., a Reminder result opens straight into that Reminder, already showing which Board it's on).

**Archive (a Reminder or a Board):** Reached via the item's own options — never a global "archive centre" — one tap, one honest confirmation ("Archive this reminder? It'll stop reminding anyone, but its history stays."), done, reversible.

**Delete (a Reminder or a Board):** Same entry point as Archive, but the confirmation is explicit about the difference — a Reminder's history quietly survives deletion (say so plainly, per the §18.9 copy recommendation); a Board's deletion is framed as genuinely permanent and requires a harder confirmation step, matching how much more is actually at stake.

---

## 17. UX Principles

1. One obvious next action beats five configurable ones.
2. If a feature needs an explanation, it isn't ready to ship.
3. Every screen must earn its existence — merge or delete it if it doesn't visibly serve "remember together."
4. Natural language is the primary interface; forms are the fallback, not the front door.
5. Never make someone choose a Board before they've said what they want to remember — infer first, ask only when genuinely ambiguous.
6. Buzzes are gentle, singular messages, never alarms — no red, no exclamation marks implying urgency that isn't real.
7. Missed reminders are shown with warmth, never guilt — no red badges, no shaming language.
8. Every icon-only control needs a real accessible label.
9. Settings should be good enough by default that the overwhelming majority of users never open the Settings screen.
10. Never let a "Delete" action visually promise erasure it doesn't actually perform — word it honestly.
11. Progressive disclosure everywhere: show the minimum on a row, put everything else one tap behind it.
12. No screen should require more than two taps to reach from Home.
13. Board membership — not any app-wide role — governs what a person can see. Never leak content across Boards.
14. Muting and privacy preferences are invisible to everyone else — privacy of *preference* matters as much as privacy of content.
15. AI proposes, a human decides — no AI-parsed action ever commits without a visible, specific confirmation.
16. Destructive-action confirmations name the specific thing being destroyed ("Delete Rex's vet reminder?"), never a generic "Are you sure?"
17. Design for interruption — most real usage is a five-second glance, not a sustained session.
18. Thumb-zone first — primary actions live in the lower two-thirds of the screen.
19. No empty state should feel broken or unfinished — every one gets warmth and an obvious next step.
20. Terminology stays identical everywhere — "Buzz," "Board," "Occurrence" mean the same thing in the UI, in support docs, and in code.
21. No feature is added because a competitor has it — every addition must trace back to the single promise.
22. Group notifications by time relevance, not by Board, app section, or type.
23. Search never requires picking a category first.
24. A returning user after a long absence should feel welcomed, never confronted with a backlog.
25. Accessibility is considered at every design decision, not audited in at the end.
26. Never stack modals — closing one screen should never require closing two.
27. Every shared action (complete, dismiss) visibly credits who did it — that attribution is a core product value, not an audit footnote.
28. When in doubt, cut it — one screen too many is the fastest way to lose the calm feeling.
29. Voice and typing are one feature with two input methods, never two separate features.
30. The app never asks a question it could have inferred the answer to.

---

## 18. Where the Domain Model & Business Behavior Create Friction — Recommendations

The Domain Model and Business Behavior Model are architecturally sound; a few of their correct, DDD-clean decisions nonetheless create real user-facing friction if implemented literally. None of these require reopening those documents — they're implementation and IA guidance layered on top.

1. **The cross-Board "Today" feed on Home requires a read-model that doesn't exist in the write-side domain.** A Reminder belongs to exactly one Board (correctly, per the Domain Model) — but Home's entire value proposition depends on aggregating Occurrences *across every Board a person belongs to* into one calm list. This isn't a flaw in the domain model; it's a flag that engineering must build this cross-Board aggregation as its own read-model/projection, not attempt to bolt it onto the write-side Board aggregate.

2. **The Ownership Transfer handshake (rightly added in the Business Behavior Model to prevent an Owner dumping responsibility on someone) creates a dead-end for a person trying to leave urgently** (e.g., leaving a board during a difficult family situation) if the intended new Owner hasn't responded yet. Recommendation: keep the handshake for *planned* transfers, but give "Leave Board" its own lightweight escape hatch — when the sole Owner wants out and no transfer has been accepted, offer one tap to auto-assign ownership to the longest-standing other Member and leave immediately, notifying that person after the fact rather than requiring their prior acceptance. This mirrors the same logic the Business Behavior Model already proposed for a platform-suspended sole Owner — the same real-world need (get someone out of an ownership deadlock fast) deserves the same answer in both places.
3. **Guest role and Public Board Join Policy should be invisible in the IA until they're real.** Surfacing settings or options for features that don't exist yet (even as disabled/greyed-out) breaks the "obvious" promise — a person shouldn't encounter a toggle that does nothing.
4. **The Personal Board must never appear as a listed Board.** It's a clean, elegant domain modeling choice (every Reminder belongs to exactly one Board, no special-casing) — but if engineering surfaces it literally as a Board card sitting next to "Family" and "Rugby," it breaks the mental model instantly ("why is there a board with just me in it?"). This must be treated as an IA rule, not a suggestion: Personal reminders flow into Home's Today/Coming Up like anything else; the Boards tab lists shared Boards only.
5. **AI's Reminder Draft needs a confirmation UI that looks like the finished thing, not a form.** The Business Behavior Model correctly requires human confirmation before any AI-parsed Reminder commits — but if that confirmation renders as a multi-field form, the whole product-defining "<10 seconds" promise dies in the one moment it matters most. This is the single highest-risk implementation detail in the entire spec.
6. **Report/Block must stay deeply secondary in the UI even though they're first-class domain concepts.** If either surfaces as a prominent, always-visible action (e.g., a big button on every profile), the app starts to feel adversarial by implication — the mere presence of a big "Report" button changes how safe a space feels. Keep both in an overflow, reachable but never featured.
7. **Category and Entity linking must never become a required step during creation.** Both are genuinely useful domain concepts, but if the creation flow ever pauses to ask "which category" or "which entity" before accepting the reminder, it directly damages the core speed promise. AI infers both; a person can correct them afterward, never before.
8. **"Delete" needs honest copy, not honest architecture.** The Domain Model is right that deleting a Reminder never deletes its History (rule 9) — but the word "Delete" naturally implies erasure to anyone who hasn't read the Domain Model. This isn't a model problem; it's a product-copy problem that must be solved deliberately (e.g., "Delete — its history will still be remembered" as inline microcopy) rather than left to whoever writes the button label last.
9. **`OccurrenceMissed`, newly formalized in the Business Behavior Model, needs a UX treatment as deliberate as its domain definition.** The correct domain behavior (auto-transition after a grace window) must never surface as anything resembling a red "overdue" badge — see Principle 7. This is the clearest single place where a technically-correct domain event could, if implemented carelessly, single-handedly undo the product's promised warmth.

---

*This document, together with [PRODUCT_VISION.md](./PRODUCT_VISION.md), [DOMAIN_MODEL.md](./DOMAIN_MODEL.md), and [BUSINESS_BEHAVIOR_MODEL.md](./BUSINESS_BEHAVIOR_MODEL.md), completes the pre-visual-design foundation for BuzzMe. Every screen, wireframe, and interaction pattern designed from here forward should be traceable to a navigation decision, journey, or principle named above — and any future feature that can't find a place in this tree without adding a fourth tab, a settings toggle, or a red badge should be treated as a signal to redesign the feature, not the tree.*
