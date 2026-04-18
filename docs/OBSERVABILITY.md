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
| `ResourceId` / `resource_id` / `resource.id` | Internal `Guid` |
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

## Verification

The allowlist is validated by unit tests in [`PiiRedactionLogProcessorTests`](../tests/Gatherstead.Api.Tests/Observability/PiiRedactionLogProcessorTests.cs) and [`PiiRedactionActivityProcessorTests`](../tests/Gatherstead.Api.Tests/Observability/PiiRedactionActivityProcessorTests.cs). Run `dotnet test` to confirm all assertions pass after any allowlist change.
