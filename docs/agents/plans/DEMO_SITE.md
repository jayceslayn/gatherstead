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

### Runtime Config

Add to `runtimeConfig.public` in [`nuxt.config.ts`](../src/Gatherstead.Web/nuxt.config.ts):

```ts
runtimeConfig: {
  public: {
    apiBaseUrl: 'http://localhost:5000',
    demoMode: false,  // Overridden by NUXT_PUBLIC_DEMO_MODE=true
  },
}
```

### Hybrid Rendering (Production)

Production uses Nuxt's hybrid rendering via `routeRules`. Public/marketing pages are server-rendered for SEO, while authenticated dashboard pages are client-only SPA (no SEO benefit, faster navigation):

```ts
routeRules: {
  '/dashboard/**': { ssr: false },   // SPA -- authenticated, no SEO need
  '/': { prerender: true },           // Static prerender -- landing page
},
```

### Demo Build

When `NUXT_PUBLIC_DEMO_MODE` is set, SSR is disabled entirely so `nuxt generate` produces a pure SPA:

```ts
ssr: !process.env.NUXT_PUBLIC_DEMO_MODE,
```

**Build command:**
```bash
NUXT_PUBLIC_DEMO_MODE=true nuxt generate
```

Output is written to `.output/public/` -- a fully static site ready for deployment.

## Repository Pattern

The data access layer uses a **repository pattern** with two implementations per domain aggregate: a live implementation that wraps `$fetch` calls to the backend proxy, and a demo implementation backed by a reactive localStorage singleton. See **[REPOSITORY-PATTERN.md](REPOSITORY-PATTERN.md)** for the full design, file structure, interface definitions, implementation steps, and verification checklist.

### Summary

- **Injection**: A `.client.ts` Nuxt plugin selects live vs. demo repositories at startup and provides them to the Vue app via `provide/inject`. Composables call `useRepositories()` — they contain no `if (demoMode)` logic.
- **Live repositories**: `app/repositories/live/Live*Repository.ts` — lift `$fetch` calls verbatim from composables.
- **Demo repositories**: `app/repositories/demo/Demo*Repository.ts` — read from and write to a shared reactive singleton (`DemoStore.ts`) backed by localStorage key `gs-demo-store`.
- **Composables**: thin `useAsyncData` wrappers over repository methods; write methods catch `DemoLimitError` and surface a warning toast.
- **`useAuth.ts`**: unchanged — authentication identity is not data and retains its own demo branch.

## Entity Limits

Limits are defined as constants in `repositories/demo/DemoStore.ts` and enforced by demo repository write methods, which throw `DemoLimitError` when exceeded. Composable write functions catch `DemoLimitError` and surface a warning toast — pages require no changes.

```ts
export const DEMO_LIMITS = {
  householdsPerTenant:    3,   // teases directory feature, encourages upgrade
  membersPerHousehold:    4,
  events:                 1,   // single event; max 3 days duration
  eventMaxDays:           3,
  mealTemplatesPerEvent:  2,
  choreTemplatesPerEvent: 2,
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
| Meal / Chore Templates | None (visitor adds up to 2 of each) |
| Attendance / Intents | None (visitor toggles interactively) |

### Initialization

`getDemoStore()` checks for an existing `gs-demo-store` key in localStorage:
- **First visit**: Key absent — write seed data
- **Return visit**: Key present — deserialise and resume; all prior mutations are intact
- **Reset**: Clear `gs-demo-store`, call `getDemoStore()` again to re-seed (exposed via the demo banner's "Learn more" page)

## Auth Bypass

`composables/useAuth.ts` provides a unified auth interface with a demo bypass:

```ts
export function useAuth() {
  const { demoMode } = useRuntimeConfig().public

  if (demoMode) {
    return {
      isAuthenticated: ref(true),
      user: ref({ name: 'Demo User', role: 'Owner' }),
      login: () => {},
      logout: () => {
        // Clear localStorage, redirect to landing
      },
    }
  }

  // Real auth implementation (Entra ID / MSAL)
}
```

This prevents any auth-related redirects or checks from blocking the demo experience. The mock user has Owner role so all features are accessible.

## Demo Banner

A `UAlert` is rendered above the main content slot in `app/layouts/default.vue`, visible only when `demoMode` is true. It is always present (not dismissible) so visitors are continually aware of the demo context.

```vue
<!-- app/layouts/default.vue — above the <slot /> -->
<UAlert
  v-if="config.public.demoMode"
  color="warning"
  variant="subtle"
  :title="$t('demo.banner.title')"
  :description="$t('demo.banner.description')"
  :actions="[{ label: $t('demo.banner.learnMore'), to: '/demo' }]"
/>
```

The `/demo` route (a public page) explains the demo's limitations, entity limits table, and provides a "Reset Demo Data" button that clears `gs-demo-store` from localStorage and reloads the seed.

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

| File | Action |
|------|--------|
| `src/Gatherstead.Web/nuxt.config.ts` | Add `demoMode` config, `routeRules`, conditional SSR |
| `src/Gatherstead.Web/app/repositories/types.ts` | **Create** — domain types moved from composables |
| `src/Gatherstead.Web/app/repositories/interfaces.ts` | **Create** — `I*Repository` interfaces + `Repositories` aggregate |
| `src/Gatherstead.Web/app/repositories/live/Live*Repository.ts` | **Create** — `$fetch`-backed live implementations (7 files) |
| `src/Gatherstead.Web/app/repositories/demo/DemoStore.ts` | **Create** — reactive localStorage singleton + `DEMO_LIMITS` + `DemoLimitError` |
| `src/Gatherstead.Web/app/repositories/demo/Demo*Repository.ts` | **Create** — localStorage-backed demo implementations (7 files) |
| `src/Gatherstead.Web/app/plugins/repositories.client.ts` | **Create** — selects live vs. demo; provides via `provide/inject` |
| `src/Gatherstead.Web/app/composables/useRepositories.ts` | **Create** — `inject()` wrapper |
| `src/Gatherstead.Web/app/composables/use*.ts` (7 files) | **Update** — remove `if (demoMode)` branches; delegate to repositories |
| `src/Gatherstead.Web/app/composables/useAuth.ts` | **Unchanged** — auth identity retains its own demo branch |
| `src/Gatherstead.Web/app/middleware/tenant.global.ts` | **Update** — add demo short-circuit before API call |
| `src/Gatherstead.Web/app/layouts/default.vue` | **Update** — add `UAlert` demo banner |
| `src/Gatherstead.Web/app/pages/demo.vue` | **Create** — public page explaining demo limits + reset button |
| `src/Gatherstead.Web/app/i18n/locales/en.json` | **Update** — add `demo.banner.*` and `demo.limitReached.*` keys |
| `src/Gatherstead.Web/app/i18n/locales/es.json` | **Update** — same keys in Spanish |
| `infrastructure/modules/staticwebapp.bicep` | **Create** — Static Web App module |
| `infrastructure/main.bicep` | Add conditional demo module |
| `.github/workflows/deploy-demo.yml` | **Create** — Demo CI/CD workflow |
