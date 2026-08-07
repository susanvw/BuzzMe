# BuzzMe — V2 Feature: Imports

*Builds on the finalized architecture and [MVP_PHILOSOPHY.md](./MVP_PHILOSOPHY.md) without reopening either. Imports is additive — it creates ordinary Reminders through the exact same path a person typing one in by hand would use. Nothing about the Reminder model, the notification model, or the "everyone on the Board sees the same thing" rule changes because of this feature.*

---

## 1. Product Behaviour

Imports solves one problem: information that already exists somewhere else (a birthday in Contacts, a public holiday, a school term date) shouldn't have to be retyped by hand. It is not synchronization — there is no ongoing link, no background watching, no automatic anything. Every read of an external source happens because a person tapped a button, right now, in the foreground.

Every import reduces to the same shape, regardless of source:

**Read → Preview → Select → Create.**

And, for sources that can meaningfully change over time:

**Read again → Diff → Show what changed → User decides → Apply only what's approved.**

An imported Reminder is not a special kind of Reminder. It's created via the same `CreateReminder` path everything else uses, on the Board the person was already in, subject to every existing rule (yearly-or-whatever recurrence, one fixed notification preset, visible and notifying the whole Board — no per-person exceptions, per [MVP_PHILOSOPHY.md](./MVP_PHILOSOPHY.md) §2). The only thing genuinely new is a small, separate record of *where a given Reminder came from*, kept purely so a later Refresh has something to compare against.

---

## 2. User Flow — Import Birthdays

1. From a Board (e.g., "Family"), open Board Options → **Import Birthdays**.
2. If Contacts permission hasn't been granted, request it with a plain reason: *"Allow contacts access to find birthdays to add to this board."*
3. Denied → a clear, non-nagging explanation with a link to system settings; the person can back out and try again later, no dead end.
4. Granted → BuzzMe reads Contacts once, keeps only entries with a birthday set, discards the rest.
5. Shows a preview list, each entry selected by default:
   ```
   ✓ Mom — 9 July
   ✓ John — 14 October
   ✓ Emma — 2 February
   ```
6. The person deselects anything they don't want (including all of it — importing nothing is a valid outcome, not an error).
7. Taps **Import Selected**.
8. For each selected entry: one yearly Reminder is created ("[Name]'s birthday"), on this Board, at the app's normal default notification timing — nothing provider-specific is invented here (see §6 on why this isn't "guessing").
9. Confirmation: *"3 birthdays imported to Family."* The Board now shows them exactly like any hand-created Reminder.

## 3. User Flow — Refresh Birthdays

1. From the same Board, **Refresh Birthdays** (only shown once at least one prior import from this source exists).
2. BuzzMe reads Contacts again and compares against what it recorded at the last import/refresh.
3. Shows a summary, grouped, nothing auto-applied:
   ```
   3 new birthdays found
   2 birthdays changed
   1 no longer in your contacts
   ```
4. Each group is reviewable and individually approvable — a person can accept all 3 new ones, ignore the 2 changes, and decide about the 1 missing one, in any combination.
5. Nothing changes until **Apply** is tapped, and only the specifically-approved items change.

---

## 4. UX Flow (Screen-Level)

| Screen | Purpose | Key behavior |
|---|---|---|
| **Import Entry Point** | A single action inside Board Options, per source (e.g., "Import Birthdays"). | Only offered on Boards where it makes sense to run at all — no restriction beyond that; any Board can try it. |
| **Permission Request** | Ask for exactly the access this provider needs, when it's needed. | Standard platform permission dialog plus one line of BuzzMe context beforehand, per the Design System's permission convention — never requested upfront at onboarding. |
| **Preview / Selection** | Show exactly what was found, let the person choose. | Every item defaults to selected (opt-out, not opt-in) since the person explicitly asked BuzzMe to look — but nothing commits until they confirm. A "Select none" / "Select all" toggle keeps this fast for large contact lists. |
| **Import Summary** | Confirm what happened. | A quiet toast/confirmation, not a celebratory moment — this is data entry, not an achievement. |
| **Refresh Diff** | Show New / Changed / No-longer-in-source, grouped. | Each item and each group is independently approvable; "no-longer-in-source" never pre-selects "remove" — the safe default is always "leave as-is." |

---

## 5. Domain Changes

**The Reminder model gains nothing.** An imported Reminder is a Reminder, full stop — this is what makes new providers pluggable without touching it.

One new, small, separate concept is needed to make Refresh possible at all: an **Import Record** — a lightweight link, not part of the Reminder aggregate, following exactly the same pattern the Domain Model already used for linking a Reminder to an Entity (Domain Model §2, `ReminderLinked`/`ReminderRemoved`) rather than inventing a new pattern:

- References: the Board, the created Reminder, which Import Source produced it (e.g., "Device Contacts — Birthdays"), an External Reference stable enough to re-identify the same source record later (e.g., a contact identifier), and the Last Known Value at the time of import (e.g., "9 July").
- Owned by: the import itself, not the person or the Reminder — deleting the Reminder doesn't require deleting the Import Record's history of the fact an import happened, though the link becomes inert.
- Without this, Refresh has no way to distinguish "new" from "already imported, unchanged" from "changed" — it would have to guess, which is explicitly out of scope.

No changes to Board, Membership, Occurrence, or Notification concepts. Imports is entirely additive.

---

## 6. Event Changes

All additive — nothing existing is modified:

| Event | Meaning |
|---|---|
| `ImportSourceRequested` | Person opened an Import entry point for a given source and Board. |
| `ImportPermissionGranted` / `ImportPermissionDenied` | Outcome of the platform permission prompt. |
| `ImportPreviewGenerated` | The source was read and filtered; N candidates are ready for review. |
| `ImportCompleted` | The person confirmed a selection; for each selected item, the *existing* `ReminderCreated` event fires exactly as it would from manual entry, plus one `ImportRecordCreated` per item. |
| `ImportRefreshRequested` | Person asked BuzzMe to check the source again. |
| `ImportDiffComputed` | The fresh read was compared against existing Import Records for this Board+Source; New/Changed/Missing buckets produced. |
| `ImportChangesApplied` | Person approved a specific subset of the diff — triggers the *existing* `ReminderCreated` (new items) and `ReminderUpdated` (changed items, date only) events per approved item, and updates the corresponding Import Record's Last Known Value. **Never** triggers `ReminderDeleted` automatically. |

`ReminderCreated`, `ReminderUpdated`, `OccurrenceGenerated`, and every downstream Notification/History consequence already defined in the Business Behavior Model and Event Storming documents apply unmodified — Imports is a new *producer* feeding an unchanged pipeline, not a new pipeline.

---

## 7. Edge Cases & Rules

Every rule below resolves the same way when in doubt: **do less, ask the person, never delete silently.**

| Situation | Rule |
|---|---|
| **New birthday found** | Shown in the New group, opted in by default, becomes a Reminder + Import Record only on explicit Apply. |
| **Birthday date changed** | Detected by comparing the Import Record's Last Known Value to the fresh read. Shown as "old → new," never auto-applied — approving it issues a normal `ReminderUpdated`. |
| **Contact deleted entirely** | The External Reference can no longer be found. Shown as "no longer in your contacts." The Reminder is **never** auto-deleted. The person chooses: *Keep it anyway* (the Import Record is simply unlinked — the Reminder becomes an ordinary, non-imported Reminder from that point on) or *Remove it* (an explicit, normal delete, same confirmation and history rules as any manual delete). |
| **Birthday field cleared but contact still exists** | Treated identically to "contact deleted" from BuzzMe's point of view — the source value is simply gone, same two choices, same non-automatic handling. |
| **Duplicate against a manually-created Reminder** | **Deliberately not detected.** Matching "Mom's birthday" (typed by hand) against "Mom" (a Contact) requires inferring that two different pieces of text refer to the same person — that's guessing, and this feature explicitly excludes it. Import always shows what it found, honestly, regardless of what might already exist; the person simply deselects anything they recognize as already covered. This is a considered omission, not a gap. |
| **Duplicate against a previously-imported record** | **Reliably prevented** — this is an exact match on External Reference, not an inference. A contact already linked to an active Import Record for this Board+Source is never offered again as "new"; it only reappears if its value has actually changed. |
| **Contacts with no birthday set** | Excluded from the preview entirely — never shown, never counted, no ambiguity. |
| **Importing the same person twice in one session** | Structurally impossible — the preview list is built once per read, de-duplicated by contact identity by construction. |

---

## 8. API Considerations (Conceptual — Provider Contract)

Every Import source, present or future, implements the same small contract, so the generic UI (permission → preview → select → commit → optional diff/refresh) never needs source-specific logic:

- **`read(source) → candidates`** — a deterministic list of `{externalRef, suggestedTitle, suggestedDate, recurrence}`. No provider is permitted to return a probabilistic or ranked guess — every field it returns must come directly from the source data via a fixed, documented mapping (e.g., "Contacts' birthday field → yearly recurrence" is a fixed rule for that provider, not an inference).
- **`supportsRefresh: boolean`** — declares whether re-checking this source later is meaningful. Contacts and any live feed (Public Holidays, Sports Fixtures) support it; a one-shot CSV upload does not, and simply never shows a "Refresh" action.
- **`requiresPermission` / `requiresConnection`** — Contacts needs an OS permission; a CSV file needs none; a Public Holiday or Sports Fixture feed needs a network call to a public endpoint but no user credential; a future Company Holiday Calendar might need an org-level connection — each provider declares its own consent shape, but all of them funnel into the identical preview/select/commit screens.
- **No provider runs on a schedule, a webhook, or a push trigger.** Every `read()` call happens synchronously in response to a person tapping Import or Refresh in the foreground — there is no background task, no silent network call, and nothing registered against OS-level change notifications (e.g., BuzzMe never subscribes to "Contacts changed" events). This is an explicit architectural constraint, not an implementation detail left open for later.

---

## 9. Future Extensibility

The provider contract in §8 is the entire extensibility mechanism — adding a source means writing one small adapter, never touching Reminder, Board, or the Import screens themselves:

| Future Source | Permission/Connection | Refreshable? | Notes |
|---|---|---|---|
| **Public Holidays** | None (public feed) | Yes | Country/region selected by the person, not inferred from device locale. |
| **School Terms** | None (public feed) | Yes | Per-school or per-district selection is an explicit user choice, not a guess from location. |
| **Sports Fixtures** | None (public feed) | Yes | Team/league selected explicitly; a genuinely live-updating fixture list is exactly the kind of source Refresh exists for. |
| **Company Holiday Calendars** | Org-level connection (future, out of scope for V2) | Yes | The one source that might eventually need real authentication — deliberately deferred rather than designed now. |
| **CSV Import** | None (local file) | No | One-shot only; the person maps columns to Title/Date/Recurrence explicitly — still no inference, just a manual mapping step before the same preview screen. |

None of these require a new domain concept beyond the Import Record already introduced — each is just a new implementation of `read()` feeding the same Reminder-creation path.

---

*This feature does exactly one thing well: it removes typing, never removes control. Every screen it adds is a variation of "here's what we found — you decide," and nothing it does is reachable except by a person choosing, right now, to look.*
