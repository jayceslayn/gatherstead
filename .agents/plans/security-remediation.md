---
updated: 2026-10-28
commit: d1f8b0b
status: in-progress
---

# Security Findings Remediation Plan

## Objective

Close the confirmed object-authorization and Guest read-scope gaps from the 2026-10-06 source review, then strengthen the browser session and Azure network/identity boundaries without interrupting production access.

This plan is limited to the findings below. The feature model remains in [guest-access.md](guest-access.md); this plan governs backend authorization rollout, sequencing, and data-safety requirements.

## Findings in scope

1. `AccommodationIntentService.UpdateAsync` authorizes the requested member but not the member currently attached to the stay, and allows the caller to supply an arbitrary status.
2. Guest-facing household/member reads do not positively scope results to the caller's allowed households. The event-scoped Guest model is designed in [guest-access.md](guest-access.md).
3. `TenantUserService.SetLinkedMemberAsync` returns a full tenant-user DTO from no-op branches before checking permission.
4. The cookie-authenticated Nuxt proxy has no explicit CSRF/Origin check; `GET /auth/logout` clears the session and performs federated logout.
5. Nuxt's CSP is report-only, so the configured policy is not enforced by browsers.
6. The web managed identity has Key Vault Secrets User at vault scope although it needs only two secrets.
7. Azure SQL has the broad `AllowAllWindowsAzureIps` firewall rule; private connectivity and the CI migration path are not provisioned.

## Settled implementation principles

- Enforce object authorization in API services. Client route guards and hidden UI controls are not security boundaries.
- Preserve tenant query filters and the tenant write interceptor. Add role/resource scope checks on top of them.
- Use positive filtering for Guest collection reads and an explicit denial for out-of-scope single-resource reads.
- Keep cookie-authenticated mutations same-origin and CSRF-protected. Keep logout as an explicit user action.
- Add narrower Key Vault grants before removing broad grants. Do not close SQL's current network path until replacement connectivity is proven.
- Make production SQL private by default. Keep dev/local networking explicit per environment rather than applying a production exception globally.

## Work packages

### WP1 — Fix accommodation intent authorization and status transitions (complete)

In `AccommodationIntentService.UpdateAsync` and `CreateAsync`:

- Authorize the member currently attached to a stay before changing it. Resolve and authorize members using their actual tenant-scoped household; never use `Guid.Empty` or trust a caller-supplied household ID.
- Require authorization for both current and target members on reassignment, and Coordinator+ for reassignment. Keep tenant and target-accommodation existence checks.
- Allow non-Coordinator users to create only `Requested` stays and edit only existing `Requested` stays for the same member, keeping status `Requested`. Household managers may edit a member's `Requested` stay within the household they manage. Require Coordinator+ for managed statuses (`Hold`, `Confirmed`, `Declined`) on create, update, and create-upsert paths, in line with [guest-access.md](guest-access.md).
- Reject undefined status enum values. Complete authorization and validation before changing tracked entities.
- Preserve current date-span validation and availability behavior.
- Align the member-facing request/edit UI to the same `Requested` boundary and expose all-stay/status controls to Coordinator+.

Add API and frontend tests proving that a linked user cannot take over another member's stay or set a managed status, can edit their own `Requested` stay, and that Coordinator+ can perform intended reassignment/status changes. Include a household-manager case using actual household IDs and preserve state on denied or invalid requests.

### WP2 — Close the TenantUser no-op disclosure

Refactor `TenantUserService.SetLinkedMemberAsync` so authorization occurs before either no-op return:

- For an existing link, derive the linked member's actual household and apply the same authorization as an unlink/update.
- For a requested link, validate tenant membership of the member and require the caller to manage that member's actual household or be Tenant Owner/Manager, as documented.
- For an already-unlinked target and a null request, require Tenant Owner/Manager or return a response that contains no target-user DTO.
- Return the full `TenantUserDto` only after authorization. Do not expose `Email`, `DisplayName`, or `ExternalId` to a caller who lacks the corresponding user-management access.

Add service tests for each no-op path with an unauthorized tenant member, a valid household manager, and a tenant manager. Assert denied responses do not contain target profile fields.

### WP3 — Enforce Guest read scopes in the API

Use [guest-access.md](guest-access.md) Phase 1 followed by Phase 2 as the final model: `EventParticipationScope` and positive filtering for collection reads; household directory reads are unavailable to Guests, and Guests can read only their own linked member plus data for invited events/properties. Keep Member+ and App Admin behavior consistent with the existing policy.

The backend lockdown is a release gate before enabling Guest invitation UX. At minimum, cover the current reported paths:

- `HouseholdService.ListAsync` returns no household directory to a Guest; `GetAsync` denies out-of-scope households.
- `HouseholdMemberService.ListAsync` filters to the Guest's own linked member; `GetAsync` denies another member even when the route contains a valid same-tenant household/member pair.
- Apply the full resource matrix in [guest-access.md](guest-access.md) to event, attendance, plan, property/accommodation, shopping, and participant reads. Collection endpoints return only in-scope records; single-resource endpoints return the standard permission error.
- Ensure accommodation stay list results are scoped to the Guest's invited-property scope and own member where appropriate.
- Keep existing sensitive-field masking and subresource guards; add a member-aware `Self` read scope as specified in the Guest design.

Add an API authorization-matrix suite covering Guest with no invite, one invite, and multiple invites; Member; Manager; and App Admin. Include negative tests for same-tenant but out-of-scope IDs. Inventory existing Guest `HouseholdUser` rows before retiring that scope; preserve them until there is an explicit migration decision.

### WP4 — Protect the Nuxt cookie-authenticated BFF

- Add a same-origin/CSRF guard for unsafe methods at the proxy boundary. Validate `Origin` against a configured canonical web origin and use a CSRF token for mutation requests; reject missing/untrusted origins for unsafe browser requests. Treat `Sec-Fetch-Site` as an additional signal, not the only check.
- Keep GET/HEAD proxy requests read-only. Verify API endpoints do not mutate state on GET.
- Change logout from GET to POST, update the UI caller, and require the same CSRF/origin validation. Return 405 for the old GET route after clients migrate.
- Build the Entra post-logout redirect URI from configured canonical public origin, not the incoming Host header. Confirm it exactly matches the registered redirect URI.
- Preserve `HttpOnly`, `Secure`, and `SameSite=Lax` session-cookie settings needed for the OIDC callback.

Add server tests for valid same-origin mutation, invalid/missing Origin, missing/invalid CSRF token, cross-site form submission, GET logout rejection, and valid POST logout. Add a browser-level check for sibling-subdomain requests if the deployed domain has sibling hosts.

### WP5 — Move CSP from report-only to enforcement

- Keep the current policy report-only while collecting violations from SSR, prerendered pages, the live authenticated SPA, and demo output.
- Configure a supported violation-reporting destination or a documented operator review process. Do not include request bodies or PII in reports.
- Resolve each violation narrowly; do not add broad `unsafe-inline` to `script-src` to silence reports. Preserve the existing nonce/hash approach for SSR and prerendered pages.
- After a clean review window, set `contentSecurityPolicyReportOnly` to `false` and verify response headers on both SSR and static routes.

Verification covers sign-in, hydration, navigation, telemetry, image/font loading, and authenticated API calls under enforced CSP. Roll back to report-only only if a required application path is broken, and record the unresolved violation before retrying.

### WP6 — Reduce the web identity's Key Vault scope

- Replace the web identity's vault-scope `Key Vault Secrets User` assignment with secret-scope grants for `web-external-identity-client-secret` and `nuxt-session-password`, or use a separate web-only vault if App Service Key Vault references cannot use secret-level RBAC in the target configuration.
- Deploy new narrow grants first. Verify the Web App resolves both references and sign-in/session creation still work.
- Then remove the old vault-wide web grant. Keep the API identity's distinct CMK/secret permissions unchanged.
- Verify effective access: web identity can read only the two required secrets and cannot read the database key material or unrelated/future secrets.

### WP7 — Replace broad SQL ingress with private connectivity

Production target: API App Service VNet integration to a subnet with a SQL Private Endpoint, linked `privatelink.database.windows.net` private DNS, and SQL public network access disabled. The web app does not need SQL connectivity.

Before changing the Bicep default or removing the firewall rule:

1. Confirm production App Service SKU supports the required VNet integration and estimate private endpoint/VNet cost.
2. Provision the VNet, delegated integration subnet, private endpoint subnet, private DNS zone/link, and API integration through Bicep.
3. Verify DNS resolution and API-to-SQL connectivity from the deployed API instance.
4. Move the migration job to a runner with private network access (for example, a self-hosted runner in the VNet). The current GitHub-hosted migration runner relies on temporary public firewall access and will not reach a private-only SQL server.
5. Run a migration and health/read smoke check through the new path, then disable SQL public access and remove `AllowAllWindowsAzureIps`.

Keep development behavior explicit and isolated. Do not retain the Azure-wide firewall exception in production as a fallback. If private networking is blocked by SKU/cost or the migration runner cannot be moved, stop before disabling the current path and bring back a narrowed, time-bounded ingress proposal.

## Phasing

1. **API authorization hotfix:** WP1 and WP2, with regression tests. This closes the direct IDOR and profile-disclosure paths first.
2. **Guest backend lockdown:** WP3, following Guest Access Phase 1–2. Guest UI/invitation feature phases 3–4 are not prerequisites for enforcing read scope.
3. **BFF session protection:** WP4; migrate the UI logout action and proxy tests together.
4. **Browser policy enforcement:** WP5 after violation review.
5. **Azure least privilege and network cutover:** WP6, then WP7 in staged infrastructure deployments.

## Verification gates

- Backend: `dotnet build Gatherstead.sln` with zero warnings/errors and `dotnet test Gatherstead.sln` passing.
- Frontend: from `src/Gatherstead.Web/`, `pnpm test:run`, `pnpm run lint`, and `pnpm build` passing; test an enforced-CSP deployment in a browser.
- Infrastructure: compile changed Bicep modules, run `az deployment group what-if`, review role assignment scopes, and verify network/DNS/migration access in a non-production environment before production cutover.
- Authorization acceptance: API-level tests prove the denied actions fail even when the caller supplies valid same-tenant IDs; do not count frontend route guards as proof.

## Stop signals

- Existing accommodation workflows rely on a self-service status transition beyond `Requested`; pause and settle the role/status policy before implementation.
- Existing Guest `HouseholdUser` data or product behavior conflicts with the event-scoped model; inventory and resolve the migration before removing household scope.
- Secret-level Key Vault grants fail App Service reference resolution; use the separate-vault option rather than restoring vault-wide access.
- Private endpoint/DNS or the migration runner is not proven end-to-end; do not disable public SQL access until the replacement path works.
- CSP reports show required script execution that cannot be expressed through the existing nonce/hash policy; keep report-only until the source is understood and addressed.
