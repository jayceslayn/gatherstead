# Red-Team White-Box Security Audit

Reviewed: 2026-06-04  
Scope: Full solution — auth pipeline, multi-tenant API, EF Core data layer, Nuxt 4 frontend  
Focus: Data exfiltration / unintentional disclosure; privilege escalation toward Tenant Owner / App Admin; lateral movement across tenant boundaries

---

## Overall Assessment

The security architecture is sound. Defense-in-depth is present across every critical boundary (JWT validation, EF query filters, interceptor write guard, service-level guards, SensitiveReadScope). No critical or high-severity vulnerabilities were found. Two medium findings require remediation before production handles real PII. Four low-severity items are hardening opportunities.

---

## 🟡 Medium — Always Encrypted: infrastructure present, schema not implemented

**The PII columns are not actually encrypted in the database.**

The provisioning infrastructure exists (`src/Gatherstead.Data.Setup/Program.cs` creates CMK + CEK in Azure Key Vault) and the connection string has `Column Encryption Setting=Enabled;Attestation Protocol=HGS`. However:

- No EF Core migration files exist anywhere in the repo (`src/Gatherstead.Data/Migrations/` is absent).
- No `ENCRYPTED WITH (...)` column definitions appear in any SQL schema file.
- EF Core has no built-in Always Encrypted support — encryption is defined in SQL DDL, not in the model. The ADO.NET driver handles it transparently, but only for columns physically created encrypted.

**Affected plaintext fields (contrary to DESIGN_PRINCIPLES.md):**
- `HouseholdMember.Name`, `.BirthDate`, `.DietaryNotes`
- `ContactMethod.Value` (emails and phone numbers)
- `Address.*`
- `User.Email`

**Impact:** A compromised DBA account, any direct database access, backup leak, or future raw SQL path exposes all member PII in cleartext.

**Remediation:** Write SQL migration(s) that `ALTER TABLE … ALTER COLUMN … ADD ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = GathersteadCEK, ENCRYPTION_TYPE = …, ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256')` for each sensitive column. Key design decision: Randomized type does not support equality filtering (fine for `Name`, `BirthDate`, `DietaryNotes`); Deterministic is required for `ContactMethod.Value` if it is used in lookup queries.

---

## 🟡 Medium — Missing input-length validation on `DietaryNotes` and `DietaryTags`

**File:** `src/Gatherstead.Api/Contracts/HouseholdMembers/HouseholdMemberContracts.cs:39-43`

```csharp
// Both CreateHouseholdMemberRequest and UpdateHouseholdMemberRequest
public string? DietaryNotes { get; init; }   // no [StringLength] — unbounded input
public string[]? DietaryTags { get; init; }  // no count limit or per-item length limit
```

The `Notes` field on the same model has `[StringLength(500)]`; `DietaryNotes` does not. A caller with Household Manager+ access can send an arbitrarily large string, causing DB truncation errors or storage abuse.

**Remediation:**
```csharp
[StringLength(500)]
public string? DietaryNotes { get; init; }

[MaxLength(20)]                              // reasonable tag count cap
public string[]? DietaryTags { get; init; } // add per-tag [StringLength] if the domain entity has one
```

---

## 🔵 Low — Response DTOs expose audit/lifecycle fields to all roles

**Affected:** All `*Dto` records in `src/Gatherstead.Api/Contracts/` (19+ files) — `HouseholdMemberDto`, `ContactMethodDto`, `TenantDto`, `EventDto`, etc.

Every DTO includes `CreatedAt`, `UpdatedAt`, `IsDeleted`, `DeletedAt`, `DeletedByUserId` regardless of the caller's role. For a Guest caller, `IsDeleted` is always `false` and `DeletedByUserId` is always `null` (soft-delete filter hides deleted records). However:

- `CreatedAt`/`UpdatedAt` leaks activity timing for all records to Guest callers.
- `DeletedByUserId` (a platform-internal User GUID) being present in the API contract enables cross-tenant identity correlation if an attacker obtains data from multiple tenants (same GUID deleted records in both — confirms same admin).
- Contradicts the "privacy by design" and data minimization principle.

**Remediation:** Conditionally omit audit fields for non-Manager callers. Either introduce a `*AdminDto` variant populated only for Manager+ responses, or suppress `IsDeleted`/`DeletedAt`/`DeletedByUserId` at the mapping layer based on the caller's `SensitiveReadScope` / role.

---

## 🔵 Low — `ValidateTenantId` interceptor only guards `EntityState.Added`

**File:** `src/Gatherstead.Data/Interceptors/AuditingSaveChangesInterceptor.cs:88-91`

```csharp
if (entry.State != EntityState.Added)
    continue;
```

The cross-tenant write guard fires only on INSERT. If a future service method were to reassign `TenantId` on a Modified entity (e.g., via an incorrectly bound request model), the interceptor would not detect it. In practice, EF query filters prevent loading cross-tenant entities, so this is theoretical — but it is a gap in the defense-in-depth chain.

**Remediation:** Extend the check to `EntityState.Modified` and compare `CurrentValue` vs `OriginalValue`; throw if the TenantId has changed.

---

## 🔵 Low — `RequireTenantAccessAttribute` silently bypasses when route has no `{tenantId}`

**File:** `src/Gatherstead.Api/Security/RequireTenantAccessAttribute.cs:41-46`

```csharp
var tenantIdValue = context.HttpContext.GetRouteValue("tenantId");
if (tenantIdValue is null)
    return;  // silently exits — no AuthZ performed
```

Intentional for top-level endpoints like `GET /api/tenants`. However, a future controller decorated with `[RequireTenantAccess(MinimumRole = …)]` that accidentally omits `{tenantId}` from its route will have the authorization check silently bypassed with no compile-time or test-time signal.

**Remediation:**
```csharp
if (tenantIdValue is null)
{
    if (MinimumRole.HasValue)
        throw new InvalidOperationException(
            $"[RequireTenantAccess(MinimumRole={MinimumRole})] is on a route without {{tenantId}}.");
    return;
}
```

---

## 🔵 Low — App Admin bypass is all-or-nothing; no documented extra factor

`IsAppAdmin` is correctly stored as a database flag (not a JWT claim — cannot be forged). However, once set, it grants unrestricted read/write access to all tenants, all soft-deleted data, and all member PII in a single hop with no additional verification. There is no documented requirement for MFA or alerting on App Admin logins.

**Remediation (policy, not code):** Require MFA for any account granted `IsAppAdmin = true`. Add an alerting rule on App Admin authentication events in Application Insights / Azure Monitor. Consider a break-glass workflow (time-limited elevation) rather than a permanent flag.

---

## ✅ Positive Findings (attack surface validated, no issues found)

| Area | Detail |
|------|--------|
| JWT validation | Issuer, audience, lifetime, signing key — all enforced. `alg=none` rejected by middleware. |
| Token revocation | JTI lookup per-request; revoked tokens fail authentication with security event logged. |
| Multi-tenant isolation | EF global query filters + `ValidateTenantId` interceptor write guard = layered isolation. |
| IDOR | TenantId required in every WHERE clause across all 28+ service methods. |
| Sensitive read scoping | `SensitiveReadScope` correctly gates `BirthDate`, `DietaryNotes`, `DietaryTags`, `ContactMethod.Value`. `MapToDto` nulls fields when scope is insufficient. |
| Privilege escalation | `RequireNonEscalatingRole` enforced on role assignment and invitation creation. Last-owner demotion blocked. |
| Mass assignment / over-posting | `TenantId` and `Id` never bindable from request body; always resolved from route/context. |
| SQL injection | No `FromSqlRaw` / `ExecuteSqlRaw` / `ExecuteRaw` — all data access via EF Core LINQ. |
| Error response leakage | Generic 500 with correlation ID; stack traces logged server-side only. |
| PII in logs | IDs only in all log statements; no names, emails, or phone numbers logged. |
| XSS | No `v-html` anywhere in the Nuxt frontend. |
| CSRF | Bearer token in `Authorization` header is inherently CSRF-resistant. |
| Frontend token storage | Access tokens in httpOnly session cookies (server-side via `nuxt-auth-utils`); never in localStorage or URLs. |
| Demo mode bypass | `__DEMO_MODE__` is a compile-time Vite constant; cannot be toggled at runtime. |
| Tenant enumeration | `GET /api/tenants` scopes to the caller's own tenants; App Admins see all. |
| `IgnoreQueryFilters()` usage | All four call sites are explicitly scoped by `TenantId` in the subsequent LINQ filter; no raw cross-tenant reads. |
| Rate limiting | 100 req/min/IP; returns 429 with security event logged. |
| Security headers | CSP, X-Frame-Options, HSTS, Referrer-Policy, Permissions-Policy all present. |
| Supply chain | Lockfiles committed; `audit-nuget`, `audit-pnpm`, and `dependency-review` gates in CI. |

---

## Prioritized Remediation Checklist

| # | Severity | Item |
|---|----------|------|
| 1 | Medium | Write SQL migrations to encrypt sensitive columns with `ENCRYPTED WITH (...)` — `HouseholdMember.Name/BirthDate/DietaryNotes`, `ContactMethod.Value`, `Address.*` |
| 2 | Medium | Add `[StringLength(500)]` to `DietaryNotes` and a count+length guard on `DietaryTags` in both `Create` and `Update` request models |
| 3 | Low | Strip `IsDeleted`, `DeletedAt`, `DeletedByUserId` (and optionally timestamps) from DTOs returned to Member/Guest callers |
| 4 | Low | Extend `ValidateTenantId` to also detect TenantId reassignment on `EntityState.Modified` entities |
| 5 | Low | Add `MinimumRole.HasValue` assertion in `RequireTenantAccessAttribute` to catch future route misconfiguration at startup |
| 6 | Policy | Require MFA + alerting for any account with `IsAppAdmin = true` |
