# Implementation Status and Next Steps

## Implemented Features
- **Shared foundation**: Tenants own households, properties, events, and users to keep families isolated.
- **Family directory context**: Households group members; member records store names, birth dates, dietary notes/tags.
- **Gathering planning context**: Events tie to properties and manage meal plans, chores, lodging resources, and member intents for attendance, meals, stays, and chores.
- **Detailed implementation**: Multi-tenant backbone with auditing, households and members (including dietary metadata), relationship graph, contact/addresses/attributes, dietary profiles, event scaffolding with meal planning, daily attendance, chore templates/tasks with assignments, lodging intents with arbitration metadata, and tenant identifiers on dependent tables to enforce tenant-aware filters across the graph.
- **Family Directory CRUD API**: Full CRUD controllers and services for all Family Directory sub-entities:
  - **Addresses** (`/members/{memberId}/addresses`): CRUD with automatic IsPrimary flag management (setting one primary unsets others).
  - **Contact Methods** (`/members/{memberId}/contacts`): CRUD for email/phone/other with same IsPrimary logic.
  - **Member Attributes** (`/members/{memberId}/attributes`): CRUD for extensible key-value pairs with duplicate-key validation.
  - **Member Relationships** (`/members/{memberId}/relationships`): CRUD with cross-member validation (related member must exist in the same tenant, can span households), self-relationship guard, and duplicate relationship check.
  - **Dietary Profiles** (`/members/{memberId}/dietary-profile`): GET/PUT(upsert)/DELETE for one-to-one profiles, plus a tenant-level list endpoint (`/tenants/{tenantId}/dietary-profiles?memberIds=`) for aggregating dietary needs across attending members for meal planning.
- **PASETO token security**: Reimplemented PASETO authentication handler with issuer/audience validation, configurable clock skew, Azure Key Vault integration for key management, and a token revocation service backed by a `RevokedTokens` table.
- **Rate limiting**: Sliding-window rate limiter configured in the API pipeline to enforce request throttling per the secure-defaults principle.
- **Tenant-scoped authorization**: `RequireTenantAccessAttribute` authorization filter that extracts the tenant from the route, verifies the user's `TenantUser` membership, and enforces a role hierarchy (Owner > Manager > Member > Guest). Applied to tenant-aware controllers.
- **Always Encrypted data protection**: Shifted from application-level encryption to SQL Server Always Encrypted, delegating column-level encryption of sensitive fields to the database driver rather than custom application code.
- **Project restructure**: Migrated data projects from `packages/db/` to `src/Gatherstead.Data/` and `src/Gatherstead.Data.Setup/`, aligning namespaces under `Gatherstead.Data`.
- **Framework upgrade**: Updated all projects to target .NET 10 (`net10.0`) with current package references.
- **Composable soft-delete query filters**: Global query filters in `GathersteadDbContext` now conditionally apply the soft-delete clause via a `_includeDeleted` field, while tenant isolation remains unconditional. List/read endpoints accept `?includeDeleted=true`, RBAC-gated to `TenantRole.Manager+` in `RequireTenantAccessAttribute`. The authorization decision flows through `IIncludeDeletedContext` → `HttpContext.Items`, ensuring the raw query parameter cannot bypass role checks. Lower-role users' flag is silently ignored.
- **Cross-tenant write prevention**: The `AuditingSaveChangesInterceptor` validates that every new entity's `TenantId` matches the current tenant context before saving, throwing an `InvalidOperationException` on mismatch. This provides a defense-in-depth backstop against accidental or malicious cross-tenant writes.
- **Temporal history retention**: `Gatherstead.Data.Setup` configures a 1-year retention policy on all temporal (system-versioned) tables, keeping history bounded and storage predictable.
- **Integration test suite**: A dedicated `Gatherstead.Api.Tests` project covers the authentication pipeline, CORS policy, rate limiting, security headers, PASETO token handling, tenant access authorization, token revocation, and context propagation (`ICurrentTenantContext`, `ICurrentUserContext`, `IIncludeDeletedContext`).
- **Resource-level RBAC (Self, Household Admin)**: Fine-grained write authorization enforced at the service layer via `IMemberAuthorizationService`. The authorization decision tree evaluates (in order): tenant Owner/Manager override, Self (user's own linked `HouseholdMember` record), and Household Admin (`HouseholdRole.Admin`). Read access remains open to all tenant members. Key changes:
  - `HouseholdMember` gains a nullable `UserId` FK linking to `User` (one-to-many: one user can be linked to multiple members across households) and a `HouseholdRole` enum (`Admin`, `Member`).
  - `User` gains a `HouseholdMembers` navigation collection.
  - All Family Directory write services (`HouseholdService`, `HouseholdMemberService`, `AddressService`, `ContactMethodService`, `DietaryProfileService`, `MemberAttributeService`, `MemberRelationshipService`) enforce `CanEditMemberAsync` or `CanManageHouseholdAsync` before mutations.
  - `CreateHouseholdMemberRequest` and `UpdateHouseholdMemberRequest` accept an optional `UserId` for linking members to authenticated users. Only Tenant Owner/Manager or Household Admin can set `UserId`; the field is rejected for non-privileged users to prevent privilege escalation.
  - Per-request caching of tenant role and linked member data via `HttpContext.Items` avoids redundant DB queries within a single request.
- **Tenant Owner-only mutations**: Tenant update and delete operations are restricted to the `TenantRole.Owner` role.
- **App Admin role**: Platform-level `IsAppAdmin` flag on the `User` entity grants full administrative privileges. App Admins bypass all tenant-level (`RequireTenantAccessAttribute`) and resource-level (`IMemberAuthorizationService`) authorization checks. Key behaviors:
  - Tenant creation (`POST /api/tenants`) is restricted exclusively to App Admins via `RequireAppAdminAttribute`. The request specifies an `OwnerUserId` to designate the initial tenant Owner.
  - `GET /api/tenants` returns all tenants for App Admins, while regular users see only tenants they belong to.
  - App Admin status is resolved per-request via `IAppAdminContext` with `HttpContext.Items` caching.
  - The first App Admin must be set via direct SQL (`UPDATE Users SET IsAppAdmin = 1 WHERE ...`).

## Planned Enhancements
- **Localization (i18n)**: Integrate `@nuxtjs/i18n` with lazy-loaded locale files, `prefix_except_default` URL strategy, cookie persistence, date/number/currency formatting, API error code translation, locale switcher component, and ESLint enforcement of `$t()` usage. English first, structured for easy addition of other languages. See [LOCALIZATION.md](agents/plans/LOCALIZATION.md).
- **Demo site**: Deploy a zero-friction public demo at `demo.gatherstead.<host>.<ext>` on Azure Static Web Apps (Free tier). Same codebase with `NUXT_PUBLIC_DEMO_MODE` flag, localStorage persistence (no backend), entity limits to drive conversion, seed data for a non-empty first experience, persistent bottom banner with restriction details, and a dedicated CI/CD workflow. See [DEMO_SITE.md](agents/plans/DEMO_SITE.md).
- **Hybrid rendering**: Production web app will use Nuxt `routeRules` for hybrid SSR/SPA — server-rendered public pages for SEO, client-only SPA for authenticated dashboard routes.
- **Daily attendance summaries**: Aggregate per-day headcounts from attendance intents so event planners can see who is present on which days at a glance.
- **Chore sign-up flows**: Let members volunteer for open chore slots during an event, with assignment confirmation and capacity enforcement.
- **Lodging arbitration**: Surface arbitration metadata through API endpoints so lodging conflicts can be resolved with transparent priority rules and audit trails.
- **Gathering Planning API**: Add operational indexes/constraints for event-related tables and expose attendance, meal intent, chore assignment, and lodging arbitration capabilities through validated, authorized API endpoints matching the Family Directory patterns.
- **Household migration workflow**: When members age out of a parent household, allow Tenant Owner/Manager to create a new household and migrate the member, automatically assigning them the Household Admin role.
- **Row-Level Security**: Evaluate SQL Server Row-Level Security (RLS) as a defense-in-depth backstop to the existing application-layer tenancy scoping.

## Schema Review and Recommended Improvements
The current model now spans tenants, households/members, relationships, contact data, event planning, chores, and lodging intents. Composite indexes on `(TenantId, ForeignKey)` and uniqueness constraints (primary contact/address per member, single dietary profile per member, unique attribute keys) are in place for the Family Directory entities. Focus the next round of tightening on:

- **Indexing for event-related tables**: Add composite indexes on `(TenantId, ForeignKey)` for high-churn Gathering Planning tables (events, chores, meal intents, stay intents) to keep tenant-specific queries predictable.

## Architecture Direction
Treat households and events as separate aggregates linked through member IDs, enabling independent evolution of directory data and event participation while maintaining traceability.

- **Batch reads, singular writes**: List endpoints should accept optional ID filters (e.g., `?ids=`) to enable batch reads within the existing `BaseEntityResponse<IReadOnlyCollection<T>>` contract. Write endpoints (create/update/delete) remain singular to preserve clean audit trails, simple error handling, and the `BaseEntityResponse<T>` contract. Workflow-specific batch write endpoints (e.g., bulk meal plan or resource creation during event setup) should be introduced only when concrete requirements arise.

- **API and workflow alignment**: Family Directory sub-entities (relationships, contacts, addresses, attributes, dietary profiles) are now fully exposed through DTOs/services with validation and tenant-scoped authorization. Next, surface Gathering Planning capabilities (attendance, meal intents, chore assignments, lodging arbitration) through similar endpoint patterns so guardianship and arbitration rules become enforceable behaviors.
- **Authorization refinement**: Three-tier authorization (App Admin → Tenant Role → Resource Role) is now in place. Next steps include proper HTTP status differentiation (401 for authentication failures, 403 for insufficient permissions) and unit/integration tests for the authorization decision trees.
