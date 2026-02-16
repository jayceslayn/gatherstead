# Implementation Status and Next Steps

## Implemented Features
- **Shared foundation**: Tenants own households, properties, events, and users to keep families isolated.
- **Family directory context**: Households group members; member records store names, birth dates, dietary notes/tags.
- **Gathering planning context**: Events tie to properties and manage meal plans, chores, lodging resources, and member intents for attendance, meals, stays, and chores.
- **Detailed Implementation**: Multi-tenant backbone with auditing, households and members (including dietary metadata), relationship graph, contact/addresses/attributes, dietary profiles, event scaffolding with meal planning, daily attendance, chore templates/tasks with assignments, lodging intents with arbitration metadata, and tenant identifiers on dependent tables to enforce tenant-aware filters across the graph.

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
- **Authorization refinement**: Implement granular authorization checks by tenant, household, and role with proper HTTP status codes (401 for authentication failures, 403 for insufficient permissions).
- **Token security**: Improve PASETO token implementation following best practices from the [paseto-dotnet library](https://github.com/daviddesmet/paseto-dotnet) to strengthen authentication token handling and validation.
