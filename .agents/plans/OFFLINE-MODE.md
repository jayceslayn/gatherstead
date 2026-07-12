---
updated: 2026-07-12
commit: 67c2cd7
status: design — not yet implemented
---

# Offline Mode — Design

## Summary

Gatherstead gets a PWA offline mode targeted at the core use case: a family gathered at a remote property with unreliable or no internet. **V1 scope (settled):**

- **Offline reads**: a per-tenant snapshot of everything the user can see, stored in IndexedDB, served through the existing repository seam.
- **Offline intent-style writes**: the per-user upsert/withdraw records — event attendance, meal attendance, meal intents (cook sign-up), task intents, accommodation intents, shopping-item intents (claim/provide/check-off) — queued in an IndexedDB outbox and replayed on reconnect. These are natural upserts keyed by (member, target), so replay is idempotent and safe.
- **Everything else stays online-only in V1**: creating/editing events, households, members, templates, plans, properties, accommodations, equipment, shopping items, users, settings. Offline attempts get a friendly "you're offline" toast; UI affordances disable while offline.
- Platform: PWA via `@vite-pwa/nuxt` (app-shell service worker + IndexedDB data). No native app.
- Phase 2 (full read-write sync with conflict handling) is sketched at the end to prove V1 doesn't paint us into a corner; it is not designed in depth here.

Why this shape works here: all authenticated routes are already `ssr: false` client-only SPAs; all data access already flows through the `Repositories` seam (`app/repositories/interfaces.ts`, provided by `app/plugins/repositories.client.ts`); and the demo mode (`app/repositories/demo/` + `DemoStore.ts`) already runs the entire app off local storage — proof the seam supports a local data source end to end.

## Settled Decisions

1. **IndexedDB via Dexie**. Rationale over raw `idb`: versioned schema migrations (the demo store already needed one storage-key bump to `gs-demo-store-v2` — Dexie formalizes this), typed tables, transactions, bulk ops. Data volume is trivial either way; developer ergonomics and the migration story decide it.
2. **Snapshot storage = collection blobs, not per-row tables.** One `collections` table keyed `[tenantId+name]` holding the full repo-layer array (same shapes `DemoStore` persists). The API already returns full tenant collections and pages consume full lists, so blob-replace is atomic, trivially consistent, and matches the read pattern. Phase 2 delta sync migrates to per-row tables via a Dexie version bump — a contained migration, not a corner.
3. **Offline repos are decorators over the live repos** — a third *partial* implementation, not a third full set. Each decorator: reads are network-first (delegating to the wrapped live repo) with **write-through** of successful responses into the snapshot store, falling back to snapshot + outbox overlay when offline; intent writes enqueue to the outbox when offline; all other writes throw `OfflineWriteError` when offline. Live repos stay untouched.
4. **Snapshot triggers**: automatic background refresh on app load / tenant switch when online (throttled), continuous freshness via read write-through, plus an explicit **"Prepare for offline"** action that does the full fan-out with progress UI and requests persistent storage.
5. **Service worker precaches the app shell only. `/api/proxy/**` is NetworkOnly — never cached by the SW.** IndexedDB is the sole data cache: it is structured, per-tenant, role-stamped, and clearable on logout. Authenticated PII in opaque Cache Storage entries would duplicate that data with none of those properties.
6. **No backend changes required for V1.** The existing intent PUT-upsert / DELETE-withdraw endpoints replay as-is. `UpdatedAt`-stamped-at-replay is accepted for V1 (audit columns record server receipt, which is what they mean; attendance semantics live in the domain fields, not the audit stamp). A `ClientRecordedAt` column is a Phase 2 option, deliberately separate from audit columns.
7. **No encryption-at-rest of the snapshot in V1.** Rely on server-side role masking (the snapshot contains only what this user may see), OS/device encryption, browser profile isolation, and aggressive lifecycle clearing (logout, user switch, role change). Documented in the privacy page. See §7 for the threat-model reasoning and the Phase 2 field-encryption option.

## 1. Client Storage Layer

### Dexie schema (`app/offline/db.ts`)

```ts
// gs-offline v1
export class GsOfflineDb extends Dexie {
  collections!: Table<CollectionRow, [string, string]>  // pk [tenantId+name]
  queries!: Table<QueryRow, [string, string]>           // pk [tenantId+key]
  outbox!: Table<OutboxRow, string>                     // pk id (uuid)
  meta!: Table<TenantMeta, string>                      // pk tenantId
  constructor() {
    super('gs-offline')
    this.version(1).stores({
      collections: '[tenantId+name]',
      queries: '[tenantId+key], [tenantId+fetchedAt]',
      outbox: 'id, [tenantId+targetKey], [tenantId+createdAt], status',
      meta: 'tenantId',
    })
  }
}

interface CollectionRow { tenantId: string; name: string; items: unknown[]; fetchedAt: number }
// Cached computed/parameterised reads (reports, availability search, myTasks, myStays,
// per-plan intent lists) keyed by a canonical query string, e.g. `report:{eventId}`.
interface QueryRow { tenantId: string; key: string; result: unknown; fetchedAt: number }
interface TenantMeta {
  tenantId: string
  userId: string            // MeSummary id — mismatch on login ⇒ wipe tenant data
  role: TenantRole          // masking differs per role — change ⇒ wipe + resnapshot
  snapshotAt: number | null // last successful full snapshot (drives staleness UI)
  schemaVersion: number     // app-level shape version (DemoStore v2-bump pattern)
}
```

**Collection names** mirror `DemoState` in `DemoStore.ts` (tenants, households, members, events, attendance, mealTemplates, mealPlans, mealIntents, mealAttendance, taskTemplates, taskPlans, taskIntents, properties, accommodations, accommodationIntents, equipment, shoppingItems) plus lookups (ageBands, dietaryTags) and `me`. Items are **repo-layer types**, post-mapping — write-through happens inside the repo decorators, so no second DTO mapping exists.

### Offline repo decorators (`app/repositories/offline/`)

One decorator class per entity repo, implementing the existing interface and wrapping the live instance:

```ts
export class OfflineEventAttendanceRepository implements IEventAttendanceRepository {
  constructor(private live: IEventAttendanceRepository, private ctx: OfflineContext) {}

  async listAttendance(tenantId: string, eventId: string) {
    return this.ctx.read(tenantId, 'attendance',
      () => this.live.listAttendance(tenantId, eventId),          // online: live + write-through
      rows => rows.filter(r => r.eventId === eventId),            // offline: snapshot slice
      rows => overlayAttendance(rows, this.ctx.pendingOps(tenantId)))  // + outbox overlay
  }

  async upsertAttendance(tenantId, eventId, householdId, memberId, day, status) {
    if (this.ctx.isOnline.value) return this.live.upsertAttendance(...)
    await this.ctx.enqueue({ kind: 'eventAttendance.upsert',
      targetKey: `evAtt:${eventId}:${memberId}:${day}`, payload: {...} })
  }
  // deleteAttendance offline: row exists in snapshot ⇒ enqueue withdraw by natural key
  // (replay resolves attendanceId from the refreshed snapshot); row is pending-only ⇒
  // just delete the queued upsert.
}
```

`OfflineContext.read` centralizes the strategy: **online → live call, then write-through into `collections` (full-tenant lists replace the blob; event-scoped lists replace matching rows within the blob); offline (or `FetchError` with no response) → snapshot slice + overlay.** Getter methods (`getEvent`, `getHousehold`, …) filter the blob. Computed endpoints (event meal report, `searchAvailability`, `listMyTasks`, `listMyStays`, per-member intent lists) use the `queries` cache-by-key table in V1; where the demo repos already implement the derivation over local collections (availability search, myTasks), extract that logic into shared pure functions under `app/repositories/shared/` and reuse it so offline results stay live against the overlay — do this opportunistically, not as a gate.

Non-intent writes offline throw `OfflineWriteError` (exported from `interfaces.ts` beside `DemoLimitError`); `useTrackedAction` and `useApiError.translateError` gain a branch that renders it as a "You're offline — this needs a connection" toast. Pages additionally disable create/edit buttons via `useConnectivity().isOnline`.

### Wiring (`app/plugins/repositories.client.ts`)

The non-demo branch becomes:

```ts
const liveRepos = await import('~/repositories/live')
const { createOfflineContext, withOffline } = await import('~/repositories/offline')
const ctx = await createOfflineContext()   // opens Dexie, starts connectivity watcher
repos = withOffline({ tenants: new liveRepos.LiveTenantRepository(), ... }, ctx)
```

`withOffline` wraps each live repo in its decorator. Demo mode is untouched (already fully local). A runtime escape hatch (`NUXT_PUBLIC_OFFLINE_MODE=false`) skips wrapping for rollback.

## 2. Snapshot / Refresh Protocol (V1)

**Full-collection refetch is acceptable** — tenant datasets are small, relational, no blobs — but the naive fan-out (members per household, plans per template, intents per accommodation, shopping per event+property) is 50–150 requests for a busy tenant. The `SnapshotService` (`app/offline/snapshot.ts`):

- Fetches through the **repo layer** (so write-through does the storing) with a concurrency cap of 4 and the existing `retryOn429` helper from the live repo layer.
- Scopes event-derived data to an **active window**: events whose `endDate >= today − 14d`, plus lookups, directory (households + members), properties, accommodations + intents, equipment, property shopping lists. Uses the aggregate endpoints where they exist (per-event attendance/intent lists, bulk reads).
- Tolerates per-collection 403s (e.g. `tenantUsers`/invitations are Manager+; Guest-role users see little by design — see `GUEST-ACCESS.md` positive filtering): record the collection as empty-with-fetchedAt, never fail the snapshot.
- Sets `meta.snapshotAt` only on full success; partial success updates individual `fetchedAt`s.

**Triggers**: (a) on app load / tenant switch when online, skipped if `snapshotAt` is under 15 minutes old; (b) manual **Prepare for offline** (dashboard card + settings entry): full fan-out with a progress bar, then `navigator.storage.persist()` request and, on iOS-not-installed, an install-to-home-screen hint (see Risks); (c) opportunistic freshness from read write-through on every page visit (the `useShoppingList` poll/visibilitychange pattern is *not* extended app-wide in V1 — write-through covers it).

**Staleness display**: a global `GsOfflineBanner` in `layouts/default.vue` driven by `useConnectivity()`: offline ⇒ "Offline — showing data from {relative snapshotAt}" (+ pending-change count from the outbox); online-with-queued ⇒ "Syncing N changes…". All strings via i18n (`offline.*` in `en.json`/`es.json`).

## 3. Offline Write Queue (Outbox)

### Schema

```ts
interface OutboxRow {
  id: string                    // crypto.randomUUID()
  tenantId: string
  targetKey: string             // natural key, e.g. `mealAtt:{planId}:{memberId}`,
                                // `taskIntent:{planId}:{memberId}`, `shopIntent:{itemId}:{memberId}`,
                                // `accIntent:{accommodationId}:{memberId}:{startNight}`
  kind: OutboxKind              // discriminated union: eventAttendance.upsert|withdraw,
                                // mealAttendance.upsert|withdraw, mealIntent.upsert|withdraw,
                                // taskIntent.upsert|withdraw, accommodationIntent.create|withdraw,
                                // shoppingIntent.upsert|withdraw
  payload: unknown              // exact repo-method args
  createdAt: number
  attempts: number
  lastError: string | null
  status: 'pending' | 'replaying' | 'failed'   // failed = terminal 4xx, kept for surfacing
}
```

**Coalescing on enqueue**: intents are upserts keyed by (member, target), so a new op for an existing `[tenantId+targetKey]` **replaces** the pending row (claim → unclaim → claim collapses to the latest). A withdraw against a target that exists *only* as a pending create simply deletes the row.

### Replay (`app/offline/replay.ts`)

- Triggered by the connectivity watcher flipping online, on app load, and after the auth round-trip completes. Guarded by **Web Locks** (`navigator.locks.request('gs-outbox-replay', …)`) so exactly one tab replays.
- Order: `createdAt` ascending (post-coalescing there is one op per target; targets are independent — FIFO kept for predictability).
- Each op maps back to a live-repo call. Withdraws that need a server id (`deleteAttendance(attendanceId)`, `deleteIntent(intentId)`) first refresh the relevant collection and resolve the id by natural key; a missing row means already-gone ⇒ success.
- **Failure policy**: network error / 5xx / 429 → keep `pending`, exponential backoff (30s, 2m, 10m, then next connectivity event), no toast spam (banner shows "retrying"). Terminal 4xx (validation, permission — e.g. a coordinator deleted the plan while we were offline) → mark `failed`, toast via `translateError` naming the target ("Couldn't sync your Tuesday dinner RSVP — the plan no longer exists"), with a per-item dismiss (delete) in an outbox review list reachable from the banner. 401 → stop replay entirely and defer to the auth flow (§4). Ops never mark themselves `failed` on 401.
- After a successful replay batch: refresh affected collections (one snapshot pass over touched entity types) so server-assigned ids/fields replace optimistic rows.

### Optimistic overlay

Reads served from snapshot (and, for immediate UI feedback, reads served live while ops are still pending) run through per-entity overlay functions: pending upserts patch matching rows or insert synthetic rows with id `pending:{targetKey}`; pending withdraws remove rows. Overlay lives beside the outbox (`app/offline/overlay.ts`) as pure functions — unit-testable, and reusable by Phase 2. `useShoppingList.patchLocal` keeps working because `upsertIntent` offline returns the optimistically patched `ShoppingItem` (overlay applied to the snapshot copy).

### Interaction with the 401 interceptor (`app/plugins/api.client.ts`)

The interceptor's full-page redirect to `/auth/azure` is **compatible as-is**: the outbox lives in IndexedDB and survives the round-trip. Required touches: (a) the replay loop checks `useReauth()` and pauses when the reauth-in-flight flag is set (the flag already de-duplicates redirects); (b) replay resumes on next app boot, so returning from Entra automatically continues the queue.

## 4. Connectivity & Auth Handling

**`useConnectivity()`** (new composable, app-wide singleton state): starts from `navigator.onLine`, listens to `online`/`offline` events, and *verifies* transitions with a probe — `$fetch('/api/ping')`, a new trivial Nitro route (`server/api/ping.get.ts`, returns 204, no auth) that proves device↔Nitro reachability without touching the API or triggering the 401 interceptor. `FetchError`s with no response anywhere in the app also flip the flag (reported via the offline context). Transitions debounce 5s (captive portals / flaky LTE flap). Same-origin probe satisfies the existing CSP `connect-src 'self'`.

**Session expired while offline**: reads keep working — they never leave IndexedDB. Queued writes keep accumulating. On reconnect the first replay call 401s → the existing interceptor clears the client session and redirects to Entra (seamless under SSO). UX: before the redirect the banner shows "Reconnected — signing you in to sync your changes"; after return, replay resumes and the banner shows sync progress. No special offline auth machinery; the invariant is simply *the queue never requires an authenticated session to persist*.

**Service worker** (`@vite-pwa/nuxt`, `generateSW` strategy):

- Precache: build assets + a navigation-fallback shell for the SPA trees. `navigateFallback` to the SPA entry HTML with a `navigateFallbackDenylist` of `^/api`, `^/auth`, and the prerendered public pages.
- Runtime: `/api/proxy/**` and `/auth/**` are **NetworkOnly** (decision 5). Fonts/icons/images may get a cache-first runtime rule (all same-origin already).
- `registerType: 'prompt'` — an in-app "Update available → Reload" toast, never an auto-reload (an auto-reload mid-offline-session or mid-replay is hostile). See Risks for the update-flow caveat.
- CSP (`nuxt.config.ts` `security.headers`): add `'worker-src': ["'self'"]` explicitly; registration script is covered by `script-src 'self'`. Verify in report-only before flipping.
- The demo build (static site) can adopt the same SW harmlessly, but offline decorators stay out of the demo branch — demo is already fully local.

## 5. Backend Changes for V1

**None required.** The attendance controllers expose PUT upsert (+ bulk) and DELETE-by-id; meal/task/shopping/accommodation intents are PUT-upsert / DELETE-withdraw per-user rows; all idempotent under replay, and intent authorization runs identically for replayed and online writes.

Assessed and deferred:

- **`effectiveAt` / client timestamp**: rejected for V1. Audit columns mean "when the server recorded it" and should stay that way; the domain payload (day, night, status) carries the semantics that matter. If Phase 2 conflict resolution wants "when the user actually acted", add a nullable `ClientRecordedAt` column on intent tables — never overload `CreatedAt`/`UpdatedAt`.
- **Rate limiting**: snapshot fan-out may brush the API limiter. Client-side `retryOn429` + concurrency cap 4 should suffice; if telemetry shows 429 storms, raise the per-user burst allowance or add an aggregate snapshot endpoint (which Phase 2's delta endpoint supersedes anyway).

## 6. Phase 2 Sketch — Full Read-Write Sync

Not designed in depth; listed to show V1 choices compose forward.

- **Concurrency tokens**: add `RowVersion` (SQL `rowversion`) to `AuditableEntity`, exposed in DTOs as an opaque base64 `etag`; writes send `If-Match`; API returns 412 on mismatch. Today's last-write-wins remains the fallback for token-less clients during rollout.
- **Delta endpoint**: `GET api/tenants/{id}/changes?since={cursor}` returning `{ cursor, changes: [{ entityType, id, etag, data | tombstone }] }`. **Cursor = max rowversion** (database-global monotonic counter), not `UpdatedAt` or temporal `SysStartTime` — rowversion is immune to clock skew and retroactive writes; temporal tables stay what they are (audit history), not a sync feed. Requires an index on `(TenantId, RowVersion)` per synced table and a union query.
- **Tombstones**: sync needs deletions below Manager+. Expose a minimal `{ entityType, id, deletedAt }` shape in the delta feed for all roles — ids only, no payload. **Open RBAC question**: this reveals *that* something the caller could once see was deleted; assess against the Manager+-only `includeDeleted` principle before building.
- **Client-generated ids**: offline creates mint **UUIDv7** client-side; create endpoints accept an optional `Id` (validated, idempotent-deduped). UUIDv7's time-ordering also answers the clustered-PK fragmentation concern in `audit-column-db-defaults.md` — changing the value-generation strategy belongs to that plan.
- **Conflict resolution** (family-scale contention is rare and social): per-**row** server-authoritative LWW with `If-Match`; on 412 the client refetches and either auto-reapplies (field-disjoint change) or surfaces "someone else updated this" with a simple choice. Intent tables keep natural-key LWW with no tokens (V1 behavior, already correct). Per-field merge is rejected as over-engineering; surfaced-to-user is reserved for the genuinely contended surfaces: meal-plan notes/menu and task-plan completion.
- **V1 artifacts that carry forward**: the outbox schema (add `etag`/`baseVersion` fields), overlay functions, connectivity/replay machinery, Dexie migration to per-row tables (`version(2)`), and the decorator seam — delta sync replaces the `SnapshotService` fan-out, nothing else moves.

## 7. Security & Privacy on Device

Alignment with `docs/DESIGN_PRINCIPLES.md` (privacy by design, minimization, data protection at rest):

- **Minimization is structural**: the snapshot is fetched through the authenticated API, so `SensitiveReadScope` masking and role/household filtering have already been applied server-side. The device stores only what this user could see anyway; a Guest's snapshot contains almost nothing.
- **Lifecycle clearing** (the real control): wipe all Dexie data for the user's tenants on explicit logout; wipe a tenant's rows when `meta.userId` doesn't match the logged-in user (shared-device user switch); wipe + resnapshot when `meta.role` changes (masking differs); wipe on `schemaVersion` bump. A "Remove offline data" button lives beside "Prepare for offline". **Deliberate exception**: the outbox is preserved across the *401 re-auth* round-trip (same user) but destroyed on explicit logout — leftover pending writes from user A must never replay as user B.
- **Encryption at rest: not in V1, documented.** Honest threat model: a WebCrypto AES-GCM key must live somewhere the offline app can reach after a cold start — i.e., IndexedDB itself (non-extractable `CryptoKey`). That defeats same-origin script exfiltration of the raw key but only raises the bar for disk-forensics attackers (browser profiles already hold session material). A server-held key would break the core use case (cold start with no network). Recommendation: rely on OS full-disk encryption + profile isolation, state it in `/privacy`, and offer Phase 2 field-level encryption (dietary notes, birth dates, contact methods — mirroring the Always-Encrypted column set) with a non-extractable IndexedDB-resident key if we want the forensics bar raised.
- SW caches contain no API data (decision 5), so purge-on-logout reduces to Dexie deletes; the precache holds only non-sensitive build assets.

## 8. Implementation Phasing

Each WP independently shippable; sizes S/M/L ≈ ½ day / 1–2 days / 3–5 days.

**WP1 — PWA shell (M)**: add `@vite-pwa/nuxt` (manifest, icons, `generateSW`, navigation fallback + denylist, NetworkOnly for `/api/**`), `worker-src` CSP, update-prompt toast, install hint component. No data offline yet.
*Verify*: Lighthouse PWA pass; DevTools → Network → Offline → reload `/app` renders the shell (data errors expected); CSP report-only shows zero violations; SW update prompt appears after a redeploy.

**WP2 — Offline foundation (M)**: `useConnectivity` + `/api/ping`; Dexie `db.ts` (collections/queries/outbox/meta); `OfflineContext` with write-through; lifecycle wipes (logout hook, user/role/schema checks).
*Verify*: unit tests for wipe rules and write-through (Vitest, `fake-indexeddb`); browse pages online then inspect IndexedDB contents in DevTools.

**WP3 — Offline reads + snapshot + staleness UI (L)**: the 18 decorator classes (reads + `OfflineWriteError` on non-intent writes), `withOffline` wiring + escape hatch, `SnapshotService` with active-window fan-out + `retryOn429` + concurrency cap, "Prepare for offline" UX with progress + `storage.persist()`, `GsOfflineBanner`, `translateError`/`useTrackedAction` branch, i18n `offline.*` en/es.
*Verify*: prepare-for-offline → DevTools offline → navigate every major page (dashboard, event detail, meals, tasks, shopping, directory, accommodations) and confirm rendered data + banner + disabled create buttons; 403-tolerance as a Guest-role user.

**WP4 — Outbox + replay + overlay (L)**: enqueue with coalescing, overlay functions per intent type, replay engine (Web Locks, backoff, id-resolution for withdraws, 401 pause via `useReauth`, post-replay refresh), failed-op review list, banner sync states.
*Verify*: offline → RSVP + claim shopping items + volunteer for a task → confirm optimistic UI → go online → confirm server rows and queue drain; kill the session server-side mid-offline, reconnect, confirm redirect → return → replay completes; two-tab replay exercises the lock; Playwright e2e using `context.setOffline(true/false)` for the happy path + auth round-trip.

**WP5 — Backend touches + hardening (S)**: none strictly required; telemetry-driven — rate-limit allowance if 429s appear, App Insights events for snapshot/replay outcomes, the privacy-page paragraph. Decide here whether `ClientRecordedAt` is wanted before Phase 2.

**Reused existing code**: `DemoStore` patterns (collection shapes, storage-key versioning, clear semantics) inform the Dexie layer; demo repos' local-query logic is extraction fodder for offline computed reads; `useTrackedAction`'s typed-error path absorbs `OfflineWriteError` exactly like `DemoLimitError`; `retryOn429` for the snapshot fan-out; `useShoppingList`'s visibilitychange/poll idiom for refresh triggers; `useReauth` for replay/auth coordination.

## 9. Risks & Open Questions

- **iOS Safari eviction**: non-installed PWAs get IndexedDB + SW wiped after ~7 days of disuse; `storage.persist()` is not honored pre-install. Mitigation: the prepare-for-offline flow detects iOS-not-installed and pushes add-to-home-screen. Residual risk: a user who prepares, doesn't install, and arrives at the property 8+ days later has nothing. **Open: how hard to push installation?**
- **Role changes invalidate snapshots** (masking): handled by wipe-on-role-change, but a *demotion happening while the user is offline* means the device briefly holds data above the new role until reconnect. Accepted for V1 — same exposure as a stale open browser tab today.
- **Multi-tab**: Dexie is multi-tab-safe; replay and snapshot are Web-Locks-guarded. Overlay reactivity across tabs is best-effort in V1 (no `liveQuery` wiring) — a second tab may need a refresh to see the first tab's pending ops.
- **SW update flow**: `prompt` mode means a user who never accepts updates runs an old shell against a new API. The proxy insulates most drift; a shell/API contract break needs a "minimum app version" check — deferred.
- **Accommodation-intent replay conflicts**: two family members can both request the last bed offline; replay accepts both `Requested` rows (status transitions are Coordinator+ anyway), so the coordinator resolves it — socially fine, but the requester must see their stay as "requested (pending sync)", never "confirmed". UI copy matters.
- **`navigator.onLine` lies** (captive portals, LTE-with-no-throughput): the probe + fetch-error signals mitigate; expect some flapping at a rural property — hence the 5s debounce.
- **Open**: should "Prepare for offline" also pre-fetch the event meal report for the cook (the cached `queries` row covers it only if they visit the page once while online — is that enough)? Should the demo site advertise offline capability (it already is offline) for marketing parity?
