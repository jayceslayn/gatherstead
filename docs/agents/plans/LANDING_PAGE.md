# Landing Page with Auth & Tenant Selection

## Context

The Nuxt 4 frontend (`src/Gatherstead.Web`) currently has only a placeholder `app.vue` with `NuxtWelcome`. The backend API is fully built with multi-tenant architecture and `GET /api/tenants`. This task bootstraps the frontend: a public landing page, Entra External ID / Azure AD B2C authentication via `nuxt-auth-utils`, and a tenant selection flow for authenticated users.

**Auth migration**: The backend currently uses PASETO tokens, but we're switching to standard JWT Bearer auth that validates Entra External ID / Azure AD B2C-issued tokens directly. This eliminates the need for a token exchange layer. The PASETO handler (`PasetoAuthenticationHandler`) will be replaced with `Microsoft.AspNetCore.Authentication.JwtBearer`. An implementation note will be added to revisit PASETO if/when it gains broader ecosystem support.

## Prerequisites

- **Entra External ID (or B2C) app registration**: An application must be registered with redirect URI `http://localhost:3000/auth/azure` for dev. For B2C tenants, the sign-up/sign-in user flow policy must exist; Entra External ID tenants use built-in flows.
- **Endpoint compatibility**: `nuxt-auth-utils` targets standard Entra ID. Entra External ID uses `ciamlogin.com` URLs and Azure AD B2C uses `b2clogin.com` URLs, so the OAuth handler will use custom endpoint configuration.

## Implementation Steps

### Phase A: Backend Auth Migration (PASETO → JWT)

#### A1. Replace PASETO auth with JWT Bearer
- Remove `PasetoAuthenticationHandler` and related classes from `src/Gatherstead.Api/Security/`
- Install `Microsoft.AspNetCore.Authentication.JwtBearer` if not already present
- Configure JWT Bearer auth in `Program.cs` to validate externally-issued tokens:
  - Authority: `https://{tenantName}.ciamlogin.com/{domain}/v2.0` (Entra External ID) or `https://{tenantName}.b2clogin.com/{domain}/{policy}/v2.0` (B2C)
  - Audience: Application client ID
  - Token validation: issuer, audience, lifetime, signing keys (auto-discovered via OIDC metadata)
- Update `ICurrentUserContext` to read claims from the standard JWT `ClaimsPrincipal` (`sub`, `emails`, `name`)
- Keep `RevokedTokens` table and `ITokenRevocationService` — token revocation is still valuable for logout/security events
- Add implementation note in code comments: "Consider PASETO migration when ecosystem support improves"

#### A2. Update API configuration
- Replace `Authentication:PublicKey` / `Authentication:Issuer` / `Authentication:Audience` config with:
  - `ExternalIdentity:Instance` (e.g., `https://gatherstead.ciamlogin.com` or `https://gathersteadb2c.b2clogin.com`)
  - `ExternalIdentity:Domain` (e.g., `gatherstead.onmicrosoft.com`)
  - `ExternalIdentity:ClientId`
  - `ExternalIdentity:SignUpSignInPolicyId` (empty for Entra External ID, policy name for B2C)
- Update `appsettings.json` and `appsettings.Development.json` with placeholder values

#### A3. Update integration tests
- Update `Gatherstead.Api.Tests` to generate JWT tokens instead of PASETO tokens for test auth
- Verify existing test scenarios still pass with JWT-based auth

### Phase B: Frontend Implementation

#### B1. Install packages
```bash
cd src/Gatherstead.Web
pnpm add nuxt-auth-utils @nuxtjs/i18n
```
i18n installed now because ARCHITECTURE.md mandates `$t()` for all UI text.

#### B2. Update `nuxt.config.ts`
- Add `'nuxt-auth-utils'` and `'@nuxtjs/i18n'` to modules
- Add `routeRules: { '/': { prerender: true }, '/tenants/**': { ssr: false } }`
- Add `i18n` config: `defaultLocale: 'en'`, `langDir: 'locales'`, `lazy: true`, `strategy: 'prefix_except_default'`
- Extend `runtimeConfig.public` with: `demoMode: false`, `githubUrl: ''`, `docsUrl: ''`
- Add server-only `externalIdentity` config block (clientId, clientSecret, tenantName, policy)

#### B3. Create locale file
**Create** `app/locales/en.json` with keys for: `common.*`, `landing.*`, `nav.*`, `tenant.*`, `error.*`

#### B4. Update `app/app.vue`
Replace `NuxtWelcome` with `NuxtLayout` + `NuxtPage`:
```vue
<template>
  <div>
    <NuxtRouteAnnouncer />
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
  </div>
</template>
```

#### B5. Create layouts

**`app/layouts/landing.vue`** - Public layout: header with logo + login/logout button, main slot, footer with GitHub/docs links from runtimeConfig. No sidebar.

**`app/layouts/default.vue`** - Authenticated layout: sidebar nav + main content area. Used by tenant pages.

#### B6. Create pages

**`app/pages/index.vue`** (landing layout, prerendered)
- Hero section: title, subtitle, description
- Feature cards (households, events, lodging, directory)
- GitHub + docs links from `runtimeConfig.public`
- Auth-conditional CTA: "Sign In" for guests, "Get Started" linking to `/tenants` for authenticated users

**`app/pages/tenants/index.vue`** (auth middleware)
- Fetches tenant list via `useTenants()` composable
- Checks `last_tenant_id` cookie — if found and valid, auto-redirects to `/tenants/:id`
- Otherwise shows tenant selection grid/list
- Handles empty state ("no tenants" message)

**`app/pages/tenants/[tenantId]/index.vue`** (auth middleware, default layout)
- Stub dashboard page
- Sets `last_tenant_id` cookie on mount
- Placeholder for future household/event overview

#### B7. Create middleware

**`app/middleware/auth.ts`** - Named middleware (not global)
- Checks `useUserSession().loggedIn`; redirects to `/` if not authenticated
- Bypasses check when `demoMode` is true (future demo site support)

#### B8. Create composables

**`app/composables/useAuth.ts`** - Thin wrapper around `nuxt-auth-utils`
- Returns `{ loggedIn, user, login, logout }`
- Demo mode branch returns mock authenticated user (per DEMO_SITE.md pattern)
- Production branch wraps `useUserSession()` with navigation helpers

**`app/composables/useTenants.ts`** - Tenant data fetching
- Uses `useAsyncData` to call `/api/proxy/tenants`
- Returns `{ tenants, pending, error, refresh }`
- Demo mode branch returns empty array (future: localStorage)
- Defines `TenantSummary` interface (`id`, `name`)

#### B9. Create server routes

**`server/routes/auth/azure.get.ts`** - Entra External ID / Azure AD B2C OAuth handler
- Uses `nuxt-auth-utils` OAuth primitives with custom identity provider endpoints
- Constructs provider-specific authorize/token URLs from runtimeConfig
- On success: stores user info + access token in session via `setUserSession()`
- Redirects to `/tenants` after login

**`server/routes/auth/logout.get.ts`** - Session cleanup
- Calls `clearUserSession()`, redirects to `/`

**`server/api/proxy/[...path].ts`** - API proxy (keeps tokens server-side)
- Reads access token from session, forwards requests to .NET API with `Authorization: Bearer` header
- Returns 401 if no session
- No token exchange needed — the .NET API validates the identity provider JWTs directly

## Files to Create/Modify

| File | Action |
|------|--------|
| **Backend (Phase A)** | |
| `src/Gatherstead.Api/Security/PasetoAuthenticationHandler.cs` | **Remove** |
| `src/Gatherstead.Api/Security/PasetoAuthenticationOptions.cs` | **Remove** |
| `src/Gatherstead.Api/Program.cs` | **Modify** — replace PASETO auth with JWT Bearer |
| `src/Gatherstead.Api/appsettings.json` | **Modify** — add ExternalIdentity config |
| `tests/Gatherstead.Api.Tests/**` | **Modify** — update test auth to use JWT |
| **Frontend (Phase B)** | |
| `src/Gatherstead.Web/nuxt.config.ts` | **Modify** |
| `src/Gatherstead.Web/app/app.vue` | **Modify** |
| `src/Gatherstead.Web/app/locales/en.json` | **Create** |
| `src/Gatherstead.Web/app/layouts/landing.vue` | **Create** |
| `src/Gatherstead.Web/app/layouts/default.vue` | **Create** |
| `src/Gatherstead.Web/app/pages/index.vue` | **Create** |
| `src/Gatherstead.Web/app/pages/tenants/index.vue` | **Create** |
| `src/Gatherstead.Web/app/pages/tenants/[tenantId]/index.vue` | **Create** |
| `src/Gatherstead.Web/app/middleware/auth.ts` | **Create** |
| `src/Gatherstead.Web/app/composables/useAuth.ts` | **Create** |
| `src/Gatherstead.Web/app/composables/useTenants.ts` | **Create** |
| `src/Gatherstead.Web/server/routes/auth/azure.get.ts` | **Create** |
| `src/Gatherstead.Web/server/routes/auth/logout.get.ts` | **Create** |
| `src/Gatherstead.Web/server/api/proxy/[...path].ts` | **Create** |

## Verification

1. **Backend**: `dotnet test` passes with JWT-based auth in tests
2. **Frontend**: `pnpm dev` — landing page renders at `localhost:3000` with i18n strings
3. Login button visible for unauthenticated users
4. `/tenants` redirects to `/` when not authenticated
5. OAuth flow redirects to identity provider (requires Entra External ID or B2C env vars)
6. After login, `/tenants` shows tenant list from API (JWT forwarded via proxy)
7. Selecting a tenant navigates to `/tenants/:id` and sets cookie
8. Revisiting `/tenants` auto-redirects to last accessed tenant
9. Logout clears session and returns to landing
10. `pnpm run build` passes TypeScript checks
