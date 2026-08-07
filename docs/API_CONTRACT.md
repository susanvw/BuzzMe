# BuzzMe — API Contract Specification

*The behavioral contract frontend and backend teams build against independently. Builds directly on [APPLICATION_LAYER_SPEC.md](./APPLICATION_LAYER_SPEC.md) — every endpoint here maps to exactly one use case already specified there; authorization levels, business validation, and side effects are referenced, not repeated. No databases, no services, no OpenAPI — the contract only.*

---

## 1. API Design Principles

1. **One endpoint, one Application Layer use case.** No endpoint bundles more than one use case's job; no use case is split across more than one endpoint.
2. **Resource-oriented, with one named exception for state transitions.** Pure CRUD uses standard HTTP verbs on a resource. Actions that are commands rather than field edits (Leave, Mute, Complete, Accept…) use `POST` on a verb sub-path of the resource — e.g. `POST /boards/{boardId}/leave`. This is a deliberate, consistent pattern applied everywhere a command exists, not an inconsistency (see §10).
3. **Resources are created and listed under their required parent when that parent is immutable and essential; they are read, updated, and deleted via their own top-level ID afterward.** A Reminder is always created under a Board (`POST /boards/{boardId}/reminders`) because Board is immutable and mandatory — but read, updated, and deleted at `/reminders/{reminderId}` directly, since by then its ID alone is sufficient. Applied identically for Invitations and Members.
4. **Every response, success or error, uses the same envelope shape** (§3). A client never needs a different parser for a different endpoint.
5. **Every list endpoint uses the same cursor-pagination shape** (§7), no exceptions.
6. **A resource the requester cannot see returns 404, never 403.** Confirming that a Board or Reminder exists to someone who isn't a Member of it is itself a privacy leak — consistent with the Domain Model's privacy stance. 403 is reserved for cases where existence is already known (e.g., a Member who isn't the Owner attempting `DELETE /boards/{boardId}`).
7. **Idempotency keys are required on every resource-creating `POST`; state-transition actions rely on the domain's own natural idempotency instead** (already specified per-use-case in the Application Layer Spec) — a client never needs to invent its own de-duplication for a Leave/Mute/Complete call.
8. **Only domain-level fields already named in the Domain Model and Application Layer Spec are exposed.** No internal event names, no database identifiers beyond opaque resource IDs, no implementation detail.
9. **URL-path versioned** (§9); breaking changes only ever occur in a new version, never silently within one.

---

## 2. Authentication Rules

- All authenticated endpoints require `Authorization: Bearer <accessToken>`.
- Access tokens are short-lived. `POST /v1/auth/refresh-token` exchanges a valid refresh token for a new access/refresh pair — this is the only endpoint that accepts a refresh token instead of an access token.
- **Unauthenticated endpoints** (explicitly, and only, these): `POST /v1/auth/register`, `POST /v1/auth/verify`, `POST /v1/auth/login`, `POST /v1/auth/forgot-password`, `POST /v1/auth/reset-password`, `GET /v1/invitations/{token}` (Validate Invitation — must work for someone with no account yet).
- **Self-resolved authorization:** `POST /v1/invitations/{token}/accept` and `.../decline` require a valid access token, but the authorization check is "the token resolves to this authenticated User," not general Board membership — matching the Application Layer Spec's "Authenticated User" level for these two, distinct from the "Board Member" level used elsewhere.
- A missing or expired access token on any other endpoint returns `401 UNAUTHORIZED` (§6).

---

## 3. Standard Request/Response Models

**Success envelope (single resource):**
```
{ "data": { ...resource fields... } }
```
**Success envelope (list):**
```
{ "data": [ ...resources... ], "pagination": { "nextCursor": "string | null" } }
```
**Error envelope (all error responses, every endpoint, no exceptions):**
```
{ "error": { "code": "STRING_CODE", "message": "human-readable", "details": [ ...optional field-level items... ] } }
```

**Resource field lists** (conceptual — not a schema, no types beyond what's needed to agree on shape):

| Resource | Fields |
|---|---|
| **User** (self only) | `id`, `displayName`, `photoUrl`, `email`, `phone`, `status`, `personalBoardId` |
| **Board** | `id`, `name`, `ownerUserId`, `createdAt` |
| **Membership** (a row in List Members) | `userId`, `displayName`, `photoUrl`, `role` (`owner`\|`member`), `muted`, `joinedAt` |
| **Reminder** | `id`, `boardId`, `title`, `recurrence` (`once`\|`daily`\|`weekly`\|`monthly`\|`yearly`), `notifyPreset` (`atTime`\|`15MinBefore`\|`1HourBefore`\|`8HoursBefore`\|`1DayBefore`\|`1WeekBefore`), `referenceTimezone`, `nextOccurrence` (`id`, `dueAt`, `status`), `createdAt`, `updatedAt` |
| **Occurrence** | `id`, `reminderId`, `dueAt`, `status` (`scheduled`\|`due`\|`completed`\|`dismissed`\|`missed`), `resolvedBy` (`userId`, `displayName`, nullable), `resolvedAt` (nullable) |
| **Invitation** | `token`, `boardId`, `boardName`, `inviterDisplayName`, `status` (`pending`\|`accepted`\|`declined`\|`revoked`\|`expired`), `expiresAt` |
| **Notification** (in-app Buzz fallback entry) | `id`, `occurrenceId`, `reminderTitle`, `boardId`, `boardName`, `dueAt`, `status` (`delivered`\|`failed`\|`seen`\|`dismissed`), `createdAt` |

---

## 4. Complete Endpoint Catalogue

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/v1/boards` | Create Board |
| `GET` | `/v1/boards/{boardId}` | Get Board |
| `GET` | `/v1/boards` | List Boards (mine) |
| `PATCH` | `/v1/boards/{boardId}` | Rename Board |
| `DELETE` | `/v1/boards/{boardId}` | Delete Board |
| `POST` | `/v1/boards/{boardId}/leave` | Leave Board |
| `POST` | `/v1/boards/{boardId}/mute` | Mute Board |
| `POST` | `/v1/boards/{boardId}/unmute` | Unmute Board |
| `POST` | `/v1/boards/{boardId}/invitations` | Invite Member (Create Invitation) |
| `DELETE` | `/v1/boards/{boardId}/members/{userId}` | Remove Member |
| `GET` | `/v1/boards/{boardId}/members` | List Members |
| `GET` | `/v1/invitations/{token}` | Validate Invitation |
| `POST` | `/v1/invitations/{token}/accept` | Accept Invitation |
| `POST` | `/v1/invitations/{token}/decline` | Decline Invitation |
| `POST` | `/v1/boards/{boardId}/reminders` | Create Reminder |
| `GET` | `/v1/reminders/{reminderId}` | Get Reminder |
| `PATCH` | `/v1/reminders/{reminderId}` | Update Reminder |
| `DELETE` | `/v1/reminders/{reminderId}` | Delete Reminder |
| `GET` | `/v1/boards/{boardId}/reminders` | List Board Reminders |
| `POST` | `/v1/reminders/{reminderId}/occurrences/{occurrenceId}/complete` | Complete Reminder |
| `POST` | `/v1/reminders/{reminderId}/occurrences/{occurrenceId}/dismiss` | Dismiss Reminder *(existing behavior, included alongside Complete — see note below)* |
| `POST` | `/v1/reminders/{reminderId}/occurrences/{occurrenceId}/reopen` | Reopen Reminder *(existing behavior, included alongside Complete — see note below)* |
| `GET` | `/v1/reminders/{reminderId}/history` | List Reminder History |
| `POST` | `/v1/auth/register` | Register |
| `POST` | `/v1/auth/verify` | Verify Account |
| `POST` | `/v1/auth/login` | Login |
| `POST` | `/v1/auth/refresh-token` | Refresh Token |
| `POST` | `/v1/auth/forgot-password` | Forgot Password |
| `POST` | `/v1/auth/reset-password` | Reset Password |
| `GET` | `/v1/users/me` | Get Current User |
| `PATCH` | `/v1/users/me` | Update Profile |
| `DELETE` | `/v1/users/me` | Delete Account |
| `GET` | `/v1/notifications` | List Notifications |
| `POST` | `/v1/notifications/{notificationId}/read` | Mark Notification Read |
| `POST` | `/v1/devices/push-tokens` | Register Push Token |
| `DELETE` | `/v1/devices/push-tokens/{pushTokenId}` | Unregister Push Token |

*Dismiss and Reopen were not named in the request list but are existing, already-specified behaviors in the Application Layer Specification (§3.8) — omitting their endpoints would remove API access to behavior the product already has, which conflicts with "do not change behaviour." Included as siblings of Complete, same resource shape.*

---

## 5. Endpoint Specifications

*Grouped by area. Validation and business rules reference [APPLICATION_LAYER_SPEC.md](./APPLICATION_LAYER_SPEC.md) by section rather than restating them. Success/error envelopes follow §3; only the resource returned and endpoint-specific errors are listed per entry.*

### Board APIs

| Field | Create Board | Get Board | List Boards | Rename Board | Delete Board |
|---|---|---|---|---|---|
| Method / Resource | `POST /boards` | `GET /boards/{boardId}` | `GET /boards` | `PATCH /boards/{boardId}` | `DELETE /boards/{boardId}` |
| Auth | Required | Required | Required | Required | Required |
| Authorization | Authenticated User | Board Member | Authenticated User (own Boards only) | Board Owner | Board Owner |
| Path Params | — | `boardId` | — | `boardId` | `boardId` |
| Query Params | — | — | `cursor`, `limit` (§7) | — | — |
| Request Body | `{ name }` | — | — | `{ name }` | — |
| Validation | Name required (format only — business rules per App Layer §3.1) | — | — | Name required | Confirmation handled client-side before calling; no body needed since the path ID + Owner check is the safeguard |
| Success | `201`, Board | `200`, Board | `200`, Board[] | `200`, Board | `204` |
| Errors | `400`, `401`, `409` (idempotency replay conflict) | `401`, `404` | `401` | `401`, `403`, `404` | `401`, `403`, `404` |
| Side Effects | App Layer §3.1 | Read-only | Read-only | App Layer §3.4 | App Layer §3.3 |
| Idempotency | `Idempotency-Key` header required | N/A (read) | N/A (read) | Natural (re-applying same name is a no-op) | Natural (deleting an already-deleted Board is a no-op → `204`) |

| Field | Leave Board | Mute Board | Unmute Board | Invite Member | Remove Member | List Members |
|---|---|---|---|---|---|---|
| Method / Resource | `POST /boards/{boardId}/leave` | `POST /boards/{boardId}/mute` | `POST /boards/{boardId}/unmute` | `POST /boards/{boardId}/invitations` | `DELETE /boards/{boardId}/members/{userId}` | `GET /boards/{boardId}/members` |
| Auth | Required | Required | Required | Required | Required | Required |
| Authorization | Board Member | Board Member (own Membership only) | Board Member (own Membership only) | Board Member | Board Owner | Board Member |
| Path Params | `boardId` | `boardId` | `boardId` | `boardId` | `boardId`, `userId` | `boardId` |
| Query Params | — | — | — | — | — | `cursor`, `limit` |
| Request Body | — | — | — | `{ channel }` (`link`\|`email`\|`sms`; target contact if `email`/`sms`) | — | — |
| Validation | None beyond auth — business rules per App Layer §3.2/§4 | — | — | Target contact format if `email`/`sms` | — | — |
| Success | `200`, `{ reassignedOwnerUserId: string \| null }` | `204` | `204` | `201`, Invitation | `204` | `200`, Membership[] |
| Errors | `401`, `403` (Personal Board / sole-Member — see App Layer §3.2), `404` | `401`, `403`, `404` | `401`, `403`, `404` | `401`, `403`, `404`, `409` (blocked relationship) | `401`, `403`, `404`, `409` (removing self) | `401`, `404` |
| Side Effects | App Layer §3.2 | App Layer §3.4 | App Layer §3.4 | App Layer §3.5 | App Layer §3.6 | Read-only |
| Idempotency | Natural (already `Left` → `204`-equivalent no-op) | Natural | Natural | `Idempotency-Key` header required | Natural (already `Removed` → no-op) | N/A (read) |

### Invitation APIs

| Field | Validate Invitation | Accept Invitation | Decline Invitation |
|---|---|---|---|
| Method / Resource | `GET /invitations/{token}` | `POST /invitations/{token}/accept` | `POST /invitations/{token}/decline` |
| Auth | **Not required** | Required (self-resolved, §2) | Required (self-resolved, §2) |
| Authorization | Public (token itself is the credential) | Authenticated User, must match the invitation's resolved invitee where one was specified | Same as Accept |
| Path Params | `token` | `token` | `token` |
| Request Body | — | — | — |
| Success | `200`, `{ boardName, inviterDisplayName, status, expiresAt }` — deliberately minimal, never the full Board resource | `200`, Membership | `200`, `{ status: "declined" }` |
| Errors | `404` (invalid/unknown token — never distinguishes "expired" from "never existed" to avoid token-guessing signal) | `401`, `404`, `409` (already resolved, expired, revoked) | `401`, `404`, `409` |
| Side Effects | Read-only | App Layer §3.5 (two-step: `InvitationAccepted` then `MembershipGranted`, per its documented eventually-consistent workflow) | App Layer §3.5 |
| Idempotency | N/A (read) | Natural (already `Accepted` by the same User → returns existing Membership, `200` not `409`) | Natural |

*Create Invitation is specified under Board APIs above (`POST /boards/{boardId}/invitations`) since it requires Board-Member authorization and is naturally scoped to a Board — consistent with Principle 3.*

### Reminder APIs

| Field | Create Reminder | Get Reminder | Update Reminder | Delete Reminder | List Board Reminders |
|---|---|---|---|---|---|
| Method / Resource | `POST /boards/{boardId}/reminders` | `GET /reminders/{reminderId}` | `PATCH /reminders/{reminderId}` | `DELETE /reminders/{reminderId}` | `GET /boards/{boardId}/reminders` |
| Auth | Required | Required | Required | Required | Required |
| Authorization | Board Member | Board Member (of the Reminder's Board) | Board Member | Board Member | Board Member |
| Path Params | `boardId` | `reminderId` | `reminderId` | `reminderId` | `boardId` |
| Query Params | — | — | — | — | `cursor`, `limit`, `status`, `from`, `to` (§8) |
| Request Body | `{ title, recurrence, startDate, notifyPreset }` | — | `{ title?, recurrence?, startDate?, notifyPreset? }` — **`boardId` is not an accepted field; present in payload → `400`, not silently ignored** | — | — |
| Validation | Title required; `recurrence`/`notifyPreset` must be valid enum values (format only — business rules per App Layer §3.7) | — | Same enum validation on any field present; `boardId` in body is a validation error, not a no-op | — | `status`/`from`/`to` must be valid filter values if present (§8) |
| Success | `201`, Reminder | `200`, Reminder | `200`, Reminder | `204` | `200`, Reminder[] |
| Errors | `400`, `401`, `403`, `404` (Board not found/not a Member), `409` (Board Deleted — App Layer §0/§3.7) | `401`, `404` | `400` (including a `boardId` present in the body), `401`, `403`, `404`, `409` (Board Deleted) | `401`, `403`, `404` | `401`, `404` |
| Side Effects | App Layer §3.7 | Read-only | App Layer §3.7 | App Layer §3.7 (halts generation, cancels pending Buzzes) | Read-only |
| Idempotency | `Idempotency-Key` header required | N/A | Natural (identical values → no-op) | Natural (already-deleted → `204`) | N/A |

| Field | Complete / Dismiss / Reopen Reminder | List Reminder History |
|---|---|---|
| Method / Resource | `POST /reminders/{reminderId}/occurrences/{occurrenceId}/complete` (or `/dismiss`, `/reopen`) | `GET /reminders/{reminderId}/history` |
| Auth | Required | Required |
| Authorization | Board Member | Board Member |
| Path Params | `reminderId`, `occurrenceId` | `reminderId` |
| Query Params | — | `cursor`, `limit` |
| Request Body | `{ expectedVersion }` — the optimistic-concurrency check named in the Implementation/Application Layer specs | — |
| Validation | `expectedVersion` required for Complete/Dismiss/Reopen — its mismatch is not a format error, it's the documented "already resolved by someone else" case (see Errors) | — |
| Success | `200`, Occurrence | `200`, HistoryEntry[] (`actor`, `action`, `timestamp`) |
| Errors | `401`, `403`, `404`, `409` **with the resolved Occurrence in the response body** when `expectedVersion` doesn't match — this is a documented, expected outcome ("already done by X"), not a generic conflict; `410 GONE` if the parent Reminder has been Deleted (App Layer §0/§3.8) *(the one endpoint family in this catalogue using `410` instead of `404`, precisely because the resource — the Occurrence — did exist and is now permanently inert; see §6)* | `401`, `404` |
| Side Effects | App Layer §3.8 | Read-only |
| Idempotency | Natural — a second call after success returns the same resolved state, `200` not an error | N/A |

### Profile APIs

| Field | Register | Verify Account | Login | Refresh Token |
|---|---|---|---|---|
| Method / Resource | `POST /auth/register` | `POST /auth/verify` | `POST /auth/login` | `POST /auth/refresh-token` |
| Auth | Not required | Not required | Not required | Not required (refresh token is the credential) |
| Request Body | `{ displayName, email or phone, password }` | `{ email or phone, code }` | `{ email or phone, password }` | `{ refreshToken }` |
| Success | `201`, `{ userId }` | `200`, `{ accessToken, refreshToken, user: User }` | `200`, `{ accessToken, refreshToken, user: User }` | `200`, `{ accessToken, refreshToken }` |
| Errors | `400`, `409` (email/phone already registered) | `400`, `401` (wrong/expired code) | `401` (generic — never confirms which field was wrong), `403` (`Suspended`) | `401` (expired/revoked refresh token) |
| Side Effects | App Layer §3.10 (Register) | App Layer §3.10 (VerifyAccount — triggers Account Provisioning) | Read-only + session issuance | Session reissuance only |
| Idempotency | `Idempotency-Key` header required | Natural (already-verified → `200`, not an error) | N/A | N/A |

| Field | Forgot Password | Reset Password | Get Current User | Update Profile | Delete Account |
|---|---|---|---|---|---|
| Method / Resource | `POST /auth/forgot-password` | `POST /auth/reset-password` | `GET /users/me` | `PATCH /users/me` | `DELETE /users/me` |
| Auth | Not required | Not required | Required | Required | Required |
| Request Body | `{ email or phone }` | `{ token, newPassword }` | — | `{ displayName?, photoUrl?, email?, phone? }` | `{ confirmation: true }` |
| Success | `200`, `{}` — **always this response, whether or not the account exists** (App Layer §3.10's privacy rule) | `200`, `{}` | `200`, User | `200`, User | `204` |
| Errors | None visible (see above) | `401` (invalid/expired/reused token) | `401` | `400`, `401`, `409` (email/phone already in use by another account) | `401` |
| Side Effects | App Layer §3.10 | App Layer §3.10 | Read-only | App Layer §3.10 — email/phone change re-triggers verification | App Layer §3.10 (multi-step orchestrated workflow) |
| Idempotency | Natural (always the same response) | Natural (reused token → `401`, not a silent no-op — a used token must not work twice) | N/A | Natural (identical values → no-op) | Natural (already-deleted → `204`) |

### Notification APIs

| Field | List Notifications | Mark Notification Read | Register Push Token | Unregister Push Token |
|---|---|---|---|---|
| Method / Resource | `GET /notifications` | `POST /notifications/{notificationId}/read` | `POST /devices/push-tokens` | `DELETE /devices/push-tokens/{pushTokenId}` |
| Auth | Required | Required | Required | Required |
| Authorization | Authenticated User (own notifications only) | Authenticated User (own only) | Authenticated User | Authenticated User (own token only) |
| Path Params | — | `notificationId` | — | `pushTokenId` |
| Query Params | `cursor`, `limit`, `status` | — | — | — |
| Request Body | — | — | `{ platform, token }` | — |
| Success | `200`, Notification[] | `200`, Notification | `201`, `{ pushTokenId }` | `204` |
| Errors | `401` | `401`, `404` | `400`, `401` | `401`, `404` |
| Side Effects | Read-only — this list exists as the in-app fallback for undelivered Buzzes (Product UX Spec §7.2), not a general activity feed | Marks the underlying Buzz `Seen` | Registers a device for the Dispatch Push Notifications background process (App Layer §6) — infrastructure-level, not a new domain aggregate | Deregisters the device |
| Idempotency | N/A | Natural (already-read → no-op) | Re-registering the same token → `200` returning the existing `pushTokenId`, not a duplicate | Natural (already-unregistered → `204`) |

---

## 6. Error Catalogue

| HTTP Status | `code` | Meaning | Example |
|---|---|---|---|
| `400` | `VALIDATION_ERROR` | Request shape/format is invalid | Missing required field, invalid enum value, `boardId` present on Update Reminder |
| `401` | `UNAUTHORIZED` | Missing, invalid, or expired credential | No access token, expired refresh token, wrong login password |
| `403` | `FORBIDDEN` | Authenticated, and the resource's existence is already known to the requester, but the action isn't permitted | Non-Owner calling Delete Board |
| `404` | `NOT_FOUND` | Resource doesn't exist, **or the requester has no visibility into it** (Principle 6) | A Board the requester isn't a Member of; an unknown Invitation token |
| `409` | `CONFLICT` | A business-rule conflict on an otherwise well-formed, authorized request | Blocked-relationship invitation, removing yourself, email already in use |
| `410` | `GONE` | The resource existed but is now permanently inert — used exclusively for Occurrence actions against a Deleted parent Reminder (App Layer §0) | Complete/Dismiss/Reopen after the Reminder was deleted |
| `429` | `RATE_LIMITED` | Too many requests in a bounded window | Repeated failed Login attempts |
| `500` | `SERVER_ERROR` | Unhandled failure | Any unexpected fault |

Every error response uses the envelope in §3 regardless of status code — a client only ever needs one error parser.

---

## 7. Pagination Standard

**Cursor-based, identical shape on every list endpoint, no exceptions:**

- Request: `?cursor={opaque string, omit for the first page}&limit={integer, default 20, max 100}`
- Response: `"pagination": { "nextCursor": "string | null" }` — `null` means no further pages.
- Cursors are opaque and must not be constructed or parsed by the client — they are returned, not built.
- Ordering is always newest-first for time-ordered resources (History, Notifications) and Board-name-then-creation-order for Boards/Reminders lists, matching the Information Architecture's chronological presentation — never client-configurable sort in V1 (no sort parameter exists).

---

## 8. Filtering Standard

*Applied only where the underlying use case actually supports it — a filter parameter is never added "for consistency" to an endpoint whose data doesn't vary along that dimension.*

| Parameter | Meaning | Valid On |
|---|---|---|
| `boardId` | Scope to one Board | Implicit via path nesting everywhere a Board-scoped list exists (§1 Principle 3) — never a duplicate query parameter alongside a path parameter that already carries it |
| `status` | Resource-appropriate status enum (`scheduled`\|`due`\|`completed`\|`dismissed`\|`missed` for Occurrence-bearing lists; `delivered`\|`failed`\|`seen`\|`dismissed` for Notifications) | List Board Reminders, List Notifications |
| `completed` | Boolean shorthand for `status=completed` on Reminder lists — offered because it's the single most common filter a client needs, not as a second, competing mechanism | List Board Reminders |
| `from` / `to` | ISO 8601 date bounds on `dueAt` | List Board Reminders, List Reminder History |

No filter parameter is accepted that isn't listed here — an unrecognized query parameter is ignored, never silently misinterpreted.

---

## 9. Versioning Strategy

- **URL-path versioning**: every endpoint lives under `/v1/`.
- A breaking change (removed field, changed meaning, removed endpoint) requires a new version (`/v2/`); the prior version continues serving unchanged until formally deprecated with advance notice.
- An additive change (new optional field, new endpoint, new optional filter) ships within the existing version — it never requires a bump.
- No endpoint is ever versioned independently of the others — the whole API moves version together, so a client is never in the position of mixing `/v1/` and `/v2/` calls to describe one coherent session.

---

## 10. API Consistency Review

A deliberate self-check against the principles in §1, confirming no mixed styles slipped in:

- **Naming:** every path segment is a lowercase, plural noun (`boards`, `reminders`, `occurrences`, `invitations`, `notifications`, `push-tokens`); every field name is `camelCase`; no endpoint mixes `snake_case` and `camelCase`, and none uses a verb as a top-level path segment except the documented action-sub-path pattern (`/leave`, `/mute`, `/complete`, etc.).
- **The one intentional nesting inconsistency** — Reminders and Invitations are created/listed under `/boards/{boardId}/...` but read/updated/deleted at their own top-level path — is applied identically in both cases and stated as Principle 3, not left implicit. Occurrence actions are nested two levels deep (`/reminders/{reminderId}/occurrences/{occurrenceId}/...`) even though Occurrence is its own aggregate root at the domain layer (Implementation Spec §1) — this is a deliberate case where the **API shape does not mirror the aggregate boundary 1:1**, because an Occurrence has no meaning to a client independent of the Reminder it belongs to. Domain aggregate boundaries and API resource boundaries are related but not required to be identical, and this is the one place that distinction actually matters in this contract.
- **Every list endpoint** uses the identical `cursor`/`limit`/`pagination.nextCursor` shape — verified across Boards, Members, Board Reminders, Reminder History, and Notifications.
- **Every error response**, across all 34 endpoints, uses the single envelope in §3 — no endpoint returns a bare string, a different key name, or an HTTP status not listed in §6.
- **404-not-403 for invisible resources** is applied consistently everywhere a Board-scoped or Reminder-scoped resource is addressed by ID — Get Board, Get Reminder, List Members, and both Invitation actions all follow it identically.
- **One deliberate status-code addition beyond the general catalogue:** `410 GONE` is used only for Occurrence actions against a Deleted Reminder, precisely because that case is meaningfully different from an ordinary `404` (the resource unambiguously existed and is now permanently inert, not merely invisible or never-created) — this is the only endpoint family using it, and it is not a general-purpose status available elsewhere in the API.

No other inconsistency was found. Every endpoint in §5 was checked against §1's nine principles individually before being included.

---

*This contract, together with [APPLICATION_LAYER_SPEC.md](./APPLICATION_LAYER_SPEC.md) and [IMPLEMENTATION_SPEC.md](./IMPLEMENTATION_SPEC.md), is sufficient for frontend and backend teams to build against independently. Where this document is silent on a behavioral question, the Application Layer Specification is authoritative; where it's silent on a domain question, the Implementation Specification is.*
