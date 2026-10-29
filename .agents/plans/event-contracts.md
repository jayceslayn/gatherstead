---
updated: 2026-10-28
commit: d1f8b0b
status: design
---

# Shared Event Contracts

This document is the single source of truth for versioned event envelopes and payload schemas shared by Billing, Notifications, and other bounded contexts. Billing and Notifications own their respective business events; this contract defines their shared wire shape.

## Event Envelope

Every event includes:

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

`TenantId` is mandatory in the envelope, aggregate storage, API DTOs, and queue partition or routing keys. Consumers reject events with a missing tenant or invalid tenant authorization context. User lifecycle events may originate outside a tenant, but any tenant-scoped side effect must identify its target tenant before dispatch.

## Event Catalog

Payload schemas list required minimum fields.

| Event Name | Version | Producer | Primary Consumers | Payload schema (minimum) |
|---|---:|---|---|---|
| `UserInvited` | 1 | Identity/Tenant Membership | Notifications, Billing | `InvitationId: guid`, `InvitedUserId: guid`, `InviterUserId: guid`, `TenantRole: string`, `ExpiresUtc: datetime` |
| `RoleAssigned` | 1 | Identity/Tenant Membership | Notifications | `AssignmentId: guid`, `UserId: guid`, `Role: string`, `AssignedByUserId: guid`, `Scope: string` |
| `EventReminderDue` | 1 | Event Scheduling | Notifications | `EventId: guid`, `ReminderType: string`, `TargetDateUtc: datetime`, `Audience: string`, `TemplateKey: string` |
| `TaskAssignmentDue` | 1 | Task Planning | Notifications | `TaskPlanId: guid`, `EventId: guid`, `DueUtc: datetime`, `AssignedMemberId: guid`, `TemplateKey: string` |
| `SubscriptionCreated` | 1 | Billing | Billing, Notifications | `SubscriptionId: guid`, `PlanCode: string`, `BillingPeriod: string`, `StartsUtc: datetime`, `CustomerRefId: guid` |
| `InvoiceDue` | 1 | Billing | Billing, Notifications | `InvoiceId: guid`, `SubscriptionId: guid`, `DueUtc: datetime`, `AmountMinor: long`, `Currency: string` |
| `PaymentSucceeded` | 1 | Billing/Payments | Billing, Notifications, Entitlements | `PaymentId: guid`, `InvoiceId: guid`, `SubscriptionId: guid`, `AmountMinor: long`, `Currency: string`, `ProcessedUtc: datetime` |
| `PaymentFailed` | 1 | Billing/Payments | Billing, Notifications, Entitlements | `PaymentId: guid`, `InvoiceId: guid`, `SubscriptionId: guid`, `FailureCode: string`, `Retryable: bool`, `ProcessedUtc: datetime` |
| `SubscriptionCanceled` | 1 | Billing | Billing, Notifications, Entitlements | `SubscriptionId: guid`, `CanceledUtc: datetime`, `EffectiveEndUtc: datetime`, `ReasonCode: string` |

## Producer Responsibilities

- Event Scheduling creates and emits `EventReminderDue`.
- Task Planning creates and emits `TaskAssignmentDue`.
- Notifications consumes both due events and owns preference evaluation, dispatch creation, retry, and delivery.

## Privacy and Observability

- Event payloads and logs contain identifiers, enums, counters, currency codes, and short reason codes only.
- Never include names, emails, phone numbers, addresses, birth dates, dietary notes, free-form member notes, or raw message bodies containing personal data.
- Observability attributes follow the allowlist in [`docs/OBSERVABILITY.md`](../../docs/OBSERVABILITY.md). SQL text or query bodies that could contain sensitive values must not be emitted to traces.

## Compatibility

- Additive payload fields are backward-compatible.
- Breaking changes require a version increment and a dual-publish/consume migration window.
- Consumers tolerate unknown fields.
