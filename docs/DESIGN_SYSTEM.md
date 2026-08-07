# BuzzMe — Design System

*Builds on the finalized [PRODUCT_VISION.md](./PRODUCT_VISION.md), [DOMAIN_MODEL.md](./DOMAIN_MODEL.md), [BUSINESS_BEHAVIOR_MODEL.md](./BUSINESS_BEHAVIOR_MODEL.md), [INFORMATION_ARCHITECTURE.md](./INFORMATION_ARCHITECTURE.md), [EVENT_STORMING.md](./EVENT_STORMING.md), and [PRODUCT_UX_SPECIFICATION.md](./PRODUCT_UX_SPECIFICATION.md). None of those are redesigned here. This document defines BuzzMe's visual and interaction language — not a component library, a set of literal tokens, or a Figma file, but the rules those artifacts must obey.*

---

## 1. Design Philosophy

BuzzMe looks and feels the way it does for one reason: **it has to feel like it came from someone who cares about you, not a system tracking you.** Every visual and interaction decision in this document is a direct translation of the product's one promise — *help people remember together* — into how things look, move, sound, and respond to touch.

A design system's job is usually to make a product feel consistent. BuzzMe's has a second, equally important job: to make sure the product never accidentally drifts into feeling like a calendar, a task manager, a chat app, a social network, or an enterprise tool — because every one of those categories has its own extremely well-worn visual vocabulary, and reaching for that vocabulary out of habit is the single easiest way to betray the product's actual identity without ever making an explicit decision to do so.

---

## 2. Emotional Tone & Brand Personality

**Warm.** Like a note left by someone who was thinking of you. **Calm.** Nothing on screen should ever feel like it's competing for panicked attention. **Human.** Plain language, real handwriting-adjacent warmth in typography and illustration, never robotic or clinical. **Fast.** Speed itself is part of the feeling — friction reads as the app not caring enough to get out of your way. **Trustworthy.** Consistent, honest, never manipulative — nothing here nudges, guilts, or games a person into engagement.

If BuzzMe were a person, they'd be the friend who remembers your dog's vet appointment before you do, mentions it once, warmly, and never brings it up again if you forget. Not a personal assistant. Not a productivity coach. Not a mascot with a personality of its own — a quiet, dependable presence.

**Explicitly not:** corporate (no enterprise-blue trust-badge aesthetic), not enterprise (no dense dashboards, no data-density-as-competence), not gamified (no streaks, points, badges, or celebratory fireworks for basic use), not a task manager (no kanban, no priority flags, no progress bars), not a calendar (no grid-of-time visual language anywhere).

---

## 3. Visual Principles

1. **Softness over sharpness** — rounded corners, rounded icon strokes, rounded typography terminals. Sharp rectangles read cold and administrative.
2. **Restraint over decoration** — every visual flourish must earn its place; the default answer to "should we add this" is no.
3. **Hierarchy through type and space, not color** — color is not a crutch for importance; size, weight, and position do that work first.
4. **One accent color, used sparingly** — a warm, honey-toned amber, evoking the bee/buzz motif without being literal or childish. It marks the single most important element on a screen, never used decoratively across a whole layout.
5. **Generous whitespace** — density is never the goal; a screen that feels like it's breathing is a screen that feels calm.
6. **Elevation means "temporary," not "important"** — a shadow indicates something floating above the base layer (a sheet, a dialog), never a way to rank static content by visual weight.
7. **Never urgent by default** — nothing in the visual language communicates alarm unless something is genuinely, rarely, irreversible.

---

## 4. Typography System

A warm, humanist sans-serif — moderate x-height, gently rounded terminals, legible at small sizes without feeling clinical. Avoid ultra-thin weights (they read as fashion-forward/enterprise) and avoid heavy, blocky weights (they read as shouty/gamified).

A deliberately small scale — four steps, not a sprawling system:

| Role | Use |
|---|---|
| **Title** | Screen and section headers, Reminder titles in Detail view |
| **Body** | List row primary text, form field content, most reading |
| **Secondary** | "When" text, participant names, supporting metadata on a row |
| **Caption** | Timestamps, micro-labels, the smallest legible unit |

All four steps respect the platform's Dynamic Type / font-scaling settings and reflow rather than clip at any size — this is non-negotiable and stated again in §16.

---

## 5. Color Philosophy

Color in BuzzMe communicates warmth and orientation, never status-by-alarm.

- **Base neutrals:** warm off-white/cream in light mode, warm charcoal (never pure black, never cold navy) in dark mode — the neutral palette itself carries warmth, not just the accent.
- **The one accent (honey/amber):** reserved for the single primary action on a screen and for the brand mark. Never used as a background fill across large areas, never used decoratively.
- **A quiet secondary (muted, calm teal-adjacent):** used sparingly for links or a second-tier accent where two distinguishable actions genuinely need to coexist — never competing in visual weight with the primary amber.
- **Status color is minimal by design:** a "done" state is communicated by an icon and reduced opacity/strikethrough treatment, not a green badge; a missed item is communicated by icon and wording, never red (see Principle 3 in §17). **Red is reserved exclusively for the single truly critical, irreversible confirmation moment (Delete Account) — nowhere else in the entire product.**
- Every color pairing meets WCAG AA contrast in both light and dark mode without exception.

---

## 6. Iconography

Simple, single-weight line icons with rounded caps and joins, matching the typography's warmth. No gradients, no 3D rendering, no photorealistic icon treatments, no double-weight "duotone" trend icons.

**A hard rule: no literal calendar-grid icon appears anywhere in the product.** A grid-of-boxes icon visually says "this is a calendar app" faster than any amount of product copy can say otherwise — clocks, bells, and the Buzz bee mark are used instead wherever "time" or "reminder" needs representing.

The bee/buzz mark is a **brand mark**, not a mascot — it appears on the app icon, loading/splash moments, and as the visual anchor for the word "Buzz" itself. It never becomes an animated character that talks, celebrates, or follows the user through the app; that would be the fastest route to the "gamified" territory this product explicitly rejects.

---

## 7. Illustration Style

Soft, warm, minimal line-and-fill illustrations used exclusively for empty states and a small number of milestone moments (first Board created, account verified). Illustrations depict a **feeling** — calm, togetherness, warmth — abstractly, rather than literalizing the screen's content ("an empty inbox," "a blank folder"), which is a tired SaaS cliché this product should visibly avoid. People depicted in illustrations are diverse and inclusive by default, never a single default representation. One consistent illustration family across the whole app — no screen invents its own separate illustration style.

---

## 8. Components

*For every component: Purpose, Behavior, Interaction, States, Accessibility, and — deliberately — When NOT to use it, because knowing a component's boundary is as important as knowing its shape.*

### 8.1 Cards

| Field | Detail |
|---|---|
| Purpose | Represent one scannable unit of content — a Reminder, a Board, an Entity. |
| Behavior | Rounded corners, generous internal padding, minimal-to-no shadow — flat with a subtle background tone shift preferred over a heavy drop shadow. |
| Interaction | The entire card is one tappable target; secondary actions (Complete/Dismiss) are distinct, clearly separate tap targets within it, never nested smaller cards. |
| States | Default, pressed (subtle opacity/scale feedback), completed (muted/checked treatment, stays visible), syncing (small quiet indicator), error (inline retry affordance). |
| Accessibility | Reading order matches visual order; the whole card and its sub-actions are each independently reachable and labeled for screen readers. |
| When NOT to use | Never nest a card inside another card. Never use elevation/shadow depth to imply one card is "more important" than another — that's type and position's job. Never use a card as a primary navigation destination — that's what tabs are for. |

### 8.2 Lists

| Field | Detail |
|---|---|
| Purpose | The primary content pattern for Home and Board Detail — chronological, scannable rows. |
| Behavior | Simple rows with generous vertical rhythm; no zebra striping, no gridlines — those read as spreadsheet/enterprise. |
| Interaction | Tap (primary), swipe (optional accelerator, always with a tap equivalent), long-press (contextual menu accelerator). |
| States | Loading (skeleton rows matching the eventual layout), empty (illustration + copy + action), populated, item-syncing, item-error. |
| Accessibility | Each row announces as one coherent phrase (title, when, participants, status), not a jumble of separately-announced fragments. |
| When NOT to use | Never force a short, fixed set of options (like Board Options' menu) into a long scrolling list styling if a simple stacked set of rows in a sheet reads more calmly. Never use a nested/collapsible tree list — too file-explorer, too enterprise. |

### 8.3 Buttons

| Field | Detail |
|---|---|
| Purpose | Commit to an explicit action. |
| Behavior | Rounded, soft-filled primary style in the single accent color; secondary/tertiary actions use a text or outline style to keep visual noise low. |
| Interaction | Tap; a loading state replaces the label with an inline spinner in place, never a separate blocking overlay. |
| States | Default, pressed, loading, disabled (used sparingly — see §17 Principle 32), brief success acknowledgment where relevant. |
| Accessibility | Always carries a real text label — primary actions are never icon-only; minimum touch target maintained regardless of visual size. |
| When NOT to use | Never add a button for an action a direct tap on the surrounding content already accomplishes (e.g., a redundant "View" button on an already-tappable card). Never show two filled/primary-style buttons in the same view — one obvious action, always. |

### 8.4 Inputs

| Field | Detail |
|---|---|
| Purpose | Capture text — most importantly, the natural-language capture box, the single most important input in the product. |
| Behavior | Soft rounded field, generous tap target; placeholder text is always a real example ("Emma's birthday every year on 9 July"), never a generic instruction ("Enter title"). |
| Interaction | Tap to focus, type or use the mic icon to dictate into the same field. |
| States | Empty/placeholder, focused, filled, error, disabled (rare). |
| Accessibility | A real label always accompanies the field for screen readers — placeholder text is never the only label, per WCAG guidance. |
| When NOT to use | Never present multiple small structured inputs where one natural-language field could capture the same intent — this is a direct, permanent design-system enforcement of "AI reduces forms" (Product Vision, Principle 7). |

### 8.5 Chips

| Field | Detail |
|---|---|
| Purpose | Represent small, factual metadata — a Category, an Entity tag, a scope indicator in Search results. |
| Behavior | Rounded pill shape, soft fill — never a hard-cornered badge. |
| Interaction | Some chips are tappable (a filter), some purely informational (a category tag on a card) — the tappable ones carry slightly more visual contrast so the distinction is legible, not just implied. |
| States | Default, selected (for filter chips), disabled. |
| When NOT to use | Never as a gamification badge — no streak counters, no "5 completed!" achievement chips, ever. Never more than two chips on a single card — beyond that, it's visual noise, not information. |

### 8.6 Bottom Sheets

| Field | Detail |
|---|---|
| Purpose | Host secondary, occasional actions and menus (Board Options, Invite channel choice) without navigating away from the current context. |
| Behavior | Slides up from the bottom, gently dims the background, rounded top corners, a visible drag handle. |
| Interaction | Swipe down or tap outside to dismiss without consequence; tapping an item performs the action or navigates one level deeper within the sheet's context. |
| States | Default (opens with its content already available — sheets should never show a loading spinner on open). |
| Accessibility | Focus moves into the sheet on open and returns to the triggering element on close; dismissible via Escape on web. |
| When NOT to use | Never for a screen's single most frequent action (that belongs inline, like the capture box). Never stack a sheet on top of another sheet. |

### 8.7 Dialogs

| Field | Detail |
|---|---|
| Purpose | Reserved exclusively for the small handful of truly irreversible, other-people-affecting confirmations named in the UX Specification (Delete Reminder, Delete Board, Remove Member, Delete Account). |
| Behavior | Centered, minimal, plain-language copy naming the specific object affected; two clear actions (Cancel, and the named destructive action). |
| Interaction | The destructive action is never pre-focused/defaulted — an accidental confirm-by-reflex must not be possible. |
| States | Default only — no loading state inside a dialog; the action it triggers happens after dismissal. |
| Accessibility | Focus trapped within, first focus lands on Cancel (the safe choice), fully readable by screen readers as one coherent warning. |
| When NOT to use | Never for a reversible action — that's a toast's job. Never to display purely informational content — that's a sheet's or inline copy's job. Dialogs are deliberately rare; their rarity is what makes them feel serious when they do appear. |

### 8.8 Navigation

| Field | Detail |
|---|---|
| Purpose | The three-destination bottom tab bar (Home / Create / Boards) plus corner Search and Profile icons, per the Information Architecture. |
| Behavior | Soft, minimal tab icons; the active tab is shown through a subtle fill/color shift, never a loud animated indicator. The center Create action is visually elevated, distinct in shape from the two flanking tabs. |
| Interaction | Tap to switch; the center action opens the capture sheet from anywhere. |
| States | Active, inactive; an optional, soft, non-numeric dot may indicate "something's due today" on the Home tab — never a red numbered badge, which reads as an unread-count/inbox pattern this product deliberately avoids. |
| Accessibility | Each tab carries a text label alongside its icon, not icon-only; current tab is announced as selected. |
| When NOT to use | Never add a fourth tab — see §17 Principle and the Information Architecture's own reasoning for why three is the ceiling. |

---

## 9. Motion Philosophy

Motion in BuzzMe always communicates something real — a state change, a spatial relationship, an object becoming another object (the capture box growing into the AI Confirmation Card). It is never decoration for its own sake, and it is never used to manufacture delight through novelty — the delight in this product comes from speed and warmth, not from motion tricks.

Easing is a single calm deceleration curve for entrances and a matching acceleration for exits — **no bounce, no overshoot, no elastic/spring physics anywhere.** Spring-and-bounce motion reads as playful in a way that tips toward the "gamified" territory this product avoids; BuzzMe's motion should feel like a held door, not a trampoline.

---

## 10. Animation Durations

A small, fixed scale — not an open palette:

| Tier | Duration | Used for |
|---|---|---|
| **Micro** | 100–150ms | Button press feedback, chip selection |
| **Short** | 150–250ms | Row insert/update, sheet dismissal, tab switch |
| **Medium** | 250–350ms | Sheet opening, the capture-box-to-confirmation-card transform |
| **Deliberate (exception)** | 400–500ms | The single Occurrence-completed checkmark tick — a rare, meaningful, one-time beat, not a repeated transition, and the only place a longer duration is justified |

Nothing routine or interactive exceeds ~400ms; anything slower starts to feel sluggish rather than considered.

---

## 11. Haptic Feedback

Used sparingly and only for meaningful confirmations: a light tick on completing an Occurrence (mirroring the visual tick), a light tick on a successful AI-confirmed Add. **No haptic feedback on routine navigation** — tab switches, sheet opens, and scrolling never trigger haptics. Reserving haptics for genuinely meaningful moments is what keeps them meaningful; using them everywhere is what makes an app feel like a game controller.

---

## 12. Sound Philosophy

The app is silent by default during normal use — no sound plays for navigation, scrolling, or routine taps. Sound is reserved almost entirely for the system notification itself: a soft, short, warm tone evoking an actual gentle buzz, never a jingle, never a bright "reward" chime. An optional, soft, off-by-default in-app confirmation sound may exist for completing an Occurrence, but it always respects the system's silent/mute setting without exception — sound is never a required channel for understanding what happened.

---

## 13. Loading Philosophy

Skeleton screens — placeholders matching the eventual layout — are preferred over spinners for any list or content area; they preserve layout stability and feel faster even at identical actual latency. A full-screen blocking spinner is reserved for the unavoidable Splash screen only. The AI "thinking" state (§14) gets its own distinct, quiet micro-treatment — never a generic spinner, and never an over-designed "AI is working hard" showcase either.

---

## 14. Empty States

Every empty state pairs one warm, abstract illustration (§7) with exactly one line of human copy and exactly one clear next action — never copy alone, never an illustration alone, never more than one competing action. All empty states across the app share the same illustration family, so a first-time Board, a first-time Search, and a first-time Entities list all feel like the same product, not five different ones stitched together.

---

## 15. AI Interaction Language

AI in BuzzMe is felt, not branded. There is no "Ask AI" button, no sparkle/magic-wand icon, no gradient-bordered "AI feature" badge, no chatbot bubble interface — the capture box simply understands what's typed or spoken, the way a thoughtful friend would, without announcing that a model is involved. If a visual marker is ever needed to distinguish an AI-inferred field from a user-confirmed one on the Confirmation Card, it is minimal — a soft, quiet indicator, never a flashy treatment that turns "AI" into a showcased feature in its own right. This is the direct visual expression of the Product Vision's own rule: *AI reduces forms, it doesn't add features.*

---

## 16. Notification Design

The system push notification is the friendly sentence and bee mark defined in the UX Specification's microcopy (§11 of that document) — no custom notification sound beyond the soft buzz tone (§12), no visual badge inflation. Grouped notifications present a calm, summarized line ("3 things coming up today"), never a stacked pile that reads as noise the moment a lock screen is glanced at.

---

## 17. Accessibility Standards

- WCAG AA contrast, minimum, for every text/background/icon pairing, in both light and dark mode, without exception.
- Full Dynamic Type / font-scaling support everywhere — reflow, never clip.
- Status is never color-only; every state pairs color with an icon or word.
- Every interactive control meets platform minimum touch target size.
- Every icon-only affordance carries a real accessible label.
- Every gesture-based interaction (swipe, long-press) has a full tap-based equivalent.
- The system `reduce motion` setting disables all non-essential animation, leaving only the motion required to communicate a state change.
- Native platform accessibility tooling (VoiceOver, TalkBack, voice control, switch control) is respected over any custom-built equivalent.

---

## 18. Dark Mode Philosophy

Dark mode is a second **warm** mode, not a colder or more "serious" one — a common mistake this system deliberately avoids. The base neutral shifts to a warm charcoal (never pure black, never a cold enterprise navy), and the honey/amber accent is gently desaturated to avoid visually vibrating against the dark background, while remaining unmistakably the same accent. Both modes should read as the same product having the same personality at a different time of day — not two different brands.

---

## 19. Platform Adaptations

**iOS:** native swipe-back gesture, native share sheet, native notification action buttons, SF-Symbols-weight-matched iconography, full Dynamic Type support.

**Android:** Material-adjacent but restrained — BuzzMe does not adopt Material's heavier elevation/ripple conventions wholesale where they clash with the calm, soft aesthetic defined here; native back-gesture, adaptive app icon, and notification channels are respected.

**Web:** the three-destination navigation reflows into a persistent side rail (per the Information Architecture) rather than a bottom bar; full keyboard navigation with a logical tab order; hover states are a touch-first-friendly *enhancement*, never a requirement, since many web visitors are on touch devices too.

Across all three, terminology, iconography, motion, and color are identical — only the chrome and native conventions differ, never the personality.

---

## 20. Fifty Immutable Design Principles

*Every future screen, component, and feature should be measurable against these. If something new conflicts with one of them, the new thing changes — not the principle.*

1. Warmth is the default emotional register — every visual decision should feel like it came from someone who cares, not a system.
2. One accent color, used sparingly, never used decoratively across large areas.
3. Red is reserved exclusively for the single truly critical, irreversible confirmation moment — nowhere else in the product.
4. Rounded corners everywhere — sharp rectangular edges read cold and corporate.
5. Shadows indicate a temporary floating layer (a sheet, a dialog) — never decoration implying importance on static content.
6. Typography, size, and spacing carry hierarchy first — color is a secondary tool, not the default one.
7. No more than one primary (filled) button visible on screen at any time.
8. Icons are simple, single-weight line icons — no gradients, no 3D rendering, no photorealistic icon sets.
9. No literal calendar-grid icon appears anywhere in the product.
10. The bee/buzz motif is a brand mark, not a mascot — it never becomes an animated character that talks to the user.
11. Illustrations depict feeling (calm, togetherness, warmth) abstractly — never a literal "empty inbox" cliché.
12. Every screen has exactly one obvious next action, visually distinguishable from everything else on it.
13. Motion always has a purpose — communicating a state change or spatial relationship — never decoration for its own sake.
14. No routine interactive transition exceeds roughly 400ms.
15. No bounce, no overshoot, no elastic easing anywhere — motion is calm deceleration, never playful spring physics.
16. Confetti, badges, streaks, and celebratory animations are permanently out of scope.
17. Haptic feedback is reserved for meaningful confirmations only, never routine navigation.
18. The app is silent by default; sound is reserved for the notification "buzz" tone and nothing else.
19. Skeleton loading states are preferred over spinners for any list or content area.
20. The AI "thinking" moment gets its own quiet, branded micro-treatment — never a generic spinner, never a flashy showcase.
21. AI is never visually branded with a sparkle icon, gradient badge, or chatbot affordance — it should feel invisible, not showcased.
22. A dialog is reserved exclusively for irreversible, other-people-affecting confirmations.
23. Reversible actions get quiet toast/snackbar feedback, never an interrupting dialog.
24. Bottom sheets are for secondary, occasional actions only — never a screen's single most frequent action.
25. Never stack a sheet on top of another sheet, or a dialog on top of a sheet.
26. Chips are for small, factual metadata only — never gamified badges or achievement indicators.
27. No card is ever nested inside another card.
28. Lists never use zebra striping, gridlines, or spreadsheet-style visual density.
29. Whitespace is generous by default — density is never the goal, clarity is.
30. Placeholder text in an input is always a real example, never a generic instruction.
31. Validation errors are shown with an icon and words, never a color change alone.
32. Disabled states are used sparingly — prefer letting an action be attempted with clear contextual feedback.
33. Tab bar active states are shown through subtle color/fill change, never a loud animated indicator.
34. Numbered notification badges are avoided — at most a soft, non-numeric dot signals "something's here."
35. Dark mode is a second warm mode, not a colder or more "serious" one.
36. Every color combination meets WCAG AA contrast, in both light and dark mode, without exception.
37. Icon-only controls are a last resort — a visible or accessible-name label accompanies them wherever feasible.
38. Motion respects the system's reduce-motion setting; every non-essential animation has a static equivalent.
39. Native platform conventions (swipe-back, back-gesture, native share sheets) are always respected over custom-built equivalents.
40. The product never invents a custom keyboard, custom date-picker wheel, or other system-level control where a native one already works well.
41. Empty states always pair an illustration with one line of human copy and exactly one clear action.
42. A completed item stays visibly present with a quiet "done" treatment — it's never yanked from view the instant it resolves.
43. Text truncation is a last resort; when space is genuinely too small, wrap before you clip.
44. The interface never uses urgency-manufacturing styling (exclamation marks, all-caps, countdown timers) for routine reminders.
45. Every destructive confirmation names the specific object being affected — a generic "Are you sure?" is never acceptable.
46. Motion, sound, and haptic feedback are always additive and skippable — never the only channel carrying essential information.
47. The design system never borrows visual patterns from project-management tools (kanban cards, priority flags, progress bars), even when a feature superficially resembles one.
48. The design system never borrows visual patterns from social platforms (like buttons, follower counts, public activity walls), even when a feature superficially resembles one.
49. If a new component doesn't clearly serve "help people remember together," it doesn't ship — regardless of craft quality.
50. When any principle above conflicts with a proposed feature, the feature changes. These fifty principles do not bend to accommodate it.

---

*This document, together with the five it builds on, completes BuzzMe's foundation from business philosophy through to pixel-level visual and interaction language. Every future Figma file, component library, and line of frontend styling code should be traceable to a rule named above — and any new component proposal that can't find a clear place inside these fifty principles should be treated as a signal to reconsider the component, not to add a fifty-first exception.*
