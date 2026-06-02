# Trial-First Strategy for Gatherstead

## Context

Gatherstead needs a public evaluation surface that converts to paid use. This plan replaces the previous "restricted, free-forever" quota model with an **unrestricted, time-limited trial**: authenticated users can self-serve-create one 90-day trial tenant with full feature access. At the end of the trial the tenant becomes read-only until the owner upgrades to a paid subscription.

The companion static demo site (see `DEMO_SITE.md` / `DEMO_SITE_IMPROVEMENTS.md`) remains for zero-friction marketing evaluation; this plan handles actual product-led growth and conversion.

## Why Trial-First?

| Dimension | Static demo site | Trial-first (this plan) | Restricted free-forever |
|---|---|---|---|
| Signup friction | None (no auth) | Medium (sign-up + captcha) | Medium (sign-up + captcha) |
| Feature demonstration | Limited (no real collaboration) | Full | Degraded (quota walls everywhere) |
| Conversion path | Weak (data can't migrate) | Strong (upgrade in place, data preserved) | Moderate |
| Implementation scope | Smaller, Nuxt-only | Medium | Large (full quota service) |
| Parity maintenance | High (dual code path) | None | None |
| Analytics | None | Full per-account engagement | Full |
| Upgrade pressure | None | Time cliff creates urgency | Quota walls create friction |

The trial model gives users the full product experience while creating a clear, low-friction conversion moment at trial expiry.

## Implementation Plan

### 1. Domain & Data Model

**`src/Gatherstead.Data/Entities/Tenant.cs`** — add:
- `TrialStartedAt: DateTime?` — set when the tenant is created as a trial
- `TrialExpiresAt: DateTime?` — `TrialStartedAt + 90 days`; null means not a trial or already upgraded

**`src/Gatherstead.Data/Entities/User.cs`** — add:
- `EmailDomain: string` — stored on signup for cheap indexed blocklist lookup
- `TrialUsedAt: DateTime?` — set when the user's first trial tenant is created; prevents re-creation after deletion

EF Core migration for all of the above.

### 2. Self-Serve Signup & Tenant Creation

**New `src/Gatherstead.Api/Services/Users/UserProvisioningService.cs`**
On the first authenticated request from a new Entra subject, auto-create the internal `User` row from Entra claims (sub, email, name). Populate `EmailDomain`. This replaces the admin-provisioned user flow.

**`src/Gatherstead.Api/Services/Tenants/TenantService.cs`**
Remove the `[RequireAppAdmin]` gate from self-serve tenant creation. Replace with:
1. **Disposable-email block** — reject if `User.EmailDomain` is in the blocklist table.
2. **Captcha verification** — require a Cloudflare Turnstile token in the request body; verify server-side against Turnstile's siteverify endpoint.
3. **One-trial-per-user cap** — if `User.TrialUsedAt` is non-null AND the user has no active paid subscription, reject. Set `TrialUsedAt` on success. This prevents deleting a lapsed trial and immediately creating a new one.
4. Set `TrialStartedAt = UtcNow`, `TrialExpiresAt = UtcNow + 90 days`.

Keep a separate admin-only `POST /api/tenants/admin` endpoint that bypasses all of the above for internal provisioning.

**`src/Gatherstead.Api/Controllers/TenantsController.cs`**
Public endpoint with `[Authorize]` only (no `[RequireAppAdmin]`). `OwnerUserId` is no longer a request field — it is always the calling user.

### 3. Abuse Mitigations

| Mitigation | Choice | Rationale |
|---|---|---|
| Disposable-email blocklist | Static list from the [disposable-email-domains](https://github.com/disposable-email-domains/disposable-email-domains) feed, stored in a DB table refreshed by a scheduled job | Free, self-hostable, widely maintained |
| Captcha | **Cloudflare Turnstile** | Free, privacy-preserving, zero per-call cost; siteverify is a single HTTP call; secret stored in Key Vault |
| Rate limiting | Extend existing global rate limit with per-IP bucket on `POST /api/tenants`: 3 creations per IP per 24 hours | Cheap defense-in-depth |
| One-trial-per-email | `User.TrialUsedAt` server-enforced | Prevents delete-and-recreate farming |

Phone verification was explicitly not chosen — see Decisions section.

### 4. Trial Enforcement

**New `src/Gatherstead.Api/Authorization/WritableTenantRequirement.cs`**
A custom ASP.NET Core authorization requirement combining:
- `TenantRole >= Member` (existing)
- `Tenant.TrialExpiresAt == null || Tenant.TrialExpiresAt > UtcNow || Tenant has active paid subscription`

Applied as a policy at controller or middleware level so every write path inherits it. Read operations are always permitted (data preserved on lapse).

### 5. Idle-Tenant Reaper

**New `src/Gatherstead.Api/Services/Background/IdleTenantReaper.cs`** (`BackgroundService`)
Runs daily. Soft-deletes tenants where:
- Trial has expired (`TrialExpiresAt < UtcNow - 30 days`) AND no upgrade
- OR last activity > 90 days with no active subscription

Emails the owner before deletion (wiring to an email provider is a follow-up; log only for now).

### 6. UI Changes (Nuxt)

- **Signup page** (`app/pages/signup.vue`) — renders Cloudflare Turnstile widget; Turnstile token posted with tenant-creation request
- **Turnstile widget** (`app/components/TurnstileWidget.vue`) — thin wrapper around the Turnstile JS embed
- **Trial expiry banner** — shown on tenants where `TrialExpiresAt` is approaching or passed; drives upgrade CTA
- **Read-only banner** — shown on lapsed tenants for non-owner users; explains the tenant is read-only and who the owner is
- **`trial.expired` i18n key** — replaces the demo plan's `demo.limitReached` pattern for 402/403 responses from the API

### 7. Infrastructure

**`infrastructure/main.bicep`**
- Add Key Vault secret for Turnstile site secret
- Add param/output for the disposable-email-domains refresh function (can be in-process `IHostedService` initially; split to Azure Function later if needed)
- Remove the `staticwebapp.bicep` reference for the demo site (separate demo deployment is handled by `deploy-demo.yml` and is independent)

**Key Vault secrets**: `turnstile-secret`

### 8. Files Summary

| File | Action |
|---|---|
| `Gatherstead.Data/Entities/Tenant.cs` | Add `TrialStartedAt?`, `TrialExpiresAt?` |
| `Gatherstead.Data/Entities/User.cs` | Add `EmailDomain`, `TrialUsedAt?` |
| `Gatherstead.Data/Entities/DisposableEmailDomain.cs` | **Create** |
| EF Core migration | **Create** |
| `TenantService.cs` | Remove admin gate; add Turnstile check, disposable-email check, one-trial cap; set trial dates |
| `TenantsController.cs` | Public `[Authorize]` endpoint, drop `OwnerUserId` from request |
| `UserProvisioningService.cs` | **Create** — auto-provision on first request |
| `TurnstileVerifier.cs` | **Create** — Cloudflare Turnstile siteverify |
| `DisposableEmailCheck.cs` | **Create** — DB-backed blocklist lookup |
| `WritableTenantRequirement.cs` | **Create** — trial-expiry authorization policy |
| `IdleTenantReaper.cs` | **Create** — daily background cleanup |
| `app/pages/signup.vue` | **Create** |
| `app/components/TurnstileWidget.vue` | **Create** |
| `app/components/TrialBanner.vue` | **Create** |
| `app/locales/en.json` | Add `trial.*` i18n keys |
| `infrastructure/main.bicep` | Add Turnstile KV secret wiring |

## Verification

1. **Schema migration** — `dotnet ef database update` applies cleanly on a fresh DB.
2. **Unit tests**
   - `TenantService.CreateAsync` rejects: missing captcha, invalid captcha, disposable email, second trial after first lapsed.
   - `WritableTenantRequirement` denies writes on lapsed trial tenants but allows reads.
3. **Integration test sequence**: signup → captcha → tenant-create → trial active → manually expire trial → confirm read-only → confirm `WritableTenantRequirement` fires.
4. **UI walkthrough**: run `pnpm dev` + API locally, sign up, create a trial tenant, manually set `TrialExpiresAt` to past, confirm read-only banner appears and writes are rejected.

## Out of Scope (Future Work)

- Billing integration (Stripe / Azure Marketplace) for the paid-tier upgrade flow — `WritableTenantRequirement` already checks for an active paid subscription; the billing wiring is a follow-up.
- Email reminders before trial expiry — `IdleTenantReaper` logs only for now.
- Quota enforcement per tier — revisit if storage or compute costs become significant at scale.

---

## Decisions: Options Considered and Rejected

### Full quota enforcement (restricted free-forever)
*Rejected.* Quota walls degrade the feature demonstration precisely where the user is trying to evaluate collaborative functionality. Every new quota-enforced entity requires server-side quota service changes plus UI feedback changes. Trial expiry achieves the same abuse mitigation (accounts have a lifetime) with far less implementation scope and no feature degradation during evaluation.

### Phone verification
*Rejected.* ACS Verify (or any SMS provider) adds: per-SMS cost, a new Azure service dependency, an extra step that is genuinely high-friction for the target demographic (families, not necessarily tech-native users), and a support surface for users who don't receive the SMS. The marginal abuse reduction doesn't justify the cost and friction at current scale. Turnstile + one-trial-per-email + rate limiting covers the same threat model adequately.

### No-invites restriction on trial tenants
*Rejected.* The core value proposition of Gatherstead is multi-household coordination. A single-user trial cannot demonstrate that. Additionally, if a paid tenant lapses to trial, blocking invites would lock out existing members — a data-loss-adjacent experience. Read-only access for lapsed tenants is the appropriate downgrade behavior.

### Separate trial environment / trial database
*Rejected.* A separate environment (separate Azure SQL instance or schema prefix) eliminates trial spam from the prod DB but adds significant infrastructure complexity and makes the trial→paid migration harder. Trial data in Gatherstead is not precious — users are setting up household structure they can recreate in minutes. With the idle-tenant reaper and reasonable rate limits, trial clutter in prod is manageable.

### Keep the static demo site as the only evaluation surface
*Rejected.* The demo site cannot show real collaboration (single-browser, localStorage-only), produces no analytics, and cannot convert to a real account (data can't migrate). It remains useful as a zero-friction marketing-site first impression, but it's not a substitute for a trial account for users who want to actually use the product.
