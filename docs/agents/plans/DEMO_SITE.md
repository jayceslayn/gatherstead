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

## Service Layer Abstraction

This is the core architectural pattern. Every data operation goes through an interface that has two implementations: a real API client and a localStorage client. Components call a factory composable and never know which implementation is active.

### File Structure

```
src/Gatherstead.Web/app/
  services/
    types.ts                        # Shared TypeScript interfaces for all service contracts
    api/
      householdService.ts           # Real API implementation ($fetch to backend)
      memberService.ts
      eventService.ts
      propertyService.ts
      ...
    demo/
      demoStorageUtils.ts           # Shared localStorage read/write helpers
      demoLimits.ts                 # Entity limit constants
      demoSeedData.ts               # Initial seed data for first visit
      demoHouseholdService.ts       # localStorage CRUD implementation
      demoMemberService.ts
      demoEventService.ts
      demoPropertyService.ts
      ...
  composables/
    useService.ts                   # Factory composable that returns correct implementation
    useDemoLimits.ts                # Limit enforcement and error handling
    useAuth.ts                      # Auth composable with demo bypass
```

### Service Interfaces

Define contracts in `services/types.ts`:

```ts
export interface IHouseholdService {
  list(): Promise<Household[]>
  get(id: string): Promise<Household>
  create(data: CreateHouseholdRequest): Promise<Household>
  update(id: string, data: UpdateHouseholdRequest): Promise<Household>
  delete(id: string): Promise<void>
}

// Same pattern for IMemberService, IEventService, IPropertyService, etc.
```

### Service Factory

`composables/useService.ts`:

```ts
export function useHouseholdService(): IHouseholdService {
  const { demoMode } = useRuntimeConfig().public
  if (demoMode) {
    return useDemoHouseholdService()
  }
  return useApiHouseholdService()
}

// Same pattern for each service
```

Components always call `useHouseholdService()` -- they never know or care which implementation is active.

### localStorage Utilities

`services/demo/demoStorageUtils.ts` provides centralized helpers:

```ts
function getCollection<T>(key: string): T[]
function setCollection<T>(key: string, items: T[]): void
function generateId(): string       // crypto.randomUUID()
function getTimestamp(): string      // new Date().toISOString()
```

## Entity Limits

Defined in `services/demo/demoLimits.ts`:

```ts
export const DEMO_LIMITS = {
  tenants: 1,
  households: 3,
  membersPerHousehold: 6,
  events: 3,
  properties: 2,
  resourcesPerProperty: 4,
  mealPlansPerEvent: 10,
  choreTemplatesPerEvent: 5,
} as const

export type DemoLimitKey = keyof typeof DEMO_LIMITS
```

### Limit Enforcement

Each demo service checks limits before create operations:

```ts
function assertLimit(collection: unknown[], limit: number, entityName: string): void {
  if (collection.length >= limit) {
    throw new DemoLimitError(entityName, limit)
  }
}
```

`DemoLimitError` is a custom error class that the UI catches and displays a conversion-oriented message.

### Conversion Messaging

Create `composables/useDemoLimits.ts`:

```ts
export function useDemoLimits() {
  const { t } = useI18n()

  function handleLimitError(error: unknown): string | null {
    if (error instanceof DemoLimitError) {
      return t('demo.limitReached', {
        entity: t(`entity.${error.entityName}`),
        limit: error.limit,
      })
    }
    return null
  }

  function getRemainingCapacity(entityType: DemoLimitKey, currentCount: number): number {
    return Math.max(0, DEMO_LIMITS[entityType] - currentCount)
  }

  return { handleLimitError, getRemainingCapacity }
}
```

Locale keys (in `app/locales/en.json`):

```json
{
  "demo": {
    "bannerText": "You're using the Gatherstead Demo",
    "learnMore": "Learn More",
    "signUp": "Sign Up for Full Access",
    "resetDemo": "Reset Demo Data",
    "limitReached": "Demo limit reached: maximum {limit} {entity}. Sign up for unlimited access!",
    "restrictions": {
      "title": "Demo Restrictions",
      "noAuth": "No login required -- data is stored in your browser",
      "localStorage": "Data persists in this browser only and may be cleared by your browser",
      "limits": "Entity limits apply to encourage you to try the full version"
    }
  }
}
```

## Seed Data

`services/demo/demoSeedData.ts` pre-populates localStorage on first visit so the demo experience is not empty.

### Seed Content

| Entity | Seed Data |
|--------|-----------|
| Tenant | 1: "The Anderson Family" |
| Households | 2: "Anderson Main" (4 members), "Anderson-West" (3 members) |
| Members | 7 total: mix of adults/children with dietary profiles, relationships, contact methods |
| Property | 1: "Lake House" with 3 resources (Master Suite, Guest Room, RV Pad) |
| Event | 1: "Summer Reunion 2026" with meal plans and chore templates |
| Relationships | Parent/child/sibling/spouse links across members |
| Dietary Profiles | Varied: vegetarian, gluten-free, nut allergy, etc. |

### Initialization

The seed function checks for a `demo_initialized` flag in localStorage:
- **First visit**: Flag absent -- write all seed data, set flag
- **Return visit**: Flag present -- skip seeding, use existing data
- **Reset**: Clear all localStorage keys, re-run seeding

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

`app/components/DemoBanner.vue` -- a persistent bottom banner, always visible in demo mode.

### Design

- **Position**: Fixed to viewport bottom, full width
- **Z-index**: Above all page content, below modals
- **Color**: Amber/warning tone from the Nuxt UI color palette
- **Content padding**: Main app content gets bottom padding when banner is visible so content is not obscured

### States

**Collapsed (default):**
```
 You're using the Gatherstead Demo    [Learn More]  [Sign Up]
```

**Expanded / Modal (on "Learn More"):**

A `UModal` or slide-up panel listing:
- Demo restrictions (entity limits table)
- "Data is stored in your browser only"
- "No login required"
- Call-to-action: "Sign up for unlimited access"
- "Reset Demo Data" button

### Buttons

- **"Learn More"**: Opens the restrictions modal/panel
- **"Sign Up"**: Links to the production site's registration page
- **"Reset Demo"**: Clears all localStorage and re-seeds data

### Rendering

In [`app.vue`](../src/Gatherstead.Web/app/app.vue):

```html
<template>
  <div>
    <NuxtRouteAnnouncer />
    <NuxtPage />
    <DemoBanner v-if="runtimeConfig.public.demoMode" />
  </div>
</template>
```

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
| `src/Gatherstead.Web/app/app.vue` | Add `<DemoBanner>` conditional rendering |
| `src/Gatherstead.Web/app/services/types.ts` | **Create** -- Service interfaces |
| `src/Gatherstead.Web/app/services/api/*.ts` | **Create** -- Real API implementations |
| `src/Gatherstead.Web/app/services/demo/demoStorageUtils.ts` | **Create** -- localStorage helpers |
| `src/Gatherstead.Web/app/services/demo/demoLimits.ts` | **Create** -- Entity limit constants |
| `src/Gatherstead.Web/app/services/demo/demoSeedData.ts` | **Create** -- First-visit seed data |
| `src/Gatherstead.Web/app/services/demo/demo*Service.ts` | **Create** -- localStorage CRUD |
| `src/Gatherstead.Web/app/composables/useService.ts` | **Create** -- Service factory |
| `src/Gatherstead.Web/app/composables/useDemoLimits.ts` | **Create** -- Limit error handling |
| `src/Gatherstead.Web/app/composables/useAuth.ts` | **Create** -- Auth with demo bypass |
| `src/Gatherstead.Web/app/components/DemoBanner.vue` | **Create** -- Bottom banner |
| `infrastructure/modules/staticwebapp.bicep` | **Create** -- Static Web App module |
| `infrastructure/main.bicep` | Add conditional demo module |
| `.github/workflows/deploy-demo.yml` | **Create** -- Demo CI/CD workflow |
