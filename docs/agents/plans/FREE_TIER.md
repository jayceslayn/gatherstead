# Free-Tier Strategy for Gatherstead (vs. Static Demo Site)

## Context

Gatherstead currently has no public evaluation surface. The existing plan in
[docs/agents/plans/DEMO_SITE.md](docs/agents/plans/DEMO_SITE.md) proposes a
separate static SPA with a localStorage-backed service layer so prospects can
try the UI with zero friction and zero backend cost.

We are reconsidering that direction in favor of a **free tier inside the main
app**: authenticated users (via Entra External ID) can self-serve-create and
own **one free-tier tenant** with tight quotas, while still being able to own
or join unlimited paid tenants. If a paid tenant reverts to free (e.g.
subscription lapse) the tenant becomes read-only until it returns to a paid
tier.

The main risk the user flagged is abuse: burner-email account farming that
creates many free tenants to degrade the service or inflate costs. The chosen
mitigation posture is: **disposable-email blocklist + captcha on signup + phone
verification at free-tenant creation**.

## Strategy Comparison

| Dimension | Static demo site | Free tier of main app |
|---|---|---|
| Code paths | Two (real + localStorage mock) | One |
| Feature drift risk | High — every feature needs a mock | None |
| Cost at rest | ~$0 (Static Web Apps free tier) | Scales with free users (SQL, compute, Entra MAU) |
| Top-of-funnel friction | Zero (no signup) | Medium (signup + captcha + phone verify) |
| Conversion path | Weak — data can't migrate; anonymous traffic | Strong — upgrade in place, data preserved |
| Analytics | None (anonymous) | Full per-account engagement signals |
| Demo of collaboration | Poor (single-browser) | Native (invite real users to a free tenant) |
| Abuse surface | None (no writes, no accounts) | Real — farming, storage bloat, MAU cost |
| Implementation scope | Smaller, Nuxt-only | Larger: domain + API + UI + infra + billing hooks |
| Reusability of the work | Disposable scaffolding | Quota + abuse infra reused by paid tiers forever |

**Net:** the free tier is decisively better for product-led growth and real
evaluation, at the cost of more engineering and a real abuse surface. The
static demo is cheaper and faster but pays a compounding dual-code-path tax
and converts poorly. **This plan commits to the free tier and deprecates the
static demo.**

## Free-Tier Implementation Plan

### 1. Domain & data model

Add a tier concept to tenants and a subscription record.

- [src/Gatherstead.Data/Entities/Enums.cs](src/Gatherstead.Data/Entities/Enums.cs) — add `TenantTier { Free, Paid }` and `SubscriptionStatus { Active, PastDue, Cancelled }`.
- [src/Gatherstead.Data/Entities/Tenant.cs](src/Gatherstead.Data/Entities/Tenant.cs) — add `Tier` (default `Free`), `SubscriptionId?`, and `LastActiveAt` (used by the reaper job).
- **New** `src/Gatherstead.Data/Entities/Subscription.cs` — `Id`, `TenantId`, `Status`, `CurrentPeriodEnd`, `ExternalSubscriptionId` (billing provider ref), audit fields. Billing-provider integration itself is out of scope for this plan — the schema is there to hang it off of.
- [src/Gatherstead.Data/Entities/User.cs](src/Gatherstead.Data/Entities/User.cs) — add `EmailDomain` (stored separately for cheap indexed lookup) and `PhoneVerifiedAt?`.
- EF Core migration for all of the above.

### 2. Quota infrastructure (server-side, authoritative)

Replace the demo plan's client-side `DEMO_LIMITS` with server-enforced
tier-scoped quotas.

- **New** `src/Gatherstead.Api/Services/Tenants/TenantQuotas.cs` — mirror the shape of [DEMO_LIMITS](docs/agents/plans/DEMO_SITE.md) but keyed by `TenantTier`:

  ```
  Free:  households=3, membersPerHousehold=6, events=3,
         properties=2, resourcesPerProperty=4,
         mealPlansPerEvent=10, choreTemplatesPerEvent=5
  Paid:  (effectively unlimited / far higher caps)
  ```
- **New** `IQuotaService` — `AssertCanCreate(tenantId, entityKind, ct)` that loads the tenant's tier and current count, throws `QuotaExceededException` on breach.
- Inject into each create path: `HouseholdService`, `MemberService`, `EventService`, `PropertyService`, `ResourceService`, `MealPlanService`, `ChoreTemplateService`. One line at the top of each `CreateAsync`.
- Custom exception surfaces as HTTP 402 Payment Required with `{ entity, limit, tier }` payload so the UI can render the conversion CTA.

### 3. Self-serve signup & tenant creation

Remove the admin-only gate from tenant creation and add the abuse mitigations.

- **New** `src/Gatherstead.Api/Services/Users/UserProvisioningService.cs` — on first authenticated request from a new Entra subject, auto-create the `User` row from Entra claims (sub, email, name). Populate `EmailDomain`.
- [src/Gatherstead.Api/Services/Tenants/TenantService.cs:134-139](src/Gatherstead.Api/Services/Tenants/TenantService.cs#L134-L139) — remove the `[RequireAppAdmin]` defense-in-depth check. Replace with:
  1. **Disposable-email block** — reject if `User.EmailDomain` is in the blocklist.
  2. **Captcha verification** — require a Cloudflare Turnstile token in the request; verify server-side against Turnstile's siteverify endpoint.
  3. **Phone verification required** — reject if `User.PhoneVerifiedAt` is null. (Triggers the phone-verify flow in the UI before tenant creation can proceed.)
  4. **One-free-tenant cap** — if the new tenant would be Free-tier, reject if the user already owns any Free-tier tenant (where they are `TenantRole.Owner`).
- [src/Gatherstead.Api/Controllers/TenantsController.cs](src/Gatherstead.Api/Controllers/TenantsController.cs) — remove `[RequireAppAdmin]`; require `[Authorize]` only. `OwnerUserId` is no longer a request field — it is always the calling user.
- Keep an admin-only `POST /api/tenants/admin` endpoint for internal provisioning that bypasses the abuse gates.

### 4. Abuse mitigations — concrete choices

| Mitigation | Choice | Rationale |
|---|---|---|
| Disposable-email blocklist | Static list from the [disposable-email-domains](https://github.com/disposable-email-domains/disposable-email-domains) maintained feed, refreshed via a scheduled job into a DB table. | Free, widely maintained, self-hostable. |
| Captcha | **Cloudflare Turnstile** | Free, privacy-preserving, no per-call cost; siteverify is a single HTTP call. Keep the secret in Key Vault. |
| Phone verification | **Azure Communication Services (ACS) Verify** | Keeps everything in Azure; billing already managed-identity-wired. Alternative: Entra External ID's built-in phone MFA (acceptable fallback, but less flexible for re-verification). |
| Rate limiting | Extend the existing global 100 rpm limit with a per-IP bucket specifically on `POST /api/tenants` (e.g. 5 creates per IP per hour). | Cheap, defense-in-depth. |
| Idle-tenant reaper | `BackgroundService` in-process, runs daily: flags Free tenants with `LastActiveAt < now - 90d`, emails owner; deletes (soft) after 30 more days of inactivity. | Caps long-tail storage cost with zero moderator effort. |

### 5. Revert-to-free authorization rule

When a Paid tenant's subscription expires, `Tenant.Tier` flips to `Free`. The
existing free-tenant owner rule would normally block this (the user may
already own another Free tenant). Handle this explicitly:

- The one-free-tenant cap applies to **creation**, not to downgrades. A user can temporarily own multiple Free tenants if they arrived there via downgrade.
- **New** authorization policy `WritableTenant` — a compound check in the existing authorization pipeline that combines `TenantRole >= Member` AND (`Tier == Paid` OR the action is a read). Applied at controller or middleware level so every write path inherits it.
- UI surfaces a banner and an upgrade CTA on downgraded tenants; reads still work fully (honoring the "data preserved" promise).

### 6. UI changes (Nuxt)

Reuse the conversion messaging from the demo plan, but point it at the real
tier/quota API responses instead of a client-side error class.

- Signup page renders Turnstile widget; captcha token posted with tenant-creation request.
- Phone-verify flow: dedicated page/modal that calls an `ACS Verify` start/complete endpoint pair; on success, `User.PhoneVerifiedAt` is set.
- Tenant create form: disabled until phone verification is done; shows a helpful state when blocked.
- **Reuse the conversion messaging already designed** in [DEMO_SITE.md:194-212](docs/agents/plans/DEMO_SITE.md#L194-L212) — lift the `demo.limitReached` i18n pattern into a tier-aware `tier.limitReached` key. Modal/toast triggered on HTTP 402 responses.
- Tier badge on tenant switcher ("Free" pill); read-only banner on downgraded tenants; "Upgrade" CTA wherever a quota is hit.

### 7. Infrastructure

- [infrastructure/main.bicep](infrastructure/main.bicep) — add params/outputs for ACS resource, Turnstile secret in Key Vault, disposable-email-domains refresh job (if we split it into an Azure Function rather than in-process). Delete the `staticwebapp.bicep` module proposed in DEMO_SITE.md — it is no longer needed.
- Key Vault secrets: `turnstile-secret`, `acs-connection-string`.
- Observability: new per-tenant metric `gatherstead_tenants_active{tier}` and an alert on `tenant_created_rate{tier="Free"}` exceeding baseline × 10 (early-warning for a farming incident).

### 8. Deprecate the static demo plan

- Move [docs/agents/plans/DEMO_SITE.md](docs/agents/plans/DEMO_SITE.md) to `docs/agents/plans/archive/` and add a top-of-file note: superseded by the free-tier strategy.
- Do **not** build `services/demo/*`, `useService` factory, `DemoBanner.vue`, or the `deploy-demo.yml` workflow. The free tier replaces them.

## Files Summary

| File | Action |
|---|---|
| [src/Gatherstead.Data/Entities/Enums.cs](src/Gatherstead.Data/Entities/Enums.cs) | Add `TenantTier`, `SubscriptionStatus` |
| [src/Gatherstead.Data/Entities/Tenant.cs](src/Gatherstead.Data/Entities/Tenant.cs) | Add `Tier`, `SubscriptionId?`, `LastActiveAt` |
| [src/Gatherstead.Data/Entities/User.cs](src/Gatherstead.Data/Entities/User.cs) | Add `EmailDomain`, `PhoneVerifiedAt?` |
| `src/Gatherstead.Data/Entities/Subscription.cs` | **Create** |
| `src/Gatherstead.Data/Entities/DisposableEmailDomain.cs` | **Create** |
| EF Core migration | **Create** |
| `src/Gatherstead.Api/Services/Tenants/TenantQuotas.cs` | **Create** — tier-scoped limits |
| `src/Gatherstead.Api/Services/Tenants/IQuotaService.cs` + impl | **Create** |
| [src/Gatherstead.Api/Services/Tenants/TenantService.cs](src/Gatherstead.Api/Services/Tenants/TenantService.cs) | Remove admin gate, add abuse gates + one-free-tenant cap |
| [src/Gatherstead.Api/Controllers/TenantsController.cs](src/Gatherstead.Api/Controllers/TenantsController.cs) | Public `[Authorize]` endpoint, drop `OwnerUserId` from request |
| `src/Gatherstead.Api/Services/Users/UserProvisioningService.cs` | **Create** — auto-provision on first request |
| `src/Gatherstead.Api/Services/Abuse/TurnstileVerifier.cs` | **Create** |
| `src/Gatherstead.Api/Services/Abuse/DisposableEmailCheck.cs` | **Create** |
| `src/Gatherstead.Api/Services/Abuse/PhoneVerificationService.cs` | **Create** — ACS Verify wrapper |
| `src/Gatherstead.Api/Services/Abuse/IdleTenantReaper.cs` | **Create** — `BackgroundService` |
| `src/Gatherstead.Api/Authorization/WritableTenantRequirement.cs` | **Create** — tier-aware write policy |
| Quota injection into each `*Service.CreateAsync` | Modify 7 services |
| [src/Gatherstead.Web/app/app.vue](src/Gatherstead.Web/app/app.vue) | Add tier badge + downgraded-banner slot |
| `src/Gatherstead.Web/app/pages/signup.vue` | **Create** |
| `src/Gatherstead.Web/app/pages/verify-phone.vue` | **Create** |
| `src/Gatherstead.Web/app/components/TurnstileWidget.vue` | **Create** |
| `src/Gatherstead.Web/app/components/QuotaReachedModal.vue` | **Create** (adapt demo plan's messaging) |
| `src/Gatherstead.Web/app/locales/en.json` | Add `tier.*` i18n keys |
| [infrastructure/main.bicep](infrastructure/main.bicep) | Add ACS + Turnstile KV secret wiring |
| `infrastructure/modules/communicationservices.bicep` | **Create** |
| [docs/agents/plans/DEMO_SITE.md](docs/agents/plans/DEMO_SITE.md) | **Move** to `archive/` with supersession note |

## Verification

1. **Schema migration** — `dotnet ef database update` applies cleanly on a fresh DB and on a copy of prod.
2. **Unit tests**
   - `TenantQuotas` returns correct caps per tier.
   - `QuotaService.AssertCanCreate` throws at the boundary, succeeds below it, for each entity kind.
   - `TenantService.CreateAsync` rejects: missing captcha, invalid captcha, disposable email, unverified phone, second-Free-tenant.
   - `WritableTenantRequirement` denies writes on Free-tier downgraded tenants but allows reads.
3. **Integration tests** (existing test project pattern) — spin up the API, issue a signup → phone-verify → tenant-create → quota-breach sequence end-to-end against SQLite or a container DB.
4. **Abuse simulation** — a small load-test script attempting 100 signups from a disposable domain confirms 100% rejection; the same from a real domain with captcha tokens completes successfully.
5. **UI walkthrough** — run `pnpm dev` + API locally, sign up as a new user, go through phone verify, create a Free tenant, hit the household cap, see the conversion modal, simulate a downgrade (manual DB tweak) and confirm read-only behavior.
6. **Observability check** — confirm the `tenants_active{tier}` metric appears in Application Insights and the farming-rate alert fires in a synthetic high-creation burst.

## Out of Scope (Future Work)

- Billing integration (Stripe / Azure Marketplace) for the paid-tier upgrade flow — schema is ready; plumbing is a follow-up.
- Email reminders for reaped tenants (wiring to an email provider).
- A recruiter-facing "guided tour" layer that could sit on top of the free tier for truly zero-friction showcase (revisit if free-tier signup friction proves too high).
