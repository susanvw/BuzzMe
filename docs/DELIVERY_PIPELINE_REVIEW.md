# Delivery Pipeline Review — Before Push Providers

*A design verification, not code — nothing in `src/` changed. Every citation below was re-read directly from the current documents and Sprint 6's actual implementation. The central finding: the correct long-term abstraction already exists in the codebase and has existed since Sprint 1 — `IPushNotificationSender`/`IEmailSender`/`ISmsSender` — Sprint 6's `INotificationDispatcher` was always meant to be temporary and should be deleted, not extended, when real providers arrive. The more consequential finding is a documentation bug: DEVELOPMENT_GUIDE.md's own process table misclassifies the one background process this review was commissioned to check.*

*Revised once, before Sprint 7, in response to explicit follow-up review: §3.2 sharpens "polling is an architectural decision" into the more precise distinction the follow-up asked for (§3.2 below), and a new §1.5 explicitly names the orchestration boundary for future channel selection. Nothing in `src/` changed for this revision either — this document is the only thing edited.*

---

## 1. Delivery Abstraction Review

### 1.1 What Sprint 6 built, and what it says about itself

`INotificationDispatcher` (`Application/Abstractions/INotificationDispatcher.cs`) is a single method, `DispatchAsync(Buzz buzz, CancellationToken)`, returning a bare `bool`. Its own doc comment is explicit: *"exists to be deleted once a real dispatch step... replaces it."* `LoggingNotificationDispatcher`, its only implementation, always returns `true` and only logs. Nothing about this interface names a channel, a provider, a recipient contact, or a preference — by design, since Sprint 6's brief scoped it to proving the claim→dispatch→mark orchestration works, nothing more.

### 1.2 What already exists, and has existed since before Sprint 6

`IPushNotificationSender`, `IEmailSender`, `ISmsSender` (`Application/Abstractions/`) have been present, registered in DI, and defaulted to `Null*Sender` implementations since the repository's Sprint 1 bootstrap — never called by anything until now, but never provisional either. `IPushNotificationSender`'s own doc comment states plainly: *"A single Buzz delivery attempt to one device (Application Layer Spec §8's external side effects; Development Guide's Messaging/Push implementations)."* DEVELOPMENT_GUIDE.md's own folder listing (§3) already names the concrete future implementations these interfaces are for: `Messaging/Push/FcmPushSender.cs, ApnsPushSender.cs`, `Messaging/Email/EmailSender.cs`, `Messaging/Sms/SmsSender.cs` — one interface, multiple providers behind it for push specifically (FCM for Android, APNs for iOS), decided at the Infrastructure layer, not the interface's shape.

### 1.3 Why a single `PushNotificationProvider` would be the wrong abstraction

Every specification that describes Buzz delivery describes it as multi-channel, never push-only:

- **DOMAIN_MODEL.md**, Buzz's own invariants: *"each person's delivery timing, **channel**, and mute state are independent"*; *"New delivery channels (**push, SMS**, wearable haptic, **email digest**) are additive."*
- **APPLICATION_LAYER_SPEC.md**, the Dispatch Push Notifications external-effect row: *"Call **the push provider (APNs/FCM) or SMS/email channel**."*
- **BUSINESS_BEHAVIOR_MODEL.md**, NOT-01: *"delivery attempted via the recipient's **preferred channel(s)**."*

A single `PushNotificationProvider` abstraction would hard-code exactly the assumption these three documents independently reject: that delivery is push-only. It is also not a name that appears anywhere in the five specification documents reviewed — introducing it now would be inventing an abstraction, not recovering one.

### 1.4 The correct long-term boundary

The system's future dependency is **not one interface** — it is the **already-existing trio**, `IPushNotificationSender` + `IEmailSender` + `ISmsSender`, called by a small piece of **orchestration logic** (not a fourth interface) that picks the right one(s) per recipient once Notification Preferences exist to inform that choice. This mirrors a rule DEVELOPMENT_GUIDE.md §2 already states for a different layer and applies here without modification: *"there is no `IApiService` or similar indirection layer between `Api` and `Application`... because there's no second implementation... ever expected to exist."* The same reasoning holds for a hypothetical `IChannelDispatcher` wrapping the three senders — there is no second implementation of "coordinate three senders" ever expected to exist, so it isn't an abstraction, it's just orchestration code, and every other Application Service in this codebase already holds multiple dependencies directly without a wrapping interface (`BuzzApplicationService` alone already depends on four repositories). *Which* component holds that orchestration code — the Worker itself, or an Application Service — is answered directly in §1.5.

### 1.5 Where future channel orchestration belongs

**`BuzzApplicationService` — not `BuzzDeliveryWorker`, and not a new component.** Three independent pieces of evidence converge on this, none of them requiring a new concept:

1. **DEVELOPMENT_GUIDE.md §2's own dependency table states the rule directly**, for `Workers` specifically: *"Hosts the outbox dispatcher and every time-scheduled job (§7); **thin — delegates all real behavior to Application**."* Channel selection, and (once built) checking a recipient's Notification Preference/mute state before sending, is exactly "real behavior" in the sense that phrase means — it's a decision with business consequence, not host/scheduling plumbing.
2. **The three sender interfaces are already declared in `Application/Abstractions`**, not `Domain` or `Infrastructure` — DEVELOPMENT_GUIDE.md §2's Application row lists them explicitly as "capability interfaces the use cases need but don't own." An interface Application already owns is a strong signal that the code calling it belongs in Application too, not in a Workers-hosted class reaching past that layer.
3. **No second Application Service for this area is named anywhere.** DEVELOPMENT_GUIDE.md §3's rule is one Application Service per bounded-context area; Buzz's area has been `BuzzApplicationService` since Sprint 4, and it already grew to hold generation (Sprint 4) and claim/mark orchestration (Sprint 6) as that area's scope grew. DOMAIN_MODEL.md's "Notification Engine" is a named bounded context at the conceptual/DDD level (its own row in the context map, §7) — but no specification anywhere gives it a corresponding C# class name, and inventing one now (`NotificationEngine`, `ChannelSelector`) would be adding a component this review was explicitly told not to add. `BuzzApplicationService` is already serving as that bounded context's code-level home, by construction, not by a new decision.

**One concrete consequence for Sprint 7, not a change made now:** Sprint 6's `BuzzDeliveryWorker` currently holds `INotificationDispatcher` directly and calls it itself — correct *for that sprint*, because the dispatcher was a trivial, single-line stub with no business logic to misplace. Once real channel selection exists, that reasoning no longer holds: the orchestration should move into a new `BuzzApplicationService` method (taking `IPushNotificationSender`/`IEmailSender`/`ISmsSender` as added constructor dependencies, alongside its existing six), and `BuzzDeliveryWorker.ProcessBatchAsync` should shrink accordingly — from "claim, call the dispatcher myself, mark" to "ask `BuzzApplicationService` to process a claimed batch," matching the "thin Workers" rule precisely. This is the one design conclusion this review reaches that Sprint 6's actual code doesn't yet reflect — recorded for Sprint 7, not applied here.

---

## 2. Dependency Review

| Concern | Finding |
|---|---|
| **Apple Push / Firebase** | Already accounted for — DEVELOPMENT_GUIDE.md §3 names both (`ApnsPushSender.cs`, `FcmPushSender.cs`) as implementations of the *same* `IPushNotificationSender` interface. No interface change needed to add either. |
| **Web Push** | Not named in any specification, but fits `IPushNotificationSender`'s existing shape (`SendAsync(token, title, body, ct)`) without modification — VAPID-based Web Push is also token-addressed. A future `WebPushSender : IPushNotificationSender` is additive, not a redesign. |
| **Desktop notifications** | Not named anywhere. If a future desktop channel is delivered over a live connection (SignalR/WebSockets — explicitly out of scope for this review and Sprint 6 alike) rather than a push token, it will **not** fit `IPushNotificationSender`'s token-addressed shape and will need its own interface at that time. Flagged, not solved — inventing that interface now would violate this review's own "do not invent unless already implied" instruction, since nothing implies its shape yet. |
| **Testing** | Already solved by construction: `IPushNotificationSender`/`IEmailSender`/`ISmsSender` are ordinary DI-injected interfaces, mockable exactly like `INotificationDispatcher` was for Sprint 6's orchestration tests. No new testing strategy is needed for the *orchestration* layer; only Infrastructure-level tests for `FcmPushSender`/`ApnsPushSender` (against sandbox credentials) are new work, and out of scope for this review. |
| **Dependency Injection** | Already wired. `Program.cs`/`InfrastructureServiceCollectionExtensions.cs` already register all three sender interfaces (against `Null*` defaults) — swapping in real implementations later is a registration change, not a structural one. |
| **Future retries** | Already has a named, *separate* home: APPLICATION_LAYER_SPEC.md §7's **Retry Failed Notifications** row (`BuzzDeliveryFailed` → bounded retry with backoff → `BuzzRetried` → `BuzzDelivered`\|`BuzzDeliveryExhausted`), hosted in its own `Workers/Jobs/RetryFailedNotificationsJob` (DEVELOPMENT_GUIDE.md §7's table) — confirmed distinct from the dispatch step itself, consistent with SPRINT_6_REPORT.md §4.2's finding that retry was correctly left out of `BuzzDeliveryWorker`. |
| **Failure handling** | Already modeled: `BuzzStatus.Failed` exists, `MarkFailedAsync` exists. The open question — who transitions `Failed` → `Retried` → `Exhausted` — belongs to the not-yet-built Retry job above, not to the dispatch worker being reviewed here. |
| **Metrics** | Not specified anywhere — APPLICATION_LAYER_SPEC.md §8 states plainly: *"Analytics: none specified in this product."* No gap; nothing to reconcile. |
| **Logging** | Already the established pattern — every `Null*Sender` and `LoggingNotificationDispatcher` already logs via `ILogger<T>`, ordinary ASP.NET Core infrastructure, not a domain concern requiring specification. |
| **Future notification preferences / mute** | Both explicitly named, both explicitly future: DOMAIN_MODEL.md's Notification Preference is *"Consulted by the Notification Engine **at the moment a Buzz would be delivered**"* — i.e., squarely inside the same orchestration step (§1.4/§1.5, living in `BuzzApplicationService`) that picks a channel and calls the right sender. Confirmed out of scope for Sprint 6 and for this review; the orchestration logic recommended there is exactly where these will plug in later, without changing the sender interfaces themselves. |

---

## 3. Required Specification Updates

### 3.1 DEVELOPMENT_GUIDE.md §7's process table misclassifies "Dispatch Push Notifications"

The table (§7) lists:

> `Dispatch Push Notifications | Reactive (via outbox, on BuzzGenerated) | Workers/Jobs/OutboxDispatcherJob`

This is inconsistent with the guide's **own stated categorization principle**, two paragraphs above the table: category A ("Event-reactive work") is for things that *"fire because something just happened, not on a clock"*; category B ("Time-scheduled work") is for things that *"run on a clock regardless of whether anything happened recently."* Buzz delivery must wait until a specific instant (`ScheduledAt`, the `NotifyPreset` lead time) — that is category B's own definition, not category A's. Reacting to `BuzzGenerated` (raised at Buzz *creation*, per Sprint 4 — SPRINT_4_REPORT.md §3.1) would dispatch every Buzz immediately upon creation, hours or days before its intended delivery time, for any Reminder with a non-zero lead time. Sprint 6's actual implementation — `BuzzDeliveryWorker`, a `PeriodicTimer`-based `BackgroundService` polling `Status == Scheduled && ScheduledAt <= now` — is the row that's correct; the table is what's stale.

**Recommended correction**, matching the table's own existing pattern for "Retry Failed Notifications" and "Expire Invitations" (both category B, each with a dedicated job):

> `Dispatch Push Notifications | Scheduled sweep (must respect each Buzz's own ScheduledAt) | Workers/Jobs/BuzzDeliveryWorker`

This is a two-cell fix (Trigger Type and Hosted In), not a rewrite — everything else about the row (`Generated Buzzes` in, `BuzzDelivered`/`BuzzDeliveryFailed` out, "provider outage handled by the retry process") remains accurate as written.

### 3.2 Refined: the architecture requires *scheduled delivery of due Buzzes* — "polling" names today's mechanism, not the requirement

This section was revised after explicit follow-up review. The original version of this document said "polling is an architectural decision," full stop — that conflated two things that need to stay separate, and the follow-up review's proposed distinction is correct.

**The architectural requirement** (durable, transport- and scheduler-agnostic): a Buzz must never be dispatched before its own `ScheduledAt`, and must eventually be dispatched once `ScheduledAt` has passed, *without depending on an upstream event having recently fired*. This is precisely DEVELOPMENT_GUIDE.md §7's own category B definition — *"run on a clock regardless of whether anything happened recently."* **Which category a process belongs to (A, event-reactive, vs. B, time-scheduled) is the architectural decision** — that's what §3.1 corrects, and that correction stands unchanged. Nothing about *how* the time-check is performed is part of this requirement.

**The implementation strategy** (today's choice, replaceable): `PeriodicTimer` firing every 5 seconds, each tick claiming a batch via atomic `FindOneAndUpdate`. This is one valid way to satisfy "scheduled delivery of due Buzzes" — not the only one. A dedicated scheduler could instead register a one-shot delayed job per Buzz, timed to its own `ScheduledAt` (Hangfire's delayed-job model, for instance); a change-stream or TTL-index-driven watcher could achieve the same requirement without a fixed-interval sweep at all. DEVELOPMENT_GUIDE.md §7 already treats *this specific mechanism* as swappable, in the very next sentence after defining category B: *"if the number or complexity of scheduled jobs grows significantly later, a dedicated scheduler (Quartz.NET, Hangfire) can replace these without restructuring anything above `Workers`."* That sentence only makes sense if `PeriodicTimer` was never the requirement in the first place — only category B's membership was.

**Corrected statement:** the architecture requires *scheduled delivery of due Buzzes* — a time-triggered process, not an event-reactive one. It does **not** require polling specifically. `BuzzDeliveryWorker`'s `PeriodicTimer` loop is Sprint 6's chosen mechanism for satisfying that requirement, appropriate for V1's job volume, explicitly not fixed for the future.

**Recommended wording** — add one sentence to DEVELOPMENT_GUIDE.md §7, immediately after the category B paragraph's existing "a dedicated scheduler... can replace these" sentence, so the requirement/mechanism split is stated once, generally, rather than needing to be re-derived per process:

> *"What makes a process category B is a requirement about **when** it must run — triggered by elapsed time relative to some due instant, independent of whether an upstream event fired recently — never a commitment to **how** that check is performed. `PeriodicTimer`-based polling is this system's current mechanism for satisfying that requirement; nothing about the category itself names a mechanism, and a future scheduler or watcher-based implementation would not move a process out of category B."*

This is an addition, not a rewrite — the existing prose and the §3.1 table fix both stand as already written; this sentence closes the gap that made "polling is an architectural decision" an easy but imprecise thing to conclude.

### 3.3 `IPushNotificationSender`'s doc comment should be updated to remove the outbox reference

Its current text — *"dispatched after commit, from the Workers outbox dispatcher"* — repeats the same stale assumption as §3.1, now contradicted by Sprint 6's actual, correct implementation. Recommend rewording to: *"dispatched after commit, from BuzzDeliveryWorker's polling loop (not the outbox — see DEVELOPMENT_GUIDE.md §7)."*

---

## 4. Implementation Recommendations

*(Recorded for whichever sprint implements real providers — nothing here is to be built now, per this review's own scope.)*

1. **Do not extend `INotificationDispatcher`.** Delete it and `LoggingNotificationDispatcher` outright once real dispatch exists — both are explicitly temporary by their own doc comments, and extending a type documented for deletion is worse than replacing it cleanly.
2. **`BuzzApplicationService` should depend on `IPushNotificationSender`/`IEmailSender`/`ISmsSender` directly**, gaining a new method that performs channel selection and calls the right sender(s) — not `BuzzDeliveryWorker` (§1.5). `BuzzDeliveryWorker` should shrink to invoking that Application method per claimed batch, matching "Workers... thin — delegates all real behavior to Application" (DEVELOPMENT_GUIDE.md §2).
3. **Channel selection is new orchestration logic, not a new abstraction.** Once Notification Preferences exist, the code that decides "push, email, or SMS for this recipient" is a plain conditional/lookup against preference data — it does not need its own injectable interface, for the same reason noted in §1.4, and it belongs inside `BuzzApplicationService`, not a new class.
4. **Keep the mute/preference check at dispatch time, not generation time** — already the established pattern from Sprint 4/5 (Buzz generation doesn't check mute or Block; DOMAIN_MODEL.md's own Buzz invariant places preference-checking *"at the moment of delivery"*). The orchestration logic in §1.5 is where this belongs, consistent with what's already built, not a new decision this review is making.
5. **Real provider integration tests belong in `BuzzMe.Infrastructure.IntegrationTests`**, following the existing pattern (real dependency, sandboxed credentials via configuration) — no new test project needed.

---

## 5. Future Provider Integration Strategy

1. Implement `FcmPushSender`, `ApnsPushSender` (`Infrastructure/Messaging/Push/`), each `: IPushNotificationSender` — no interface change.
2. Implement real `EmailSender`, `SmsSender` (`Infrastructure/Messaging/Email/`, `Infrastructure/Messaging/Sms/`) — same, no interface change.
3. Register the real implementations in `InfrastructureServiceCollectionExtensions.cs` in place of the `Null*` defaults — a registration-only change, already anticipated by the existing DI wiring (§2).
4. Add the channel-selection orchestration to a new `BuzzApplicationService` method (§1.5), replacing the single `INotificationDispatcher.DispatchAsync` call with: resolve the recipient's Notification Preference → skip if muted → call the matching sender(s) → return the same outcome `MarkDeliveredAsync`/`MarkFailedAsync` already expect. `BuzzDeliveryWorker` shrinks to calling this one new method per claimed batch; its claim/mark plumbing does not otherwise change.
5. Build **Retry Failed Notifications** (APPLICATION_LAYER_SPEC.md §7) as its own, separate `Workers/Jobs/RetryFailedNotificationsJob` at the same time or immediately after — real provider failures are what finally make the retry cadence's open parameter (IMPLEMENTATION_SPEC.md §6) decidable, per SPRINT_6_REPORT.md §5's own recommendation.
6. Apply §7's documentation fixes before or alongside that sprint, so the next implementer isn't re-deriving the same table/doc-comment/category-B-wording inconsistencies a third time.

---

## 6. Risks If the Current Abstraction Remains Unchanged

1. **Low risk from `INotificationDispatcher` itself, if it is actually deleted on schedule.** Its own doc comment already commits to deletion; the risk is procedural (someone extends it instead of replacing it under time pressure), not architectural.
2. **Real risk if `IPushNotificationSender`'s doc comment isn't fixed first (§3.3).** A future implementer reading "dispatched... from the Workers outbox dispatcher" and taking it literally could re-wire real push dispatch through `OutboxDispatcherJob`, silently reintroducing the exact "delivers immediately on Buzz creation, ignoring `ScheduledAt`" bug this review traced back to DEVELOPMENT_GUIDE.md's stale table row. The doc comment and the table entry are two independent places carrying the same wrong assumption; fixing only one leaves the other as a trap.
3. **Real risk if channel selection is built as a new interface instead of orchestration logic.** Given no second implementation is ever specified to exist, an `IChannelDispatcher`-style abstraction would be speculative from day one — exactly the kind of functionality Sprint 6's own review instruction ("do not silently keep speculative functionality") was written to prevent, applied here pre-emptively rather than after the fact.
4. **Moderate risk if Retry Failed Notifications is folded into `BuzzDeliveryWorker`'s own loop** rather than built as the separate job the specification already names. The two processes have different trigger types (scheduled sweep vs. reactive-with-delay) and different idempotency keys (`occurrenceId`+`recipientId` vs. "per Buzz ID, per attempt") — merging them would blur a distinction the specification draws deliberately.
5. **No risk identified in the sender-interface shape itself** — `IPushNotificationSender`/`IEmailSender`/`ISmsSender` already accommodate every named provider (APNs, FCM) and the one unnamed-but-compatible one (Web Push); only a genuinely different transport (a live connection, not a token) would require new interface design, and none is in scope until a desktop/SignalR channel is actually specified.
6. **Moderate risk if `BuzzApplicationService` isn't confirmed as the orchestration boundary before Sprint 7 starts.** Without §1.5 written down, there is no obvious reason a Sprint 7 implementer wouldn't simply extend `BuzzDeliveryWorker` in place — it already holds a sender-like dependency (`INotificationDispatcher`) — quietly pulling real business logic (channel choice, mute/preference checks) into a layer DEVELOPMENT_GUIDE.md explicitly says should stay thin. Nothing would fail loudly; it would just be the wrong layer, discovered later.

---

## 7. Documentation Drift to Correct Before Sprint 7

*Identified only — nothing below has been edited. Split into genuine drift (something currently stated that's wrong) and recommended clarification (nothing wrong, but worth stating more precisely given this review's own back-and-forth).*

**Genuine drift:**

1. **DEVELOPMENT_GUIDE.md §7's process table**, the "Dispatch Push Notifications" row — Trigger Type still reads "Reactive (via outbox, on `BuzzGenerated`)" and Hosted In still reads `Workers/Jobs/OutboxDispatcherJob`. Both contradict the guide's own category B principle and Sprint 6's actual, correct implementation (§3.1).
2. **`IPushNotificationSender`'s doc comment** (`src/BuzzMe.Application/Abstractions/IPushNotificationSender.cs`) — still says *"dispatched after commit, from the Workers outbox dispatcher,"* the same stale assumption as #1 (§3.3).

**Recommended clarification (not currently wrong, but worth adding given how easily "polling is an architectural decision" was over-stated once already):**

3. **DEVELOPMENT_GUIDE.md §7's category B paragraph** — add the requirement-vs-mechanism sentence drafted in §3.2, so a future process being classified into category B doesn't require re-deriving the distinction between "must run on elapsed time" (the requirement) and "`PeriodicTimer`" (today's mechanism for satisfying it) from first principles again.

**Checked, no drift found:**

- `BuzzDeliveryWorker.cs`'s own doc comment (Sprint 6) already correctly identifies category B and already flags the table mismatch — written before this review existed, and still accurate.
- `INotificationDispatcher.cs`/`LoggingNotificationDispatcher.cs` — both already correctly documented as temporary; no change needed regardless of §1.5's conclusion.
- EVENT_STORMING.md's "Buzz Dispatcher"/"Delivery worker" actor names (§B4/§E1) — these operate at the conceptual event-storming level and don't specify a C#-level split between Worker and Application Service, so they neither confirm nor contradict §1.5's `BuzzApplicationService` conclusion; DEVELOPMENT_GUIDE.md remains the correct, more granular authority for that split.
- DOMAIN_MODEL.md's Notification Preference/channel language and APPLICATION_LAYER_SPEC.md's Dispatch Push Notifications row — both already accurately describe multi-channel delivery and are consistent with every conclusion in this review.
