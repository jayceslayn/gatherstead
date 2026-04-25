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
- `GET /api/billing/subscription`
- `POST /api/billing/subscription`
- `POST /api/billing/subscription/cancel`
- `POST /api/billing/subscription/reactivate`

### Invoices
- `GET /api/billing/invoices`
- `GET /api/billing/invoices/{invoiceId}`
- `POST /api/billing/invoices/{invoiceId}/finalize`

### Payments
- `POST /api/billing/invoices/{invoiceId}/payments`
- `GET /api/billing/payments?invoiceId=...`
- `POST /api/billing/payments/{paymentId}/reconcile`

### Entitlements
- `GET /api/billing/entitlements`
- `POST /api/internal/billing/entitlements/rebuild` (internal only)

## Azure Alignment
- Use Azure-secured secret handling for provider credentials (Managed Identity + Key Vault).
- Prefer durable, replayable event processing through Azure messaging.
- Emit OTel metrics/traces for subscription transitions, invoice aging, payment retry rates.

## Canonical Event Catalog (Shared with Notifications)
This catalog is **canonical** and shared across Billing and Notifications contexts. Event names and payload schemas are versioned contracts.

### Event envelope (required for all events)
```json
{
  "EventName": "string",
  "Version": 1,
  "TenantId": "guid",
  "EventId": "guid",
  "OccurredUtc": "2026-04-25T00:00:00Z",
  "CorrelationId": "guid or trace id",
  "Producer": "BoundedContext.Service",
  "Payload": {}
}
```

### Catalog
| Event Name | Version | Producer | Primary Consumers | Payload schema (minimum) |
|---|---:|---|---|---|
| `UserInvited` | 1 | Identity/Tenant Membership | Notifications, Billing | `InvitationId: guid`, `InvitedUserId: guid`, `InviterUserId: guid`, `TenantRole: string`, `ExpiresUtc: datetime` |
| `RoleAssigned` | 1 | Identity/Tenant Membership | Notifications | `AssignmentId: guid`, `UserId: guid`, `Role: string`, `AssignedByUserId: guid`, `Scope: string` |
| `EventReminderDue` | 1 | Event Scheduling | Notifications | `EventId: guid`, `ReminderType: string`, `TargetDateUtc: datetime`, `Audience: string`, `TemplateKey: string` |
| `ChoreAssignmentDue` | 1 | Chore Planning | Notifications | `ChorePlanId: guid`, `EventId: guid`, `DueUtc: datetime`, `AssignedMemberId: guid`, `TemplateKey: string` |
| `SubscriptionCreated` | 1 | Billing | Billing, Notifications | `SubscriptionId: guid`, `PlanCode: string`, `BillingPeriod: string`, `StartsUtc: datetime`, `CustomerRefId: guid` |
| `InvoiceDue` | 1 | Billing | Billing, Notifications | `InvoiceId: guid`, `SubscriptionId: guid`, `DueUtc: datetime`, `AmountMinor: long`, `Currency: string` |
| `PaymentSucceeded` | 1 | Billing/Payments | Billing, Notifications, Entitlements | `PaymentId: guid`, `InvoiceId: guid`, `SubscriptionId: guid`, `AmountMinor: long`, `Currency: string`, `ProcessedUtc: datetime` |
| `PaymentFailed` | 1 | Billing/Payments | Billing, Notifications, Entitlements | `PaymentId: guid`, `InvoiceId: guid`, `SubscriptionId: guid`, `FailureCode: string`, `Retryable: bool`, `ProcessedUtc: datetime` |
| `SubscriptionCanceled` | 1 | Billing | Billing, Notifications, Entitlements | `SubscriptionId: guid`, `CanceledUtc: datetime`, `EffectiveEndUtc: datetime`, `ReasonCode: string` |

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
