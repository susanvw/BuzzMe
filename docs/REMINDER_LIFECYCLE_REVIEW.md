# Reminder Lifecycle Review

*A design decision, not code. Every source cited below was re-read directly from the current documents before being used as evidence — this review's central finding is that the answer was already specified, more than once, before Sprint 1 ever started; Sprint 2's hard delete was a deliberate, explicitly-flagged deviation from it, not a gap in the specifications.*

---

## 1. Lifecycle Analysis

Four independent documents, written at different points in this project's history, already converge on the same design — none of them were reconciled with each other until now, which is exactly why Sprint 3 was able to "discover" a contradiction that was actually already resolved on paper.

**IMPLEMENTATION_SPEC.md's own command table (§2, `DeleteReminder` row)** states plainly: *"Halts future Occurrence generation **and** cancels every pending, not-yet-delivered Buzz for every not-yet-resolved Occurrence of this Reminder, in the same operation... History and past Occurrences are untouched."* Invariant 12 in the same document repeats it as a standing rule. This is Option B's exact behavior, specified before Sprint 1 began.

**API_CONTRACT.md deliberately created a dedicated `410 GONE` status** (§6), used *"exclusively for Occurrence actions against a Deleted parent Reminder,"* specifically because that case is *"meaningfully different from an ordinary 404 (the resource unambiguously existed and is now permanently inert, not merely invisible or never-created)"* (§10, consistency review). A status code whose entire reason to exist is distinguishing "existed, now deleted" from "never existed" is only implementable if the deleted Reminder remains a queryable fact — i.e., only if deletion is soft. A hard delete makes `410 GONE` permanently unreachable code.

**MVP_SCOPE.md's own reasoning for removing Archive as a separate concept** was: *"Deleting a Reminder already keeps its history (that's a hard rule, not a UI choice) — so Archive and Delete do almost the same thing... One honest 'Delete' beats two confusing near-synonyms."* That reasoning is only true if Delete preserves the underlying record — it was already presupposing soft-delete semantics when it recommended collapsing Archive into Delete, not proposing physical removal.

**DOMAIN_MODEL.md's own stated Reminder lifecycle** is `Created → Active → (Edited)* → Archived/Deleted` (§2) — a `Deleted` state sitting alongside `Archived` in one lifecycle, not a state that erases the aggregate's existence.

**Where the actual deviation entered the project:** DEVELOPMENT_GUIDE.md §6 states Reminder *"does not carry the same grace period [as Board/User]... its document is removed promptly on `ReminderDeleted`,"* reasoning that History's future denormalization would keep entries readable regardless. That reasoning addresses only a future *read-model's* readability — it doesn't account for API_CONTRACT.md's already-specified `410` distinction, and it was written before `Occurrence` existed as an aggregate with its own identity and its own need to reference a still-identifiable (if inert) parent. Sprint 2's hard delete correctly implemented what DEVELOPMENT_GUIDE.md said — the guide itself was the point where the design quietly diverged from the other three documents, apparently unnoticed until Occurrence existed to expose it.

**Considered against every item on the review's own checklist:**
- **Occurrence integrity** — a soft-deleted Reminder keeps `Occurrence.ReminderId` meaningful; a hard-deleted one turns every existing Occurrence into a dangling reference the moment its parent is removed.
- **History** — not yet built, but every existing invariant (Domain Model §2, Implementation Spec rule 9/15) assumes a Reminder's deletion is a state its History can still point to, not an erasure.
- **Future Buzzes / notification cancellation** — already correctly specified as querying Occurrences by `reminderId`, not by loading the Reminder itself, so this doesn't depend on the outcome of this review either way.
- **Audit** — a soft-delete marker (who/when) is a strictly better audit record than a document that silently disappears; noted below as a related, smaller gap (`ReminderDeleted`/`ReminderCreated` currently carry no actor field at all — a pre-existing gap, not unique to this review, worth remembering when History is eventually built).
- **Future imports, background workers** — unaffected either way; no import or worker logic currently depends on Reminder's deletion mechanics.
- **Data retention / privacy** — anonymization on account deletion is already handled at the *User* level (Implementation Spec §2, `ConfirmAccountDeletion`); Reminder's own soft-delete doesn't need to solve privacy, only existence.
- **Aggregate consistency** — soft-delete is a single-aggregate state change (Reminder only), exactly like every other Reminder mutation already specified — it does not touch Occurrence or Board.
- **Event sourcing** — `ReminderDeleted` already exists and already means the right thing; only what the repository *does* in response needs to change, not the event.
- **Mongo persistence** — a single nullable field and a query-time filter; no new collection, no new background process.

---

## 2. Recommendation

**Option B.** Not as a new design — as a correction, bringing the actual implementation back into agreement with what four separate documents already specified before Sprint 2 ever wrote a line of repository code.

Reminder gains a `DeletedAt` marker (soft delete). `DeleteReminder` continues to halt future Occurrence generation and cancel pending Buzzes (both already fully specified, both already unaffected by soft- vs. hard-delete since they operate on Occurrence, not on the Reminder document itself). No cascade to Occurrence or History — that was never specified anywhere and would directly contradict the standing "History is permanent" rule.

---

## 3. Required Specification Updates

1. **DEVELOPMENT_GUIDE.md §6 ("Soft deletes")** — correct the Reminder-specific sentence. Replace *"Reminder does not carry the same grace period... its document is removed promptly"* with: Reminder carries a `DeletedAt` marker like Board/User, but — unlike them — **is never purged**. Board/Account's purge exists to eventually complete an expensive cascade after a grace window; Reminder has no cascade to complete (its Occurrences and History are meant to survive permanently, not just through a grace window), so no purge job is needed for it at all. This is simpler than Board's pattern, not an extension of it.
2. **EVENT_STORMING.md §P.1** — its soft-delete recommendation names only "Board and Account." Extend it to include Reminder, with the distinction above (no purge) noted explicitly, so a future reader doesn't assume Reminder needs the same 14-day-then-purge machinery.
3. **IMPLEMENTATION_SPEC.md, APPLICATION_LAYER_SPEC.md, API_CONTRACT.md** — no changes required. All three already describe the correct behavior; this review closes the gap between them and the two documents that drifted (DEVELOPMENT_GUIDE.md, and Sprint 2/3's actual implementation).
4. **SPRINT_2_REPORT.md, SPRINT_3_REPORT.md** — no edits needed; both already flagged this exact tension honestly at the time (Sprint 2 §5 "a real, consequential decision a future sprint... will need to revisit"; Sprint 3 §4.1 "the *right* long-term fix is almost certainly changing Sprint 2's Delete Reminder to a soft-delete"). This review is that future sprint's revisit, recorded as such rather than silently overwriting the earlier reports.

---

## 4. Required Implementation Updates

*Described for a future implementation sprint — nothing here has been written as code, per this review's scope.*

1. **`Reminder` (Domain):** add `DeletedAt: DateTimeOffset?`. `Delete()` — currently raises `ReminderDeleted` with no state mutation (a comment on that method from Sprint 2 explicitly says "no state mutation needed for a hard delete," which is now the wrong premise) — must additionally set `DeletedAt`.
2. **`IReminderRepository`:** the existing `DeleteAsync(id, ct)` (currently a real document removal) becomes an update that sets `DeletedAt`, not a removal — worth renaming (e.g., `MarkDeletedAsync`) since "Delete" now misleadingly implies removal. The existing `GetByIdAsync` should exclude soft-deleted Reminders by default (returns null for one, same as "doesn't exist," so `CreateReminder`/`UpdateReminder`/`GenerateOccurrences`/normal `GetReminder` all correctly treat a deleted Reminder as unusable with no special-case code). A **new** method is needed — e.g. `GetByIdIncludingDeletedAsync` — used only where the distinction actually matters (see next point).
3. **`OccurrenceApplicationService.GetOccurrenceAsync` (and the future Complete/Dismiss/Reopen use cases):** should use the "including deleted" lookup specifically to distinguish three cases, not two: Reminder never existed → `NotFound`; Reminder exists and is active → proceed normally; Reminder exists but `DeletedAt` is set → for a **read** (`GetOccurrenceAsync`/`ListOccurrencesAsync`), this should still **succeed** (historical data about a deleted Reminder's Occurrence is exactly what "History is permanent" is meant to protect); for a future **resolution action** (Complete/Dismiss/Reopen, Sprint 4+), this is where `410 GONE` (Application Layer Spec §3.8, already specified) actually applies. This is a refinement this review surfaced, not present in Sprint 3's implementation: Sprint 3's `GetOccurrenceAsync` currently returns `NotFound` for *any* missing-or-deleted Reminder, collapsing a distinction the API Contract already asked for.
4. **`ReminderDocument` (Infrastructure):** add `DeletedAt: DateTimeOffset?`. The Mapper is unaffected beyond carrying the new field. The repository's default query path adds a `DeletedAt == null` filter; the new "including deleted" method omits it.
5. **Indexes:** the existing `CreateReminderIndexes` migration's `(boardId, _id)` index remains correct for the default (non-deleted) query path; no new index is strictly required, though a future sprint doing this for real should consider whether `List Board Reminders` at scale wants a partial index scoped to `DeletedAt: null` — a performance refinement, not a correctness requirement, and not needed to resolve this review's actual question.

---

## 5. Migration Strategy

Minimal, because this is pre-launch: `DeletedAt` is a new, nullable, additive field — MongoDB's schemaless documents don't need a data migration for it; every existing (non-deleted) Reminder document is correctly interpreted as active simply by the field's absence. No script is needed to backfill anything.

**One honest limitation, not a migration to perform:** any Reminder already hard-deleted by Sprint 2/3's existing test runs is irrecoverably gone — a hard delete cannot be retroactively converted into a soft one after the fact. This has no real consequence today (no production data exists yet), which is precisely why resolving this now, before Sprint 4 introduces Buzz generation and this becomes load-bearing for real notification behavior, is the right time to do it — exactly as this review's closing instruction frames the urgency.

---

## 6. Why the Rejected Options Were Rejected

**Option A (remain hard delete, cascade dependent data) — rejected outright, on two independent grounds:**
- It directly contradicts an already-standing invariant repeated in two documents (Domain Model §2, Implementation Spec rule 9/15): *"A deleted or archived Reminder never deletes its History or past Occurrences."* Cascading the delete to Occurrence would be implementing the literal opposite of a rule that already exists.
- It reintroduces the exact *"synchronous cascade risk"* Event Storming already identified and rejected for Board deletion (§B2) — deleting years of a long-lived Reminder's Occurrence history in one transactional burst is the same anti-pattern in miniature, for no benefit the specifications ever asked for.

**A fourth design — rejected because none exists in the specifications to recommend.** No document proposes an alternative persistence mechanism for Reminder deletion (a tombstone collection, event-sourced replay, or anything else) — every piece of evidence in §1 points at the same soft-delete design from four independent angles. Inventing a different mechanism now, when the specifications already agree with each other, would be solving an already-solved problem differently for no stated reason — precisely what this review was told not to do.
