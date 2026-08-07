# BuzzMe — Architecture Pressure Test

*A senior-review pass across all nine documents, verified against the actual current text of each (not memory of earlier drafts). Every finding below was confirmed by re-reading the specific passage cited before being included — nothing here is speculative. Where a finding turned out, on inspection, to already be handled correctly, it's recorded under Confirmed Good Decisions or Areas With No Issues instead of being padded into a problem.*

---

## 1. Critical Issues (must fix before development)

### 1.1 — A Personal Board and a fresh single-Member shared Board are structurally identical, but the UI needs to tell them apart

The Domain Model deliberately (and correctly) refuses to make Personal Board a separate type — it's "a Board with one Member" ([DOMAIN_MODEL.md](./DOMAIN_MODEL.md) §1.2). But the Information Architecture requires the Boards tab to **never list the Personal Board** while listing every other Board a person has ([INFORMATION_ARCHITECTURE.md](./INFORMATION_ARCHITECTURE.md), Boards tab spec: "The Personal Board never appears here"). If Susan creates a new "Trip Planning" Board and hasn't invited anyone yet, it is — right now, in the data — indistinguishable from her Personal Board: both are Boards with exactly one Member. There is no field anywhere in the Domain Model that says *which* single-Member Board is the auto-provisioned one to hide. As written, either the system can't correctly hide the Personal Board, or it incorrectly hides every Board a person hasn't invited anyone to yet.
**Fix direction:** a single provenance marker on Board — set only by the automatic registration-time provisioning, never settable by any user action — resolves this without reintroducing a `PersonalBoard` type. This is additive to the Domain Model, not a reversal of its decision.

### 1.2 — Timezone resolution was never actually resolved

Three separate documents flag this as a blocker and none of them close it. [EVENT_STORMING.md](./EVENT_STORMING.md) calls it a "hard blocker... must be resolved before Occurrence generation can be built" (§C2 hotspot, restated in §O.5). [BUSINESS_BEHAVIOR_MODEL.md](./BUSINESS_BEHAVIOR_MODEL.md) independently flags it as "a genuine open gap" (§I.4) with a *recommendation* (anchor to the Board's or creator's timezone) — but a recommendation was never promoted into the Domain Model as an actual rule. Today, `GenerateNextOccurrence` has no way to deterministically compute a due-instant for "every year on 9 July" when a Board's members span time zones. This is the literal mechanism behind the one-sentence architecture ("when the Reminder becomes due, notify every Member") and it is currently unimplementable as specified.
**Fix direction:** the recommendation already on the table (a reference timezone stored on the Reminder at creation, resolved once, never recomputed per viewer) just needs to be formally adopted into the Domain Model rather than left as a flagged risk in three different documents.

### 1.3 — Deleting a Reminder doesn't explicitly cancel already-scheduled future Buzzes

[EVENT_STORMING.md](./EVENT_STORMING.md) §C3 states plainly: on `ReminderDeleted`, the only policy that fires is `HaltOccurrenceGeneration` — stopping *new* Occurrences from being generated. It explicitly says "History and past Occurrences are... not touched," but says nothing about Occurrences that were **already generated** (per the rolling-horizon generation strategy) and are still in the future with a Buzz already scheduled against them. The product's own confirmation copy promises otherwise: *"Delete '[Title]'? It'll stop reminding anyone, but its history stays"* ([PRODUCT_UX_SPECIFICATION.md](./PRODUCT_UX_SPECIFICATION.md) §11). As specified, that promise can be broken — a person could delete a Reminder today and still get buzzed about it next week.
**Fix direction:** the Delete policy needs an explicit second action — cancel all not-yet-due, already-generated Occurrences' pending Buzzes — alongside halting future generation. Straightforward to add; genuinely missing today.

### 1.4 — The Product UX Specification predates both simplification passes and still describes cut features as present

This is the most consequential finding because it's the document engineering will build screens from directly. Verified line-by-line, not merely recalled:

- **Board Options** (§4.4) still lists Entities, a separate History item, and an explicit Transfer Ownership action as always-present — all three were cut or changed by [MVP_SCOPE.md](./MVP_SCOPE.md) ("Board Options: Members/Invite, Mute, Rename, Delete, Leave — nothing else"; ownership handoff is now automatic).
- **Reminder Detail** (§3.2) lists **Archive** as a live secondary action, alongside Delete — Archive was explicitly removed ("Archive — REMOVE," MVP_SCOPE.md §1).
- **Board Members** (§4.5) shows a role badge of "Owner/Member/**Guest**" — Guest was moved to future.
- **Search** (§6.1) groups results by "Reminders, Boards, **People, Entities, History**" — the last three were explicitly cut from V1 search.
- **Home rows and the Manual Editor** (§3.1, §3.5) still describe per-row participant avatars and a collapsed Board/participants/entity/category field set — all of this assumes participant subsetting and Entity/Category exist in V1; neither does.

None of these are stylistic quibbles — an engineer implementing straight from this document today would build several screens at the wrong scope. This needs a dedicated pass before development starts, not a footnote.

---

## 2. Medium Issues (should fix)

### 2.1 — Domain Model still describes Board Invitation Policy as a configurable setting

[DOMAIN_MODEL.md](./DOMAIN_MODEL.md) (§Board's relationships, §Invitation's Owner field) and [BUSINESS_BEHAVIOR_MODEL.md](./BUSINESS_BEHAVIOR_MODEL.md) (§I.7, recommending it be formalized as an enum with `OwnerOnly`/`AnyMember`/future values) both still treat this as a real, per-Board configurable field. [MVP_SCOPE.md](./MVP_SCOPE.md) resolved this to a fixed, non-configurable rule: any Member can always invite. The Domain Model should say that plainly rather than describing a setting that no longer has a UI to set it.

### 2.2 — Moving a Reminder to a different Board via Edit is unaddressed

The Create/Edit form ([MVP_SCOPE.md](./MVP_SCOPE.md) §4, [INFORMATION_ARCHITECTURE.md](./INFORMATION_ARCHITECTURE.md) §5) lists **Board** as one of the form's editable fields, reused identically for both creation and editing. No document states whether changing a Reminder's Board after creation is actually permitted, and if it is, what happens to History entries and completions already recorded by Members of the *original* Board who aren't Members of the destination one — a real privacy exposure, not just a UX rough edge. This needs an explicit rule (most likely: Board is fixed at creation and not editable afterward — consistent with "a Reminder belongs to exactly one Board" reading as permanent, not just initial).

### 2.3 — Delete Reminder has no stated permission owner

Every simplified Member permission list given across this project's recent turns (and reproduced in [MVP_SCOPE.md](./MVP_SCOPE.md)) enumerates View/Add/Edit/Complete/Leave — **Delete is never listed**. Yet Delete Reminder is used throughout [PRODUCT_UX_SPECIFICATION.md](./PRODUCT_UX_SPECIFICATION.md) and [EVENT_STORMING.md](./EVENT_STORMING.md) (attributed only to a vague "Entitled Member") as a normal, available action. This is a genuine specification gap, not an inconsistency between documents — nobody has actually stated who may delete a Reminder. The likely intent (any Member, matching Edit's shared-responsibility default) should be stated explicitly rather than assumed.

### 2.4 — Changing a Reminder's notification preset after Occurrences are already scheduled is unaddressed

The Recurrence Rule change policy explicitly limits itself to "only Occurrences not yet generated/due" ([EVENT_STORMING.md](./EVENT_STORMING.md) §C2) — a sound, explicit rule. No equivalent rule exists for changing the **Notify** preset. If a Reminder's timing is changed from "1 day before" to "1 week before," it's unspecified whether already-scheduled Buzzes for already-generated future Occurrences follow the new preset or the one that was in effect when they were scheduled.

---

## 3. Minor Issues (could improve)

### 3.1 — Domain Model's "Owner:" field label for Reminder invites misreading

[DOMAIN_MODEL.md](./DOMAIN_MODEL.md)'s Reminder entry uses "Owner: The Board it belongs to; authored by one Member" — using "Owner" in the DDD aggregate-administration sense, not the permission sense. Sitting next to the product's explicit "no reminder ownership" rule, this reads confusingly on a skim even though the actual behavior (any Member may edit regardless of authorship) is correct. A terminology fix, not a functional one.

### 3.2 — Missed-transition grace window has no concrete value

[BUSINESS_BEHAVIOR_MODEL.md](./BUSINESS_BEHAVIOR_MODEL.md) and [EVENT_STORMING.md](./EVENT_STORMING.md) both correctly specify that a Due Occurrence auto-transitions to Missed after "a defined grace window" — but no document picks an actual number, the way the Board/Account deletion grace period was given a concrete "e.g., 14 days." A product decision to pin down, not an architectural flaw.

---

## 4. Confirmed Good Decisions

Verified as holding up correctly under this pass, unchanged by either simplification round:

- **Occurrence as an independent Aggregate Root, separate from Reminder** — still the right call at scale; nothing in the shared-Boards simplification touches this boundary.
- **Buzz modeled per-recipient, independently of the Reminder or Occurrence it references** — unaffected by removing participant subsetting; if anything, this design already anticipated "notify everyone" cleanly.
- **History as permanent and append-only, surviving deletion of its parent Reminder or Board** — checked against every deletion workflow; no contradiction found anywhere.
- **A new Member sees a Board's full pre-existing History with no join-date privacy window** — initially looked like a possible gap; on inspection this is simply the correct, consistent outcome of "Membership governs visibility" applied uniformly, and is arguably a feature (a new family member should see the shared past), not an omission.
- **Remove Member as an Owner capability was already present in the original Domain Model** ("grant, remove, change role, transfer ownership") before this was flagged during MVP simplification as newly "added" — it was a documentation omission in a summarized permission list, not an actual domain-model gap. No real issue here, and it's now consistently non-negotiable per the standing safety rule.
- **Block kept deliberately separate from Board-membership removal** (personal safety vs. Board governance) — holds up under every workflow checked, including the block-vs-pending-invitation race condition.
- **Sole-Owner leave/account-deletion handling** (auto-handoff to the longest-standing Member, or dissolution if no one remains) — consistent across the Domain Model, Business Behavior Model, Event Storming, and MVP Scope. No contradictions found.
- **V2 Imports' explicit refusal to fuzzy-match against manually-created Reminders** — still holds, still correctly reasoned, unaffected by the AI-removal refactor (it was never AI in the first place, which is exactly why it survived).

---

## 5. Areas With No Issues

- **Identity lifecycle** (Register → Verify → Login → Recover → Suspend → Delete) — internally consistent across every document that touches it; no contradictions found.
- **Board core lifecycle** at the domain/event level (Create, Archive, Delete, sole-Owner handling) — sound; the only problem found is the UX Specification not reflecting the simplified Board Options surface (§1.4), which is a documentation-sync issue, not a design flaw in the lifecycle itself.
- **Trust & Safety** (Block, Report — the latter correctly deferred to future) — consistent, no gaps.
- **Design System** (visual, motion, interaction principles) — unaffected by both simplification passes; it was already built around restraint, so nothing in it needed to change when AI and extra permissions were removed.
- **Offline behavior and sync** — reviewed against both simplification passes; the optimistic-update/queue model doesn't depend on anything that was cut.

---

## 6. Final Assessment

The core architecture is sound. The one-sentence backend description — *a Reminder belongs to a Board, a Board has Members, when the Reminder becomes due, notify every Member* — holds up under pressure at the domain and event level; nothing found in this review requires redesigning it, and several of the trickiest-looking questions (join-date history visibility, Remove Member's origin, Block-vs-membership) turned out to already be correctly and deliberately handled.

**The architecture is not yet development-ready as a documentation set**, but for a narrower and more fixable reason than "the design is wrong": two rapid simplification passes changed the product without fully propagating into every downstream document, and one genuinely hard problem (timezone anchoring) has been correctly *identified* three times without ever being *resolved* once. None of the four critical items require new features, new permissions, or new screens to fix — three are rule clarifications and one is a documentation-sync pass. Recommend closing all four before development begins; none should take long relative to the rest of this project.

---

*Every citation above points to an actual line in an actual current document — nothing here was invented to fill out a category. Where a suspected issue turned out to already be resolved correctly on inspection, it was moved to §4 or §5 instead of being reported as a problem.*
