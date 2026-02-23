# Implementation Status and Next Steps

## Implemented Features
- **Shared foundation**: Tenants own households, properties, events, and users to keep families isolated.
- **Family directory context**: Households group members; member records store names, birth dates, dietary notes/tags.
- **Gathering planning context**: Events tie to properties and manage meal plans, chores, lodging resources, and member intents for attendance, meals, stays, and chores.
- **Detailed implementation**: Multi-tenant backbone with auditing, households and members (including dietary metadata), relationship graph, contact/addresses/attributes, dietary profiles, event scaffolding with meal planning, daily attendance, chore templates/tasks with assignments, lodging intents with arbitration metadata, and tenant identifiers on dependent tables to enforce tenant-aware filters across the graph.
- **PASETO token security**: Reimplemented PASETO authentication handler with issuer/audience validation, configurable clock skew, Azure Key Vault integration for key management, and a token revocation service backed by a `RevokedTokens` table.
- **Rate limiting**: Sliding-window rate limiter configured in the API pipeline to enforce request throttling per the secure-defaults principle.
- **Tenant-scoped authorization**: `RequireTenantAccessAttribute` authorization filter that extracts the tenant from the route, verifies the user's `TenantUser` membership, and enforces a role hierarchy (Owner > Manager > Member > Guest). Applied to tenant-aware controllers.
- **Always Encrypted data protection**: Shifted from application-level encryption to SQL Server Always Encrypted, delegating column-level encryption of sensitive fields to the database driver rather than custom application code.
- **Project restructure**: Migrated data projects from `packages/db/` to `src/Gatherstead.Data/` and `src/Gatherstead.Data.Setup/`, aligning namespaces under `Gatherstead.Data`.
- **Framework upgrade**: Updated all projects to target .NET 10 (`net10.0`) with current package references.

## Planned Enhancements
- Member relationship graphs
- Richer contact/address data
- Daily attendance summaries
- Chore sign-up flows
- Arbitration metadata for lodging
- Audit trails across mutable entities
- Add operational indexes/constraints and surface the new lineage/contact/attendance/arbitration capabilities through API endpoints and workflows with validation and authorization to match guardian/admin needs.

## Schema Review and Recommended Improvements
The current model now spans tenants, households/members, relationships, contact data, event planning, chores, and lodging intents. Focus the next round of tightening on:

- **Indexing and constraints for frequent lookups**: Add composite indexes on `(TenantId, ForeignKey)` for high-churn tables (members, events, chores) and uniqueness constraints where applicable (primary contact per member, single dietary profile per member) to keep tenant-specific queries predictable.

## Architecture Direction
Treat households and events as separate aggregates linked through member IDs, enabling independent evolution of directory data and event participation while maintaining traceability.

- **API and workflow alignment**: Now that lineage, contact data, attendance, and assignment structures exist, expose them through DTOs/services with validation and authorization hooks so guardianship and arbitration rules become enforceable behaviors rather than schema-only constructs.
- **Authorization refinement**: The `RequireTenantAccessAttribute` provides tenant- and role-level enforcement. Next steps include household-scoped permission checks and proper HTTP status differentiation (401 for authentication failures, 403 for insufficient permissions).
