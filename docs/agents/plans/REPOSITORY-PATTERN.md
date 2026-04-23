# Repository Pattern — Data Access Layer Refactor

## Context

Every data composable currently contains an inline `if (demoMode)` branch that returns empty stubs or hardcoded objects. This is repetitive, hard to maintain, and leaves the demo experience mostly empty (only `useAuth` and `useTenants` return real demo objects; every other composable returns empty arrays). The goal is to introduce a repository abstraction layer so:

- Composables become thin wrappers with no demo-awareness
- A **live** repository wraps `$fetch` + `useAsyncData` (extracted from the existing composables verbatim)
- A **demo** repository is backed by a reactive localStorage singleton so the demo is fully interactive (mutations persist across page refreshes within conversion-gate limits)
- The correct implementation is selected once at startup via a Nuxt plugin and injected via `provide/inject`

This refactor also fixes a bug in `tenant.global.ts` where demo mode skips the auth check but still falls through to an unconditional `$fetch('/api/proxy/tenants')`, which fails without a real session.

---

## Directory Structure

All new files are within `src/Gatherstead.Web/app/`. No server-side changes.

```
app/
  plugins/
    repositories.client.ts         ← selects live vs. demo; provides to Vue app
  repositories/
    types.ts                       ← all domain types (moved from composables)
    interfaces.ts                  ← I*Repository interfaces + Repositories aggregate
    live/
      LiveTenantRepository.ts
      LiveHouseholdRepository.ts
      LiveHouseholdMemberRepository.ts
      LiveEventRepository.ts
      LiveEventAttendanceRepository.ts
      LiveMealPlanRepository.ts
      LiveChoreRepository.ts
    demo/
      DemoStore.ts                 ← shared reactive singleton + localStorage sync + limits
      DemoTenantRepository.ts
      DemoHouseholdRepository.ts
      DemoHouseholdMemberRepository.ts
      DemoEventRepository.ts
      DemoEventAttendanceRepository.ts
      DemoMealPlanRepository.ts
      DemoChoreRepository.ts
  composables/
    useRepositories.ts             ← inject() wrapper (new)
    useTenants.ts                  (updated — remove demoMode branch)
    useHouseholds.ts               (updated)
    useHouseholdMembers.ts         (updated)
    useEvents.ts                   (updated)
    useEventAttendance.ts          (updated)
    useMealPlans.ts                (updated)
    useChoreTemplates.ts           (updated)
    useAuth.ts                     (unchanged — auth identity is not data)
    useApiError.ts                 (unchanged)
    useTenantRole.ts               (unchanged)
  middleware/
    tenant.global.ts               (bug fix: demo short-circuit before API call)
  layouts/
    default.vue                    (add demo warning banner)
```

---

## Key Design Decisions

### Injection mechanism
A `.client.ts` Nuxt plugin calls `nuxtApp.vueApp.provide(REPOSITORIES_KEY, repos)` once at startup. `useRepositories()` wraps `inject()` with a throw-guard. This is the single point where live vs. demo is selected — every composable just calls `useRepositories()`.

### `useAsyncData` preserved
Both live and demo repositories implement methods returning `Promise<T>`. Composables wrap them with `useAsyncData` as today. Demo promises resolve instantly from in-memory refs. The key-based deduplication and `{ pending, error, refresh }` shape are preserved with zero page changes required.

### Demo store as reactive singleton
`DemoStore.ts` holds a module-level reactive state tree seeded from localStorage on first access and written back after every mutation. All demo repository classes share the same `ref` instances — a mutation in one is immediately reflected in others. `refresh()` after writes triggers `useAsyncData` to re-read the updated values, matching the live path's behavior.

### Pinia stores unchanged
`tenant.ts`, `member.ts`, `event.ts` remain pure navigation-context stores. No data caching is added to Pinia.

### Demo conversion limits
The demo repository enforces hard entity limits to encourage paid conversion. When a limit is hit, write methods throw a `DemoLimitError`. Composable write functions catch this and surface a warning toast — pages require no changes.

---

## Implementation Steps (in order)

Each step leaves the app fully working; the live path is not broken at any intermediate commit.

### 1. `app/repositories/types.ts`
Move all exported domain types here from their current composable files:
- `TenantRole`, `TenantSummary` ← `useTenants.ts`
- `HouseholdSummary`, `HouseholdMember`, `DietaryProfile` ← `useHouseholds.ts` / `useHouseholdMembers.ts`
- `EventSummary` ← `useEvents.ts`
- `AttendanceStatus`, `AttendanceRecord` ← `useEventAttendance.ts`
- `MealType`, `MealIntentStatus`, `MealTemplate`, `MealPlan`, `MealIntent` ← `useMealPlans.ts`
- `ChoreTimeSlot`, `ChoreTemplate`, `ChorePlan`, `ChoreIntent` ← `useChoreTemplates.ts`

Composable files re-export them for backward compatibility so no page imports break.

### 2. `app/repositories/interfaces.ts`
One interface per domain aggregate plus the `Repositories` aggregate:

```typescript
export interface ITenantRepository {
  listTenants(): Promise<TenantSummary[]>
}
export interface IHouseholdRepository {
  listHouseholds(tenantId: string): Promise<HouseholdSummary[]>
  getHousehold(tenantId: string, householdId: string): Promise<HouseholdSummary | null>
}
export interface IHouseholdMemberRepository {
  listMembers(tenantId: string, householdId: string): Promise<HouseholdMember[]>
  getMember(tenantId: string, householdId: string, memberId: string): Promise<HouseholdMember | null>
  getDietaryProfile(tenantId: string, householdId: string, memberId: string): Promise<DietaryProfile | null>
}
export interface IEventRepository {
  listEvents(tenantId: string): Promise<EventSummary[]>
  getEvent(tenantId: string, eventId: string): Promise<EventSummary | null>
}
export interface IEventAttendanceRepository {
  listAttendance(tenantId: string, eventId: string): Promise<AttendanceRecord[]>
  upsertAttendance(tenantId: string, eventId: string, householdId: string,
    memberId: string, day: string, status: AttendanceStatus): Promise<void>
}
export interface IMealPlanRepository {
  listMealTemplates(tenantId: string, eventId: string): Promise<MealTemplate[]>
  listPlans(tenantId: string, eventId: string, templateId: string): Promise<MealPlan[]>
  listIntentsForMember(tenantId: string, eventId: string, templateId: string,
    planId: string, memberId: string): Promise<MealIntent[]>
  upsertIntent(tenantId: string, eventId: string, templateId: string, planId: string,
    householdId: string, memberId: string, status: MealIntentStatus, bringOwnFood: boolean): Promise<void>
}
export interface IChoreRepository {
  listChoreTemplates(tenantId: string, eventId: string): Promise<ChoreTemplate[]>
  listPlans(tenantId: string, eventId: string, templateId: string): Promise<ChorePlan[]>
  listIntentsForMember(tenantId: string, eventId: string, templateId: string,
    planId: string, memberId: string): Promise<ChoreIntent[]>
  upsertIntent(tenantId: string, eventId: string, templateId: string, planId: string,
    householdId: string, memberId: string, volunteered: boolean): Promise<void>
}
export interface Repositories {
  tenants: ITenantRepository
  households: IHouseholdRepository
  householdMembers: IHouseholdMemberRepository
  events: IEventRepository
  eventAttendance: IEventAttendanceRepository
  mealPlans: IMealPlanRepository
  chores: IChoreRepository
}
```

### 3. Live repository classes (7 files)
Each class lifts the existing `$fetch` call verbatim from the composable. Internal API response types (`TenantsApiResponse`, etc.) move with them.

```typescript
// LiveTenantRepository.ts
export class LiveTenantRepository implements ITenantRepository {
  async listTenants(): Promise<TenantSummary[]> {
    const r = await $fetch<TenantsApiResponse>('/api/proxy/tenants')
    return (r.entity ?? []).map(t => ({ id: t.id, name: t.name, userRole: t.userRole }))
  }
}
```

### 4. `app/repositories/demo/DemoStore.ts`
Module-level reactive singleton. Exports limits and the custom error class:

```typescript
const STORAGE_KEY = 'gs-demo-store'

export const DEMO_LIMITS = {
  householdsPerTenant:    3,
  membersPerHousehold:    4,
  events:                 1,
  eventMaxDays:           3,
  mealTemplatesPerEvent:  2,
  choreTemplatesPerEvent: 2,
} as const

export class DemoLimitError extends Error {
  constructor(public readonly limitKey: keyof typeof DEMO_LIMITS) {
    super(`Demo limit reached: ${limitKey}`)
    this.name = 'DemoLimitError'
  }
}

function seedState(): DemoState {
  // One "Demo Community" tenant (Owner), one "The Demo Family" household,
  // one adult "Demo User" member, one "Summer Gathering" event (3 days,
  // starting next weekend). Leaves room to add 2 more households, 3 more
  // members, 2 meal templates, and 2 chore templates before hitting limits.
}

let _state: ReactiveState | null = null

export function getDemoStore(): ReactiveState {
  if (_state) return _state
  const raw = tryParseLocalStorage() ?? seedState()
  _state = buildReactiveRefs(raw)   // ref() per collection
  return _state
}

export function persistDemoStore(): void {
  if (!_state) return
  localStorage.setItem(STORAGE_KEY, JSON.stringify(snapshot(_state)))
}

export function demoId(): string {
  return 'demo-' + Math.random().toString(36).slice(2, 10)
}
```

### 5. Demo repository classes (7 files)
Read from the shared store; write methods enforce limits and throw `DemoLimitError`:

```typescript
// DemoHouseholdRepository.ts — example of limit-gated create
async createHousehold(tenantId: string, name: string): Promise<HouseholdSummary> {
  const store = getDemoStore()
  if (store.households.value.filter(h => h.tenantId === tenantId).length >= DEMO_LIMITS.householdsPerTenant) {
    throw new DemoLimitError('householdsPerTenant')
  }
  const h = { id: demoId(), tenantId, name }
  store.households.value.push(h)
  persistDemoStore()
  return h
}

// DemoEventAttendanceRepository.ts — attendance has no limit, upsert-or-insert
async upsertAttendance(_tenantId, eventId, _householdId, memberId, day, status) {
  const store = getDemoStore()
  const idx = store.attendance.value.findIndex(
    a => a.eventId === eventId && a.householdMemberId === memberId && a.day === day)
  if (idx >= 0) store.attendance.value[idx] = { ...store.attendance.value[idx], status }
  else store.attendance.value.push({ id: demoId(), eventId, householdMemberId: memberId, day, status })
  persistDemoStore()
}
```

Enforced limits by repository:

| Repository method | Limit key | Value |
|---|---|---|
| `DemoHouseholdRepository.createHousehold` | `householdsPerTenant` | 3 |
| `DemoHouseholdMemberRepository.createMember` | `membersPerHousehold` | 4 |
| `DemoEventRepository.createEvent` | `events` | 1 |
| `DemoEventRepository.createEvent` | `eventMaxDays` | 3 |
| `DemoMealPlanRepository.createTemplate` | `mealTemplatesPerEvent` | 2 |
| `DemoChoreRepository.createTemplate` | `choreTemplatesPerEvent` | 2 |

Attendance, meal intent, and chore intent upserts have no limit (they toggle existing data).

### 6. `app/plugins/repositories.client.ts`
```typescript
export const REPOSITORIES_KEY = Symbol('repositories')

export default defineNuxtPlugin((nuxtApp) => {
  const config = useRuntimeConfig()

  const repos: Repositories = config.public.demoMode
    ? {
        tenants:          new DemoTenantRepository(),
        households:       new DemoHouseholdRepository(),
        householdMembers: new DemoHouseholdMemberRepository(),
        events:           new DemoEventRepository(),
        eventAttendance:  new DemoEventAttendanceRepository(),
        mealPlans:        new DemoMealPlanRepository(),
        chores:           new DemoChoreRepository(),
      }
    : {
        tenants:          new LiveTenantRepository(),
        households:       new LiveHouseholdRepository(),
        householdMembers: new LiveHouseholdMemberRepository(),
        events:           new LiveEventRepository(),
        eventAttendance:  new LiveEventAttendanceRepository(),
        mealPlans:        new LiveMealPlanRepository(),
        chores:           new LiveChoreRepository(),
      }

  nuxtApp.vueApp.provide(REPOSITORIES_KEY, repos)

  // Bootstrap demo context stores from seed data
  if (config.public.demoMode) {
    const store = getDemoStore()
    const member = store.members.value[0]
    if (member) {
      const memberStore = useCurrentMemberStore()
      if (!memberStore.linkedMemberId) {
        memberStore.setLinkedMember(member.id, member.householdId)
      }
    }
  }
})
```

### 7. `app/composables/useRepositories.ts`
```typescript
export function useRepositories(): Repositories {
  const repos = inject<Repositories>(REPOSITORIES_KEY)
  if (!repos) throw new Error('[Gatherstead] Repositories not provided.')
  return repos
}
```

### 8. Composable refactors (7 files, one at a time)
Remove `if (demoMode)` branch. Keep `useAsyncData` wrapper. Re-export types for backward compatibility.

**Before** (`useTenants.ts`):
```typescript
if (config.public.demoMode) {
  return { tenants: ref([{ id: 'demo-tenant', ... }]), pending: ref(false), ... }
}
const { data, pending, error, refresh } = useAsyncData('tenants', async () => {
  const r = await $fetch<TenantsApiResponse>('/api/proxy/tenants')
  return r.entity.map(...)
})
```

**After**:
```typescript
export type { TenantSummary, TenantRole } from '~/repositories/types'

export function useTenants() {
  const { tenants: repo } = useRepositories()
  const { data, pending, error, refresh } = useAsyncData<TenantSummary[]>(
    'tenants', () => repo.listTenants())
  return { tenants: computed(() => data.value ?? []), pending, error, refresh }
}
```

Write methods catch `DemoLimitError` and surface a toast (pages unchanged):

```typescript
async function upsert(...) {
  try {
    await repo.upsertAttendance(...)
    await refresh()
  }
  catch (e) {
    if (e instanceof DemoLimitError) {
      useToast().add({
        title: t('demo.limitReached.title'),
        description: t('demo.limitReached.description'),
        color: 'warning',
      })
      return
    }
    throw e
  }
}
```

**Migration order** (each step leaves the app working):
1. `useTenants.ts`
2. `useHouseholds.ts`
3. `useHouseholdMembers.ts`
4. `useEvents.ts`
5. `useEventAttendance.ts`
6. `useMealPlans.ts`
7. `useChoreTemplates.ts`

### 9. Middleware fix (`tenant.global.ts`)
Add a demo short-circuit at the top of the middleware body before any API call:

```typescript
if (config.public.demoMode) {
  if (!tenantStore.currentTenantId) {
    tenantStore.setTenant('demo-tenant', 'Demo Community', 'Owner')
  }
  return
}
// ... existing live logic below unchanged
```

The tenant name `'Demo Community'` must match the seed in `DemoStore.ts`.

### 10. Demo warning banner (`app/layouts/default.vue`)
Add a `UAlert` above the main content slot, visible only in demo mode:

```vue
<UAlert
  v-if="config.public.demoMode"
  color="warning"
  variant="subtle"
  :title="$t('demo.banner.title')"
  :description="$t('demo.banner.description')"
  :actions="[{ label: $t('demo.banner.learnMore'), to: '/demo' }]"
/>
```

Add i18n keys under `demo.banner.*` and `demo.limitReached.*` in all locale files (`en.json`, `es.json`).

---

## Critical Files

| File | Role |
|------|------|
| `app/repositories/interfaces.ts` | TypeScript contracts — author first; everything else depends on it |
| `app/repositories/demo/DemoStore.ts` | Reactive localStorage singleton — seed quality determines demo UX |
| `app/plugins/repositories.client.ts` | The single live-vs-demo selection point |
| `app/composables/useRepositories.ts` | `inject()` wrapper used by every refactored composable |
| `app/middleware/tenant.global.ts` | Bug fix — wrong here breaks all `/app/**` navigation in demo mode |
| `app/layouts/default.vue` | Demo warning banner |

---

## Verification

1. **Live mode:** `NUXT_PUBLIC_DEMO_MODE=false` → `pnpm dev` → navigate all app routes; API calls work, no console errors.
2. **Demo mode:** `NUXT_PUBLIC_DEMO_MODE=true` → `pnpm dev` → seed data visible (one tenant, household, member, event); toggle attendance; reload page; confirm toggle persisted in localStorage.
3. **Limit enforcement:** attempt to add a 4th household — confirm warning toast appears and data is unchanged.
4. **Banner:** confirm demo warning renders in app layout with working link.
5. **Middleware fix:** in demo mode, navigate directly to `/app/events` — no redirect to `/tenants`, no 401.
6. **Type check:** `pnpm build` → 0 errors, 0 warnings.
7. **Lint:** `pnpm run lint` → clean.
