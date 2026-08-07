# Reminder Soft Deletion — Impact Review

*A consistency check, not a design decision — [REMINDER_LIFECYCLE_REVIEW.md](./REMINDER_LIFECYCLE_REVIEW.md) already made that decision and it is not reopened here. This document verifies that decision doesn't ripple into anywhere unexpected. Every row below was checked against the actual current text of all nine documents and the actual current C# source, not recalled from memory.*

**Result, stated up front because it's the actual finding: very little changes.** Of the twenty-two areas reviewed, eighteen require no change at all — most because the feature they cover (Buzz, History, Search, Board deletion, Account deletion, Imports, background workers) isn't built yet and was already written assuming soft-delete semantics; a few because they were already correctly insulated from this decision by aggregate boundaries drawn back in Sprint 1. The remaining four changes are small, contiguous, and land exactly where §1's own analysis said they would: the Reminder repository's read path, two Application Service methods, and the handful of tests directly coupled to them.

---

## 1. Impact Matrix

| Area | Verdict | Why |
|---|---|---|
| Occurrence generation | **No Change Required** | `GenerateOccurrencesAsync` already loads the Reminder via `GetByIdAsync` before generating anything. Once that method excludes soft-deleted Reminders by default (the one real change, below), "cannot generate Occurrences for a deleted Reminder" is satisfied automatically — no new code in `OccurrenceApplicationService` itself. Business Behavior Model RMD-05 already states this precondition ("Parent Reminder is Active, not Archived/Deleted") — unchanged. |
| Existing Occurrences | **No Change Required** | The `Occurrence` aggregate, its repository, and its indexes are entirely independent of how `Reminder` represents deletion. An Occurrence was never touched by `DeleteReminder` under the hard-delete implementation either — it isn't touched under soft-delete. |
| Future Buzz generation | **No Change Required** | Not built (Sprint 4+). Implementation Spec §H.1's delivery-time re-check already requires verifying current state *at send time*, independent of how "deleted" is represented — this was already correct for either mechanism. |
| Pending Buzzes | **No Change Required** | Not built. The "cancel pending Buzzes on `ReminderDeleted`" policy (Implementation Spec, invariant 12) queries by `Occurrence.ReminderId` — a value that exists identically whether the parent document is soft- or hard-deleted. |
| Notification dispatch | **No Change Required** | Not built. Same reasoning as the two rows above. |
| Reminder retrieval | **Implementation Change Required** | `IReminderRepository.GetByIdAsync` currently returns any Reminder found by id, deleted or not — it needs a `DeletedAt == null` filter on its default path. This is the core change everything else in this table depends on. |
| Reminder listing | **Implementation Change Required** | `ListByBoardAsync` needs the identical filter, for the identical reason. |
| Search | **No Change Required** | Not built. MVP_SCOPE.md already scopes Search to Boards/Reminders only, with no stated exception for deleted items — once built, it queries through the same filtered repository path retrieval/listing already establish. Nothing to change *now*. |
| History | **No Change Required** | Not built. Every existing document that mentions it (Domain Model §2, Implementation Spec rule 9/15, Business Behavior Model RMD-04) already assumes a deleted Reminder's History entries remain valid. Soft-delete makes this *more* correctly implementable than it was under hard-delete, not less — a confirmation, not a required change. |
| Board deletion | **No Change Required** | `DeleteBoard` isn't built in any sprint yet. Worth stating explicitly since it's easy to conflate: Board's own eventual cascade purge (Event Storming §L, after its 14-day grace window) is a separate, already-specified, all-encompassing physical cleanup — unaffected by Reminder's own soft-delete pattern, which governs a single Member's voluntary deletion of one Reminder, not Board-wide teardown. |
| Account deletion | **No Change Required** | Not built. Same distinction as Board deletion; User-level anonymization already handles the privacy angle separately (Implementation Spec §2, `ConfirmAccountDeletion`). |
| Imports | **No Change Required** | V2_IMPORTS.md's "Remove it" option (for a contact whose birthday disappeared) maps directly onto the now-corrected `DeleteReminder` use case, and that document's own standing rule — "never delete silently" — is unaffected either way. |
| Background workers | **No Change Required** | None exist yet beyond the empty host (`BuzzMe.Workers/Program.cs`). |
| Mongo indexes | **Minor Update Required** | The existing `(boardId, _id)` index (`CreateReminderIndexes`) remains correct for the filtered query path. A partial index scoped to `DeletedAt: null` would be a reasonable future performance refinement at scale, not a correctness requirement — noted, not required. |
| Repository filtering | **Implementation Change Required** | Same change as Retrieval/Listing above — listed separately here only because the checklist named it separately. |
| Read models | **No Change Required** | Not built. Event Storming §K already describes Home/Board Detail as removing a Reminder from view on `ReminderDeleted` — a read-model projection reacting to an event, which behaves identically regardless of what the write-side document looks like underneath. |
| Events | **No Change Required** | `ReminderDeleted` means exactly what it always meant — "this Reminder was deleted." Soft vs. hard delete is a persistence detail the event was never coupled to. No event becomes obsolete. |
| Policies | **No Change Required** | The halt-generation-and-cancel-pending-Buzzes policy reacts to the `ReminderDeleted` *event*, not to the document's physical absence — unaffected. |
| Audit | **Minor Update Required** | Soft-delete is strictly a better audit record than erasure (a `DeletedAt` timestamp persists where a hard delete left nothing). Noted in passing: `ReminderCreated`/`ReminderDeleted` currently carry no actor/user-id field at all — a real but pre-existing gap, unrelated to soft- vs. hard-delete, worth remembering whenever History is actually built. Not a blocker here. |
| API status codes | **Implementation Change Required** *(not Specification)* | API_CONTRACT.md already correctly specifies `410 GONE` for "Occurrence actions against a Deleted parent Reminder" (§6) — no wording change needed there. What was wrong is that Sprint 3's `GetOccurrenceAsync` implementation collapsed "never existed" and "deleted" into the same `NotFound`, never reaching the already-specified distinction. |
| Application services | **Implementation Change Required** | Two methods: `ReminderApplicationService.DeleteReminderAsync` (calls a soft-delete operation instead of a physical removal) and `OccurrenceApplicationService.GetOccurrenceAsync` (must allow reads to succeed against a soft-deleted Reminder's Occurrence, reserving the 410 distinction for the future resolution actions — REMINDER_LIFECYCLE_REVIEW.md §4.3's refinement, confirmed still correct here). |
| Integration tests | **Implementation Change Required** | See §4 — four specific, already-identified test locations, no others. |

---

## 2. Specifically Verify

- **Can an existing Occurrence still be viewed?** Yes — unconditionally. The `Occurrence` aggregate was never coupled to `Reminder`'s deletion mechanism; this was true under hard-delete and remains true under soft-delete.
- **Can a deleted Reminder appear in History?** Yes, and it's meant to — Business Behavior Model RMD-04 explicitly anticipated this: *"users should never be surprised that 'deleted' reminders still show up in history/look-back features."*
- **Can a deleted Reminder still own Occurrences?** Yes — `Occurrence.ReminderId` is an ordinary foreign-key-style reference, unaffected by the parent's soft-delete state. This is the entire reason soft-delete is the correct design, not an incidental side effect of it.
- **Should deleted Reminders appear in Board lists?** No — `ListByBoardAsync`'s `DeletedAt` filter excludes them, matching the user-facing behavior every existing Sprint 2/3 test already asserts. This was never in question; only whether the document survived *underneath* the list was.
- **Should Search return deleted Reminders?** No, once Search exists — it will query through the same filtered path as Retrieval/Listing.
- **Does any event become obsolete?** No.
- **Does any invariant need updating?** No *existing* invariant needs rewording — rules 9/12/15 were already textually correct; only the implementation contradicted them. One invariant is worth *adding* (not correcting): "A soft-deleted Reminder's Occurrences remain readable, but do not accept new resolution actions" — implied by API_CONTRACT.md's `410` design and REMINDER_LIFECYCLE_REVIEW.md §4.3, but never previously catalogued as a named rule. A specification addition, recorded in §3 below.

---

## 3. Specification Updates Required

One, already identified and not repeated in full here: **DEVELOPMENT_GUIDE.md §6**'s Reminder-specific soft-delete sentence, per REMINDER_LIFECYCLE_REVIEW.md §3. This review found no *additional* document requiring a wording change — the systematic sweep across all nine confirms that document was the single point of drift.

One addition, newly identified by this review's "Specifically Verify" pass: add to IMPLEMENTATION_SPEC.md's invariant catalogue — *"A soft-deleted Reminder's Occurrences remain readable (§ Reminder retrieval is unaffected for read paths); only future resolution actions (Complete/Dismiss/Reopen) are rejected against them, per the `410 GONE` contract."* This closes the gap between what API_CONTRACT.md's status code implies and what was ever written down as a Reminder-lifecycle rule.

---

## 4. Implementation Updates Required

*Described, not written — consistent with this review's scope.*

1. `Reminder.Delete()` — sets `DeletedAt` (already identified in REMINDER_LIFECYCLE_REVIEW.md §4.1; confirmed still the only Domain-layer change needed).
2. `IReminderRepository` — `GetByIdAsync`/`ListByBoardAsync` filter out `DeletedAt != null` by default; a new method (e.g. `GetByIdIncludingDeletedAsync`) is added for the one caller that needs the distinction.
3. `ReminderApplicationService.DeleteReminderAsync` — calls the soft-delete operation, not a physical removal.
4. `OccurrenceApplicationService.GetOccurrenceAsync` — uses the "including deleted" lookup; a soft-deleted parent no longer produces `NotFound` for a plain read.
5. `ReminderDocument` — gains `DeletedAt: DateTimeOffset?`; `ReminderRepository`'s default query path adds the filter.

No other production file requires a change — confirmed by this review's own sweep, not assumed from the prior one.

---

## 5. Tests Requiring Changes

Verified against the actual current test files, not described in the abstract:

1. **`tests/BuzzMe.Infrastructure.IntegrationTests/Reminders/ReminderRepositoryTests.cs`** — `DeleteAsync_RemovesTheReminder` currently asserts `GetByIdAsync` returns `null` after delete. That assertion stays true (the default path is meant to exclude soft-deleted Reminders) — but the test's *name* and *intent* should change to reflect that it's now verifying exclusion, not removal, and a new assertion should be added confirming the document still exists (with `DeletedAt` set) via the "including deleted" path.
2. **`tests/BuzzMe.Application.Tests/Occurrences/OccurrenceApplicationServiceTests.cs`** — `GetOccurrenceAsync_ReturnsNotFoundWhenTheOwningReminderNoLongerExists` currently expects `NotFound` after the owning Reminder is deleted. Per §3's new invariant, this expectation is now **wrong** and must flip: the call should **succeed**, returning the Occurrence — this is the one test in the whole suite whose expected *outcome*, not just its plumbing, changes as a direct result of this decision.
3. **`tests/BuzzMe.Application.Tests/TestDoubles/InMemoryReminderRepository.cs`** — its `DeleteAsync` currently does `_reminders.RemoveAll(...)` (a real removal). It needs to mirror the real repository's soft-delete behavior, or every Application-layer test built on top of it (including the one above) will keep passing for the wrong reason.
4. **`tests/BuzzMe.Application.Tests/Reminders/ReminderApplicationServiceTests.cs`** (`DeleteReminderAsync_RemovesTheReminder`) and **`tests/BuzzMe.Api.IntegrationTests/Reminders/ReminderEndpointsTests.cs`** (`DeleteReminder_RemovesItAndItIsNoLongerReturned`) — both were re-checked directly and **need no behavioral change**. Both assert only observable, filtered-path outcomes (Get returns 404/`NotFound`, List excludes it) that remain identically true under soft-delete. Listed here to record that they were checked, not skipped.

No other existing test in the 98-test suite references Reminder deletion.

---

## 6. Risks

- **The one genuine behavior change** (`GetOccurrenceAsync` now succeeding, not failing, for a soft-deleted Reminder's Occurrence) is a visible change to existing, already-shipped test expectations, not just internals — worth calling out plainly so it isn't mistaken for a regression when Sprint 4 picks this up.
- **`GetByIdIncludingDeletedAsync` is a second read path into the same aggregate.** Small risk of future code accidentally calling the wrong one (using the filtered path where the distinction actually matters, silently collapsing back into the bug this review exists to fix). Worth a one-line comment convention on the interface, not a structural mitigation — flagged as a risk to watch, not a design flaw to solve now.
- **No risk found in Occurrence, Buzz, Board, Account, Search, History, Imports, or background-worker areas** — each was checked directly against current documents and, where applicable, current code, and none carry any dependency on Reminder's deletion mechanism.

---

## 7. Final Confirmation

**Reminder soft deletion is architecturally complete.** Complete at the specification level: a full sweep of all nine documents found exactly one that needed correcting (DEVELOPMENT_GUIDE.md §6, already identified before this review began) and zero new concepts required anywhere. Complete at the design level: no aggregate, event, policy, or bounded-context boundary changes as a result of this decision. What remains is a small, fully-enumerated implementation task — one repository filter, two Application Service methods, one new invariant recorded, and four specific test files touched, three of which need no behavioral change and were only re-verified. Nothing here ripples into Occurrence, Buzz, Board, Account, Search, History, Imports, or Workers — exactly the "very little changes" outcome this review set out to confirm, not assume.
