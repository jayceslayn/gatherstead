# Demo Site

This document describes the architecture and implementation plan for deploying a public demo site alongside the production Gatherstead app. The demo lets potential users and recruiters experience the full UI with zero friction -- no login, no backend, no cost.

## Architecture Overview

The demo site uses the **same codebase** as production. A runtime config flag switches behavior:

| Aspect | Production | Demo |
|--------|-----------|------|
| **Rendering** | Hybrid SSR/SPA (`nuxt build`) | Static SPA (`nuxt generate`) |
| **Data** | Real API + SQL Server | Browser localStorage |
| **Auth** | Entra ID / PASETO tokens | Mock authenticated owner user |
| **Hosting** | Azure App Service | Azure Static Web Apps (Free tier) |
| **Cost** | App Service plan + SQL | Free |
| **URL** | `gatherstead.<host>.<ext>` | `demo.gatherstead.<host>.<ext>` |

## Configuration

### Build-time Constant

The primary mechanism is a build-time `__DEMO_MODE__` constant injected via `vite.define`, which enables Rollup to dead-code-eliminate the inactive branch — including its `import()` expressions — so live and demo bundles contain only their respective repository implementations:

```ts
vite: {
  define: {
    __DEMO_MODE__: JSON.stringify(process.env.NUXT_PUBLIC_DEMO_MODE === 'true'),
  },
},
ssr: process.env.NUXT_PUBLIC_DEMO_MODE !== 'true',
```

An ambient declaration in `app/types/env.d.ts` makes `__DEMO_MODE__` available throughout the TypeScript codebase:

```ts
declare const __DEMO_MODE__: boolean
```

### Runtime Config

`runtimeConfig.public` in [`nuxt.config.ts`](../src/Gatherstead.Web/nuxt.config.ts) still includes `demoMode: false` (overrideable via `NUXT_PUBLIC_DEMO_MODE`) for runtime use in a small number of places (e.g., the demo banner's "Go Live" link guard). All mode branching in code uses `__DEMO_MODE__` rather than `config.public.demoMode`.

### Hybrid Rendering (Production)

Production uses Nuxt's hybrid rendering via `routeRules`. Public/marketing pages are server-rendered for SEO, while authenticated dashboard pages are client-only SPA (no SEO benefit, faster navigation):

```ts
routeRules: {
  '/tenants/**': { ssr: false },
  '/app/**': { ssr: false },
  '/': { prerender: true },
},
```

### Demo Build

When `NUXT_PUBLIC_DEMO_MODE=true`, `ssr` is set to `false` and `nuxt generate` produces a pure SPA:

**Build command:**
```bash
NUXT_PUBLIC_DEMO_MODE=true pnpm generate
```

Output is written to `.output/public/` — a fully static site ready for deployment.

## Repository Pattern

The data access layer uses a **repository pattern** with two implementations per domain aggregate: a live implementation that wraps `$fetch` calls to the backend proxy, and a demo implementation backed by a reactive localStorage singleton. See **[REPOSITORY-PATTERN.md](REPOSITORY-PATTERN.md)** for the full design, file structure, interface definitions, implementation steps, and verification checklist.

### Summary

- **Injection**: `app/plugins/repositories.client.ts` (a Nuxt client plugin) selects live vs. demo repositories at build time using `__DEMO_MODE__`. Each branch performs a single `import()` from the relevant barrel file — `~/repositories/live` or `~/repositories/demo` — so Rollup emits one chunk per mode and eliminates the dead branch entirely. The resolved repositories are provided to the Vue app via `provide/inject`; composables call `useRepositories()` with no mode awareness.
- **Live repositories**: `app/repositories/live/Live*Repository.ts` — `$fetch`-backed implementations; re-exported from `repositories/live/index.ts`.
- **Demo repositories**: `app/repositories/demo/Demo*Repository.ts` — read from and write to a shared reactive singleton (`DemoStore.ts`) backed by localStorage key `gs-demo-store`; re-exported from `repositories/demo/index.ts`.
- **Demo constants**: Pure static values (`DEMO_TENANT`, `DEMO_USER`, `DEMO_LIMITS`, etc.) live in `repositories/demo/demoConstants.ts` — no reactive state — so they can be imported directly in `useAuth` and `tenant.global.ts` without pulling `DemoStore`'s reactive internals into the live bundle.
- **Composables**: thin `useAsyncData` wrappers over repository methods; write methods catch `DemoLimitError` and surface a warning toast. No `demoMode` checks anywhere in pages, layouts, or components.
- **`useAuth.ts`**: branches on `__DEMO_MODE__` (build-time) to stub auth with demo user identity.

## Entity Limits

Limits are defined as constants in `repositories/demo/demoConstants.ts` and enforced by demo repository write methods, which throw `DemoLimitError` when exceeded. Composable write functions catch `DemoLimitError` and surface a warning toast — pages require no changes.

```ts
export const DEMO_LIMITS = {
  householdsPerTenant:      3,
  membersPerHousehold:      5,
  events:                   1,
  eventMaxDays:             3,
  mealTemplatesPerEvent:    3,
  taskTemplatesPerEvent:    4,
  propertiesPerTenant:      2,
  accommodationsPerProperty: 6,
  equipmentPerTenant:       10,
} as const
```

### Conversion Messaging

When a limit is hit, the composable's write function catches `DemoLimitError` and shows a toast via `useToast()`. No separate composable or utility is needed.

Locale keys to add under `demo.*` in `en.json` and `es.json`:

```json
{
  "demo": {
    "banner": {
      "title": "You're exploring the Gatherstead Demo",
      "description": "Data is stored in your browser. Some features are limited.",
      "learnMore": "Learn more"
    },
    "limitReached": {
      "title": "Demo limit reached",
      "description": "Sign up for full access to remove these restrictions."
    }
  }
}
```

## Seed Data

`repositories/demo/DemoStore.ts` writes seed data to the `gs-demo-store` localStorage key on first visit. The seed is intentionally minimal — leaving room for the visitor to add entities up to the limits — so the demo interaction feels like real use rather than a pre-filled snapshot.

### Seed Content

| Entity | Seed Data |
|--------|-----------|
| Tenant | 1: "Demo Community" (Owner role) |
| Households | 1: "The Demo Family" |
| Members | 1 adult: "Demo User" |
| Events | 1: "Summer Gathering" (3 days, starting next weekend) |
| Meal / Task Templates | None (visitor adds up to 2 of each) |
| Attendance / Intents | None (visitor toggles interactively) |

### Initialization

`getDemoStore()` checks for an existing `gs-demo-store` key in localStorage:
- **First visit**: Key absent — write seed data
- **Return visit**: Key present — deserialise and resume; all prior mutations are intact
- **Reset**: Clear `gs-demo-store`, call `getDemoStore()` again to re-seed (exposed via the demo banner's "Learn more" page)

## Auth Bypass

`composables/useAuth.ts` provides a unified auth interface with a demo bypass, branching on the build-time `__DEMO_MODE__` constant so the live bundle contains no demo code path:

```ts
import { DEMO_USER_DISPLAY_NAME, DEMO_USER_EXTERNAL_ID } from '~/repositories/demo/demoConstants'

export function useAuth() {
  if (__DEMO_MODE__) {
    return {
      loggedIn: ref(true),
      user: ref({ name: DEMO_USER_DISPLAY_NAME, email: DEMO_USER_EXTERNAL_ID }),
      login: () => navigateTo('/tenants'),
      logout: () => navigateTo('/'),
    }
  }

  const { loggedIn, user } = useUserSession()
  return {
    loggedIn,
    user,
    login: () => navigateTo('/auth/azure', { external: true }),
    logout: () => navigateTo('/auth/logout', { external: true }),
  }
}
```

This prevents any auth-related redirects or checks from blocking the demo experience. The mock user has Owner role so all features are accessible. The `auth.ts` route middleware requires no demo-mode check — `useAuth()` already returns `loggedIn: ref(true)` in demo mode, so `if (!loggedIn.value)` naturally passes through.

## Demo Banner

`app/components/DemoBanner.vue` renders a sticky amber banner at the top of every page when `__DEMO_MODE__` is `true` (its own `v-if` guard), providing a persistent demo context indicator. It is always present (not dismissible).

The banner includes a "Learn more" button that opens a modal (`UModal`) with:
- The entity limits table (sourced from `DEMO_LIMITS` in `demoConstants.ts`)
- A "Reset Demo Data" button that calls `clearDemoStore()` and re-runs `seedDemoData()`
- A "Go Live" CTA button (shown only when `config.public.liveUrl` is set)

The banner is included in both `layouts/default.vue` and `layouts/landing.vue` via `<DemoBanner />`. In a live build the component is present but its `v-if="isDemoMode"` (where `isDemoMode = __DEMO_MODE__`) evaluates to `false` at compile time, so it renders nothing. `clearDemoStore` and `seedDemoData` are loaded via dynamic `import()` inside `resetDemoData()`, guarded by `if (!__DEMO_MODE__ || !repos) return`, ensuring these async chunks are never created in the live build.

## Infrastructure

### Azure Static Web Apps (Free Tier)

The demo site is deployed to Azure Static Web Apps, which provides free hosting for static sites with custom domain support, SSL, and global CDN.

**New file**: `infrastructure/modules/staticwebapp.bicep`

```bicep
@description('The Azure region for the Static Web App.')
param location string

resource demoSite 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'gat-demo-swa'
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    buildProperties: {
      appLocation: 'src/Gatherstead.Web'
      outputLocation: '.output/public'
    }
  }
}

output demoSiteUrl string = demoSite.properties.defaultHostname
```

**Wire into [`main.bicep`](../infrastructure/main.bicep):**

```bicep
@description('Whether to deploy the demo static web app.')
param deployDemo bool = false

module demo 'modules/staticwebapp.bicep' = if (deployDemo) {
  name: 'demo'
  scope: rg
  params: {
    location: location
  }
}

output demoSiteUrl string = deployDemo ? demo.outputs.demoSiteUrl : ''
```

Add `deployDemo: true` to the appropriate Bicep parameter files when ready to deploy.

### Custom Domain

Configure `demo.gatherstead.<host>.<ext>` as a custom domain on the Static Web App via DNS CNAME record pointing to the default hostname.

## CI/CD

**New file**: `.github/workflows/deploy-demo.yml`

```yaml
name: Deploy Demo Site

on:
  push:
    branches: [main]
  workflow_dispatch:

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: pnpm/action-setup@v4

      - uses: actions/setup-node@v4
        with:
          node-version-file: '.node-version'
          cache: 'pnpm'
          cache-dependency-path: src/Gatherstead.Web/pnpm-lock.yaml

      - name: Install dependencies
        working-directory: src/Gatherstead.Web
        run: pnpm install --frozen-lockfile

      - name: Generate static demo site
        working-directory: src/Gatherstead.Web
        env:
          NUXT_PUBLIC_DEMO_MODE: 'true'
        run: pnpm generate

      - name: Deploy to Azure Static Web Apps
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          action: upload
          app_location: src/Gatherstead.Web/.output/public
          skip_app_build: true
```

This workflow keeps the demo site in sync with the latest code on `main`.

## Routing Considerations

With `nuxt generate`, all routes must be statically pre-renderable. Since the demo has no real data-driven dynamic routes (no server-side data fetching at build time), all routes work as long as:

- Pages use client-side data fetching (from localStorage) rather than `useAsyncData` with server-side fetches
- Dynamic route segments are rendered client-side
- SSR is disabled for the demo build (producing a pure SPA with client-side routing)

## Testing

- **Unit tests for demo services**: Verify CRUD operations against localStorage, limit enforcement (create beyond limit throws `DemoLimitError`), seed data initialization, and reset functionality.
- **Unit tests for `useService` composable**: Mock `runtimeConfig` to verify the correct implementation is returned based on `demoMode`.
- **Component test for `DemoBanner`**: Verify it renders only when `demoMode` is true, expands/collapses correctly, and the Reset button clears localStorage.
- **E2E**: A Playwright test could verify the full demo flow -- page loads with seed data, user can create entities up to limits, limit error appears with conversion message, and reset works.

## Files Summary

| File | Status |
|------|--------|
| `src/Gatherstead.Web/nuxt.config.ts` | Updated — `vite.define` (`__DEMO_MODE__`), `ssr !== 'true'`, `routeRules` |
| `src/Gatherstead.Web/app/types/env.d.ts` | Added — ambient `declare const __DEMO_MODE__: boolean` |
| `src/Gatherstead.Web/app/repositories/types.ts` | Added — domain types barrel |
| `src/Gatherstead.Web/app/repositories/interfaces.ts` | Added — `I*Repository` interfaces + `Repositories` aggregate |
| `src/Gatherstead.Web/app/repositories/live/Live*Repository.ts` | Added — `$fetch`-backed live implementations (15 files) |
| `src/Gatherstead.Web/app/repositories/live/index.ts` | Added — barrel re-export of all 15 live repository classes |
| `src/Gatherstead.Web/app/repositories/demo/demoConstants.ts` | Added — pure static constants (`DEMO_TENANT`, `DEMO_USER`, `DEMO_LIMITS`, etc.) |
| `src/Gatherstead.Web/app/repositories/demo/DemoStore.ts` | Added — reactive localStorage singleton + `DemoLimitError`; re-exports from `demoConstants` |
| `src/Gatherstead.Web/app/repositories/demo/Demo*Repository.ts` | Added — localStorage-backed demo implementations (15 files) |
| `src/Gatherstead.Web/app/repositories/demo/DemoNotImplemented.ts` | Added — `notImplemented(method)` utility for intentionally deferred stub methods |
| `src/Gatherstead.Web/app/repositories/demo/index.ts` | Added — barrel re-export of all 15 demo repository classes |
| `src/Gatherstead.Web/app/repositories/demo/seedDemoData.ts` | Added — seeds full demo dataset through repository CRUD methods |
| `src/Gatherstead.Web/app/plugins/repositories.client.ts` | Added — `__DEMO_MODE__`-gated single dynamic barrel import; provides via `provide/inject` |
| `src/Gatherstead.Web/app/composables/useRepositories.ts` | Added — `inject()` wrapper |
| `src/Gatherstead.Web/app/composables/useAuth.ts` | Updated — branches on `__DEMO_MODE__`; no `isDemo` in return shape |
| `src/Gatherstead.Web/app/middleware/auth.ts` | Updated — removed redundant `demoMode` check |
| `src/Gatherstead.Web/app/middleware/tenant.global.ts` | Updated — branches on `__DEMO_MODE__`; `getDemoStore` via dynamic import |
| `src/Gatherstead.Web/app/composables/useTenantUsers.ts` | Updated — `setLinkedMember` mode-agnostic (matches `userId` to auth user `externalId`) |
| `src/Gatherstead.Web/app/components/DemoBanner.vue` | Added — sticky banner + modal with limits table, reset, and Go Live CTA |
| `src/Gatherstead.Web/app/layouts/default.vue` | Updated — `<DemoBanner />`; sign-out item gated by `__DEMO_MODE__` |
| `src/Gatherstead.Web/app/layouts/landing.vue` | Updated — `<DemoBanner />`; auth buttons gated by `isDemoMode = __DEMO_MODE__` |
| `src/Gatherstead.Web/app/locales/en.json` | Updated — `demo.banner.*`, `demo.modal.*` keys |
| `src/Gatherstead.Web/app/locales/es.json` | Updated — same keys in Spanish |
| `infrastructure/modules/staticwebapp.bicep` | Added — Static Web App module |
| `infrastructure/main.bicep` | Updated — conditional `deployDemo` demo module |
| `.github/workflows/deploy-demo.yml` | Added — Demo CI/CD workflow |
