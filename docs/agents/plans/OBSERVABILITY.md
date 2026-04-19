# Observability & Logging Plan — Gatherstead

## Context

Gatherstead today ships with essentially no observability: the API has default `ILogger` with two call sites (JWT failure, rate-limit rejection), no Application Insights or OpenTelemetry, no correlation IDs, and no exception middleware. The Nuxt frontend has no error tracking. The Bicep infra provisions no Log Analytics workspace or App Insights resource. Yet [DESIGN_PRINCIPLES.md](../../DESIGN_PRINCIPLES.md) calls for "monitoring for anomalous access patterns, failed logins, and data exfiltration signals" and [ARCHITECTURE.md](../../ARCHITECTURE.md) mandates "App Insights/Log Analytics hooks, structured logs, and dashboards/alerts for auth failures, data-access anomalies, and PII access patterns."

This plan closes that gap with a three-tier observability stack:

1. **Azure Monitor OpenTelemetry** for ephemeral diagnostics (traces, metrics, logs) across API + Web.
2. **DB-persisted SecurityEvent table** for long-retention, RBAC-queryable audit events that must outlive App Insights retention.
3. **Marketing attribution on User/Tenant** for signup-source analytics (UTM + "via" tags).

The stack serves three distinct consumers: **developers** troubleshooting bugs, **operators** responding to incidents, and **product/growth** mining usage patterns.

## Architecture at a glance

| Signal | Sink | Retention | Query surface |
|---|---|---|---|
| Request/dep traces, metrics, logs (ephemeral) | App Insights via Azure Monitor OTel | 90 days (default) | KQL in Log Analytics |
| Unhandled exceptions | App Insights | 90 days | KQL |
| Security events (auth fail, authz deny, PII read, cross-tenant attempt, soft-delete/restore) | SQL `SecurityEvent` table **and** App Insights custom event (mirrored) | DB: indefinite; AI: 90d | Admin API (RBAC) + KQL |
| Signup attribution (UTM / Referer / via) | `User.Attribution*` + `Tenant.Attribution*` columns | Indefinite | EF queries, admin reports |
| Frontend pageviews, JS errors, custom events | App Insights JS SDK (separate cloud role) | 90 days | KQL |
| Audit metadata on entities (who/when) | Existing audit columns + SQL temporal history | Temporal: 1 year (already configured) | EF + `FOR SYSTEM_TIME` |

Azure Monitor OpenTelemetry (`Azure.Monitor.OpenTelemetry.AspNetCore`) is the chosen SDK — it wraps OTel auto-instrumentation and exports to App Insights via Managed Identity, matching the existing deployment posture.

## 1. Infrastructure (Bicep)

**New module: [infrastructure/modules/observability.bicep](../../../infrastructure/modules/)**

Provisions:
- `Microsoft.OperationalInsights/workspaces` — Log Analytics workspace (PerGB2018, 30-day retention on dev, 90-day on prod).
- `Microsoft.Insights/components` — Application Insights (workspace-based), one instance per environment. Cloud roles distinguished at SDK config time: `gatherstead-api`, `gatherstead-web`, `gatherstead-demo`.
- Diagnostic settings on the API + Web App Service resources piping `AppServiceHTTPLogs`, `AppServiceConsoleLogs`, `AppServiceAppLogs`, `AppServiceAuditLogs` to the workspace.
- Diagnostic settings on SQL DB (SQLSecurityAuditEvents, SQLInsights) and Key Vault (AuditEvent) to the same workspace.
- Action Group `ag-gatherstead-oncall` with email recipient.
- Starter alert rules (see §9).

**Wire into [main.bicep](../../../infrastructure/main.bicep):**
Add an `observability` module **before** `appservice` so the App Insights connection string can be injected as an app setting. Pass the connection string to [appservice.bicep](../../../infrastructure/modules/appservice.bicep) as a new param and set `APPLICATIONINSIGHTS_CONNECTION_STRING` on both API and Web apps (Web gets it too so SSR pages can forward it to the client via runtime config).

RBAC: grant the app's user-assigned managed identity the **Monitoring Metrics Publisher** role on the App Insights component so the OTel exporter authenticates without keys.

## 2. Backend: Azure Monitor OpenTelemetry wiring

**Packages** (add to [Gatherstead.Api.csproj](../../../src/Gatherstead.Api/Gatherstead.Api.csproj)):
- `Azure.Monitor.OpenTelemetry.AspNetCore` — includes SqlClient and ASP.NET Core auto-instrumentation; no separate SqlClient package needed.
- `Azure.Identity` — for `DefaultAzureCredential`.
- `OpenTelemetry.Instrumentation.EntityFrameworkCore 1.15.0-beta.1` — EF Core query spans (prerelease accepted).

**New file: `src/Gatherstead.Api/Observability/TelemetryExtensions.cs`**

Single `AddGathersteadTelemetry(this IServiceCollection, IConfiguration)` extension that:
- Calls `services.AddOpenTelemetry().UseAzureMonitor(o => { o.Credential = new DefaultAzureCredential(); })` — managed-identity auth, same pattern as SQL/KV.
- Registers a custom `ActivitySource` named `Gatherstead.Api` for service-layer spans.
- Registers a custom `Meter` named `Gatherstead.Api` for business metrics (see §4).
- Sets the cloud role name via `ResourceBuilder.AddService("gatherstead-api")`.
- Skips setup when `APPLICATIONINSIGHTS_CONNECTION_STRING` is absent (clean dev-machine experience).
- Registers the PII redaction processor (see §5).
- Adds EF Core instrumentation via `AddEntityFrameworkCoreInstrumentation()` (prerelease package accepted; `SetDbStatementForText` defaults to `false`, and `PiiRedactionActivityProcessor` strips `db.statement` as a second layer).

Call this once from [Program.cs](../../../src/Gatherstead.Api/Program.cs) right after `var builder = ...`.

**Exception middleware** — new `ExceptionLoggingMiddleware` inserted **first** in the pipeline (before CORS). Catches unhandled exceptions, logs with `ILogger` (auto-captured by OTel), sets the current `Activity` status to error, returns a correlation-id-bearing RFC 7807 ProblemDetails body.

**Correlation ID middleware** — ASP.NET Core already parses W3C `traceparent`; add middleware that also:
- Emits `X-Correlation-Id` response header sourced from `Activity.Current.TraceId`.
- Enriches `Activity.Current` with baggage: `tenant.id`, `user.id`, `app_admin` (resolved lazily via the existing `ICurrentTenantContext` / `ICurrentUserContext` after auth runs).

**ILogger rollout** — inject `ILogger<T>` into every service in `src/Gatherstead.Api/Services/**` and log at key decision points: authorization decisions in `MemberAuthorizationService`, tenant-access denials in `RequireTenantAccessAttribute`, cross-tenant write attempts in [AuditingSaveChangesInterceptor.cs](../../../src/Gatherstead.Data/Interceptors/AuditingSaveChangesInterceptor.cs). Logs use structured properties — never interpolated strings.

## 3. Security event persistence

**New entity: `src/Gatherstead.Data/Entities/SecurityEvent.cs`**

```
Id              Guid       PK
OccurredAt      datetime2  indexed desc
EventType       enum       AuthFailure, AuthzDenial, CrossTenantWriteBlocked,
                           TokenRevoked, PiiFieldRead, SoftDelete, Restore,
                           AppAdminAction, RateLimitBreach
Severity        enum       Info, Warning, Critical
TenantId        Guid?      nullable (pre-auth events), indexed
UserId          Guid?      nullable, indexed
CorrelationId   Guid       indexed (maps to OTel trace_id)
IpAddressHash   binary(32) SHA-256 of IP + daily salt (no raw IP)
UserAgentHash   binary(32)
Resource        string     e.g. "HouseholdMember:{id}"
Detail          nvarchar(max) JSON — IDs only, no PII
```

Composite index `(TenantId, OccurredAt DESC)` for tenant admin queries; `(EventType, OccurredAt DESC)` for global triage.

**New service: `ISecurityEventLogger`** — fire-and-forget async writer. Events written to both the DB table **and** emitted as an OTel event on the current `Activity` so live dashboards stay in sync. Failures to write to DB degrade gracefully (log error, don't fail the request) — App Insights is the fallback record.

**Call sites** (add on this PR):
- `OnAuthenticationFailed` in [Program.cs](../../../src/Gatherstead.Api/Program.cs) (lines 89-100) — replace `LogWarning` with `ISecurityEventLogger` call.
- Rate-limit `OnRejected` in [Program.cs](../../../src/Gatherstead.Api/Program.cs) (lines 156-172).
- `AuditingSaveChangesInterceptor` cross-tenant write throw site.
- `RequireTenantAccessAttribute` denial branch.
- `RequireAppAdminAttribute` denial branch.
- `MemberAuthorizationService.CanEditMemberAsync` denial path.
- `TokenRevocationService` on first observation of a revoked jti.

**No PII read events** in v1 — defer until we have a concrete list of "sensitive reads" worth flagging; adding it blindly to every query is noisy.

**Admin query API**: `GET /api/tenants/{tenantId}/security-events` (RBAC: tenant Owner/Manager) and `GET /api/admin/security-events` (RBAC: App Admin) with filtering by type/severity/date. Reuse `RequireTenantAccessAttribute` and `RequireAppAdminAttribute`.

## 4. Business metrics

Custom `Meter` counters (non-PII, all tagged with `tenant_id`):
- `gatherstead.tenant.created`
- `gatherstead.household.created`
- `gatherstead.member.created`
- `gatherstead.event.created`
- `gatherstead.authz.denied` (tag: `reason`)
- `gatherstead.authn.failed` (tag: `reason`)
- `gatherstead.soft_delete` (tag: `entity_type`)
- `gatherstead.security_event` (tag: `event_type`, `severity`)

EF Core and ASP.NET Core auto-instrumentation already provides: request duration, DB query duration, dependency calls, exception counts.

## 5. PII redaction (strict posture)

New `PiiRedactionLogProcessor : BaseProcessor<LogRecord>` invoked before export:
- Allowlist of safe property names: `tenant_id`, `user_id`, `member_id`, `household_id`, `event_id`, `correlation_id`, `trace_id`, `span_id`, `http.*`, `db.*` (minus statement params), `severity`, `event_type`, `reason`, `role`.
- Everything else → redacted to `[redacted]` rather than dropped, so the shape of the log is preserved for debugging.
- Companion `PiiRedactionActivityProcessor` for spans applies the same rule to tags and removes `db.statement` contents (keeps the operation name).

**Logging contract doc**: add a short `docs/OBSERVABILITY.md` (new) listing the allowlist and the rule "if you need to log an entity, log its ID — never its fields." Code reviewers enforce.

Tests in `Gatherstead.Api.Tests`: feed a log record with `email` / `dietaryNote` / `birthDate` properties and assert they are redacted.

## 6. Frontend: App Insights JS + attribution capture

**Package** (add to [package.json](../../../src/Gatherstead.Web/package.json)):
- `@microsoft/applicationinsights-web`
- `@microsoft/applicationinsights-clickanalytics-js` (optional, defer)

**New file: `src/Gatherstead.Web/app/plugins/appInsights.client.ts`**

Nuxt client plugin that:
- Reads `runtimeConfig.public.appInsightsConnectionString` (injected from App Service app setting via `nuxt.config.ts`).
- Initializes App Insights with `autoTrackPageVisitTime: true`, `enableAutoRouteTracking: true`, `disableCookiesUsage: false` (first-party only), `disableFetchTracking: false`.
- Sets cloud role `gatherstead-web` (or `gatherstead-demo` when `NUXT_PUBLIC_DEMO_MODE=true`).
- Adds a telemetry initializer that:
  - Sets `ai.user.authenticatedId` = hashed internal `User.Id` once auth resolves (never the raw Entra `sub`).
  - Adds custom dimensions `tenant.id` and `app_admin` from the auth session.
  - Propagates the W3C `traceparent` on outgoing `$fetch` calls (done automatically by the AI fetch instrumentation).

**Correlation to backend** — relies on W3C traceparent header. Backend enriches the incoming `Activity` with `tenant.id`, so a frontend page view and its downstream API calls share a single trace in App Insights' end-to-end view.

**Error tracking** — register `vueApp.config.errorHandler` and `window.onerror` handlers that forward to `appInsights.trackException`. The existing [useApiError.ts](../../../src/Gatherstead.Web/app/composables/useApiError.ts) composable gains a side effect that calls `trackException` with the API error code + correlation ID from the response header.

**UTM / "via" attribution capture** — new client plugin `src/Gatherstead.Web/app/plugins/attribution.client.ts` that runs on every page load:
- On first landing per session (sessionStorage miss), read `utm_source`, `utm_medium`, `utm_campaign`, `utm_term`, `utm_content`, `via`, and `document.referrer`.
- Store the snapshot in **localStorage key `gatherstead.attribution`** with a TTL of 30 days.
- On signup flow completion, post the snapshot to the backend (see §7).
- Fire App Insights custom event `landing` with the snapshot for funnel analysis in App Insights too.

## 7. Marketing attribution persistence

**Schema additions** (EF Core migration `AddMarketingAttribution`):

On `User`:
- `AttributionSource`, `AttributionMedium`, `AttributionCampaign`, `AttributionTerm`, `AttributionContent`, `AttributionVia`, `AttributionReferer` — all `nvarchar(256)` nullable.
- `AttributionCapturedAt datetime2` nullable.

On `Tenant`: same columns (captured at tenant creation, which may be different from user creation since App Admins create tenants on behalf of Owners).

**Capture points:**
- User record: populated the first time an authenticated user record is upserted from an Entra `sub` claim in `HttpContextCurrentUserContext`. The Nuxt plugin POSTs the attribution snapshot to a new `POST /api/users/me/attribution` endpoint on first login. Idempotent — only writes if columns are null.
- Tenant record: captured in `TenantService.CreateAsync` from an `attribution` object in the request body (submitted by the Owner's client on creation).

Strings are **truncated to 256 chars** and stripped of control characters before persistence. No server-side interpretation — the columns are opaque strings queried later by analytics.

## 8. Demo site telemetry

The demo Static Web App ([DEMO_SITE.md](./DEMO_SITE.md), currently planned) uses the same frontend bundle. The App Insights plugin reads `NUXT_PUBLIC_DEMO_MODE` and sets cloud role `gatherstead-demo` so demo traffic is filterable and doesn't pollute production funnels. The demo has no backend to persist attribution, so attribution stays in localStorage only and is emitted as a `landing` custom event for App Insights. A `demo_signup_click` custom event fires when the demo CTA to the real app is clicked — this is the primary demo→prod conversion metric.

## 9. Dashboards & alerts

**Dashboards** (provisioned as JSON in `infrastructure/dashboards/` — optional follow-up; v1 can be built by hand):
- **Ops health**: request rate, 5xx rate, p95 latency, EF query duration, dependency failure rate.
- **Auth / security**: auth failures over time, authz denials by reason, revoked-token hits, rate-limit breaches by IP hash.
- **Usage**: new tenants, new members, events created (weekly). Broken down by `AttributionSource`.

**Alert rules** (in Bicep):
| Rule | Condition | Severity |
|---|---|---|
| API 5xx spike | 5xx rate > 2% over 5m | Sev 2 |
| Auth failure burst | `gatherstead.authn.failed` > 50/5m for one IP hash | Sev 3 |
| Cross-tenant write blocked | any `CrossTenantWriteBlocked` in 5m | Sev 1 |
| Dependency down | SQL dependency failure rate > 10% over 5m | Sev 1 |
| App Insights ingestion cap hit | daily cap metric | Sev 3 |

## 10. Rollout phases

1. ✅ **Infra first**: `observability.bicep` provisions Log Analytics workspace + workspace-based App Insights + diagnostic settings (App Service, SQL, Key Vault) + action group + starter alert rules. `main.bicep` wires the module and passes `appInsightsConnectionString` down to `appservice.bicep`. Managed-identity `Monitoring Metrics Publisher` RBAC granted. No deployment yet — project not live.

2. ✅ **Backend OTel + exception middleware + correlation ID**: `Azure.Monitor.OpenTelemetry.AspNetCore 1.4.0` + `Azure.Identity 1.21.0` + `OpenTelemetry.Instrumentation.EntityFrameworkCore 1.15.0-beta.1` added. `GathersteadTelemetry` (static `ActivitySource` + `Meter`), `TelemetryExtensions.AddGathersteadTelemetry` (includes `AddEntityFrameworkCoreInstrumentation()`), `ExceptionLoggingMiddleware` (first in pipeline, RFC 7807 response with correlation ID), and `CorrelationEnrichmentMiddleware` (after auth, sets `X-Correlation-Id` header + `tenant.id` / `user.id` activity tags) all implemented. `ILogger<T>` wired into `MemberAuthorizationService`, `RequireTenantAccessAttribute`, `RequireAppAdminAttribute`, `AuditingSaveChangesInterceptor`.

3. ✅ **PII redaction processor + tests**: `PiiRedactionLogProcessor` (explicit allowlist, lazy redacted-list allocation, clears `FormattedMessage` on any redaction) and `PiiRedactionActivityProcessor` (always strips `db.statement` / `db.query.text`) implemented. `OBSERVABILITY.md` logging contract document written. 15 unit tests covering all allowlisted keys, PII keys (Email, BirthDate, DietaryNote), mixed payloads, FormattedMessage preservation/clearing, and zero-attribute no-throw guard.

4. ✅ **SecurityEvent entity + ISecurityEventLogger + call sites + admin API**: `SecurityEvent` entity (append-only, excluded from temporal tables, 5 indexes), `SecurityEventType` and `SecurityEventSeverity` enums, `ISecurityEventLogger` / `SecurityEventLogger` (OTel Activity event first, then DB write with graceful failure), `SecurityEventsController` (`GET /api/tenants/{id}/security-events` Manager+, `GET /api/admin/security-events` App Admin). `AuditingSaveChangesInterceptor` patched to lazy-evaluate `UserId` so pre-auth SecurityEvent writes don't throw. Wired at: `OnAuthenticationFailed`, `OnTokenValidated` (revoked token), rate-limit `OnRejected`, `RequireTenantAccessAttribute` (both denial branches), `RequireAppAdminAttribute`, `MemberAuthorizationService.CanEditMemberAsync` (both denial paths). EF migration not yet created — project not deployed.

5. ✅ **Business metrics**: `GathersteadMetrics` static class with 7 `Counter<long>` instruments on `GathersteadTelemetry.Meter`. Wired: `gatherstead.tenant.created` (TenantService), `gatherstead.household.created` (HouseholdService, tag: `tenant.id`), `gatherstead.member.created` (HouseholdMemberService, tags: `tenant.id` + `household.id`), `gatherstead.authz.denied` (3 sites, tag: `reason`), `gatherstead.authn.failed` (Program.cs, tag: `reason`), `gatherstead.soft_delete` (all 8 service delete methods, tags: `entity_type` + `tenant.id`), `gatherstead.security_event` (SecurityEventLogger, tags: `event_type` + `severity`). `gatherstead.event.created` omitted — no EventService exists yet.

6. ⬜ **Frontend App Insights plugin + error handler**: `@microsoft/applicationinsights-web` package, `appInsights.client.ts` Nuxt plugin (hashed `ai.user.authenticatedId`, `tenant.id` dimension, Vue/window error handlers), `useApiError.ts` `trackException` side effect.

7. ⬜ **Attribution plugin + User/Tenant schema + API endpoint**: `attribution.client.ts` plugin (localStorage TTL snapshot on first landing), `AttributionSource/Medium/Campaign/Term/Content/Via/Referer/CapturedAt` columns on `User` and `Tenant`, `POST /api/users/me/attribution` (idempotent, first-write-wins), EF migration `AddMarketingAttribution`.

8. ⬜ **Dashboards + alert rules**: additional Bicep alert rules for `gatherstead.authn.failed` burst and `CrossTenantWriteBlocked`; dashboard JSON in `infrastructure/dashboards/`.

9. ⬜ **Demo site instrumentation** (concurrent with DEMO_SITE.md rollout): `NUXT_PUBLIC_DEMO_MODE` cloud-role switch to `gatherstead-demo`, attribution stays localStorage-only, `demo_signup_click` custom event.

Each phase is a separate PR.

## 11. Critical files

Modify:
- [src/Gatherstead.Api/Program.cs](../../../src/Gatherstead.Api/Program.cs) — add telemetry, middleware, replace two existing log calls.
- [src/Gatherstead.Api/Gatherstead.Api.csproj](../../../src/Gatherstead.Api/Gatherstead.Api.csproj) — OTel packages.
- [src/Gatherstead.Data/Interceptors/AuditingSaveChangesInterceptor.cs](../../../src/Gatherstead.Data/Interceptors/AuditingSaveChangesInterceptor.cs) — log cross-tenant attempt.
- `src/Gatherstead.Api/Security/RequireTenantAccessAttribute.cs` and `RequireAppAdminAttribute.cs` — log denials.
- `src/Gatherstead.Api/Services/Authorization/MemberAuthorizationService.cs` — log denials.
- `src/Gatherstead.Api/Services/TokenRevocationService.cs` — log revocation hits.
- `src/Gatherstead.Api/Services/Tenants/TenantService.cs` — capture attribution on create.
- `src/Gatherstead.Api/Security/HttpContextCurrentUserContext.cs` — capture attribution on first user upsert.
- [src/Gatherstead.Data/](../../../src/Gatherstead.Data/) `GathersteadDbContext.cs` — DbSet for SecurityEvent, Attribution columns.
- [src/Gatherstead.Web/](../../../src/Gatherstead.Web/) `nuxt.config.ts` — runtime config for AI connection string.
- [src/Gatherstead.Web/package.json](../../../src/Gatherstead.Web/package.json) — `@microsoft/applicationinsights-web`.
- [src/Gatherstead.Web/app/composables/useApiError.ts](../../../src/Gatherstead.Web/app/composables/useApiError.ts) — `trackException` side effect.
- [infrastructure/main.bicep](../../../infrastructure/main.bicep) and [modules/appservice.bicep](../../../infrastructure/modules/appservice.bicep).

Create:
- `infrastructure/modules/observability.bicep`
- `src/Gatherstead.Api/Observability/TelemetryExtensions.cs`
- `src/Gatherstead.Api/Observability/PiiRedactionLogProcessor.cs`
- `src/Gatherstead.Api/Observability/PiiRedactionActivityProcessor.cs`
- `src/Gatherstead.Api/Middleware/ExceptionLoggingMiddleware.cs`
- `src/Gatherstead.Api/Middleware/CorrelationEnrichmentMiddleware.cs`
- `src/Gatherstead.Data/Entities/SecurityEvent.cs`
- `src/Gatherstead.Api/Services/Observability/ISecurityEventLogger.cs` + impl
- `src/Gatherstead.Api/Controllers/UserAttributionController.cs` (POST /api/users/me/attribution)
- `src/Gatherstead.Web/app/plugins/appInsights.client.ts`
- `src/Gatherstead.Web/app/plugins/attribution.client.ts`
- `docs/OBSERVABILITY.md` — logging contract + allowlist.
- EF migration `AddSecurityEventAndAttribution`.

Reuse (already present):
- `ICurrentTenantContext`, `ICurrentUserContext`, `IAppAdminContext` for enriching spans.
- `AuditingSaveChangesInterceptor` — stays as-is; SecurityEvents are independent of entity audit columns.
- Managed-identity auth pattern from SQL + Key Vault wiring — same `DefaultAzureCredential` flow for AI exporter.

## 12. Verification

- **Local dev**: run API with `APPLICATIONINSIGHTS_CONNECTION_STRING` pointing at a dev AI resource. Hit a controller; confirm a trace lands in App Insights with `tenant.id`/`user.id` dimensions and correlation to the EF query span.
- **PII redaction**: unit test in `Gatherstead.Api.Tests` asserts that a log emitted with `{ Email = "a@b.com", DietaryNote = "x" }` is redacted before export (using an in-memory OTel exporter).
- **SecurityEvent round-trip**: integration test that an authenticated request with a bad JWT creates an `AuthFailure` row and emits the mirrored OTel event.
- **Attribution**: E2E — open `/?utm_source=reddit&via=foo`, sign in, confirm `User.AttributionSource == "reddit"` and `AttributionVia == "foo"` in DB.
- **Cross-tenant alert**: fire a synthetic cross-tenant write in a test environment; confirm the Sev 1 alert email arrives.
- **Correlation**: open App Insights end-to-end transaction for a Nuxt page load; confirm browser pageview → API request → EF query all share one trace ID.
- **Dashboard sanity**: generate a handful of signup events with different UTM sources; confirm the "new tenants by attribution source" tile splits correctly.
