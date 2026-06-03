# Demo Site: Bundle Isolation & demoMode Cleanup

## Context

The static demo site (deployed to Azure Static Web Apps via `deploy-demo.yml`) and the live app share a single Nuxt codebase, distinguished by `NUXT_PUBLIC_DEMO_MODE=true` at build time. Currently, all 30 repository classes (15 live + 15 demo), `DemoStore.ts` (~237 lines of reactive state), and `seedDemoData.ts` (~257 lines) are **statically imported into every bundle** — including the live production bundle. Rollup cannot tree-shake runtime-branched imports, so prod users download ~500 lines of dead demo code.

Additionally, `config.public.demoMode` checks are scattered across layouts, middleware, and one settings page — the presentational layer shouldn't need to know which mode it's running in.

This plan addresses both issues.

---

## Part 1: Build-Time Dead Code Elimination

### Mechanism

Promote `demoMode` from a Vite runtime config value to a **build-time constant** via `vite.define`. When Rollup sees `if (false) { await import('...') }`, it eliminates the entire block — including the `import()` expression — from the chunk graph. The demo build gets no live code; the live build gets no demo code.

### Changes

**`nuxt.config.ts`** — add `vite.define`:
```typescript
vite: {
  define: {
    __DEMO_MODE__: JSON.stringify(!!process.env.NUXT_PUBLIC_DEMO_MODE),
  },
},
```
`JSON.stringify(!!...)` emits `true`/`false` as literal tokens (not strings), which is required for Rollup dead-code elimination. The existing `ssr: !process.env.NUXT_PUBLIC_DEMO_MODE` and `runtimeConfig.public.demoMode` remain unchanged — `runtimeConfig` is still needed for runtime template reads like `config.public.demoUrl`.

**`app/types/env.d.ts`** — new ambient declaration:
```typescript
declare const __DEMO_MODE__: boolean
```
No `export {}` — must be an ambient file. Picked up by Nuxt 4's generated tsconfig via `app/**`.

**`app/repositories/demo/demoConstants.ts`** — new file extracting pure-data constants from `DemoStore.ts`:
- `DEMO_USER_DISPLAY_NAME`, `DEMO_TENANT_ID`, `DEMO_USER_ID`, `DEMO_USER_EXTERNAL_ID`
- `DEMO_TENANT` (plain object, type `TenantSummary`)
- `DEMO_USER` (plain object, type `TenantUserSummary`)
- `DEMO_LIMITS` (object literal)

This file has no reactive state and imports only from `../types`. It ends up in both bundles but is tiny (~30 lines). Its purpose is to sever the live-bundle dependency on `DemoStore.ts`'s reactive internals for the three files that only need the constants.

`DemoStore.ts` is updated to import these constants from `demoConstants.ts` and re-export them (preserving backward compatibility for demo repositories that import from DemoStore).

**`app/plugins/repositories.client.ts`** — replace 34 static imports with `__DEMO_MODE__`-gated `Promise.all` dynamic imports:
```typescript
import { REPOSITORIES_KEY } from '~/repositories/interfaces'
import type { Repositories } from '~/repositories/interfaces'

export default defineNuxtPlugin(async (nuxtApp) => {
  let repos: Repositories

  if (__DEMO_MODE__) {
    const [{ DemoTenantRepository }, /* ...14 more... */ { getDemoStore }, { seedDemoData }] =
      await Promise.all([
        import('~/repositories/demo/DemoTenantRepository'),
        // ...
        import('~/repositories/demo/DemoStore'),
        import('~/repositories/demo/seedDemoData'),
      ])
    repos = { tenants: new DemoTenantRepository(), /* ... */ }
    nuxtApp.vueApp.provide(REPOSITORIES_KEY, repos)
    const store = getDemoStore()
    if (store.properties.value.length === 0) await seedDemoData(repos)
  } else {
    const [{ LiveTenantRepository }, /* ...14 more... */] =
      await Promise.all([
        import('~/repositories/live/LiveTenantRepository'),
        // ...
      ])
    repos = { tenants: new LiveTenantRepository(), /* ... */ }
    nuxtApp.vueApp.provide(REPOSITORIES_KEY, repos)
  }
})
```
`Promise.all` loads all modules in parallel — matches the speed of previous static imports since both are resolved from the local chunk cache after initial load.

**`app/composables/useAuth.ts`** — swap line 1 import:
```typescript
// Before
import { DEMO_USER, DEMO_USER_DISPLAY_NAME } from '~/repositories/demo/DemoStore'
// After
import { DEMO_USER_DISPLAY_NAME, DEMO_USER_EXTERNAL_ID } from '~/repositories/demo/demoConstants'
```
Update `DEMO_USER.externalId` → `DEMO_USER_EXTERNAL_ID`.

**`app/middleware/tenant.global.ts`** — swap line 4 import:
```typescript
// Before
import { DEMO_TENANT, DEMO_USER, getDemoStore } from '~/repositories/demo/DemoStore'
// After
import { DEMO_TENANT, DEMO_USER } from '~/repositories/demo/demoConstants'
```
Move `getDemoStore` to a dynamic import inside the `if (__DEMO_MODE__)` block (the middleware callback is already `async`). Use `__DEMO_MODE__` instead of `config.public.demoMode` for both branches so Rollup eliminates the entire dead branch including the dynamic import reference.

**`app/components/DemoBanner.vue`** — swap lines 5–6 imports:
```typescript
// Before
import { DEMO_LIMITS, clearDemoStore } from '~/repositories/demo/DemoStore'
import { seedDemoData } from '~/repositories/demo/seedDemoData'
// After
import { DEMO_LIMITS } from '~/repositories/demo/demoConstants'
```
Move `clearDemoStore` and `seedDemoData` to dynamic imports inside the `async resetDemoData()` function.

**`app/repositories/demo/DemoNotImplemented.ts`** — new utility:
```typescript
export function notImplemented(method: string): never {
  throw new Error(`[Demo] ${method} is not implemented`)
}
```
Use when intentionally deferring a demo implementation of a new interface method. Makes the gap explicit and immediately visible in the demo UI, rather than silently returning empty data.

---

## Part 2: Eliminate demoMode Checks from Vue Files

**Goal**: no `config.public.demoMode` in `.vue` files or layouts. Checks in composables and middleware are acceptable (infrastructure boundary).

### Current violations

| File | Check | Fix |
|---|---|---|
| `middleware/auth.ts:3` | `if (demoMode) return` | Remove — `useAuth()` returns `loggedIn: ref(true)` in demo, so the existing `!loggedIn.value` guard passes through naturally |
| `layouts/default.vue:48` | Hide sign-out in demo | Use `isDemo` from `useAuth()` |
| `layouts/landing.vue:10,20,27` | Toggle, "Try Demo" link, auth buttons | Use `isDemo` from `useAuth()` |
| `pages/app/settings/users/[userId]/index.vue:56` | Update `currentMemberStore` after `setLinkedMember` | Move side-effect into `useTenantUserActions` composable |
| `components/DemoBanner.vue:37` | The component's own render guard | Leave — inherently demo-specific; acceptable |

### `useAuth()` return shape

Add `isDemo: boolean` as the single mode indicator:

```typescript
if (config.public.demoMode) {
  return { loggedIn: ref(true), user: ref({...}), login, logout, isDemo: true }
}
// ...
return { loggedIn, user, login, logout, isDemo: false }
```

### Template changes

```html
<!-- default.vue: hide sign-out -->
<template v-if="!auth.isDemo">…sign-out item…</template>

<!-- landing.vue: hamburger toggle, "Try Demo" link, auth buttons -->
<UHeader :toggle="!auth.isDemo">
<UButton v-if="!auth.isDemo && config.public.demoUrl" …>Try Demo</UButton>
<template v-if="!auth.isDemo">…sign-in/sign-out…</template>
```

### `useTenantUserActions` side-effect

Add `householdId?: string` parameter to `setLinkedMember`. Inside the composable (not the page), update `currentMemberStore` when in demo mode:

```typescript
async function setLinkedMember(userId: string, memberId: string | null, householdId?: string) {
  // ...repo call...
  if (config.public.demoMode) {
    memberId && householdId
      ? currentMemberStore.setLinkedMember(memberId, householdId)
      : currentMemberStore.clear()
  }
}
```

The settings page calls `setLinkedMember(userId.value, memberId, memberHouseholdId.value || undefined)` with no demoMode check.

---

## Implementation Sequence

1. `nuxt.config.ts` — add `vite.define`
2. `app/types/env.d.ts` — new ambient declaration
3. `app/repositories/demo/demoConstants.ts` — new file
4. `app/repositories/demo/DemoStore.ts` — import from demoConstants, re-export for compat
5. `app/plugins/repositories.client.ts` — dynamic imports
6. `app/composables/useAuth.ts` — swap import + add `isDemo`
7. `app/middleware/auth.ts` — remove demoMode check
8. `app/middleware/tenant.global.ts` — demoConstants import + dynamic getDemoStore
9. `app/layouts/default.vue` — use `auth.isDemo`
10. `app/layouts/landing.vue` — use `auth.isDemo`
11. `app/composables/useTenantUsers.ts` — absorb store side-effect
12. `app/pages/app/settings/users/[userId]/index.vue` — remove demoMode check
13. `app/components/DemoBanner.vue` — demoConstants + dynamic imports
14. `app/repositories/demo/DemoNotImplemented.ts` — new utility

---

## Verification

```bash
# From src/Gatherstead.Web/

# Live build — demo modules must not appear in output chunks
pnpm build
grep -rl "DemoStore\|seedDemoData\|DemoTenantRepository" .output/public/_nuxt/ || echo "CLEAN"

# Demo build — live modules must not appear in output chunks
NUXT_PUBLIC_DEMO_MODE=true pnpm generate
grep -rl "LiveTenantRepository\|/api/proxy" .output/public/_nuxt/ || echo "CLEAN"

# No demoMode checks in .vue files
grep -rn "demoMode" src/Gatherstead.Web/app/pages/ src/Gatherstead.Web/app/layouts/ src/Gatherstead.Web/app/components/ || echo "CLEAN"

pnpm run lint
```
