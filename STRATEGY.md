# Gatherstead Strategy and Domain Map

## Vision and Goals
- **Family details**: Maintain canonical records of each person's current name, birth date, family relationships, contact details, dietary needs/preferences, and other extensible attributes. Individuals and guardians/admins should be able to edit these details.
- **Gathering planning**: Plan events with date ranges, attendance by day/meal, meal prep assignments, chore duties, and lodging usage that supports flexible, arbitrated requests rather than hard reservations.
- **Evolution over time**: Support family groupings that can change as children form their own households, while keeping history and relationships coherent.
- **Extensibility**: Allow additional goals and modules to attach without disrupting existing domains.

## Security, Privacy, and Safety Principles
- **Privacy by design**: Treat personal data (PII, contact details, dietary notes) as sensitive by default. Favor minimization, avoid unnecessary replication across contexts, and prefer references over denormalized copies.
- **Tenant isolation**: Enforce tenant boundaries in persistence and APIs; every query should filter by tenant, and indexes should include tenant keys to avoid cross-family data leakage.
- **Data protection in transit and at rest**: Require TLS for all client/server and service/service traffic. Use encryption-at-rest for databases and explicitly encrypt high-sensitivity fields (e.g., contact methods, medical/dietary notes) within the application layer where appropriate.
- **Role-aware access control**: Scope permissions to tenant and household contexts, supporting guardians/admins editing minors' data while preventing lateral access across households. Favor least privilege defaults and explicit elevation for admin tasks.
- **Auditability and accountability**: Capture who created/updated sensitive records (members, contacts, intents, assignments) and when; surface audit trails in admin tooling to detect misuse.
- **Secure defaults and hardening**: Apply input validation, rate limiting, CSRF protections, and strong authentication. Disable risky defaults (e.g., open CORS, overly broad tokens) and maintain secret rotation and key management practices.
- **Data lifecycle and consent**: Track consent for sharing details, support export/delete workflows per tenant requests, and document retention policies for logs and backups.
- **Operational readiness**: Establish monitoring for anomalous access patterns, failed logins, and data exfiltration signals. Include security reviews in change management and keep dependencies updated with vulnerability scanning.

## Domain-Driven Design Overview
Gatherstead is organized around bounded contexts that align with the two core goals while sharing a multi-tenant foundation.

### Shared Foundation (Tenancy)
- **Tenant**: Top-level aggregate for an extended family or organization. Tenants own households, properties, events, and users, providing isolation across families or groups.【F:packages/db/Entities/Tenant.cs†L7-L14】

### Family Directory Context
- **Household**: Represents a family grouping under a tenant and owns its members.【F:packages/db/Entities/Household.cs†L7-L12】 Households can evolve as families split or merge.
- **HouseholdMember**: Person-centric record that stores name, birth date, dietary notes/tags, and adult/child markers, with encrypted fields for sensitive data.【F:packages/db/Entities/HouseholdMember.cs†L7-L22】 This is the anchor for relationships to gatherings, meals, and lodging intents.
- **Relationships**: Parent/child/sibling/spouse links will be modeled as member-to-member relationships to keep lineage flexible; this is a planned enhancement beyond current entities.
- **Contact & extensible details**: Additional value objects (addresses, phone/email, custom attributes) can be attached to HouseholdMember in future iterations to fulfill the directory goal.

### Gathering Planning Context
- **Property**: Physical location that can host events and belongs to a tenant.【F:packages/db/Entities/Property.cs†L7-L12】
- **Event**: Time-bounded gathering tied to a tenant and property; aggregates resources, meal plans, and chore templates for the event window.【F:packages/db/Entities/Event.cs†L7-L19】
- **Resource**: Lodging or facility slot (e.g., guest room, RV space) with capacities and notes; collects stay intents from members instead of hard reservations.【F:packages/db/Entities/Resource.cs†L7-L16】
- **StayIntent**: Member's request to use a resource on a given night, with status and notes for offline arbitration.【F:packages/db/Entities/StayIntent.cs†L7-L15】
- **MealPlan**: Defines meals for specific days within an event and aggregates meal intents and notes.【F:packages/db/Entities/MealPlan.cs†L7-L16】
- **MealIntent**: Member-level response indicating attendance for a meal, dietary considerations, and bring-your-own-food choices.【F:packages/db/Entities/MealIntent.cs†L7-L15】
- **ChoreTemplate**: Volunteer-friendly template for recurring chores (by time slot) across an event.【F:packages/db/Entities/ChoreTemplate.cs†L7-L16】
- **ChoreTask**: Dated chore assignments created from templates, allowing optional meal alignment, multiple assignees, completion tracking, and notes.【F:packages/db/Entities/ChoreTask.cs†L7-L16】

## Schema Review and Recommended Improvements
The current model now spans tenants, households/members, relationships, contact data, event planning, chores, and lodging intents. Focus the next round of tightening on:

- **Indexing and constraints for frequent lookups**: Add composite indexes on `(TenantId, ForeignKey)` for high-churn tables (members, events, chores) and uniqueness constraints where applicable (primary contact per member, single dietary profile per member) to keep tenant-specific queries predictable.【F:packages/db/Entities/HouseholdMember.cs†L7-L29】【F:packages/db/Entities/Event.cs†L7-L20】【F:packages/db/Entities/ChoreTask.cs†L7-L17】
- **API and workflow alignment**: Now that lineage, contact data, attendance, and assignment structures exist, expose them through DTOs/services with validation and authorization hooks so guardianship and arbitration rules become enforceable behaviors rather than schema-only constructs.【F:packages/db/Entities/MemberRelationship.cs†L5-L15】【F:packages/db/Entities/EventAttendance.cs†L5-L19】【F:packages/db/Entities/StayIntent.cs†L5-L17】【F:packages/db/Entities/ChoreAssignment.cs†L5-L14】

## Implementation Status and Next Steps
- **Implemented**: Multi-tenant backbone with auditing, households and members (including dietary metadata), relationship graph, contact/addresses/attributes, dietary profiles, event scaffolding with meal planning, daily attendance, chore templates/tasks with assignments, lodging intents with arbitration metadata, and tenant identifiers on dependent tables to enforce tenant-aware filters across the graph.【F:packages/db/Entities/Tenant.cs†L6-L15】【F:packages/db/Entities/HouseholdMember.cs†L7-L29】【F:packages/db/Entities/MemberRelationship.cs†L5-L15】【F:packages/db/Entities/ContactMethod.cs†L5-L14】【F:packages/db/Entities/Address.cs†L5-L18】【F:packages/db/Entities/MemberAttribute.cs†L5-L13】【F:packages/db/Entities/DietaryProfile.cs†L5-L15】【F:packages/db/Entities/Event.cs†L6-L20】【F:packages/db/Entities/MealPlan.cs†L6-L17】【F:packages/db/Entities/EventAttendance.cs†L5-L19】【F:packages/db/Entities/ChoreTemplate.cs†L6-L17】【F:packages/db/Entities/ChoreTask.cs†L7-L18】【F:packages/db/Entities/ChoreAssignment.cs†L5-L14】【F:packages/db/Entities/StayIntent.cs†L6-L17】
- **Planned**: Add operational indexes/constraints and surface the new lineage/contact/attendance/arbitration capabilities through API endpoints and workflows with validation and authorization to match guardian/admin needs.【F:packages/db/GathersteadDbContext.cs†L108-L149】【F:packages/db/Entities/MemberRelationship.cs†L5-L15】【F:packages/db/Entities/ContactMethod.cs†L5-L14】【F:packages/db/Entities/EventAttendance.cs†L5-L19】【F:packages/db/Entities/StayIntent.cs†L6-L17】
  - **Authorization refinement**: Implement granular authorization checks by tenant, household, and role with proper HTTP status codes (401 for authentication failures, 403 for insufficient permissions).
  - **Enhanced encryption**: Implement per-tenant Data Encryption Keys (DEK) derived from a master Key Encryption Key (KEK) to provide additional data isolation and support key rotation workflows.
  - **Token security**: Improve PASETO token implementation following best practices from the [paseto-dotnet library](https://github.com/daviddesmet/paseto-dotnet) to strengthen authentication token handling and validation.
- **Architecture direction**: Treat households and events as separate aggregates linked through member IDs, enabling independent evolution of directory data and event participation while maintaining traceability.
