# Buzz Generation Boundary Review

*A design verification, not code — nothing in this document was changed in `src/`. Every citation below was re-read directly from the current documents. The central finding: the boundary the brief calls "Option A" is not just the better of two choices, it is the only one the existing specifications describe — and the current Sprint 4 implementation already sits exactly on it. The more consequential finding is a piece of terminology two older documents never finished retiring, which matters precisely because Sprint 5 is about to make the distinction it blurs load-bearing again.*

---

## 1. Responsibility Analysis

### 1.1 What the specifications actually say, read together

**APPLICATION_LAYER_SPEC.md §2** lists `GenerateBuzzes` under **Internal / System-invoked** use cases — "reused by several use cases below, never called directly by the UI." It is an Application-layer capability by explicit classification, not a Domain concept and not a UI-triggered command.

**APPLICATION_LAYER_SPEC.md §7's Background Processes table**, the "Generate Buzzes" row, states its own **Reads** column plainly: *"Occurrences, **current Board Membership**."* This is not an inference — the specification names Board Membership as something the Generate Buzzes process itself consults, directly, as part of doing its job.

**EVENT_STORMING.md §D1** traces the same flow at finer grain: `OccurrenceGenerated` → 🟩 Policy → `ScheduleBuzz` for each current [recipient] → `BuzzScheduled`. The Actor for this policy chain is named explicitly at §E1: *"System (policy-triggered, per D1/D2)."* A Policy, per this document's own legend (§0), is *"the automatic glue... this is where reactive, asynchronous behavior lives"* — canonically an Application-layer (or Worker-invoked Application-layer) responsibility in every other policy this document describes (`CancelPendingBuzzesForMember`, `GrantMembership`, `RegenerateFutureOccurrences` — none of these live inside an aggregate's own methods).

**IMPLEMENTATION_SPEC.md §3's Buzz invariants** and **§4's "Buzz Scheduling & Delivery" policy row** both independently describe a **two-checkpoint model**: eligibility (Active Membership, non-muted, non-blocked) is evaluated once at generation/scheduling time, and *"delivery is gated by a re-check, at the moment of send... not just the state captured when the Buzz was scheduled."* Two separate reads, at two separate times, by two separate (future) processes — never a property the Buzz aggregate carries or computes for itself.

**DEVELOPMENT_GUIDE.md §7** confirms how this is meant to be wired end to end: event-reactive Policies are dispatched by `OutboxDispatcherJob` (in `Workers`), which *"invokes the matching Policy in `Application`."* Policies are an Application-layer artifact by construction in this codebase's own architecture, not by convention alone.

**DEVELOPMENT_GUIDE.md §6** independently confirms why the Buzz aggregate itself cannot be the one doing this: *"Occurrence and Buzz were split out from Reminder specifically so high-frequency writes wouldn't require touching a large parent document"* — Board, Reminder, Occurrence, and Buzz are four separate Aggregate Roots, each its own MongoDB document, referenced only by ID. An aggregate reaching into another aggregate's own collection (Buzz loading Board's `Memberships`) would violate the one-aggregate-one-transaction-boundary rule every other aggregate in this codebase already respects — nothing about Buzz was ever modeled as an exception to it.

### 1.2 What "eligible recipients" already means, independent of Membership mechanics

**MVP_SCOPE.md** (line 40, restated at line 110) permanently removed per-Reminder recipient subsetting: *"Every Reminder belongs to and notifies the whole Board, no exceptions... Participant subsetting is now permanently ruled out, not deferred."* This closes a question the boundary review would otherwise have to leave open: the eligible-recipient set for a given Occurrence's Buzzes is not "some Reminder-specific audience," it is exactly "the Board's current Membership" — full stop, no additional per-Reminder filtering concept exists or is planned. `GenerateBuzzes` therefore only ever needs to consult Board (today) and, in future sprints, whatever User-level or Membership-level filters get added (Mute, Block, NotificationPreferences) — never anything Reminder-level.

### 1.3 What the current implementation actually does

`BuzzApplicationService.GenerateBuzzesAsync` (`src/BuzzMe.Application/Buzzes/BuzzApplicationService.cs`) loads the Occurrence, then its owning Reminder, then that Reminder's Board, then iterates `board.Memberships` directly, calling `Buzz.Generate(...)` once per Member not already covered. `Buzz.Generate` (`src/BuzzMe.Domain/Buzzes/Buzz.cs`) takes a single, already-resolved `recipientUserId` and has no reference to `Board`, `Membership`, or any repository — it cannot load anything, by construction. This is already exactly the shape §1.1 describes: the Application layer resolves eligibility; the Buzz aggregate is, and remains, ignorant of Membership entirely.

---

## 2. Recommended Boundary

**Option A, precisely stated** — not "the Buzz aggregate loads the Board" (no specification anywhere proposes that, and DDD aggregate-boundary rules already established elsewhere in this codebase forbid it), but: **the Application layer's `GenerateBuzzes` capability is the sole owner of recipient-eligibility resolution.** It loads Board and reads Membership directly, inline, as part of the same orchestration that then calls the Buzz aggregate's `Generate` factory once per eligible recipient. The Buzz aggregate itself never gains, and should never gain, any awareness of Board, Membership, Mute, Block, or NotificationPreferences — it only ever receives a single, pre-resolved `recipientUserId`.

**Option B, as posed — a separate Application-layer step that resolves eligibility and hands a list to a narrower `GenerateBuzzes` — is not what the existing architecture specifies.** APPLICATION_LAYER_SPEC.md's Internal/System-invoked list names exactly three capabilities: `GenerateRecurrences`, `GenerateBuzzes`, `CancelBuzzes`. There is no fourth, separately-named "resolve eligible recipients" capability anywhere in any of the five documents reviewed. Introducing that split now would be adding an abstraction the specifications don't call for — precisely what this review was asked not to do ("do not invent Option C," "do not introduce new abstractions"). Option B is not a safer, more decoupled version of the architecture; relative to what's actually specified, it *is* the invented option.

**The two-checkpoint model extends this same boundary to the future delivery sprint, not a different one.** IMPLEMENTATION_SPEC.md's "re-check... not just the state captured when scheduled" describes a second, independent Application-layer read of Board Membership (plus Mute/Block once they exist), performed by whatever future `DispatchBuzz`/`DeliverBuzz` capability is built — not a reuse of the recipient set captured at generation time, and not a new responsibility for the Buzz aggregate either. Both checkpoints belong to the Application layer; neither belongs to Domain.

---

## 3. Required Specification Updates

Two documents carry stale terminology from before MVP_SCOPE.md's "whole Board, no exceptions" decision — neither is a live contradiction in the *authoritative* specs (IMPLEMENTATION_SPEC.md, APPLICATION_LAYER_SPEC.md, and API_CONTRACT.md contain **zero** occurrences of "Participant," confirmed directly), but both are still readable today and both describe the exact concept this review had to rule out in §1.2.

1. **DOMAIN_MODEL.md's Reminder section (lines 108–110) and its master rule list (line 337)** still state *"Participants... must be a subset of the Board's Active Members."* REFACTOR_SHARED_BOARDS.md already identified this exact passage by name as needing a rewrite (§3, and again at §5: *"a real domain-level contradiction... resolved in §3"*) — that resolution appears to have been recorded as a decision but never actually applied back into DOMAIN_MODEL.md's text. Recommend closing that loop now: rewrite these passages to state a Reminder's audience is unconditionally its Board's full Active Membership, matching every later document.
2. **EVENT_STORMING.md line 178** (`ScheduleBuzz for each current Participant`) carries the same retired word. It doesn't change what the policy actually does today (it already resolves to full Board Membership under the current rule), but it's worth updating to "current Board Member" so a future reader doesn't mistake it for a still-live, narrower concept.

Both are documentation-only fixes; neither changes behavior, since the current implementation already follows the correct (later, authoritative) rule, not the stale one.

No update is needed to IMPLEMENTATION_SPEC.md, APPLICATION_LAYER_SPEC.md, or DEVELOPMENT_GUIDE.md — the boundary this review confirms is already exactly what they describe.

---

## 4. Required Implementation Changes

**None.** `BuzzApplicationService.GenerateBuzzesAsync` already implements the recommended boundary precisely (§1.3): Application-layer orchestration loads Board and reads Membership; the Buzz aggregate remains fully ignorant of it. No code in `src/` needs to change as a result of this review.

One piece of forward guidance for Sprint 5 and beyond, not a change required now: as Block, Mute, RemoveMember/LeaveBoard, and NotificationPreferences are implemented, each should be added as an additional repository read inside this same `GenerateBuzzesAsync` orchestration (extending its dependency list, the same way it already depends on four repositories today) — not by extracting a new "recipient resolver" abstraction. That would keep the implementation on the boundary this review confirms; extracting one preemptively would not.

---

## 5. Risks If the Current Boundary Is Not Deliberately Preserved

1. **Orchestration sprawl.** As Block/Mute/RemoveMember/NotificationPreferences accumulate inside `GenerateBuzzesAsync`, the method risks growing into exactly the kind of class DEVELOPMENT_GUIDE.md warns against — *"no `*Manager`/`*Helper` classes that accumulate unrelated behavior over time."* The boundary itself is correct; keeping the method doing exactly one job (compute today's eligible recipient set) as its dependency list grows is a discipline risk, not a design risk.
2. **The delivery-time re-check being skipped or short-circuited.** IMPLEMENTATION_SPEC.md and EVENT_STORMING.md (§E1's explicitly-flagged Hotspot, line 216) both require the future delivery/dispatch capability to independently re-read Membership/Mute/Block at send time, not trust whatever was true at generation time. If a future sprint takes a shortcut and reuses the generation-time recipient list, it would violate an already-specified invariant and could deliver a Buzz to someone who has since left, been removed, been blocked, or muted the Board — the exact scenario the specification's own Hotspot already anticipated.
3. **Multi-recipient generation is still functionally unverified.** Per SPRINT_4_REPORT.md §4.3, `Board.Create` has never supported more than one Membership anywhere in this codebase — the iteration-over-Memberships logic in `GenerateBuzzesAsync` is written generically but has only ever executed against N=1. The first multi-member Board Sprint 5 produces is also the first real exercise of this code path; it should get explicit test coverage as one of Sprint 5's first steps, not assumed correct by extension.
4. **A well-intentioned refactor could invent Option C by accident.** `BuzzApplicationService` depending on four repositories (`IBoardRepository`, `IReminderRepository`, `IOccurrenceRepository`, `IBuzzRepository`) can look, superficially, like a layering smell to someone who hasn't read APPLICATION_LAYER_SPEC.md's own "Reads: Occurrences, current Board Membership" line. Without this review on record, a future "cleanup" pass could extract a recipient-resolution service that no specification actually calls for — silently drifting the architecture away from what's documented, in the name of tidiness.
5. **The stale "Participant" language (§3) reads as authoritative to anyone who doesn't cross-reference MVP_SCOPE.md.** A future engineer — or a future AI session — primed by DOMAIN_MODEL.md's still-present "Participants must be a subset of Active Members" could reintroduce per-Reminder recipient subsetting into Buzz generation, directly contradicting both MVP_SCOPE.md's permanent removal and this review's own conclusion that the eligible-recipient set is exactly "current Board Membership," nothing narrower. This risk exists specifically *because* the stale document was never swept, not because the current implementation is wrong.
