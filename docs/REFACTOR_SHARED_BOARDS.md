# BuzzMe — Refactor: Shared Boards, Not AI

*BuzzMe evolved from an AI-first reminder app to a shared reminder app. This document is the record of that refactor: what changed, what was removed, what's inconsistent across the existing documents as a result, and what's still open. Some of this has already been applied directly to the source documents (noted below); the rest is a precise, actionable list rather than a wholesale rewrite of seven large files.*

**The one-sentence architecture, for engineering:** *A Reminder belongs to a Board. A Board has Members. When the Reminder becomes due, notify every Member.*

**The one-sentence product, for a first-time user:** *A WhatsApp group for remembering things.*

---

## 1. Updated Product Philosophy

Already applied directly to [PRODUCT_VISION.md](./PRODUCT_VISION.md):

- Core Design Principle 7 changed from *"AI removes forms, it doesn't add features"* to **"One Board, one shared truth"** — AI no longer occupies a numbered slot in the product's ten founding principles at all.
- §11 Long-Term Product Vision rewritten: the growth direction is now **Imports**, not AI-noticed reminders or AI-suggested Boards. Any future natural-language input is explicitly framed as "a second way to fill in the same short form," never a dependency.
- §13's anti-pattern list changed *"AI overreach"* to **"AI dependency"** — the risk is no longer "AI acts too visibly," it's "AI becomes load-bearing at all."

Nothing else in the Vision document needed to change. The philosophy underneath — shared memory over personal productivity, calm over corporate, warmth as the default register — was never about AI in the first place; AI was a *mechanism* the earlier drafts leaned on, not the *reason BuzzMe exists*. Removing the mechanism left the reason intact.

---

## 2. Updated MVP Scope

[MVP_SCOPE.md](./MVP_SCOPE.md) is now the single, consolidated source of truth for what ships first — it previously existed as two overlapping documents (a scope-cut pass and a later philosophy-tightening pass); they've been merged into one, since carrying two "MVP" documents was itself a small case of the exact complexity this refactor exists to remove (see §8).

Net effect on scope, beyond what was already decided: participant subsetting is now **permanently removed**, not deferred; AI creation is now explicitly optional and off the default path; Remove Member is added as a non-negotiable Owner power; the invite-permission question is resolved (any Member, no toggle). Full detail, including the complete KEEP/SIMPLIFY/MOVE TO FUTURE/REMOVE table, lives there — not duplicated here.

---

## 3. Updated Domain Model Recommendations

The Domain Model's core shape needs no rework — §1.2's Personal Board decision (a Board with one Member, not a separate concept) already anticipated exactly this simplification. Three specific amendments are recommended:

1. **Remove Reminder Participant subsetting as a described capability, not just an unused one.** [DOMAIN_MODEL.md](./DOMAIN_MODEL.md)'s Reminder section currently states Participants "must be a subset of the Board's Active Members" as an available feature. This should be rewritten so a Reminder's audience *is* the Board's full Active Membership, full stop — no subset concept, no field describing one. This isn't hiding a feature behind a flag; the feature shouldn't be described as existing.
2. **Demote the Reminder Draft concept from "core, needs formalizing" to "future, needed only if AI ships."** It remains a sound design (§2's Domain Model challenge list was right that it needs its own lifecycle and privacy boundary) — it's simply no longer part of the required V1 model, since the default structured creation flow commits directly with no draft/review step at all.
3. **Remove Board Invitation Policy as a configurable field.** Any Member can always invite; this is a fixed rule now, not a per-Board setting with an enumeration of values (`OwnerOnly`/`AnyMember`/future `OwnerAndDesignatedMembers`) as earlier drafts proposed.

Nothing about Board, Membership, Occurrence, Notification (Buzz), or History needs to change. Add **Remove Member** to Membership's described Owner capabilities — it was absent from the simplified permission set that prompted this refactor and is now a standing, non-negotiable requirement (see [MVP_SCOPE.md](./MVP_SCOPE.md) §1's Permissions note).

---

## 4. Updated UX Recommendations

Already applied directly to [INFORMATION_ARCHITECTURE.md](./INFORMATION_ARCHITECTURE.md):

- §2 (Home): the always-visible free-text "capture box" is replaced with a single "Add a reminder" prompt that opens the structured Create form — Home no longer implies inline AI parsing.
- §5 (Reminder Creation): rewritten around the four-field structured form as the one required path; AI is described as an optional, later alternate way to fill the same fields, with its confirmation card appearing only on that path.

Still needed, catalogued rather than applied line-by-line given their size:

- **[PRODUCT_UX_SPECIFICATION.md](./PRODUCT_UX_SPECIFICATION.md) §3.3–3.5** currently describe Create Reminder / AI Confirmation Card / Manual Editor with the AI path as default and the structured form as "fallback." This needs inverting: the structured form (§3.5's content, minus its "fallback" framing) becomes the one *Create/Edit* screen; the AI Confirmation Card's spec is still accurate but should be re-labeled as appearing only within the optional future AI path, not the default flow. §3.4's line calling it *"the single highest-risk screen in the whole spec"* is no longer true — it's not on the critical path at all anymore.
- **[EVENT_STORMING.md](./EVENT_STORMING.md) §M** lists the LLM/NLU provider as *"core today, not future."* That line is now false and should read as future/optional, matching every other AI-adjacent dependency in that table. §O.7's architectural risk ("LLM latency sits directly on the product's signature promise") is resolved by this refactor, not just deprioritized — it no longer applies, since the 10-second promise is now measured against a short form, not a network call.
- **[DESIGN_SYSTEM.md](./DESIGN_SYSTEM.md) §15** ("AI Interaction Language") needs the least change of anything in the whole set — it already argued AI should be invisible and unbranded, which turns out to have been the right instinct even before AI was demoted entirely. It only needs one framing sentence added: this language applies *if and when* AI ships, not to the current product, which has no AI surface at all.
- **[INFORMATION_ARCHITECTURE.md](./INFORMATION_ARCHITECTURE.md)**'s remaining scattered AI references (the Birthday/Recurring/Entity-linked journeys in §16, Principle 15, and the friction item in §18 about the Confirmation Card) are all still individually correct *descriptions of the optional AI path* — they don't need to be deleted, just understood as describing something no longer in v1, which this document now makes explicit.

---

## 5. Updated Roadmap

| Phase | Contents |
|---|---|
| **V1 (this MVP)** | Boards, Members, Reminders — create, edit, complete, delete. Fixed recurrence and notification presets. Owner/Member permissions with open inviting and Remove Member. Block. No AI, no Entities, no Categories, no Notes, no Attachments, no discovery search. Fifteen screens (see [MVP_SCOPE.md](./MVP_SCOPE.md) §4). |
| **V2 — Imports** | User-initiated, one-way, no-background-processing imports: Contacts birthdays first, then Public Holidays, School Terms, Sports Fixtures, CSV. Full spec in [V2_IMPORTS.md](./V2_IMPORTS.md). This is now explicitly *the* growth direction, not a parallel track to AI. |
| **V3 and beyond (unordered, evidence-gated)** | Entities, Categories, Reminder Notes, Attachments, dedicated History screen, People/username Search, Guests, per-Board visibility tiers, Report + Moderation Queue, Company Holiday Calendar imports (the one source needing real org-level auth). **Optional natural-language creation** belongs here too, on equal footing with everything else in this tier — not a special, earlier-promised capability, just one more thing that ships if and when it earns its place. |

---

## 6. What Should Be Removed From Existing Documents

- **[MVP_PHILOSOPHY.md](./MVP_PHILOSOPHY.md) — deleted.** Fully merged into [MVP_SCOPE.md](./MVP_SCOPE.md); keeping both was redundant the moment the second one existed only to revise the first.
- **PRODUCT_VISION.md** — the AI-as-pillar language in Principle 7, §11, and §13 (already rewritten, see §1 above).
- **INFORMATION_ARCHITECTURE.md** — the AI-first capture-box framing in §2 and §5 (already rewritten, see §4 above).
- **PRODUCT_UX_SPECIFICATION.md** — the "AI is the default path, manual is the fallback" framing in §3.3–3.5 (catalogued in §4 above, not yet applied).
- **EVENT_STORMING.md** — the "LLM provider is core today" line in §M and the associated architectural risk in §O.7 (catalogued in §4 above, not yet applied).
- Any future mockup, ticket, or roadmap slide that still says "AI-first" as BuzzMe's identity — that framing is retired, not just deprioritized.

---

## 7. Inconsistencies Introduced by Removing AI

Being direct about what genuinely broke, versus what just needs updating:

- **A real domain-level contradiction:** the original Domain Model describes Reminder Participants as a subset of Board Membership; this refactor's "everyone on the Board, always" rule directly contradicts that description. This is the one inconsistency with actual architectural weight — it's resolved in §3 above, not just noted.
- **A real, now-resolved risk, not just a stale sentence:** Event Storming's flag that LLM latency sits on the product's core promise was a legitimate architectural risk under the old philosophy. It isn't downgraded by this refactor, it's *eliminated* — worth stating plainly so nobody spends engineering effort mitigating a risk that no longer exists.
- **Stale but harmless:** most of the remaining AI references across Information Architecture, the UX Specification, and the Event Storming document are accurate descriptions of a path that used to be primary and is now optional. None of them are wrong on their own terms — they just need the "this describes a future, optional path" framing this document now provides everywhere it was missing.
- **Nothing broke in the Business Behavior Model.** Its AI-Assisted Reminder Creation workflow (`RMD-02`) was already described as one path alongside manual creation (`RMD-01`), never framed as the only one — it needs no correction, only the same "optional, future" label everything else AI-adjacent now carries.

---

## 8. Opportunities to Simplify Even Further

Three genuinely new simplifications this pass surfaced, beyond what was already cut:

1. **Per-row participant indicators can disappear entirely.** With subsetting permanently removed, "who else sees this" is always the same answer — everyone on the Board. Showing small participant avatars on every single Reminder row (as the original UX Specification did) is now redundant information repeated dozens of times per screen; Board membership, visible once in Board Options, already answers the question. Recommend removing this element from the Reminder row entirely.
2. **The Create form's default visible surface can shrink from four fields to two.** Board and Notify timing are already fully defaulted (current Board context; "At time"). There's no reason to *show* them as fields a person must look past — only Title and When need to be visible by default, with Board and Notify tucked behind a single "change" affordance for the rare person who wants something other than the sensible default. This mirrors the Design System's own principle that a setting most people never touch shouldn't occupy visible space.
3. **The two MVP documents becoming one (§2, §6) is itself the clearest evidence of the pattern to watch for going forward:** every time a new pass produces a document that mostly revises a previous one, the right response is to merge them immediately, not to let a chain of "see also" documents accumulate. This refactor is deliberately structured as edits-plus-one-index rather than an eighth freestanding document, for exactly that reason.

---

*Everything in this document is either an edit already made to a source file, or a specific, actionable instruction for one still pending. Nothing here is a new idea competing with the architecture that precedes it — it's the same architecture, with the one part of its identity that turned out not to be load-bearing removed.*
