# Gatherstead Repository Quality Assessment

**Date:** 2026-06-17
**Commit:** e20caab (`feat: Refactor modals to use GsFormFooter for consistent footer layout and add delete functionality`)
**Scope:** High-level analysis of production readiness, security, code quality, and portfolio/showcase value.
**Status:** Post-MVP (active development)

---

## Executive Summary

Gatherstead has continued to grow since the April 2026 assessment. The codebase is now ~19,800 lines across 220 C# files organized into six projects (API, Data, Data.Setup, two test projects, and a `FlagsCodegen` codegen tool), plus a Nuxt 4 / Vue 3 frontend. All major domain contexts are implemented end-to-end — 27 controllers, 57 services, 100+ REST endpoints. Testing has deepened to 29 test classes across unit and integration suites, including the service-layer coverage flagged as the top gap in April. CI/CD now runs three workflows on every push (build/test, dependency audit, and a demo-site deploy). The frontend, skeletal in April (~19 files), has expanded into a substantial Vue 3 / Nuxt 4 application (163 Vue/TS files across 23 pages). The critical gaps from the original assessment (zero tests, no CI, missing security hardening) remain closed.

---

## Scorecard

| Dimension | Feb Score | Apr Score | Jun Score | Notes |
|---|---|---|---|---|
| **Architecture & Design** | 9/10 | 9/10 | 9/10 | Patterns hold at 27 controllers / 57 services; no architectural debt introduced |
| **Code Quality & Consistency** | 9/10 | 9/10 | 9/10 | Uniform patterns maintained across the expanded controller/service layer |
| **Security** | 8/10 | 9/10 | 9/10 | CORS, security headers, HSTS, JWT/Entra, token revocation all in place |
| **Documentation** | 8/10 | 8/10 | 8/10 | OBSERVABILITY.md and SECURITY-DEPS.md present; XML docs still missing |
| **Testing** | 1/10 | 7/10 | 8/10 | 29 test classes; service-layer coverage now exists alongside security/integration |
| **CI/CD & DevOps** | 1/10 | 8/10 | 8/10 | Build/test, dependency-audit, Codecov, plus demo-site deploy; no Dockerfile yet |
| **Production Readiness** | 3/10 | 7/10 | 8/10 | Tests, CI, security hardened, demo deploy live; missing containerization |
| **Portfolio/Showcase Value** | 5/10 | 9/10 | 10/10 | Full domain surface plus a substantial 23-page frontend — compelling showcase |

### **Overall Quality: 8.6 / 10** *(was 8.1 / 10)*

---

## What Changed Since April 2026

### Domain Surface: 20 → 27 Controllers

The domain expanded with reference-data and access-management contexts:

| Added Context | Controllers |
|---|---|
| **Reference Data** | AgeBands, DietaryTags, Equipment |
| **Access & Membership** | Invitations, HouseholdUsers, TenantUsers, Me |
| **Gathering Planning** | MealAttendance, Reports |

Service count grew correspondingly to 57. A user-provisioning / invitation flow now backs tenant and household membership.

### Testing: 16 → 29 Test Classes (Service Layer Now Covered)

April's top gap — no direct unit tests for the expanded service layer — is substantially closed. New service tests cover AccommodationIntentService, AgeBandService, EquipmentService, EventReportService, HouseholdUserService, InvitationService, MealPlanService, MealTemplateService, TaskPlanService, TenantUserService, and UserProvisioningService, plus `Gatherstead.Data.Tests/AgeBandsTests`.

### CI/CD: 2 → 3 Workflows

The demo deploy was initially added as a standalone `deploy-demo.yml` and subsequently integrated into the unified **`ci-cd.yml`** as the `deploy-demo` job (running after `deploy-api`, with the SWA token fetched at runtime via the CI managed identity instead of a stored secret). The current CI/CD surface is `ci-cd.yml` plus `dependency-audit.yml`.

### Tooling: `FlagsCodegen`

A new `tools/FlagsCodegen` project generates flag/enum code, bringing the solution to six projects (3 src, 2 test, 1 tool).

### Frontend: Skeletal → Substantial

The frontend grew from ~19 files / 3 routes to **163 Vue/TS files across 23 pages**. Recent work includes an attendance wizard for managing member attendance and meal preferences, event report components on a grid layout, accommodation stay-span handling with delete/confirmation flows, and a `GsFormFooter` refactor standardizing modal footers. This closes April's "backend depth vs. frontend breadth" gap.

---

## What Changed Since February 2026

### Domain Surface: 3 → 20 Controllers

All planned bounded contexts are now implemented:

| Context | Controllers |
|---|---|
| **Shared Foundation** | Tenants, SecurityEvents |
| **Family Directory** | Households, HouseholdMembers, Addresses, ContactMethods, DietaryProfiles, MemberAttributes, MemberRelationships |
| **Properties** | Properties, Accommodations, AccommodationIntents |
| **Gathering Planning** | Events, TaskTemplates, TaskPlans, TaskIntents, MealTemplates, MealPlans, MealIntents, EventAttendance |

Each controller follows the established pattern: `[Authorize]`, `[RequireTenantAccess]`, full CRUD, `CancellationToken` propagation, `BaseEntityResponse<T>` wrappers.

### Testing: 0 → 16 Test Classes

Two test projects were added:

**`Gatherstead.Api.Tests`** (15 classes):
- Integration: `AuthenticationPipelineTests`, `CorsTests`, `RateLimitingTests`, `SecurityHeadersTests`
- Security: `HttpContextCurrentTenantContextTests`, `HttpContextCurrentUserContextTests`, `HttpContextIncludeDeletedContextTests`, `JwtAuthenticationTests`, `RequireTenantAccessAttributeTests`
- Services: `MemberAuthorizationServiceTests`, `PlanSyncServiceTests`, `TokenRevocationServiceTests`
- Observability: `PiiRedactionActivityProcessorTests`, `PiiRedactionLogProcessorTests`
- Data: `AuditingSaveChangesInterceptorTenantValidationTests`

**`Gatherstead.Data.Tests`** (1 class):
- Planning: `PlanGeneratorTests`

Framework: xUnit v3, Moq, `Microsoft.AspNetCore.Mvc.Testing`, SQLite (in-memory).

### CI/CD: 0 → 2 GitHub Actions Workflows

- **`ci-cd.yml`**: Runs on every push and PR to main. Builds backend (dotnet) and frontend (pnpm/Node 24) in parallel; executes all tests; collects Cobertura coverage via `coverlet.collector`; generates a Markdown summary written to the Actions job summary; uploads a full HTML report as a build artifact; uploads merged coverage to Codecov. On push to `main`, deploys in sequence: migrations → setup → api → web + demo (in parallel).
- **`dependency-audit.yml`**: NuGet locked restore + `--vulnerable` check; pnpm audit at `high` level; GitHub dependency-review action on PRs.

### Security Hardening: All Prior Gaps Addressed

- **CORS**: Configurable allowlist via `Cors:AllowedOrigins` in appsettings — no longer relying on framework defaults.
- **Security headers**: Inline middleware emits `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Content-Security-Policy: default-src 'none'`, `Referrer-Policy: no-referrer`, `Permissions-Policy`.
- **HSTS**: 365-day max-age, subdomains included.
- **Swagger**: Development environment only.
- **Rate limiting**: Fixed-window values are now configurable via appsettings.
- **Sync-over-async**: Eliminated — no `.GetAwaiter().GetResult()` anywhere in the codebase.

### Authentication: PASETO → JWT (Azure Entra External ID)

The custom `PasetoAuthenticationHandler` was replaced with standard `JwtBearer` configured against Azure Entra External ID. This trades the marginal cryptographic advantage of PASETO for significantly lower operational complexity, better ecosystem support, and alignment with Azure-first strategy. Token revocation and security event logging are preserved.

### Frontend: Nuxt 4 / Vue 3 Added

A frontend project exists at `src/Gatherstead.Web/` with:
- Nuxt 4 + Vue 3 + Pinia + Tailwind CSS + @nuxt/ui
- FullCalendar Vue3 for event scheduling views
- i18n support via `@nuxtjs/i18n`
- Auth middleware and composables (`useAuth`, `useTenants`, `useApiError`)
- Route structure: landing, tenant dashboard, tenant detail
- CI integration: `pnpm build` runs on every push

---

## What's Still Done Well (Unchanged)

The patterns documented in the February assessment remain intact at 4× the code volume — a meaningful signal that the architecture scales:

- Defense-in-depth tenant isolation (auth attribute + service validation + EF global query filters)
- `AuditingSaveChangesInterceptor` now includes cross-tenant write validation that throws on violation
- Soft-delete with temporal audit trail
- `DeleteBehavior.Restrict` on all foreign keys
- Constructor injection + `ArgumentNullException` guards
- `AsNoTracking()` for reads, `CancellationToken` throughout
- Static `Expression<Func<>>` DTO mapping fields
- Records for response DTOs, `init` properties with trimming on request DTOs

---

## Remaining Gaps

### 1. No Dockerfile / Containerization

No `Dockerfile` or `docker-compose.yml` exists. The deployment guide describes Azure App Service deployment but a multi-stage Dockerfile would demonstrate containerization awareness and simplify local development parity.

**Impact:** Low for showcase value given Azure App Service is a reasonable production target; medium for developer experience.

### 2. Broaden Service-Layer Test Coverage

**Substantially addressed since April.** Direct unit tests now exist across the service layer (AccommodationIntent, AgeBand, Equipment, EventReport, HouseholdUser, Invitation, MealPlan, MealTemplate, TaskPlan, TenantUser, UserProvisioning) in addition to the security and core services. Coverage is measured and reported via Codecov on every CI run. Remaining work is breadth — several services added during domain expansion still lack direct tests — rather than the wholesale absence flagged in April.

**Impact:** Low/Medium — the infrastructure and a representative cross-section are in place; the remaining gap is incremental.

### 3. Frontend Polish

**Substantially addressed since April.** The frontend grew from ~19 files / 3 routes to 163 Vue/TS files across 23 pages, covering attendance wizards, event reports, accommodation management, and standardized modal flows. It is no longer skeletal relative to the backend. The remaining opportunity is polish and end-to-end coverage of the deeper domain flows rather than basic surface area.

**Impact:** Low — the breadth gap that set expectations in April is closed.

### 4. No XML Documentation / Swagger Enrichment

Public API methods have no `<summary>` XML docs, so Swagger UI shows raw parameter/type information without descriptions. This limits the Swagger page as API documentation.

**Impact:** Low for function; moderate for first-impression quality.

### 5. No CONTRIBUTING.md

**Impact:** Low — primarily signals open-source readiness.

---

## Suggested Improvements (Priority Order)

### Priority 1: Dockerfile

**Effort:** 30-60 minutes

Multi-stage build:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Gatherstead.Api -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Gatherstead.Api.dll"]
```

Optional: `docker-compose.yml` with API + SQL Server for local dev.

### Priority 2: Broaden Service-Layer Test Coverage

**Effort:** 1-2 days

The core and security services plus a representative cross-section of domain services are now tested. Fill the remaining gaps Codecov reveals — services added during domain expansion that still lack direct tests. Target 70%+ line coverage overall, 100% for tenant isolation and RBAC paths.

### Priority 3: XML Documentation on Controllers

**Effort:** 2-4 hours

Add `<summary>` docs to controller actions and key request/response DTOs. Swagger will render these as inline descriptions, making the API self-documenting for reviewers.

### Priority 4: CONTRIBUTING.md

**Effort:** 30 minutes

Standard contributing guide: local setup, branch naming, PR process. Signals open-source readiness.

---

## Portfolio/Showcase Assessment

### Current State: Compelling

**What a hiring manager sees today:**
- Full-domain REST API with 100+ endpoints across 27 controllers / 57 services
- Modern security posture (JWT/Entra, RBAC, CORS, security headers, rate limiting, token revocation)
- 29 test classes spanning security, integration, observability, and the service layer
- GitHub Actions running build/test, dependency audits, and a demo-site deploy on every push
- Substantial Vue 3 / Nuxt 4 frontend — 23 pages, 163 files — with CI coverage
- Azure-first architecture with Key Vault integration
- ~19,800 lines of consistent, clean C# following the same patterns throughout

**What would push it to a clean 10/10 across the board:**
- Dockerfile / containerization awareness
- A meaningful Codecov percentage driven up by broadening service-layer coverage
- XML/Swagger enrichment so the API is self-documenting

The hardest parts — architecture, consistency, and now a full-stack surface — are the strongest aspects and scale to a production-representative codebase size.
