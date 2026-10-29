---
updated: 2026-10-28
commit: d1f8b0b
status: design
---

# Billing Architecture

## Purpose and Scope
The Billing context manages subscription lifecycle, invoice/payment processing, and entitlement projection for each tenant. It integrates with Azure-native payment orchestration and emits canonical events consumed by Notifications and entitlement enforcement.

### Goals
- Provide deterministic tenant-scoped subscription state transitions.
- Model invoicing and payment attempts with clear audit trails.
- Project entitlement state consumed by API authorization and frontend feature gating.

### Non-goals
- Selecting a specific third-party payment provider in this document.
- Defining presentation-level billing UI details.

## Bounded Context Responsibilities

### Billing context owns
- Subscription lifecycle state machine.
- Invoice generation, due tracking, and dunning retries.
- Payment attempt recording and reconciliation.
- Entitlement projection (current plan capabilities by tenant).

### Billing context consumes
- Tenant/user lifecycle events when needed for account bootstrap (`UserInvited`, `RoleAssigned`).

### Billing context publishes
- `SubscriptionCreated`, `InvoiceDue`, `PaymentSucceeded`, `PaymentFailed`, `SubscriptionCanceled`.

## Domain Model

### 1) Subscription (aggregate root)
Represents tenant plan enrollment.

- **Identity**: `SubscriptionId`, `TenantId` (mandatory)
- **Core fields**: `PlanCode`, `Status`, `CurrentPeriodStartUtc`, `CurrentPeriodEndUtc`, `CanceledAtUtc`, `CancelAtPeriodEnd`
- **Lifecycle states**:
  - `PendingActivation`
  - `Active`
  - `PastDue`
  - `Grace`
  - `Canceled`
- **Invariants**:
  - One active subscription per tenant at a time.
  - State transitions only via domain commands/events.

### 2) Invoice (aggregate root)
Billable artifact for a subscription period.

- **Identity**: `InvoiceId`, `TenantId` (mandatory)
- **Core fields**: `SubscriptionId`, `InvoiceNumber`, `IssueUtc`, `DueUtc`, `Currency`, `SubtotalMinor`, `TaxMinor`, `TotalMinor`, `Status`
- **Statuses**: `Draft`, `Issued`, `Due`, `Paid`, `Voided`, `Uncollectible`
- **Invariants**:
  - Totals are immutable once issued (except credit-note workflow).
  - `TenantId` must match referenced subscription tenant.

### 3) PaymentAttempt (aggregate root)
Tracks each payment processing attempt.

- **Identity**: `PaymentId`, `TenantId` (mandatory)
- **Core fields**: `InvoiceId`, `AttemptNumber`, `ProviderRef`, `AmountMinor`, `Currency`, `Status`, `FailureCode`, `ProcessedUtc`
- **Statuses**: `Initiated`, `Succeeded`, `Failed`, `Abandoned`
- **Invariants**:
  - Attempts are append-only for auditability.
  - Exactly one successful payment may settle an invoice total.

### 4) EntitlementProjection (read model)
Materialized view used by backend authorization and frontend feature checks.

- **Identity**: `TenantId` (mandatory)
- **Core fields**: `PlanCode`, `EntitlementSet`, `QuotaSnapshot`, `EffectiveFromUtc`, `EffectiveToUtc`
- **Update drivers**: subscription and payment events.

## Subscription Lifecycle
1. **Create**: Tenant enrolls in a plan -> `SubscriptionCreated` emitted.
2. **Bill**: Invoice issued each billing cycle -> `InvoiceDue` emitted when due window opens.
3. **Collect**:
   - Success -> `PaymentSucceeded`, invoice becomes `Paid`, subscription remains/returns `Active`.
   - Failure -> `PaymentFailed`, subscription enters `PastDue` then optional `Grace`.
4. **Cancel**: Immediate or end-of-period cancellation -> `SubscriptionCanceled`.
5. **Entitlements**: Projected continuously from canonical billing events.

## API Surface (.NET Web API)
All endpoints are tenant-scoped and protected with role-based access.

### Subscriptions
- `GET /api/tenants/{tenantId}/billing/subscription`
- `POST /api/tenants/{tenantId}/billing/subscription`
- `POST /api/tenants/{tenantId}/billing/subscription/cancel`
- `POST /api/tenants/{tenantId}/billing/subscription/reactivate`

### Invoices
- `GET /api/tenants/{tenantId}/billing/invoices`
- `GET /api/tenants/{tenantId}/billing/invoices/{invoiceId}`
- `POST /api/tenants/{tenantId}/billing/invoices/{invoiceId}/finalize`

### Payments
- `POST /api/tenants/{tenantId}/billing/invoices/{invoiceId}/payments`
- `GET /api/tenants/{tenantId}/billing/payments?invoiceId=...`
- `POST /api/tenants/{tenantId}/billing/payments/{paymentId}/reconcile`

### Entitlements
- `GET /api/tenants/{tenantId}/billing/entitlements`
- `POST /api/internal/billing/entitlements/rebuild` (internal only)

## Azure Alignment
- Use Azure-secured secret handling for provider credentials (Managed Identity + Key Vault).
- Prefer durable, replayable event processing through Azure messaging.
- Emit OTel metrics/traces for subscription transitions, invoice aging, payment retry rates.

## Shared Event Contracts

The shared envelope and versioned event catalog live in [Shared Event Contracts](event-contracts.md). Billing publishes `SubscriptionCreated`, `InvoiceDue`, `PaymentSucceeded`, `PaymentFailed`, and `SubscriptionCanceled`; the shared document defines their payloads and consumers.

## Tenant Scoping Rules
- `TenantId` is mandatory in event envelope, aggregate storage, API DTOs, and queue partition/routing keys.
- Billing commands and event handlers reject requests/events with missing or mismatched tenant context.
- No cross-tenant joins for operational workflows.
- **Exception (User domain only):** user entities can be global (outside a tenant) or linked to multiple tenants. User-domain mutations are allowed without `TenantId`, but billing state transitions and entitlement projections must remain tenant-scoped.

## PII Constraints (per `docs/OBSERVABILITY.md`)
- Billing events/logs contain IDs, enums, currency codes, amounts, and short reason codes only.
- Never include personal profile data (name/email/phone/address/birthdate) in payloads or log arguments.
- Observability attributes must remain within approved allowlist semantics (`TenantId`, `UserId`, `MemberId`, `EventId`, `CorrelationId`, roles/reasons/counts).
- Do not emit SQL statements or provider payload blobs that may include sensitive values.

## Versioning and Compatibility
- Additive payload fields are backward-compatible.
- Breaking schema changes require version increments with dual publish/consume windows.
- Consumers must ignore unknown fields for forward compatibility.
