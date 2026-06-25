---
updated: 2026-06-25
commit: 31a127e
---

# Observability Contract

Gatherstead uses Azure Monitor OpenTelemetry (`Azure.Monitor.OpenTelemetry.AspNetCore`) with a two-stage PII guard: a `PiiRedactionLogProcessor` (logs) and a `PiiRedactionActivityProcessor` (spans) that run before every export. This document is the authoritative reference for what you are allowed to log and why.

## The one rule

> **Log entity IDs, never entity fields.**

If you need to correlate a log entry to a record in the database, log its `Guid` primary key. Never log a name, email address, phone number, birth date, dietary note, address, or any other value sourced from a `HouseholdMember`, `ContactMethod`, `Address`, `DietaryProfile`, or similar PII-bearing entity.

Bad:
```csharp
_logger.LogWarning("Cannot edit member {MemberName}", member.FirstName);
```

Good:
```csharp
_logger.LogWarning("Member edit denied for {MemberId}", member.Id);
```

## Allowlisted attribute keys

The processors maintain an explicit allowlist. Any structured log property or span tag whose key is **not** on this list is replaced with `[redacted]` before export. When redaction occurs, `LogRecord.FormattedMessage` is also cleared so the expanded string doesn't leak values.

### Application identity dimensions
| Key (case-insensitive) | Safe value |
|---|---|
| `TenantId` / `tenant_id` / `tenant.id` | Internal `Guid` |
| `UserId` / `user_id` / `user.id` | Internal `Guid` |
| `MemberId` / `member_id` / `member.id` | Internal `Guid` |
| `HouseholdId` / `household_id` / `household.id` | Internal `Guid` |
| `EventId` / `event_id` / `event.id` | Internal `Guid` |
| `AccommodationId` / `accommodation_id` / `accommodation.id` | Internal `Guid` |
| `CorrelationId` / `correlation_id` / `correlation.id` | OTel `TraceId` string |
| `EntityTenantId`, `CurrentTenantId` | Internal `Guid` (cross-tenant log) |

### Auth / security metadata
| Key | Safe value |
|---|---|
| `Jti` / `jti` | JWT token ID (not the token itself) |
| `Role` / `UserRole` / `TenantRole` / `HouseholdRole` | Enum name |
| `RequiredRole` | Enum name |
| `Reason` | Short string (revocation reason, authz denial reason) |
| `EventType` | Enum name |
| `Severity` | Enum name |
| `EntityType` | .NET type name |
| `Count`, `Method`, `Path` | Numeric / HTTP verb / URL path |

### ILogger internals
| Key | Safe value |
|---|---|
| `{OriginalFormat}` | Message template string (no values) |
| `CategoryName` | Logger category name |

### OTel standard semantic conventions (always safe)
All `http.*`, `net.*`, `rpc.*`, `service.*`, `code.*`, `otel.*`, `messaging.*`, `exception.*` attributes are allowed. Within `db.*`, only `db.system`, `db.name`, `db.operation`, and `db.sql.table` are allowed — **`db.statement` and `db.query.text` are always redacted** by the Activity processor regardless of allowlist membership, because SQL text can contain bound parameter values.

## Adding to the allowlist

If you need to log a new safe attribute (e.g., a new aggregate ID), add it to the `AllowedKeys` set in [`PiiRedactionLogProcessor.cs`](../src/Gatherstead.Api/Observability/PiiRedactionLogProcessor.cs) and update this document in the same PR. The allowlist is shared between the log and span processors via `PiiRedactionLogProcessor.IsAllowed()`.

## Code review checklist

When reviewing any PR that touches logging, verify:

- [ ] Every structured log parameter is either an ID, enum, count, or short metadata string.
- [ ] No entity field values (`Name`, `Email`, `BirthDate`, `Phone`, `Notes`, `DietaryRestrictions`, etc.) appear as log arguments.
- [ ] New attribute keys are either in the existing allowlist or have been explicitly added with a justification comment.
- [ ] Exception messages caught and logged do not originate from user-controlled input that could embed PII.

## Frontend telemetry

The Nuxt frontend uses the **Application Insights JavaScript SDK**
(`@microsoft/applicationinsights-web`), initialized in
[`app/plugins/analytics.client.ts`](../src/Gatherstead.Web/app/plugins/analytics.client.ts)
and consumed through the [`useAnalytics()`](../src/Gatherstead.Web/app/composables/useAnalytics.ts)
composable. It is **client-only** and **no-ops** when no connection string is configured
(local dev).

Conventions:

- **Cookieless.** `disableCookiesUsage: true` — no consent banner, no persistent identifiers.
- **Separate destinations.** Demo → its own App Insights component (`gat-ai-demo-*`); Prod →
  the shared `gat-ai-*` (same resource as the backend, giving end-to-end frontend↔backend
  trace correlation). Every item is tagged with `ai.cloud.role` (`gatherstead-web` /
  `gatherstead-web-demo`).
- **Session/user stitching (hybrid).** Demo and anonymous Prod pages group a visit by an
  in-memory session GUID (resets on full reload). Authenticated Prod users are stitched via
  `setAuthenticatedUserContext(userId, tenantId, /*storeInCookie*/ false)` — no cookie.
- **The one rule still applies.** Custom event names, property values, and the `setUser`
  arguments must be opaque IDs / enums / counts / short metadata only — never member names,
  emails, notes, birth dates, or any other entity field value.
- **Two instrumentation layers.**
  - *Within-SPA non-persisted interactions* (modal opens, view toggles, the demo→live CTA)
    are tracked in components/composables via `useAnalytics().trackEvent(...)` and fire in
    both modes — they never reach the backend, so the frontend is the only place they exist.
  - *Persisted actions* (create/update/delete) are tracked at the **repository layer** via
    `trackPersistence(entity, action, props?)` in
    [`app/utils/telemetry.ts`](../src/Gatherstead.Web/app/utils/telemetry.ts) — a module-level
    accessor because repos run outside Vue/Nuxt context. They fire in both modes: in Demo
    they are the only record (no backend); in Prod they intentionally complement the backend
    API trace with frontend-session funnel correlation. Event names match across Demo and
    Live repos (e.g. `event_create`, `attendance_set`) so the two destinations align.
    Extend coverage by calling `trackPersistence` from additional repo write methods.
- **Demographics are PII-safe.** Coarse geo (city/region/country) is derived at ingestion and
  the IP is not stored; browser/OS come from the User-Agent; `language`/`locale` are sent as
  custom dimensions. None identify an individual.

The connection string is delivered as `NUXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` — a web-app
app setting in Prod (read at runtime) and a GitHub Actions secret baked into the static Demo
build at `pnpm generate` time. It is an ingestion-only key and safe to expose in client code.

## Verification

The allowlist is validated by unit tests in [`PiiRedactionLogProcessorTests`](../tests/Gatherstead.Api.Tests/Observability/PiiRedactionLogProcessorTests.cs) and [`PiiRedactionActivityProcessorTests`](../tests/Gatherstead.Api.Tests/Observability/PiiRedactionActivityProcessorTests.cs). Run `dotnet test` to confirm all assertions pass after any allowlist change.
