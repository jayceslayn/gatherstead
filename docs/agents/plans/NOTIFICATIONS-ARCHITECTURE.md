# Notifications Architecture

## Purpose and Scope
The Notifications context coordinates tenant-scoped reminders and role/user lifecycle notifications across Gatherstead. It remains Azure-first and C#/.NET service aligned, while exposing APIs that can be consumed by the Vue/Nuxt frontend and internal jobs.

### Goals
- Deliver reminder and role/invitation notifications without cross-tenant data leakage.
- Maintain strict PII-safe event payloads and observability output.
- Support idempotent processing and replay-safe integrations.

### Non-goals
- Designing UI copy templates (handled by frontend/content workflows).
- Defining transport-specific implementation details (Service Bus/Event Grid choice can be finalized during implementation).

## Bounded Context Responsibilities

### Notifications context owns
- Notification schedule derivation for time-based reminders (`EventReminderDue`, `ChoreAssignmentDue`).
- Notification dispatch lifecycle (queued, sent, failed, retried).
- User preference policies (opt-in/out by channel and category) where implemented.
- Notification delivery logs using safe IDs/metadata only.

### Notifications context consumes
- Identity and access events from shared foundation (`UserInvited`, `RoleAssigned`).
- Billing state changes affecting eligibility to send certain categories (`SubscriptionCanceled`, failed payment grace logic).

### Notifications context publishes
- Optional delivery/audit domain events (`NotificationQueued`, `NotificationSent`, `NotificationDeliveryFailed`) for downstream analytics/ops.

## Aggregate Model

### 1) NotificationRule (aggregate root)
Defines what should be sent and when.

- **Identity**: `NotificationRuleId`, `TenantId` (mandatory)
- **Core fields**: `Category`, `TriggerEventName`, `LeadTimeMinutes`, `ChannelSet`, `IsEnabled`
- **Invariants**:
  - `TenantId` is required and immutable.
  - `TriggerEventName` must exist in canonical catalog.
  - Rule changes are versioned for auditability.

### 2) NotificationPreference (aggregate root)
Per-recipient channel/category policy.

- **Identity**: `NotificationPreferenceId`, `TenantId` (mandatory), `UserId` or `HouseholdMemberId`
- **Core fields**: `Category`, `Channel`, `IsMuted`, `MuteUntilUtc`
- **Invariants**:
  - Preference belongs to exactly one recipient scope.
  - Tenant-scoped uniqueness per `(TenantId, Recipient, Category, Channel)`.

### 3) NotificationDispatch (aggregate root)
Execution unit for sending a concrete notification.

- **Identity**: `NotificationDispatchId`, `TenantId` (mandatory)
- **Core fields**: `EventName`, `RecipientRef`, `Channel`, `Status`, `AttemptCount`, `NextAttemptUtc`, `CorrelationId`
- **Invariants**:
  - Exactly-once semantics per idempotency key `(TenantId, EventName, RecipientRef, CorrelationId)`.
  - Retry policy bounded with dead-letter transition.

## API Surface (.NET Web API)
All endpoints require tenant context and enforce tenant access authorization.

### Rules
- `GET /api/notifications/rules`
- `POST /api/notifications/rules`
- `PUT /api/notifications/rules/{ruleId}`
- `DELETE /api/notifications/rules/{ruleId}` (soft delete)

### Preferences
- `GET /api/notifications/preferences/me`
- `PUT /api/notifications/preferences/me`
- `GET /api/notifications/preferences/{memberId}` (Manager+)

### Dispatch / operations
- `GET /api/notifications/dispatches?status=...&eventName=...`
- `POST /api/notifications/dispatches/{dispatchId}/retry` (Manager+)

### Internal ingress
- `POST /api/internal/notifications/events` (internal service principal only)
  - Accepts canonical event envelope and enqueues dispatch generation.

## Processing and Azure Alignment
- Prefer Azure messaging primitives (Service Bus topics/queues or Event Grid) for durable at-least-once delivery.
- Use App Insights + OpenTelemetry for traces/metrics with PII guardrails.
- Persist scheduling and dispatch state in tenant-scoped SQL tables with global query filters.

## Canonical Event Catalog (Shared with Billing)
This catalog is **canonical** and shared across Notifications and Billing contexts. Event names and payload schemas are versioned contracts.

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
- Cross-tenant fan-out is forbidden.
- Consumers must reject events missing `TenantId` or with invalid tenant authorization context.
- **Exception (User domain only):** user lifecycle data can exist outside tenant scope or span multiple tenants. User-domain reads/writes may be global, but any notification side effects must project back into explicit tenant context before dispatch.

## PII Constraints (per `docs/OBSERVABILITY.md`)
- Event payloads and logs must use identifiers (`...Id` GUIDs), enums, counters, short reason codes.
- Do **not** include names, emails, phones, addresses, birthdates, dietary notes, free-form member notes, or raw message bodies containing personal data.
- Observability attributes should stay on the documented allowlist (e.g., `TenantId`, `UserId`, `MemberId`, `EventId`, `CorrelationId`, role/reason metadata).
- SQL text/query bodies that could embed sensitive data must not be emitted to traces.

## Versioning and Compatibility
- Backward-compatible changes: additive payload fields only.
- Breaking changes require new event version and dual-publish migration window.
- Consumers must be tolerant readers: ignore unknown fields.
