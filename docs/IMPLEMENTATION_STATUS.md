---
updated: 2026-07-14
commit: 4e3603e
---

# Implementation Status and Direction

This document is **forward-looking**: what is planned, and where the architecture is headed.

It deliberately does not catalogue what has already been built — the code is authoritative for
that, and the conceptual detail behind every shipped feature lives in
[ARCHITECTURE.md](ARCHITECTURE.md) (entities, conventions, mechanisms) and
[DESIGN_PRINCIPLES.md](DESIGN_PRINCIPLES.md) (security, privacy, authorization rules). The
at-a-glance table exists only to orient; follow it to those docs for detail.

## At a glance

| Area | State |
|---|---|
| **Multi-tenant foundation** — tenancy, auditing, soft-delete, Always Encrypted PII | In place |
| **Family Directory** — households, members, addresses, contacts, relationships, dietary profiles | CRUD complete (`MemberRelationship` has no frontend UI — known gap) |
| **Gathering Planning** — events, properties, accommodations, meal/task templates + plans, intents, attendance, equipment, shopping items | CRUD complete |
| **Authorization** — four-tier (App Admin → Tenant role → Event/Coordinator → Profile/Intent/Self), sensitive-read scoping | In place |
| **Identity & access** — Entra External ID JWT, JIT provisioning, app-managed invitations with email claiming, token revocation, account erasure | In place |
| **Reporting** — event meal/attendance report (daily headcounts, per-meal dietary tally) | Done; task-coverage-per-slot report pending |
| **Frontend** — MVP create/edit surface, i18n (en/es), demo site, hybrid rendering | MVP complete |
| **Supply chain & ops** — Dependabot, CI audit gates, lockfile enforcement, 1-year temporal retention, legal pages | In place |

## Planned Enhancements

Larger items have a dedicated design doc under [`.agents/plans/`](../.agents/plans/); where one
exists, it is the source of truth for scope and approach.

- **Guest access (event-scoped guests)**: True-guest participation scoped to invited events — hidden per-tenant system household, `EventInvitee` entity, positive read scoping for the Guest role, one-step guest invitations, and guest-tailored frontend. Replaces the latent household-linked guest flavor. → [GUEST-ACCESS.md](../.agents/plans/GUEST-ACCESS.md)
- **Billing & subscriptions**: Subscription / invoice / payment-attempt model and payment-provider integration. → [BILLING-ARCHITECTURE.md](../.agents/plans/BILLING-ARCHITECTURE.md)
- **Notifications**: Event-driven notification dispatch with per-channel/category preferences and retry. → [NOTIFICATIONS-ARCHITECTURE.md](../.agents/plans/NOTIFICATIONS-ARCHITECTURE.md)
- **Offline mode**: Local-first reads plus an outbox that replays queued writes on reconnect. → [OFFLINE-MODE.md](../.agents/plans/OFFLINE-MODE.md)
- **Trial-first / free tier**: Self-serve 90-day trial tenant with full feature access, going read-only at expiry pending upgrade. Depends on billing. → [FREE_TIER.md](../.agents/plans/FREE_TIER.md)
- **Email/Graph invitation delivery**: Layer out-of-band invite delivery (Entra Graph invite or email) on top of the existing app-managed `Invitation` model — the data model and JIT claim flow are designed so this slots in without rework.
- **Task sign-up flows**: Capacity enforcement using `TaskTemplate.MinimumAssignees` — surface unfilled slots and prevent overbooking when members volunteer via `TaskIntent`.
- **Accommodation arbitration**: Surface arbitration metadata through dedicated admin endpoints so accommodation conflicts can be resolved with transparent priority rules and audit trails. (The previous unused `AccommodationIntent.Priority` placeholder column was removed; re-introduce a priority signal when this feature is designed against real requirements.)
- **Household migration workflow**: When members age out of a parent household, allow Tenant Owner/Manager to create a new household and migrate the member, transferring or creating their `HouseholdUser` entry in the new household.
- **Audit-column DB defaults**: Database-side default values for `Id`/`CreatedAt`/`UpdatedAt`/`IsDeleted` as a backstop for inserts that bypass EF (seeding, raw SQL, manual fixes). → [audit-column-db-defaults.md](../.agents/plans/audit-column-db-defaults.md)
- **Row-Level Security**: Evaluate SQL Server Row-Level Security (RLS) as a defense-in-depth backstop to the existing application-layer tenancy scoping.

## Architecture Direction

Treat households and events as separate aggregates linked through member IDs, enabling
independent evolution of directory data and event participation while maintaining traceability.

- **API and workflow alignment**: Both bounded contexts are fully exposed through REST controllers with consistent guard composition, tenant-scoped authorization, and soft-delete conventions. Next focus: task-coverage-per-slot reports, and enforcement of capacity constraints (`TaskTemplate.MinimumAssignees`).
- **Authorization refinement**: The four-tier model is in place and both tiers now map denials to the correct HTTP status — the attribute tier separates 401 from 403 (`RequireTenantAccessAttribute`/`RequireAppAdminAttribute`), and the service tier returns 403 for any `PERMISSION_*` error via `ServiceErrorActionResult`, preserving the response body so the frontend keeps localizing by `ErrorCode`. Remaining hardening is tracked under Planned Enhancements: Row-Level Security as a persistence-layer backstop, and positive read scoping for the Guest role via the guest-access design.
