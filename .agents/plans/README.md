---
updated: 2026-10-28
commit: d1f8b0b
---

# Plan Index

This directory contains active design documents and implementation proposals. Current implementation status belongs in [`docs/IMPLEMENTATION_STATUS.md`](../../docs/IMPLEMENTATION_STATUS.md); archived reviews are in [`../archive/plans/`](../archive/plans/README.md).

Plan filenames use lowercase kebab case. Each plan records when it was last reviewed, the repository commit used as its code baseline, and whether it is a proposal or a broader design.

## Active Plans

| Plan | Status | Scope |
|---|---|---|
| [Audit-column database defaults](audit-column-db-defaults.md) | Proposed | Database defaults as a backstop for audit columns |
| [Billing architecture](billing-architecture.md) | Design | Subscriptions, invoices, payments, and entitlements |
| [Demo resource-group split](demo-resource-group-split.md) | Proposed | Move demo infrastructure out of the production resource group |
| [Shared event contracts](event-contracts.md) | Design | Canonical envelope and versioned event catalog |
| [Trial-first strategy](trial-first-strategy.md) | Design | Self-serve trial tenants and expiry enforcement |
| [Guest access](guest-access.md) | Design | Event-scoped guests, invitation flow, and frontend behavior |
| [Notifications architecture](notifications-architecture.md) | Design | Reminder and lifecycle notification processing |
| [Offline mode](offline-mode.md) | Design | PWA snapshots and queued intent writes |
| [Security remediation](security-remediation.md) | Proposed | API authorization, browser session, CSP, identity, and SQL network hardening |

## Resolved Cross-Plan Decisions

- **Self-service tenant creation is approved.** The trial plan adds this after Billing; App Admins retain an internal provisioning endpoint. The existing App-Admin-only behavior remains in place until the plan is implemented.
- **Billing precedes trial launch.** Implement the billing domain and paid-upgrade path before enabling trial tenants.
- **Event contracts have one source.** [Shared Event Contracts](event-contracts.md) owns the envelope and catalog used by Billing and Notifications.
- **Domain contexts emit due events.** Event Scheduling emits `EventReminderDue`; Task Planning emits `TaskAssignmentDue`; Notifications evaluates preferences and handles dispatch and delivery.
- **Notification delivery is at-least-once.** Use idempotent dispatch creation and provider idempotency keys where available; do not claim exactly-once user-visible delivery.
- **Offline snapshots are bounded and unencrypted at the application layer in V1.** Cache only role-filtered active-event participation and related property data, including authorized dietary details only when needed for offline meal planning. Exclude general directory, contacts, addresses, invitations, tenant administration, and unrelated member data. Document and accept the residual stale-access risk for every role; a revoked user may retain the bounded snapshot until reconnect.

Individual plans retain their own implementation questions, such as payment-provider selection and notification transport.

## Source of Truth for Guest Work

[Guest Access](guest-access.md) owns the feature model and user experience. [Security Remediation](security-remediation.md) owns the rollout sequence and backend authorization release gate. Inventory existing Guest `HouseholdUser` rows before migration; preserve them until a migration decision is made.
